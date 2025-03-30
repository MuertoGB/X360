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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using X360.IO;
using X360.IO.FATXExtensions;
using X360.Other;

namespace X360.FATX
{
    /// <summary>
    /// Generic entry for FATX
    /// </summary>
    public class FATXEntry
    {
        #region Variables
        internal byte FXNameLength;
        internal string FXEntryName;
        internal int FXEntrySize;
        internal uint FXStartBlock;
        internal int FXTimestampCreated;
        internal int FXTimestampModified;
        internal int FXTimestampAccessed;
        internal bool FXIsValid = false;
        internal bool FXIsFolder = false;
        FATXPartition FXPartition;
        internal long FXOffset;
        internal FATXDrive FXDrive;

        /// <summary>
        /// Entry Size
        /// </summary>
        public int Size
        {
            get
            {
                if (!FXIsValid)
                {
                    throw FATXExcepts.ValidExcept;
                }

                return FXEntrySize;
            }
        }

        /// <summary>
        /// Entry Start Block
        /// </summary>
        public uint StartBlock
        {
            get
            {
                if (!FXIsValid)
                {
                    throw FATXExcepts.ValidExcept;
                }

                return StartBlock;
            }
        }

        /// <summary>
        /// Entry folder flag
        /// </summary>
        public bool IsFolder
        {
            get
            {
                if (!FXIsValid)
                {
                    throw FATXExcepts.ValidExcept;
                }

                return IsFolder;
            }
        }

        /// <summary>
        /// Entry name
        /// </summary>
        public string Name
        {
            get
            {
                if (!FXIsValid)
                {
                    throw FATXExcepts.ValidExcept;
                }

                return FXEntryName;
            }
            set
            {
                if (value.Length > 0x2A)
                {
                    value = value.Substring(0, 0x2A);
                }

                FXEntryName = value;

                if (FXNameLength != 0xE5)
                {
                    FXNameLength = (byte)value.Length;
                }
            }
        }

        /// <summary>
        /// is a FATX partition
        /// </summary>
        public FATXPartition Partition
        {
            get
            {
                return FXPartition;
            }
        }
        #endregion

        internal FATXEntry(ref FATXEntry entry, ref FATXDrive xdrive)
        {
            FXOffset = entry.FXOffset;
            FXNameLength = entry.FXNameLength;
            FXEntryName = entry.FXEntryName;
            FXStartBlock = entry.StartBlock;
            FXEntrySize = entry.FXEntrySize;
            FXTimestampCreated = entry.FXTimestampCreated;
            FXTimestampModified = entry.FXTimestampModified;
            FXTimestampAccessed = entry.FXTimestampAccessed;
            FXIsValid = entry.FXIsValid;
            FXIsFolder = entry.IsFolder;
            FXPartition = entry.FXPartition;
            FXDrive = entry.FXDrive;
        }

        internal FATXEntry(long position, byte[] buffer, ref FATXDrive drive)
        {
            FXDrive = drive;
            FXOffset = position;

            try
            {
                DJsIO ioStream = new DJsIO(buffer, true);
                FXNameLength = ioStream.ReadByte();

                // Validate name length
                if (FXNameLength == 0xE5 || FXNameLength == 0xFF || FXNameLength == 0 || FXNameLength > 0x2A)
                {
                    return;
                }

                byte attributeFlag = (byte)((ioStream.ReadByte() >> 4) & 1);
                byte actualNameLength = (byte)(FXNameLength & 0x3F);
                FXEntryName = ioStream.ReadString(StringForm.ASCII, actualNameLength);
                FXEntryName.IsValidXboxName();

                ioStream.Position = 0x2C;
                FXStartBlock = ioStream.ReadUInt32();

                // Check for invalid start block
                if (FXStartBlock == Constants.FATX32End)
                {
                    return;
                }

                FXEntrySize = ioStream.ReadInt32();
                FXTimestampCreated = ioStream.ReadInt32();
                FXTimestampModified = ioStream.ReadInt32();
                FXTimestampAccessed = ioStream.ReadInt32();

                FXIsFolder = attributeFlag == 1;

                // Validate entry size if not a folder
                if (!FXIsFolder && FXEntrySize == 0)
                {
                    return;
                }

                FXIsValid = true;
            }
            catch
            {
                FXIsValid = false;
            }
        }

        internal FATXEntry(string entryName, uint startBlock, int entrySize, long position, bool isFolder, ref FATXDrive drive)
        {
            int currentTimeStamp = TimeStamps.FatTimeInt(DateTime.Now);
            FXTimestampCreated = currentTimeStamp;
            FXTimestampModified = currentTimeStamp;
            FXTimestampAccessed = currentTimeStamp;

            Name = entryName;

            FXStartBlock = startBlock;
            FXEntrySize = (FXIsFolder = isFolder) ? 0 : entrySize;
            FXOffset = position;
            FXIsValid = true;
            FXDrive = drive;
        }

