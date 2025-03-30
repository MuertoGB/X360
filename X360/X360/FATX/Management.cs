// NOTE This class is protected under GPL License as well as terms and conditions.
// Most notably, you must not obfuscate/protect this code, you must include an open source
// to your project that uses this code, and you must also not make profit on it.
// For more details, access:
// *http://www.gnu.org/
// *License included in the library source
// *License located at X360.PublicResources.GPL30
// *X360.XAbout.GNUProtected for GNU and TaC (Terms and Conditions)
// You agree to these terms when you use this code.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using X360.IO;
using X360.Other;

namespace X360.FATX
{
    #region Enums
    /// <summary>
    /// Types of Xbox devices
    /// </summary>
    public enum DriveTypes : byte
    {
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown,
        /// <summary>
        /// Stock Hard Drive
        /// </summary>
        HardDrive,
        /// <summary>
        /// Xbox Memory Unit
        /// </summary>
        MemoryUnit,
        /// <summary>
        /// Developer Kit Hard Drive
        /// </summary>
        DevHardDrive,
        /// <summary>
        /// Formatted Flash Drive for the Xbox
        /// </summary>
        USBFlashDrive
    }

    internal enum FATXType : byte { None, FATX16 = 2, FATX32 = 4 }

    /// <summary>
    /// Xbox Memory Card values
    /// </summary>
    public enum MU : long
    {
        /// <summary>Cache</summary>
        Partition1 = 0,
        /// <summary>Content</summary>
        Partition2 = 0x7FF000
    }

    /// <summary>
    /// USB Flash Drive offsets
    /// </summary>
    public enum USB : long
    {
        /// <summary>Cache</summary>
        Partition1 = 0x8000400,
        /// <summary>Content</summary>
        Partition2 = 0x20000000
    }

    /// <summary>
    /// Hard Drive Values
    /// </summary>
    public enum HDD : long
    {
        /// <summary>
        /// Security Sector
        /// </summary>
        SecuritySector = 0x2000,
        /// <summary>
        /// Partition 1
        /// </summary>
        Partition1 = 0x80000,
        /// <summary>
        /// Partition 2
        /// </summary>
        Partition2 = 0x80080000,
        /// <summary>
        /// Partition 3
        /// </summary>
        Partition3 = 0x118EB0000,
        /// <summary>
        /// Partition 4
        /// </summary>
        Partition4 = 0x120EB0000,
        /// <summary>
        /// Partition 5
        /// </summary>
        Partition5 = 0x130EB0000
    }
    #endregion

    /// <summary>
    /// Exceptions for FATX
    /// </summary>
    [DebuggerStepThrough]
    public static class FATXExcepts
    {
        [CompilerGenerated]
        static readonly Exception xPartitionExcept = new Exception("Partition error");
        [CompilerGenerated]
        static readonly Exception xDriveExcept = new Exception("Not a FATX Drive");
        [CompilerGenerated]
        static readonly Exception xSizeExcept = new Exception("Out device has not enough memory");
        [CompilerGenerated]
        static readonly Exception xValidExcept = new Exception("Not a valid instance");
        [CompilerGenerated]
        static readonly Exception xTypeConflict = new Exception("Not a folder");
        [CompilerGenerated]
        static readonly Exception xFolderContents = new Exception("Folder has contents");

        /// <summary>
        /// General error
        /// </summary>
        public static Exception PartitionExcept { get { return xPartitionExcept; } }
        /// <summary>
        /// Not a FATX drive
        /// </summary>
        public static Exception DriveExcept { get { return xDriveExcept; } }
        /// <summary>
        /// Invalid size
        /// </summary>
        public static Exception SizeExcept { get { return xSizeExcept; } }
        /// <summary>
        /// Not a valid instance
        /// </summary>
        public static Exception ValidExcept { get { return xValidExcept; } }
        /// <summary>
        /// Not a folder
        /// </summary>
        public static Exception TypeConflict { get { return xTypeConflict; } }
        /// <summary>
        /// Folder has contents
        /// </summary>
        public static Exception FolderContents { get { return xFolderContents; } }
    }
    /// <summary>
    /// Object to hold a device
    /// </summary>
    [DebuggerStepThrough]
    public sealed class DeviceReturn
    {
        [CompilerGenerated]
        internal byte index;
        [CompilerGenerated]
        internal DeviceType type;

