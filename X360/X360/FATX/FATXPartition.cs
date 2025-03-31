// NOTE This class is protected under GPL License as well as terms and conditions.
// Most notably, you must not obfuscate/protect this code, you must include an open source
// to your project that uses this code, and you must also not make profit on it.
// For more details, access:
// *http://www.gnu.org/
// *License included in the library source
// *License located at X360.PublicResources.GPL30
// *X360.XAbout.GNUProtected for GNU and TaC (Terms and Conditions)
// You agree to these terms when you use this code.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using X360.IO;
using X360.Other;

namespace X360.FATX
{
    /// <summary>
    /// Object to hold a FATX partition
    /// </summary>
    public sealed class FATXPartition
    {
        #region Variables
        readonly long FXPBase;
        internal int FXPFATSize;
        readonly uint FXPSectorsPerBlock;
        internal List<FATXFolderEntry> FXPFolders;
        internal List<FATXFileEntry> FXPFiles;
        internal FATXType FXPFatType = FATXType.None;
        internal FATXDrive FXPDrive;
        internal AllocationTable FXPAllocTable;
        internal List<FATXPartition> FXPExtParts;
        readonly string FXPName;

        /// <summary>
        /// Folders
        /// </summary>
        public FATXFolderEntry[] Folders { get { return FXPFolders.ToArray(); } }
        /// <summary>
        /// Files in the partition
        /// </summary>
        public FATXFileEntry[] Files { get { return FXPFiles.ToArray(); } }
        /// <summary>
        /// Subpartitions
        /// </summary>
        public FATXPartition[] SubPartitions { get { return FXPExtParts.ToArray(); } }
        internal long xFATLocale { get { return FXPBase + 0x1000; } }
        long xDataStart { get { return FXPBase + 0x1000 + FXPFATSize; } }
        internal int xBlockSize { get { return (int)(FXPSectorsPerBlock * FXPDrive.SectorSize); } }
        internal short xEntryCount { get { return (short)(xBlockSize / 0x40); } }
        /// <summary>
        /// Valid instance
        /// </summary>
        public bool IsValid { get { return (FXPFolders != null && FXPFiles != null && (FXPFolders.Count + FXPFiles.Count != 0)); } }
        /// <summary>
        /// Partition name
        /// </summary>
        public string PartitionName { get { return FXPName; } }
        #endregion

        internal FATXPartition(long xOffset, long xPartitionSize, FATXDrive xDrive, string xLocaleName)
        {
            FXPDrive = xDrive;
            FXPName = xLocaleName;
            FXPBase = xOffset;
            xDrive.GetIO();
            xDrive.FXDIO.IsBigEndian = true;
            xDrive.FXDIO.Position = xOffset;
            if (xDrive.FXDIO.ReadUInt32() != 0x58544146)
                return;
            xDrive.FXDIO.ReadBytes(4); // Partition ID
            FXPSectorsPerBlock = xDrive.FXDIO.ReadUInt32();
            uint blockct = (uint)(xPartitionSize / xBlockSize);
            if (blockct < 0xFFF5)
                FXPFatType = FATXType.FATX16;
            else FXPFatType = FATXType.FATX32;
            uint dirblock = FXPDrive.FXDIO.ReadUInt32();
            FXPFATSize = (int)(blockct * (byte)FXPFatType);
            FXPFATSize += (0x1000 - (FXPFATSize % 0x1000));
            FXPAllocTable = new AllocationTable(new FXIO(true), (uint)((xPartitionSize - 0x1000 - FXPFATSize) / xBlockSize), FXPFatType);
            FXPAllocTable.xAllocTable.Position = 0;
            xDrive.FXDIO.Position = xFATLocale;
            for (int i = 0; i < FXPFATSize; i += 0x1000)
                FXPAllocTable.xAllocTable.Write(FXPDrive.FXDIO.ReadBytes(0x1000));
            FXPAllocTable.xAllocTable.Flush();
            long DirOffset = BlockToOffset(dirblock);
            FXPFolders = new List<FATXFolderEntry>();
            FXPFiles = new List<FATXFileEntry>();
            List<FATXEntry> xEntries = new List<FATXEntry>();
            for (byte x = 0; x < xEntryCount; x++)
            {
                xDrive.FXDIO.Position = DirOffset + (0x40 * x);
                FATXEntry z = new FATXEntry((DirOffset + (0x40 * x)), FXPDrive.FXDIO.ReadBytes(0x40), ref FXPDrive);
                z.SetAttributes(this);
                if (z.FXEIsValid)
                    xEntries.Add(z);
                else if (z.FXENameLength != 0xE5)
                    break;
            }
            foreach (FATXEntry x in xEntries)
            {
                if (x.IsFolder)
                    FXPFolders.Add(new FATXFolderEntry(x, ref FXPDrive));
                else FXPFiles.Add(new FATXFileEntry(x, ref FXPDrive));
            }
            FXPExtParts = new List<FATXPartition>();
            for (int i = 0; i < FXPFiles.Count; i++)
            {
                if (FXPFiles[i].Name.ToLower() != "extendedsystem.partition")
                    continue;
                FATXPartition x = new FATXPartition(BlockToOffset(FXPFiles[i].StartBlock), FXPFiles[i].Size, FXPDrive, FXPFiles[i].Name);
                if (!x.IsValid)
                    continue;
                FXPExtParts.Add(x);
                FXPFiles.RemoveAt(i--);
            }
        }