        internal void SetAttributes(FATXPartition partition)
        {
            FXPartition = partition;
        }

        internal byte[] GetData()
        {
            List<byte> dataBytes = new List<byte>
            {
                FXNameLength,
                (byte)((FXIsFolder ? 1 : 0) << 4)
            };

            dataBytes.AddRange(Encoding.ASCII.GetBytes(FXEntryName));
            dataBytes.AddRange(new byte[0x2A - FXEntryName.Length]);
            dataBytes.AddRange(BitConv.GetBytes(FXStartBlock, true));
            dataBytes.AddRange(BitConv.GetBytes(FXEntrySize, true));
            dataBytes.AddRange(BitConv.GetBytes(FXTimestampCreated, true));
            dataBytes.AddRange(BitConv.GetBytes(FXTimestampModified, true));
            dataBytes.AddRange(BitConv.GetBytes(FXTimestampAccessed, true));
            return dataBytes.ToArray();
        }

        internal bool WriteEntryInternal()
        {
            try
            {
                byte[] entryData = GetData();
                FXDrive.GetIO();
                FXDrive.xIO.Position = FXOffset;
                FXDrive.xIO.Write(entryData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Writes the entry data
        /// </summary>
        /// <returns></returns>
        public bool WriteEntry()
        {
            if (FXDrive.ActiveCheck())
            {
                return false;
            }

            return (WriteEntryInternal() & !(FXDrive.xActive = false));
        }
    }

    /// <summary>
    /// Object to hold FATX File Entry
    /// </summary>
    public sealed class FATXFileEntry : FATXEntry
    {
        internal FATXFileEntry(FATXEntry x, ref FATXDrive xdrive) : base(ref x, ref xdrive) { }

        /// <summary>
        /// Overwrite the file
        /// </summary>
        /// <param name="inFile"></param>
        /// <returns></returns>
        public bool Inject(string inFile)
        {
            if (FXDrive.ActiveCheck())
            {
                return false;
            }

            DJsIO inIO;

            try
            {
                inIO = new DJsIO(inFile, DJFileMode.Open, true);
            }
            catch
            {
                return FXDrive.xActive = false;
            }

            if (inIO == null || !inIO.Accessed)
            {
                return FXDrive.xActive = false;
            }

            try
            {
                return InjectInternal(inIO) & !(FXDrive.xActive = false);
            }
            catch
            {
                inIO.Close();
                return FXDrive.xActive = false;
            }
        }

        internal bool InjectInternal(DJsIO inIO)
        {
            List<uint> blockChain = new List<uint>(Partition.xTable.GetBlocks(StartBlock));

            if (blockChain.Count == 0)
            {
                throw new Exception("No blocks found in the partition table.");
            }

            uint requiredBlockCount = inIO.BlockCountFATX(Partition);

            if (blockChain.Count < requiredBlockCount)
            {
                uint[] additionalBlocks = Partition.xTable.GetNewBlockChain((uint)(requiredBlockCount - blockChain.Count), 1);
                if (additionalBlocks.Length == 0)
                {
                    throw new Exception("Failed to retrieve new block chain.");
                }            
                blockChain.AddRange(additionalBlocks);
                uint[] updatedBlockChain = blockChain.ToArray();
                if (!Partition.xTable.WriteChain(ref updatedBlockChain))
                {
                    throw new Exception("Failed to write the block chain.");
                }
            }
            else if (blockChain.Count > requiredBlockCount)
            {
                uint[] blocksToRelease = new uint[blockChain.Count - requiredBlockCount];
                for (uint i = requiredBlockCount; i < blockChain.Count; i++)
                {
                    blocksToRelease[(int)i] = i;
                    blockChain.RemoveAt((int)i--);
                }
                if (!Partition.xTable.DC(ref blocksToRelease))
                {
                    throw new Exception("Failed to release unneeded blocks.");
                }
            }

            inIO.Position = 0;
            FXDrive.GetIO();
            foreach (uint blockIndex in blockChain)
            {
                FXDrive.xIO.Position = Partition.BlockToOffset(blockIndex);
                FXDrive.xIO.Write(inIO.ReadBytes(Partition.xBlockSize));
            }

            if ((FXEntrySize == 0 || (uint)(((FXEntrySize - 1) / Partition.xBlockSize) + 1) != requiredBlockCount) && !Partition.WriteAllocTable())
            {
                throw new Exception("Failed to write allocation table.");
            }

            FXEntrySize = (int)inIO.Length;
            inIO.Close();
            return WriteEntryInternal();
        }

        /// <summary>
        /// Replace the file
        /// </summary>
        /// <param name="inFile"></param>
        /// <returns></returns>
        public bool Replace(string inFile)
        {
            if (FXDrive.ActiveCheck())
            {
                return false;
            }

            DJsIO inIO;

            try
            {
                inIO = new DJsIO(inFile, DJFileMode.Open, true);
            }
            catch
            {
                return FXDrive.xActive = false;
            }
            if (inIO == null || !inIO.Accessed)
            {
                return FXDrive.xActive = false;
            }

            return xReplace(inIO) & !(FXDrive.xActive = false);
        }

        internal bool xReplace(DJsIO xIOIn)
        {
            uint bu = StartBlock;
            int size = FXEntrySize;
            try
            {
                uint[] curblocks = Partition.xTable.GetBlocks(StartBlock);
                uint[] blocks = Partition.xTable.GetNewBlockChain(xIOIn.BlockCountFATX(Partition), 1);
                if (blocks.Length == 0)
                    throw new Exception();
                if (!Partition.xTable.WriteChain(ref blocks))
                    throw new Exception();
                if (!Partition.xTable.DC(ref curblocks))
                    throw new Exception();
                xIOIn.Position = 0;
                FXDrive.GetIO();
                if (!Partition.WriteFile(blocks, ref xIOIn))
                    throw new Exception();
                if (!Partition.WriteAllocTable())
                    throw new Exception();
                FXStartBlock = blocks[0];
                base.FXEntrySize = (int)xIOIn.Length;
                xIOIn.Close();
                return WriteEntryInternal();
            }
            catch { xIOIn.Close(); FXStartBlock = bu; base.FXEntrySize = size; return false; }
        }

        /// <summary>
        /// Delete the file
        /// </summary>
        /// <returns></returns>
        public bool Delete()
        {
            if (FXDrive.ActiveCheck())
                return false;
            try
            {
                uint[] blocks = Partition.xTable.GetBlocks(StartBlock);
                if (blocks.Length == 0 || !Partition.xTable.DC(ref blocks) || !Partition.WriteAllocTable())
                    return (FXDrive.xActive = false);
                FXNameLength = 0xE5;
                if (!WriteEntryInternal())
                    return (FXDrive.xActive = false);
                return !(FXDrive.xActive = false);
            }
            catch { return (FXDrive.xActive = false); }
        }

        internal bool xExtract(ref DJsIO xIOOut)
        {
            try
            {
                xIOOut.Position = 0;
                uint[] xChain = Partition.xTable.GetBlocks(StartBlock);
                uint xct = (uint)(((FXEntrySize - 1) / Partition.xBlockSize) + 1);
                if (xChain.Length < xct)
                    return false;
                FXDrive.GetIO();
                for (uint i = 0; i < xct - 1; i++)
                {
                    FXDrive.xIO.Position = Partition.BlockToOffset(xChain[(int)i]);
                    xIOOut.Write(FXDrive.xIO.ReadBytes(Partition.xBlockSize));
                }
                int xleft = (int)(((FXEntrySize - 1) % Partition.xBlockSize) + 1);
                FXDrive.xIO.Position = Partition.BlockToOffset(xChain[(int)xct - 1]);
                xIOOut.Write(FXDrive.xIO.ReadBytes(xleft));
                xIOOut.Flush();
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Extract the file
        /// </summary>
        /// <param name="OutLocation"></param>
        /// <returns></returns>
        public bool Extract(string OutLocation)
        {
            if (FXDrive.ActiveCheck())
                return false;
            bool xReturn = false;
            DJsIO xIO = new DJsIO(true);
            try
            {
                xReturn = xExtract(ref xIO);
                xIO.Close();
                if (xReturn)
                    xReturn = VariousFunctions.MoveFile(xIO.FileNameLong, OutLocation);
            }
            catch
            {
                xIO.Close();
                xReturn = false;
            }
            VariousFunctions.DeleteFile(xIO.FileNameLong);
            FXDrive.xActive = false;
            return xReturn;
        }

        /// <summary>
        /// Grabs the STFS name of the package
        /// </summary>
        /// <returns></returns>
        public string GetSTFSName()
        {
            if (FXDrive.ActiveCheck())
                return null;
            string xReturn = null;
            try
            {
                if (FXEntrySize < 0x500)
                    throw new Exception();
                FXDrive.GetIO();
                uint[] blocks = Partition.xTable.GetBlocks(StartBlock);
                if (blocks.Length == 0)
                    throw new Exception();
                FXDrive.xActive = false;
                FATXStreamIO io = new FATXStreamIO(this, ref blocks, true);
                uint xBuff = io.ReadUInt32();
                if (xBuff != (uint)STFS.PackageMagic.CON &&
                    xBuff != (uint)STFS.PackageMagic.LIVE &&
                    xBuff != (uint)STFS.PackageMagic.PIRS)
                    throw new Exception();
                io.Position = 0x411;
                xReturn = io.ReadString(StringForm.Unicode, 0x80);
                io.Position = 0x340;
                byte xbase = (byte)(((io.ReadUInt32() + 0xFFF) & 0xF000) >> 0xC);
                if (io.ReadUInt32() != (uint)STFS.PackageType.Profile)
                    throw new Exception();
                io.Position = 0x379;
                if (io.ReadByte() != 0x24 || io.ReadByte() != 0)
                    throw new Exception();
                byte idx = (byte)(io.ReadByte() & 3);
                byte[] Desc = io.ReadBytes(5);
                if (idx == 0 || idx == 2)
                {
                    if (xbase != 0xA)
                        throw new Exception();
                }
                else if (idx == 1)
                {
                    if (xbase != 0xB)
                        throw new Exception();
                }
                else throw new Exception();
                io.Position = 0x395;
                STFS.STFSDescriptor xDesc = new X360.STFS.STFSDescriptor(Desc, io.ReadUInt32(), io.ReadUInt32(), idx);
                int pos = (int)xDesc.GenerateDataOffset(xDesc.DirectoryBlock);
                uint block = xDesc.DirectoryBlock;
                while (pos != -1)
                {
                    for (int i = 0; i < 0x40; i++)
                    {
                        if (pos == -1)
                            break;
                        io.Position = pos + 0x28 + (0x40 * i);
                        byte nlen = (byte)(io.ReadByte() & 0x3F);
                        if (nlen > 0x28)
                            nlen = 0x28;
                        io.Position = pos + (0x40 * i);
                        if (io.ReadString(StringForm.ASCII, nlen) == "Account")
                        {
                            io.Position = pos + (0x40 * i) + 0x2F;
                            List<byte> buff = new List<byte>(io.ReadBytes(3));
                            buff.Add(0);
                            block = BitConv.ToUInt32(buff.ToArray(), false);
                            pos = -1;
                        }
                    }
                    if (pos != -1)
                    {
                        byte shift = xDesc.TopRecord.Index;
                        if (xDesc.BlockCount >= Constants.BlockLevel[1])
                        {
                            io.Position = (int)xDesc.GenerateHashOffset(block, X360.STFS.TreeLevel.L2) + 0x14 +
                                (shift << 0xC);
                            shift = (byte)((io.ReadByte() >> 6) & 1);
                        }
                        if (xDesc.BlockCount >= Constants.BlockLevel[0])
                        {
                            io.Position = (int)xDesc.GenerateHashOffset(block, X360.STFS.TreeLevel.L1) + 0x14 +
                                (xDesc.ThisType == STFS.STFSType.Type0 ? 0 : (shift << 0xC));
                            shift = (byte)((io.ReadByte() >> 6) & 1);
                        }
                        io.Position = (int)xDesc.GenerateHashOffset(block, X360.STFS.TreeLevel.L0) + 0x15 +
                                (xDesc.ThisType == STFS.STFSType.Type0 ? 0 : (shift << 0xC));
                        List<byte> xbuff = new List<byte>(io.ReadBytes(3));
                        xbuff.Reverse();
                        xbuff.Insert(0, 3);
                        block = BitConv.ToUInt32(xbuff.ToArray(), true);
                        if (block == Constants.STFSEnd)
                            pos = -1;
                    }
                }
                if (block == 0xFFFFFF)
                    throw new Exception();
                io.Position = (int)xDesc.GenerateDataOffset(block);
                byte[] databuff = io.ReadBytes(404);
                Profile.UserAccount ua = new X360.Profile.UserAccount(new DJsIO(databuff, true), X360.Profile.AccountType.Stock, false);
                if (!ua.Success)
                {
                    ua = new X360.Profile.UserAccount(new DJsIO(databuff, true), X360.Profile.AccountType.Kits, false);
                    if (!ua.Success)
                        throw new Exception();
                }
                xReturn = ua.GetGamertag();
                io.Close();
                FXDrive.xActive = false;
                return xReturn;
            }
            catch { FXDrive.xActive = false; return xReturn; }
        }
    }

    /// <summary>
    /// Object to hold contents of a read folder
    /// </summary>
    public sealed class FATXReadContents
    {
        [CompilerGenerated]
        internal List<FATXFolderEntry> xfolds;
        [CompilerGenerated]
        internal List<FATXFileEntry> xfiles;
        [CompilerGenerated]
        internal List<FATXPartition> xsubparts = new List<FATXPartition>();

        /// <summary>
        /// Files
        /// </summary>
        public FATXFileEntry[] Files { get { return xfiles.ToArray(); } }
        /// <summary>
        /// Folders
        /// </summary>
        public FATXFolderEntry[] Folders { get { return xfolds.ToArray(); } }
        /// <summary>
        /// Subpartitions
        /// </summary>
        public FATXPartition[] SubPartitions { get { return xsubparts.ToArray(); } }

        internal FATXReadContents() { }
    }

    /// <summary>
    /// Object to hold FATX Folder
    /// </summary>
    public sealed class FATXFolderEntry : FATXEntry
    {
        internal FATXFolderEntry(FATXEntry xEntry, ref FATXDrive xdrive) : base(ref xEntry, ref xdrive) { }

        /// <summary>
        /// Reads the contents
        /// </summary>
        /// <returns></returns>
        public FATXReadContents Read()
        {
            if (FXDrive.ActiveCheck())
                return null;
            FATXReadContents xReturn = xRead();
            FXDrive.xActive = false;
            return xReturn;
        }

        internal FATXReadContents xRead()
        {
            FATXReadContents xreturn = new FATXReadContents();
            try
            {
                FXDrive.GetIO();
                List<FATXEntry> xEntries = new List<FATXEntry>();
                uint[] xBlocks = Partition.xTable.GetBlocks(StartBlock);
                for (int i = 0; i < xBlocks.Length; i++)
                {
                    long xCurrent = Partition.BlockToOffset(xBlocks[i]);
                    if (xCurrent == -1)
                        break;
                    for (int x = 0; x < Partition.xEntryCount; x++)
                    {
                        FXDrive.xIO.Position = xCurrent + (0x40 * x);
                        FATXEntry z = new FATXEntry((xCurrent + (0x40 * x)), FXDrive.xIO.ReadBytes(0x40), ref FXDrive);
                        z.SetAttributes(Partition);
                        if (z.FXIsValid)
                            xEntries.Add(z);
                        else if (z.FXNameLength != 0xE5)
                            break;
                    }
                }
                xreturn.xfolds = new List<FATXFolderEntry>();
                xreturn.xfiles = new List<FATXFileEntry>();
                for (int i = 0; i < xEntries.Count; i++)
                {
                    if (xEntries[i].IsFolder)
                        xreturn.xfolds.Add(new FATXFolderEntry(xEntries[i], ref FXDrive));
                    else xreturn.xfiles.Add(new FATXFileEntry(xEntries[i], ref FXDrive));
                }
                return xreturn;
            }
            catch { return (xreturn = null); }
        }

        /// <summary>
        /// Gets a location for a new entry
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        long GetNewEntryPos(out uint block)
        {
            block = 0;
            List<uint> xFileBlocks = new List<uint>(Partition.xTable.GetBlocks(StartBlock));
            FXDrive.GetIO();
            // Searches current allocated blocks
            for (int x = 0; x < xFileBlocks.Count; x++)
            {
                long xCurOffset = Partition.BlockToOffset(xFileBlocks[x]);
                for (int i = 0; i < Partition.xEntryCount; i++)
                {
                    FXDrive.xIO.Position = xCurOffset + (0x40 * i);
                    byte xCheck = FXDrive.xIO.ReadByte();
                    if (xCheck == 0 || xCheck > 0x2A || xCheck == 0xFF)
                        return --FXDrive.xIO.Position;
                }
            }
            uint[] xBlock = Partition.xTable.GetNewBlockChain(1, 1);
            if (xBlock.Length > 0)
            {
                // Nulls out a new block and returns the start of the new block
                FXDrive.xIO.Position = Partition.BlockToOffset(xBlock[0]);
                //List<byte> xbuff = new List<byte>();
                byte[] xnull = new byte[Partition.xBlockSize];
                FXDrive.xIO.Write(xnull);
                xFileBlocks.Add(xBlock[0]);
                block = xBlock[0];
                return Partition.BlockToOffset(xBlock[0]); // Returns the beginning of the allocated block
            }
            return -1;
        }

        /* Note: Have plans for safer and better manipulation to prevent
         * minimal block loss to human error */

        /// <summary>
        /// Adds a folder
        /// </summary>
        /// <param name="FolderName"></param>
        /// <returns></returns>
        public bool AddFolder(string FolderName)
        {
            FolderName.IsValidXboxName();
            if (FXDrive.ActiveCheck())
                return false;
            try
            {
                FATXReadContents xconts = xRead();
                foreach (FATXFolderEntry x in xconts.xfolds)
                {
                    if (x.Name == FolderName)
                        return (FXDrive.xActive = false);
                }
                DJsIO xIOIn = new DJsIO(new byte[Partition.xBlockSize], true);
                uint xnew = 0;
                long xpos = GetNewEntryPos(out xnew);
                if (xpos == -1)
                    return (FXDrive.xActive = false);
                uint[] blocks = Partition.xTable.GetNewBlockChain(xIOIn.BlockCountFATX(Partition), xnew + 1);
                if (blocks.Length == 0)
                    return (FXDrive.xActive = false);
                if (!Partition.WriteFile(blocks, ref xIOIn))
                    return (FXDrive.xActive = false);
                FATXEntry y = new FATXEntry(FolderName, blocks[0], (int)xIOIn.Length, xpos, true, ref FXDrive);
                if (!y.WriteEntryInternal())
                    return (FXDrive.xActive = false);
                if (xnew > 0)
                {
                    List<uint> fileblocks = new List<uint>(Partition.xTable.GetBlocks(StartBlock));
                    fileblocks.Add(xnew);
                    uint[] xtemp = fileblocks.ToArray();
                    if (!Partition.xTable.WriteChain(ref xtemp))
                        return (FXDrive.xActive = false);
                }
                if (!Partition.xTable.WriteChain(ref blocks))
                    return (FXDrive.xActive = false);
                if (Partition.WriteAllocTable())
                    return !(FXDrive.xActive = false);
                return (FXDrive.xActive = false);
            }
            catch { return FXDrive.xActive = false; }
        }

        /// <summary>
        /// Adds a file
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="FileLocation"></param>
        /// <param name="xType"></param>
        /// <returns></returns>
        public bool AddFile(string FileName, string FileLocation, AddType xType)
        {
            FileName.IsValidXboxName();
            if (FXDrive.ActiveCheck())
                return false;
            DJsIO xIOIn = null;
            try { xIOIn = new DJsIO(FileLocation, DJFileMode.Open, true); }
            catch { return (FXDrive.xActive = false); }
            try
            {
                FATXReadContents xconts = xRead();
                foreach (FATXFileEntry x in xconts.xfiles)
                {
                    if (x.Name == FileName)
                    {
                        bool xreturn = false;
                        if (xType == AddType.NoOverWrite)
                            return (FXDrive.xActive = false);
                        else if (xType == AddType.Inject)
                            xreturn = x.InjectInternal(xIOIn);
                        else xreturn = x.xReplace(xIOIn);
                        return (xreturn & !(FXDrive.xActive = false));
                    }
                }
                uint xnew = 0;
                long xpos = GetNewEntryPos(out xnew);
                if (xpos == -1)
                    return (FXDrive.xActive = false);
                uint[] blocks = Partition.xTable.GetNewBlockChain(xIOIn.BlockCountFATX(Partition), xnew + 1);
                if (blocks.Length == 0)
                    return (FXDrive.xActive = false);
                if (!Partition.WriteFile(blocks, ref xIOIn))
                    return (FXDrive.xActive = false);
                FATXEntry y = new FATXEntry(FileName, blocks[0], (int)xIOIn.Length, xpos, false, ref FXDrive);
                if (!y.WriteEntryInternal())
                    return (FXDrive.xActive = false);
                if (xnew > 0)
                {
                    List<uint> fileblocks = new List<uint>(Partition.xTable.GetBlocks(StartBlock));
                    fileblocks.Add(xnew);
                    uint[] xtemp = fileblocks.ToArray();
                    if (!Partition.xTable.WriteChain(ref xtemp))
                        return (FXDrive.xActive = false);
                }
                if (!Partition.xTable.WriteChain(ref blocks))
                    return (FXDrive.xActive = false);
                if (Partition.WriteAllocTable())
                    return !(FXDrive.xActive = false);
                return (FXDrive.xActive = false);
            }
            catch { xIOIn.Close(); return (FXDrive.xActive = false); }
        }

        bool xExtract(string xOut, bool Sub)
        {
            if (!VariousFunctions.xCheckDirectory(xOut))
                return false;
            FATXReadContents xread = xRead();
            if (xread == null)
                return false;
            foreach (FATXFileEntry x in xread.Files)
            {
                DJsIO xIOOut = new DJsIO(xOut + "/" + x.Name, DJFileMode.Create, true);
                if (!xIOOut.Accessed)
                    continue;
                x.xExtract(ref xIOOut);
                xIOOut.Dispose();
            }
            if (!Sub)
                return true;
            foreach (FATXFolderEntry x in xread.Folders)
                x.xExtract(xOut + "/" + x.Name, Sub);
            return true;
        }

        /// <summary>
        /// Extracts a file
        /// </summary>
        /// <param name="xOutPath"></param>
        /// <param name="IncludeSubFolders"></param>
        /// <returns></returns>
        public bool Extract(string xOutPath, bool IncludeSubFolders)
        {
            if (FXDrive.ActiveCheck())
                return false;
            xOutPath = xOutPath.Replace('\\', '/');
            if (xOutPath[xOutPath.Length - 1] == '/')
                xOutPath += FXEntryName;
            else xOutPath += "/" + FXEntryName;
            return (xExtract(xOutPath, IncludeSubFolders) &
                !(FXDrive.xActive = false));
        }
    }

    /// <summary>
    /// Object to hold a FATX partition
    /// </summary>
    public sealed class FATXPartition
    {
        #region Variables
        [CompilerGenerated]
        long xBase;
        [CompilerGenerated]
        internal int xFATSize;
        [CompilerGenerated]
        uint SectorsPerBlock;
        [CompilerGenerated]
        internal List<FATXFolderEntry> xFolders;
        [CompilerGenerated]
        internal List<FATXFileEntry> xFiles;
        [CompilerGenerated]
        internal FATXType FatType = FATXType.None;
        [CompilerGenerated]
        internal FATXDrive xdrive;
        [CompilerGenerated]
        internal AllocationTable xTable;
        [CompilerGenerated]
        internal List<FATXPartition> xExtParts;
        [CompilerGenerated]
        string xName;

        /// <summary>
        /// Folders
        /// </summary>
        public FATXFolderEntry[] Folders { get { return xFolders.ToArray(); } }
        /// <summary>
        /// Files in the partition
        /// </summary>
        public FATXFileEntry[] Files { get { return xFiles.ToArray(); } }
        /// <summary>
        /// Subpartitions
        /// </summary>
        public FATXPartition[] SubPartitions { get { return xExtParts.ToArray(); } }
        internal long xFATLocale { get { return xBase + 0x1000; } }
        long xDataStart { get { return xBase + 0x1000 + xFATSize; } }
        internal int xBlockSize { get { return (int)(SectorsPerBlock * xdrive.SectorSize); } }
        internal short xEntryCount { get { return (short)(xBlockSize / 0x40); } }
        /// <summary>
        /// Valid instance
        /// </summary>
        public bool IsValid { get { return (xFolders != null && xFiles != null && (xFolders.Count + xFiles.Count != 0)); } }
        /// <summary>
        /// Partition name
        /// </summary>
        public string PartitionName { get { return xName; } }
        #endregion

        internal FATXPartition(long xOffset, long xPartitionSize, FATXDrive xDrive, string xLocaleName)
        {
            xdrive = xDrive;
            xName = xLocaleName;
            xBase = xOffset;
            xDrive.GetIO();
            xDrive.xIO.IsBigEndian = true;
            xDrive.xIO.Position = xOffset;
            if (xDrive.xIO.ReadUInt32() != 0x58544146)
                return;
            xDrive.xIO.ReadBytes(4); // Partition ID
            SectorsPerBlock = xDrive.xIO.ReadUInt32();
            uint blockct = (uint)(xPartitionSize / xBlockSize);
            if (blockct < 0xFFF5)
                FatType = FATXType.FATX16;
            else FatType = FATXType.FATX32;
            uint dirblock = xdrive.xIO.ReadUInt32();
            xFATSize = (int)(blockct * (byte)FatType);
            xFATSize += (0x1000 - (xFATSize % 0x1000));
            xTable = new AllocationTable(new DJsIO(true), (uint)((xPartitionSize - 0x1000 - xFATSize) / xBlockSize), FatType);
            xTable.xAllocTable.Position = 0;
            xDrive.xIO.Position = xFATLocale;
            for (int i = 0; i < xFATSize; i += 0x1000)
                xTable.xAllocTable.Write(xdrive.xIO.ReadBytes(0x1000));
            xTable.xAllocTable.Flush();
            long DirOffset = BlockToOffset(dirblock);
            xFolders = new List<FATXFolderEntry>();
            xFiles = new List<FATXFileEntry>();
            List<FATXEntry> xEntries = new List<FATXEntry>();
            for (byte x = 0; x < xEntryCount; x++)
            {
                xDrive.xIO.Position = DirOffset + (0x40 * x);
                FATXEntry z = new FATXEntry((DirOffset + (0x40 * x)), xdrive.xIO.ReadBytes(0x40), ref xdrive);
                z.SetAttributes(this);
                if (z.FXIsValid)
                    xEntries.Add(z);
                else if (z.FXNameLength != 0xE5)
                    break;
            }
            foreach (FATXEntry x in xEntries)
            {
                if (x.IsFolder)
                    xFolders.Add(new FATXFolderEntry(x, ref xdrive));
                else xFiles.Add(new FATXFileEntry(x, ref xdrive));
            }
            xExtParts = new List<FATXPartition>();
            for (int i = 0; i < xFiles.Count; i++)
            {
                if (xFiles[i].Name.ToLower() != "extendedsystem.partition")
                    continue;
                FATXPartition x = new FATXPartition(BlockToOffset(xFiles[i].StartBlock), xFiles[i].Size, xdrive, xFiles[i].Name);
                if (!x.IsValid)
                    continue;
                xExtParts.Add(x);
                xFiles.RemoveAt(i--);
            }
        }

        internal long BlockToOffset(uint xBlock)
        {
            switch (FatType)
            {
                case FATXType.FATX16:
                    return ((xBlock == Constants.FATX16End || xBlock == 0 || xBlock >= xTable.BlockCount) ? -1 : ((long)(xBlock - 1) * (long)xBlockSize) + xDataStart);
                case FATXType.FATX32:
                    return ((xBlock == Constants.FATX32End || xBlock == 0 || xBlock >= xTable.BlockCount) ? -1 : ((long)(xBlock - 1) * (long)xBlockSize) + xDataStart);
                default: return -1;
            }

        }

        internal bool WriteFile(uint[] xChain, ref DJsIO xIOIn)
        {
            try
            {
                xdrive.GetIO();
                for (int i = 0; i < xChain.Length; i++)
                {
                    xdrive.xIO.Position = BlockToOffset(xChain[i]);
                    xIOIn.Position = (i * xBlockSize);
                    xdrive.xIO.Write(xIOIn.ReadBytes(xBlockSize));
                    xdrive.xIO.Flush();
                }
                return true;
            }
            catch { return false; }
        }

        internal void RestoreAllocTable()
        {
            string xfn = xTable.xAllocTable.FileNameLong;
            xTable.xAllocTable.Close();
            xTable.xAllocTable = new DJsIO(xfn, DJFileMode.Create, true);
            xdrive.GetIO();
            xdrive.xIO.Position = xFATLocale;
            xTable.xAllocTable.Write(xdrive.xIO.ReadBytes(xFATSize));
            xTable.xAllocTable.Flush();
        }

        internal bool WriteAllocTable()
        {
            try
            {
                xdrive.GetIO();
                xdrive.xIO.Position = xFATLocale;
                for (int i = 0; i < (((xFATSize - 1) / xdrive.SectorSize) + 1); i++)
                {
                    xTable.xAllocTable.Position = i * xdrive.SectorSize;
                    xdrive.xIO.Write(xTable.xAllocTable.ReadBytes((int)xdrive.SectorSize));
                    xdrive.xIO.Flush();
                }
                return true;
            }
            catch { return false; }
        }
    }

    class AllocationTable
    {
        [CompilerGenerated]
        public DJsIO xAllocTable;
        [CompilerGenerated]
        public uint BlockCount;
        [CompilerGenerated]
        FATXType PartitionType;

        public AllocationTable(DJsIO xIOIn, uint xCount, FATXType xType)
        {
            xAllocTable = xIOIn;
            BlockCount = xCount;
            PartitionType = xType;
        }

        internal bool DC(ref uint[] xChain)
        {
            if (PartitionType == FATXType.None)
                return false;
            try
            {
                for (int i = 0; i < xChain.Length; i++)
                {
                    if (xChain[i] >= BlockCount || xChain[i] == 0)
                        continue;
                    for (int x = 0; x < (byte)PartitionType; x++)
                        xAllocTable.Write(0);
                }
                return true;
            }
            catch { return false; }
        }

        uint GetNextBlock(uint xBlock)
        {
            if (PartitionType == FATXType.None)
                return Constants.FATX32End;
            xAllocTable.Position = (xBlock * (byte)PartitionType);
            List<byte> xList = xAllocTable.ReadBytes((byte)PartitionType).ToList();
            for (int i = (int)PartitionType; i < 4; i++)
                xList.Insert(0, 0);
            return BitConv.ToUInt32(xList.ToArray(), true);
        }

        internal uint[] GetBlocks(uint xBlock)
        {
            List<uint> xReturn = new List<uint>();
            while (xBlock < BlockCount && xBlock != 0)
            {
                switch (PartitionType)
                {
                    case FATXType.FATX16:
                        if (xBlock == Constants.FATX16End)
                            return xReturn.ToArray();
                        break;
                    case FATXType.FATX32:
                        if (xBlock == Constants.FATX32End)
                            return xReturn.ToArray();
                        break;
                    default: return xReturn.ToArray();
                }
                if (!xReturn.Contains(xBlock))
                    xReturn.Add(xBlock);
                else break;
                xBlock = GetNextBlock(xBlock);
            }
            return xReturn.ToArray();
        }

        internal bool WriteChain(ref uint[] xChain)
        {
            if (PartitionType == FATXType.None)
                return false;
            try
            {
                for (int i = 0; i < xChain.Length; i++)
                {
                    xAllocTable.Position = (xChain[i] * (byte)PartitionType);
                    uint xblock = (i < (xChain.Length - 1)) ?
                        xChain[i + 1] : Constants.FATX32End;
                    switch (PartitionType)
                    {
                        case FATXType.FATX16: xAllocTable.Write((ushort)xblock); break;

                        case FATXType.FATX32: xAllocTable.Write(xblock); break;

                        default: break;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        internal uint[] GetNewBlockChain(uint xCount, uint xBlockStart)
        {
            List<uint> xReturn = new List<uint>();
            for (uint i = xBlockStart; i < BlockCount; i++)
            {
                if (xReturn.Count == xCount)
                    return xReturn.ToArray();
                xAllocTable.Position = ((byte)PartitionType * i);
                uint xCheck = Constants.FATX32End;
                switch (PartitionType)
                {
                    case FATXType.FATX16: xCheck = xAllocTable.ReadUInt16(); break;

                    case FATXType.FATX32: xCheck = xAllocTable.ReadUInt32(); break;

                    default: break;
                }
                if (xCheck == 0)
                    xReturn.Add(i);
            }
            return new uint[0];
        }
    }
}