        /// <summary>
        /// Returns the Type of the device
        /// </summary>
        public DeviceType Type { get { return type; } }

        /// <summary>
        /// Device index
        /// </summary>
        public byte Index { get { return index; } }

        /// <summary>
        /// Device Name
        /// </summary>
        public string Name { get { return (type.ToString() + index.ToString()); } }

        internal DeviceReturn(byte xindex, DeviceType xtype) { index = xindex; type = xtype; }
    }

    /// <summary>
    /// Tools for FATX drives
    /// </summary>
    [DebuggerStepThrough]
    public static class FATXManagement
    {
        /// <summary>
        /// Gets a set of Indexes for available drives
        /// </summary>
        /// <param name="PassCount"></param>
        /// <returns></returns>
        public static DeviceReturn[] GetDrives(byte PassCount)
        {
            List<DeviceReturn> xReturn = new List<DeviceReturn>();
            for (byte i = 0; i < PassCount; i++)
            {
                Drive xDrive = new Drive(i, DeviceType.PhysicalDrive);
                if (xDrive.Accessed)
                    xReturn.Add(new DeviceReturn(i, DeviceType.PhysicalDrive));
            }
            DriveInfo[] xlogics = DriveInfo.GetDrives();
            foreach (DriveInfo x in xlogics)
            {
                Drive xDrive = new Drive(x.Name[0]);
                if (xDrive.Accessed)
                    xReturn.Add(new DeviceReturn((byte)x.Name[0], DeviceType.LogicalDrive));
            }
            return xReturn.ToArray();
        }

        /// <summary>
        /// Is a FATX drive
        /// </summary>
        /// <param name="xDrive"></param>
        /// <param name="xType"></param>
        /// <returns></returns>
        public static bool IsFATX(ref Drive xDrive, out DriveTypes xType)
        {
            xType = DriveTypes.Unknown;
            if (xDrive == null)
                return false;
            xDrive.MakeHandle();
            FXIO xIO = new DriveIO(xDrive, true);
            return IsFATX(ref xIO, out xType);
        }