        internal long BlockToOffset(uint xBlock)
        {
            switch (FXPFatType)
            {
                case FATXType.FATX16:
                    return ((xBlock == Constants.FATX16End || xBlock == 0 || xBlock >= FXPAllocTable.BlockCount) ? -1 : ((long)(xBlock - 1) * (long)xBlockSize) + xDataStart);
                case FATXType.FATX32:
                    return ((xBlock == Constants.FATX32End || xBlock == 0 || xBlock >= FXPAllocTable.BlockCount) ? -1 : ((long)(xBlock - 1) * (long)xBlockSize) + xDataStart);
                default: return -1;
            }

        }

        internal bool WriteFile(uint[] xChain, ref FXIO xIOIn)
        {
            try
            {
                FXPDrive.GetIO();
                for (int i = 0; i < xChain.Length; i++)
                {
                    FXPDrive.FXDIO.Position = BlockToOffset(xChain[i]);
                    xIOIn.Position = (i * xBlockSize);
                    FXPDrive.FXDIO.Write(xIOIn.ReadBytes(xBlockSize));
                    FXPDrive.FXDIO.Flush();
                }
                return true;
            }
            catch { return false; }
        }

        internal void RestoreAllocTable()
        {
            string xfn = FXPAllocTable.xAllocTable.FileNameLong;
            FXPAllocTable.xAllocTable.Close();
            FXPAllocTable.xAllocTable = new FXIO(xfn, DJFileMode.Create, true);
            FXPDrive.GetIO();
            FXPDrive.FXDIO.Position = xFATLocale;
            FXPAllocTable.xAllocTable.Write(FXPDrive.FXDIO.ReadBytes(FXPFATSize));
            FXPAllocTable.xAllocTable.Flush();
        }

        internal bool WriteAllocTable()
        {
            try
            {
                FXPDrive.GetIO();
                FXPDrive.FXDIO.Position = xFATLocale;
                for (int i = 0; i < (((FXPFATSize - 1) / FXPDrive.SectorSize) + 1); i++)
                {
                    FXPAllocTable.xAllocTable.Position = i * FXPDrive.SectorSize;
                    FXPDrive.FXDIO.Write(FXPAllocTable.xAllocTable.ReadBytes((int)FXPDrive.SectorSize));
                    FXPDrive.FXDIO.Flush();
                }
                return true;
            }
            catch { return false; }
        }
    }

    class AllocationTable
    {
        [CompilerGenerated]
        public FXIO xAllocTable;
        [CompilerGenerated]
        public uint BlockCount;
        [CompilerGenerated]
        FATXType PartitionType;

        public AllocationTable(FXIO xIOIn, uint xCount, FATXType xType)
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