        /// <summary>
        /// Determins if a Drive is FATX format
        /// </summary>
        /// <param name="xIO">Stream to check</param>
        /// <param name="xType">Grabs the type of drive</param>
        /// <returns></returns>
        public static bool IsFATX(ref FXIO xIO, out DriveTypes xType)
        {
            // Tries to read the offsets of Xbox 360 drives to see if the magic's match
            xType = DriveTypes.Unknown;
            try
            {
                xIO.Position = (long)MU.Partition2;
                if (xIO.ReadUInt32() == (uint)AllMagic.FATX)
                {
                    xType = DriveTypes.MemoryUnit;
                    return true;
                }
            }
            catch { }
            try
            {
                if (xIO.IOType != DataType.Drive || ((DriveIO)xIO).xDrive.Type != DeviceType.LogicalDrive)
                    throw new Exception();
                string dat0 = ((DriveIO)xIO).xDrive.DeviceName + @"\Xbox360\Data0000";
                if (!File.Exists(dat0))
                    throw new Exception();
                FXIO xio = new FXIO(dat0, DJFileMode.Open, true);
                if (!xio.Accessed)
                    throw new Exception();
                xio.Position = (long)USB.Partition1;
                try
                {
                    if (xio.ReadUInt32() == (uint)AllMagic.FATX)
                    {
                        xType = DriveTypes.USBFlashDrive;
                        xio.Dispose();
                        return true;
                    }
                }
                catch { }
                xio.Dispose();
            }
            catch { }
            try
            {
                xIO.Position = (long)HDD.Partition5;
                if (xIO.ReadUInt32() == (uint)AllMagic.FATX)
                {
                    xType = DriveTypes.HardDrive;
                    return true;
                }
            }
            catch { }
            try
            {
                xIO.Position = 8;
                xIO.Position = (xIO.ReadUInt32() * 0x200);
                if (xIO.ReadUInt32() == (uint)AllMagic.FATX)
                {
                    xType = DriveTypes.DevHardDrive;
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Determins if this Index is a FATX drive pointed index
        /// </summary>
        /// <param name="Device">Device to check</param>
        /// <param name="xType">Grabs the FATX type</param>
        /// <returns></returns>
        public static bool IsFATX(DeviceReturn Device, out DriveTypes xType)
        {
            Drive xDrive = new Drive(Device.index, Device.type);
            return IsFATX(ref xDrive, out xType);
        }

        /// <summary>
        /// Gets a result of available set of FATX Drives from a set of Indexes
        /// </summary>
        /// <param name="Drives">General drive list</param>
        /// <returns></returns>
        public static DeviceReturn[] GetFATXDrives(DeviceReturn[] Drives)
        {
            List<DeviceReturn> xReturn = new List<DeviceReturn>();
            for (int i = 0; i < Drives.Length; i++)
            {
                Drive xDrive = new Drive(Drives[i].index, Drives[i].type);
                DriveTypes xType = DriveTypes.Unknown;
                if (IsFATX(ref xDrive, out xType))
                    xReturn.Add(Drives[i]);
            }
            return xReturn.ToArray();
        }

        /// <summary>
        /// Gets a set of available FATX Drive indexes from a set pass
        /// </summary>
        /// <param name="PassCount"></param>
        /// <returns></returns>
        public static DeviceReturn[] GetFATXDrives(byte PassCount)
        {
            DeviceReturn[] xList = GetDrives(PassCount);
            return GetFATXDrives(xList);
        }
    }

    /// <summary>
    /// FATX Drive Class
    /// </summary>
    [DebuggerStepThrough]
    public sealed class FATXDrive
    {
        Drive FXDDrive = null;
        List<FATXPartition> FXDPartitions;
        DriveTypes FXDType = DriveTypes.Unknown;
        internal FXIO FXDIO = null;
        bool FXDActive = false; // To prevent multithread errors

        internal bool Active
        {
            get
            {
                return FXDActive;
            }
            set
            {
                FXDActive = value;
                if (!value && IsDriveIO && FXDType != DriveTypes.USBFlashDrive)
                {
                    FXDIO.Close();
                }
            }
        }
        /// <summary>
        /// Memory Card or Hard Drive
        /// </summary>
        public DriveTypes Type
        {
            get
            {
                if (!Success)
                {
                    throw FATXExcepts.ValidExcept;
                }
                    
                return FXDType;
            }
        }
        /// <summary>
        /// Is an IO to a device
        /// </summary>
        public bool IsDriveIO { get { return FXDDrive != null; } }
        /// <summary>
        /// Determines if this Drive is successfully obtained
        /// </summary>
        public FATXPartition[] Partitions { get { return FXDPartitions.ToArray(); } }
        /// <summary>
        /// True if parse success
        /// </summary>
        public bool Success
        {
            get
            {
                if (FXDIO == null)
                    return (FXDDrive != null && FXDDrive.Accessed);
                else return (FXDIO.Accessed);
            }
        }
        /// <summary>
        /// Size of device sector
        /// </summary>
        public uint SectorSize
        {
            get
            {
                if (IsDriveIO)
                    return FXDDrive.Geometry.BytesPerSector;
                return 0x200;
            }
        }
        /// <summary>
        /// Size of device
        /// </summary>
        public long DriveSize { get { return FXDIO.Length; } }
        /// <summary>
        /// Friendly size
        /// </summary>
        public string DriveSizeFriendly { get { return FXDIO.LengthFriendly; } }
        /// <summary>
        /// Name of the device
        /// </summary>
        public string DriveName { get { return (FXDDrive != null) ? FXDDrive.DeviceName : FXDIO.FileNameShort; } }

        internal bool ActiveCheck()
        {
            if (Active)
                return true;
            return !(Active = true);
        }

        internal FATXReadContents xReadToFolder(string Path, out FATXFolderEntry xFolderOut)
        {
            xFolderOut = null;
            if (Path == null || Path == "")
                return null;
            Path = Path.Replace("\\", "/");
            if (Path[0] == '/')
                Path = Path.Substring(1, Path.Length - 1);
            if (Path[Path.Length - 1] == '/')
                Path = Path.Substring(0, Path.Length - 1);
            string[] Folders = Path.Split(new char[] { '/' });
            sbyte PartitionIndex = -1;
            for (int i = 0; i < FXDPartitions.Count; i++)
            {
                if (FXDPartitions[i].PartitionName.ToLower() != Folders[0].ToLower())
                    continue;
                PartitionIndex = (sbyte)i;
                break;
            }
            if (PartitionIndex == -1)
                return null;
            if (Folders.Length == 1)
            {
                FATXReadContents xread = new FATXReadContents();
                xread.xfiles = new List<FATXFileEntry>();
                xread.xfolds = new List<FATXFolderEntry>();
                foreach (FATXFolderEntry xz in Partitions[PartitionIndex].Folders)
                    xread.xfolds.Add(xz);
                foreach (FATXPartition xz in Partitions[PartitionIndex].SubPartitions)
                    xread.xsubparts.Add(xz);
                return xread;
            }
            FATXPartition xcurpart = FXDPartitions[PartitionIndex];
            int idx = 1;
            for (int i = 0; i < xcurpart.SubPartitions.Length; i++)
            {
                if (xcurpart.SubPartitions[i].PartitionName.ToLower() != Folders[1].ToLower())
                    continue;
                xcurpart = xcurpart.SubPartitions[i];
                idx++;
                if (Folders.Length == 2)
                {
                    FATXReadContents xread = new FATXReadContents();
                    xread.xfiles = new List<FATXFileEntry>();
                    xread.xfolds = new List<FATXFolderEntry>();
                    foreach (FATXFolderEntry xz in xcurpart.Folders)
                        xread.xfolds.Add(xz);
                    return xread;
                }
                break;
            }
            FATXFolderEntry xFold = null;
            foreach (FATXFolderEntry x in xcurpart.Folders)
            {
                if (x.Name.ToLower() != Folders[idx].ToLower())
                    continue;
                xFold = x;
                break;
            }
            if (xFold == null)
                return null;
            idx++;
            FATXReadContents xreadct;
            for (int i = idx; i < Folders.Length; i++)
            {
                bool found = false;
                xreadct = xFold.xRead();
                foreach (FATXFolderEntry x in xreadct.Folders)
                {
                    if (x.Name.ToLower() != Folders[i].ToLower())
                        continue;
                    found = true;
                    xFold = x;
                    break;
                }
                if (!found)
                    return null;
            }
            xFolderOut = xFold;
            return xFold.xRead();

        }
        /// <summary>
        /// Attempts to read to a specified location
        /// </summary>
        /// <param name="Path">Path to read to</param>
        /// <param name="xFolderOut">FATXFolder of result</param>
        /// <returns></returns>
        public FATXReadContents ReadToFolder(string Path, out FATXFolderEntry xFolderOut)
        {
            if (ActiveCheck())
            {
                xFolderOut = null;
                return null;
            }
            FATXReadContents xreturn = xReadToFolder(Path, out xFolderOut);
            FXDActive = false;
            return xreturn;
        }

        bool LoadPartitions()
        {
            if (!Success)
                return false;
            FXDPartitions = new List<FATXPartition>();
            new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(System.DLLIdentify.PrivilegeCheck)).Start(System.Threading.Thread.CurrentThread);
            GetIO();
            if (FXDType == DriveTypes.HardDrive)
            {
                /*FATXPartition x = new FATXPartition((long)HDD.Partition1, (long)HDD.Partition2 - (long)HDD.Partition1, this, current);
                if (x.IsValid)
                {
                    xPartitions.Add(x); // Unknown
                    current++;
                }
                x = new FATXPartition((long)HDD.Partition2, (long)HDD.Partition3 - (long)HDD.Partition2, this, current);
                if (x.IsValid)
                {
                    xPartitions.Add(x); // Unknown
                    current++;
                }*/
                FATXPartition x = new FATXPartition((long)HDD.Partition3, (long)HDD.Partition4 - (long)HDD.Partition3, this, "System");
                if (x.IsValid)
                    FXDPartitions.Add(x); // Unknown
                x = new FATXPartition((long)HDD.Partition4, (long)HDD.Partition5 - (long)HDD.Partition4, this, "Compatability"); // Compatability
                if (x.IsValid)
                    FXDPartitions.Add(x); // Compatability
                x = new FATXPartition((long)HDD.Partition5,
                    FXDIO.Length - (long)HDD.Partition5, this, "Content");
                if (x.IsValid)
                    FXDPartitions.Add(x); // Main Partition
            }
            else if (FXDType == DriveTypes.MemoryUnit)
            {
                FATXPartition x = new FATXPartition((long)MU.Partition1, (long)MU.Partition2 - (long)MU.Partition1, this, "Cache");
                if (x.IsValid)
                    FXDPartitions.Add(x);
                x = new FATXPartition((long)MU.Partition2,
                    FXDIO.Length - (long)MU.Partition2, this, "Content");
                if (x.IsValid)
                    FXDPartitions.Add(x);
            }
            else if (FXDType == DriveTypes.USBFlashDrive)
            {
                // Dunno why there's space between o.o
                FATXPartition x = new FATXPartition((long)USB.Partition1, 0x47FF000, this, "Cache");
                if (x.IsValid)
                    FXDPartitions.Add(x);
                x = new FATXPartition((long)USB.Partition2,
                    FXDIO.Length - (long)USB.Partition2, this, "Content");
                if (x.IsValid)
                    FXDPartitions.Add(x);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    FXDIO.Position = (8 + (i * 8));
                    uint off = FXDIO.ReadUInt32();
                    uint len = FXDIO.ReadUInt32();
                    if (off == 0 || len == 0)
                        break;
                    string name = "Partition" + i.ToString();
                    if (i == 0)
                        name = "System";
                    else if (i == 2)
                        name = "Compatability";
                    else name = "Content";
                    FATXPartition x = new FATXPartition((off * SectorSize), (len * SectorSize),
                        this, name);
                    if (x.IsValid)
                        FXDPartitions.Add(x);
                }
            }
            return !(Active = false);
        }

        /// <summary>
        /// Closes the stream
        /// </summary>
        public void Close()
        {
            FXDIO.Dispose();
            foreach (FATXPartition x in Partitions)
                x.FXPAllocTable.xAllocTable.Dispose(true);
        }

        /// <summary>
        /// Initializes a new FATX Drive Class from an already set Drive
        /// </summary>
        /// <param name="InDrive"></param>
        public FATXDrive(ref Drive InDrive)
        {
            if (!InDrive.Accessed)
                throw new Exception("Invalid input");
            if (!FATXManagement.IsFATX(ref InDrive, out FXDType))
                throw new Exception("Drive is not FATX");
            FXDActive = true;
            FXDDrive = InDrive;
            LoadPartitions();
        }

        /// <summary>
        /// Sets a FATX Drive from an index
        /// </summary>
        /// <param name="DeviceIn"></param>
        public FATXDrive(DeviceReturn DeviceIn)
        {
            Drive xdrive = new Drive(DeviceIn.index, DeviceIn.type);
            if (!xdrive.Accessed)
                throw new Exception("Invalid input");
            if (!FATXManagement.IsFATX(ref xdrive, out FXDType))
                throw new Exception("Drive is not FATX");
            FXDActive = true;
            FXDDrive = xdrive;
            LoadPartitions();
        }

        /// <summary>
        /// Load FATX from an Image
        /// </summary>
        /// <param name="FileLocale"></param>
        public FATXDrive(string FileLocale)
        {
            FXIO xImage = new FXIO(FileLocale, DJFileMode.Open, true);
            if (xImage == null || !xImage.Accessed)
                return;
            if (!FATXManagement.IsFATX(ref xImage, out FXDType))
                throw new Exception("Drive is not FATX");
            FXDActive = true;
            FXDIO = xImage;
            LoadPartitions();
        }

        /// <summary>
        /// Read a FATX Image
        /// </summary>
        /// <param name="xImage"></param>
        internal FATXDrive(FXIO xImage)
        {
            if (xImage == null || !xImage.Accessed)
                return;
            if (!FATXManagement.IsFATX(ref xImage, out FXDType))
                throw new Exception("Drive is not FATX");
            FXDIO = xImage;
            LoadPartitions();
        }

        /// <summary>
        /// Extracts the image via a file location
        /// </summary>
        /// <param name="fileOut"></param>
        /// <returns></returns>
        public bool ExtractImage(string fileOut)
        {
            if (ActiveCheck())
                return false;
            FXIO xIOOut = null;
            try { xIOOut = new FXIO(fileOut, DJFileMode.Create, true); }
            catch { return FXDActive = false; }
            if (!xIOOut.Accessed)
                return FXDActive = false;
            bool result = extractimg(xIOOut);
            xIOOut.Dispose();
            return result;
        }

        void extthrd(object ioz)
        {
            FXIO xIOOut = (FXIO)ioz;
            try
            {
                GetIO();
                FXDIO.Position = xIOOut.Position = 0;
                for (long i = 0; i < FXDIO.Length; i += 0x1000)
                    xIOOut.Write(FXDIO.unbufferedread(0x1000));
                xIOOut.Flush();
                FXDActive = false;
            }
            catch { }

        }

        bool extractimg(FXIO xIOOut)
        {
            System.Threading.Thread x = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(extthrd));
            x.Start(xIOOut);
            while (x.IsAlive)
                System.Windows.Forms.Application.DoEvents();
            if (FXDActive)
                return (FXDActive = false);
            return (!FXDActive);
        }

        /// <summary>
        /// Extract a binary image of your FATX Drive
        /// </summary>
        /// <param name="xIOOut"></param>
        /// <returns></returns>
        public bool ExtractImage(FXIO xIOOut)
        {
            if (ActiveCheck())
                return false;
            return extractimg(xIOOut);
        }

        /// <summary>
        /// Overwrite a binary image of your FATX Drive
        /// </summary>
        /// <param name="xImageDrive"></param>
        /// <returns></returns>
        public bool RestoreImage(FATXDrive xImageDrive)
        {
            if (ActiveCheck())
                return false;
            return restoreimg(xImageDrive) & ReloadDrive();
        }

        void rstthrd(object drv)
        {
            FATXDrive xImageDrive = (FATXDrive)drv;
            xImageDrive.GetIO();
            GetIO();
            FXDIO.Position = xImageDrive.FXDIO.Position = 0;
            for (long i = 0; i < FXDIO.Length && i < xImageDrive.FXDIO.Length; i += 0x1000)
                FXDIO.unbufferedwrite(xImageDrive.FXDIO.ReadBytes(0x1000));
        }

        bool restoreimg(FATXDrive xImageDrive)
        {
            if (xImageDrive == null || xImageDrive.IsDriveIO ||
                !xImageDrive.Success || xImageDrive.Type != FXDType)
                return (FXDActive = false);
            System.Threading.Thread x = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(rstthrd));
            x.Start(xImageDrive);
            while (x.IsAlive)
                System.Windows.Forms.Application.DoEvents();
            return !(FXDActive = false);
        }

        /// <summary>
        /// Restores image via file location
        /// </summary>
        /// <param name="ImageLocation"></param>
        /// <returns></returns>
        public bool RestoreImage(string ImageLocation)
        {
            if (ActiveCheck())
                return false;
            FATXDrive ximg = new FATXDrive(ImageLocation);
            if (!ximg.Success)
                return FXDActive = false;
            bool success = restoreimg(ximg);
            ximg.Close();
            return success & ReloadDrive();
        }

        /// <summary>
        /// Reloads the Partitions
        /// </summary>
        /// <returns></returns>
        public bool ReloadDrive()
        {
            if (ActiveCheck())
                return false;
            Close();
            FXDPartitions.Clear();
            return LoadPartitions();
        }

        internal void GetIO()
        {
            if (IsDriveIO && FXDType != DriveTypes.USBFlashDrive)
            {
                // Close previous handle
                if (FXDIO != null)
                {
                    FXDIO.Close();
                    // Make a new handle to a drive
                    FXDIO.OpenAgain();
                }
                else FXDIO = new DriveIO(ref FXDDrive, true);
            }
            else if (FXDType == DriveTypes.USBFlashDrive && FXDIO == null || !FXDIO.Accessed)
            {
                List<string> files = new List<string>();
                for (int i = 0; i <= 9999; i++)
                {
                    string file = FXDDrive.DeviceName + @"\Xbox360\Data" + i.ToString("000#");
                    if (File.Exists(file))
                        files.Add(file);
                    else break;
                }
                FXDIO = new MultiFileIO(files.ToArray(), true);
            }
        }

        /// <summary>
        /// Disposer
        /// </summary>
        ~FATXDrive()
        {
            Close();
            FXDDrive = null;
        }
    }
}