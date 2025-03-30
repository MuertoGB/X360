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
        internal byte FXENameLength;
        internal string FXEEntryName;
        internal int FXEEntrySize;
        internal uint FXEStartBlock;
        internal int FXETimestampCreated;
        internal int FXETimestampModified;
        internal int FXETimestampAccessed;
        internal bool FXEIsValid = false;
        internal bool FXEIsFolder = false;
        internal FATXPartition FXEPartition;
        internal long FXEOffset;
        internal FATXDrive FXEDrive;

        /// <summary>
        /// Entry Size
        /// </summary>
        public int Size
        {
            get
            {
                if (!FXEIsValid)
                {
                    throw FATXExcepts.ValidExcept;
                }

                return FXEEntrySize;
            }
        }

        /// <summary>
        /// Entry Start Block
        /// </summary>
        public uint StartBlock
        {
            get
            {
                if (!FXEIsValid)
                {
                    throw FATXExcepts.ValidExcept;
                }

                return FXEStartBlock;
            }
        }

        /// <summary>
        /// Entry folder flag
        /// </summary>
        public bool IsFolder
        {
            get
            {
                if (!FXEIsValid)
                {
                    throw FATXExcepts.ValidExcept;
                }

                return FXEIsFolder;
            }
        }

        /// <summary>
        /// Entry name
        /// </summary>
        public string Name
        {
            get
            {
                if (!FXEIsValid)
                {
                    throw FATXExcepts.ValidExcept;
                }

                return FXEEntryName;
            }
            set
            {
                if (value.Length > 0x2A)
                {
                    value = value.Substring(0, 0x2A);
                }

                FXEEntryName = value;

                if (FXENameLength != 0xE5)
                {
                    FXENameLength = (byte)value.Length;
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
                return FXEPartition;
            }
        }
        #endregion

        internal FATXEntry(ref FATXEntry entry, ref FATXDrive drive)
        {
            FXEOffset = entry.FXEOffset;
            FXENameLength = entry.FXENameLength;
            FXEEntryName = entry.FXEEntryName;
            FXEStartBlock = entry.StartBlock;
            FXEEntrySize = entry.FXEEntrySize;
            FXETimestampCreated = entry.FXETimestampCreated;
            FXETimestampModified = entry.FXETimestampModified;
            FXETimestampAccessed = entry.FXETimestampAccessed;
            FXEIsValid = entry.FXEIsValid;
            FXEIsFolder = entry.IsFolder;
            FXEPartition = entry.FXEPartition;
            FXEDrive = entry.FXEDrive;
        }

        internal FATXEntry(long position, byte[] buffer, ref FATXDrive drive)
        {
            FXEDrive = drive;
            FXEOffset = position;

            try
            {
                FXIO ioStream = new FXIO(buffer, true);
                FXENameLength = ioStream.ReadByte();

                // Validate name length
                if (FXENameLength == 0xE5 || FXENameLength == 0xFF || FXENameLength == 0 || FXENameLength > 0x2A)
                {
                    return;
                }

                byte attributeFlag = (byte)((ioStream.ReadByte() >> 4) & 1);
                byte actualNameLength = (byte)(FXENameLength & 0x3F);
                FXEEntryName = ioStream.ReadString(StringForm.ASCII, actualNameLength);
                FXEEntryName.IsValidXboxName();

                ioStream.Position = 0x2C;
                FXEStartBlock = ioStream.ReadUInt32();

                // Check for invalid start block
                if (FXEStartBlock == Constants.FATX32End)
                {
                    return;
                }

                FXEEntrySize = ioStream.ReadInt32();
                FXETimestampCreated = ioStream.ReadInt32();
                FXETimestampModified = ioStream.ReadInt32();
                FXETimestampAccessed = ioStream.ReadInt32();

                FXEIsFolder = attributeFlag == 1;

                // Validate entry size if not a folder
                if (!FXEIsFolder && FXEEntrySize == 0)
                {
                    return;
                }

                FXEIsValid = true;
            }
            catch
            {
                FXEIsValid = false;
            }
        }

        internal FATXEntry(string entryName, uint startBlock, int entrySize, long position, bool isFolder, ref FATXDrive drive)
        {
            int currentTimeStamp = TimeStamps.FatTimeInt(DateTime.Now);
            FXETimestampCreated = currentTimeStamp;
            FXETimestampModified = currentTimeStamp;
            FXETimestampAccessed = currentTimeStamp;

            Name = entryName;

            FXEStartBlock = startBlock;
            FXEEntrySize = (FXEIsFolder = isFolder) ? 0 : entrySize;
            FXEOffset = position;
            FXEIsValid = true;
            FXEDrive = drive;
        }

        internal void SetAttributes(FATXPartition partition)
        {
            FXEPartition = partition;
        }

        internal byte[] GetData()
        {
            List<byte> dataBytes = new List<byte>
            {
                FXENameLength,
                (byte)((FXEIsFolder ? 1 : 0) << 4)
            };

            dataBytes.AddRange(Encoding.ASCII.GetBytes(FXEEntryName));
            dataBytes.AddRange(new byte[0x2A - FXEEntryName.Length]);
            dataBytes.AddRange(BitConv.GetBytes(FXEStartBlock, true));
            dataBytes.AddRange(BitConv.GetBytes(FXEEntrySize, true));
            dataBytes.AddRange(BitConv.GetBytes(FXETimestampCreated, true));
            dataBytes.AddRange(BitConv.GetBytes(FXETimestampModified, true));
            dataBytes.AddRange(BitConv.GetBytes(FXETimestampAccessed, true));
            return dataBytes.ToArray();
        }

        internal bool WriteEntryInternal()
        {
            try
            {
                byte[] entryData = GetData();
                FXEDrive.GetIO();
                FXEDrive.FXDIO.Position = FXEOffset;
                FXEDrive.FXDIO.Write(entryData);
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
            if (FXEDrive.ActiveCheck())
            {
                return false;
            }

            return (WriteEntryInternal() & !(FXEDrive.Active = false));
        }
    }

    /// <summary>
    /// Object to hold FATX File Entry
    /// </summary>
    public sealed class FATXFileEntry : FATXEntry
    {
        internal FATXFileEntry(FATXEntry entry, ref FATXDrive drive) : base(ref entry, ref drive) { }

        /// <summary>
        /// Overwrite the file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public bool Inject(string file)
        {
            if (FXEDrive.ActiveCheck())
            {
                return false;
            }

            FXIO io;

            try
            {
                io = new FXIO(file, DJFileMode.Open, true);
            }
            catch
            {
                return FXEDrive.Active = false;
            }

            if (io == null || !io.Accessed)
            {
                return FXEDrive.Active = false;
            }

            try
            {
                return InjectInternal(io) & !(FXEDrive.Active = false);
            }
            catch
            {
                io.Close();
                return FXEDrive.Active = false;
            }
        }

        internal bool InjectInternal(FXIO inputIO)
        {
            List<uint> blockChain = new List<uint>(Partition.FXPAllocTable.GetBlocks(StartBlock));

            if (blockChain.Count == 0)
            {
                throw new Exception("No blocks found in the partition table.");
            }

            uint requiredBlockCount = inputIO.BlockCountFATX(Partition);

            if (blockChain.Count < requiredBlockCount)
            {
                uint[] additionalBlocks = Partition.FXPAllocTable.GetNewBlockChain((uint)(requiredBlockCount - blockChain.Count), 1);
                if (additionalBlocks.Length == 0)
                {
                    throw new Exception("Failed to retrieve new block chain.");
                }
                blockChain.AddRange(additionalBlocks);
                uint[] updatedBlockChain = blockChain.ToArray();
                if (!Partition.FXPAllocTable.WriteChain(ref updatedBlockChain))
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
                if (!Partition.FXPAllocTable.DC(ref blocksToRelease))
                {
                    throw new Exception("Failed to release unneeded blocks.");
                }
            }

            inputIO.Position = 0;
            FXEDrive.GetIO();
            foreach (uint blockIndex in blockChain)
            {
                FXEDrive.FXDIO.Position = Partition.BlockToOffset(blockIndex);
                FXEDrive.FXDIO.Write(inputIO.ReadBytes(Partition.xBlockSize));
            }

            if ((FXEEntrySize == 0 || (uint)(((FXEEntrySize - 1) / Partition.xBlockSize) + 1) != requiredBlockCount) && !Partition.WriteAllocTable())
            {
                throw new Exception("Failed to write allocation table.");
            }

            FXEEntrySize = (int)inputIO.Length;
            inputIO.Close();
            return WriteEntryInternal();
        }

        /// <summary>
        /// Replace the file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public bool Replace(string file)
        {
            if (FXEDrive.ActiveCheck())
            {
                return false;
            }

            FXIO io;

            try
            {
                io = new FXIO(file, DJFileMode.Open, true);
            }
            catch
            {
                return FXEDrive.Active = false;
            }
            if (io == null || !io.Accessed)
            {
                return FXEDrive.Active = false;
            }

            return ReplaceInternal(io) & !(FXEDrive.Active = false);
        }

        internal bool ReplaceInternal(FXIO inputIO)
        {
            uint originalStartBlock = StartBlock;
            int originalEntrySize = FXEEntrySize;

            try
            {
                uint[] currentBlocks = Partition.FXPAllocTable.GetBlocks(StartBlock);
                uint[] newBlocks = Partition.FXPAllocTable.GetNewBlockChain(inputIO.BlockCountFATX(Partition), 1);

                if (newBlocks.Length == 0)
                {
                    throw new Exception("Failed to retrieve new blocks for replacement.");
                }

                if (!Partition.FXPAllocTable.WriteChain(ref newBlocks))
                {
                    throw new Exception("Failed to write the new block chain.");
                }

                if (!Partition.FXPAllocTable.DC(ref currentBlocks))
                {
                    throw new Exception("Failed to decommission current blocks.");
                }

                inputIO.Position = 0;
                FXEDrive.GetIO();

                if (!Partition.WriteFile(newBlocks, ref inputIO))
                {
                    throw new Exception("Failed to write file data to the new blocks.");
                }

                if (!Partition.WriteAllocTable())
                {
                    throw new Exception("Failed to write the allocation table.");
                }

                FXEStartBlock = newBlocks[0];
                FXEEntrySize = (int)inputIO.Length;

                inputIO.Close();
                return WriteEntryInternal();
            }
            catch
            {
                // Rollback in case of failure
                inputIO.Close();
                FXEStartBlock = originalStartBlock;
                FXEEntrySize = originalEntrySize;
                return false;
            }
        }

        /// <summary>
        /// Delete the file
        /// </summary>
        /// <returns></returns>
        public bool Delete()
        {
            if (FXEDrive.ActiveCheck())
            {
                return false;
            }

            try
            {
                uint[] entryBlocks = Partition.FXPAllocTable.GetBlocks(StartBlock);

                if (entryBlocks.Length == 0 || !Partition.FXPAllocTable.DC(ref entryBlocks) || !Partition.WriteAllocTable())
                {
                    return FXEDrive.Active = false;
                }

                FXENameLength = 0xE5;

                if (!WriteEntryInternal())
                {
                    return FXEDrive.Active = false;
                }

                return !(FXEDrive.Active = false);
            }

            catch
            {
                return FXEDrive.Active = false;
            }
        }

        internal bool Extract(ref FXIO outputIO)
        {
            try
            {
                outputIO.Position = 0;

                uint[] blockChain = Partition.FXPAllocTable.GetBlocks(StartBlock);

                // Calculate the number of blocks required for the entry
                uint requiredBlockCount = (uint)(((FXEEntrySize - 1) / Partition.xBlockSize) + 1);

                // If there are not enough blocks, return false
                if (blockChain.Length < requiredBlockCount)
                {
                    return false;
                }

                FXEDrive.GetIO();

                for (uint i = 0; i < requiredBlockCount - 1; i++)
                {
                    FXEDrive.FXDIO.Position = Partition.BlockToOffset(blockChain[i]);
                    outputIO.Write(FXEDrive.FXDIO.ReadBytes(Partition.xBlockSize));
                }

                int remainingBytes = (int)(((FXEEntrySize - 1) % Partition.xBlockSize) + 1);
                FXEDrive.FXDIO.Position = Partition.BlockToOffset(blockChain[requiredBlockCount - 1]);
                outputIO.Write(FXEDrive.FXDIO.ReadBytes(remainingBytes));

                outputIO.Flush();

                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// Extract the file
        /// </summary>
        /// <param name="outLocation"></param>
        /// <returns></returns>
        public bool Extract(string outLocation)
        {
            if (FXEDrive.ActiveCheck())
            {
                return false;
            }

            FXIO io = new FXIO(true);

            bool extractSuccess;

            try
            {
                extractSuccess = Extract(ref io);
                io.Close();

                if (extractSuccess)
                {
                    extractSuccess = VariousFunctions.MoveFile(io.FileNameLong, outLocation);
                }
            }
            catch
            {
                io.Close();
                extractSuccess = false;
            }
            finally
            {
                // Ensure file deletion and drive deactivation
                VariousFunctions.DeleteFile(io.FileNameLong);
                FXEDrive.Active = false;
            }

            return extractSuccess;
        }

        /// <summary>
        /// Grabs the STFS name of the package
        /// </summary>
        /// <returns></returns>
        public string GetSTFSName()
        {
            if (FXEDrive.ActiveCheck())
            {
                return null;
            }

            string extractedGamertag = null;

            try
            {
                // Validate entry size
                if (FXEEntrySize < 0x500)
                {
                    throw new Exception("Invalid entry size. Expected size >= 0x500.");
                }

                // Fetch block allocation
                FXEDrive.GetIO();
                uint[] allocationBlocks = Partition.FXPAllocTable.GetBlocks(StartBlock);

                if (allocationBlocks.Length == 0)
                {
                    throw new Exception("Failed to retrieve block allocation.");
                }

                FXEDrive.Active = false;
                FATXStreamIO io = new FATXStreamIO(this, ref allocationBlocks, true);

                try
                {
                    uint packageMagic = io.ReadUInt32();

                    if (packageMagic != (uint)STFS.PackageMagic.CON &&
                        packageMagic != (uint)STFS.PackageMagic.LIVE &&
                        packageMagic != (uint)STFS.PackageMagic.PIRS)
                    {
                        throw new Exception("Unsupported package type detected.");
                    }

                    io.Position = 0x411;
                    extractedGamertag = io.ReadString(StringForm.Unicode, 0x80);

                    io.Position = 0x340;
                    byte packageBase = (byte)(((io.ReadUInt32() + 0xFFF) & 0xF000) >> 0xC);

                    if (io.ReadUInt32() != (uint)STFS.PackageType.Profile)
                    {
                        throw new Exception("Invalid package type. Expected Profile.");
                    }

                    io.Position = 0x379;
                    if (io.ReadByte() != 0x24 || io.ReadByte() != 0)
                    {
                        throw new Exception("Invalid account type detected.");
                    }

                    byte accountIndex = (byte)(io.ReadByte() & 3);
                    byte[] descriptorBytes = io.ReadBytes(5);

                    if ((accountIndex == 0 || accountIndex == 2) && packageBase != 0xA ||
                        accountIndex == 1 && packageBase != 0xB ||
                        accountIndex > 2)
                    {
                        throw new Exception("Unexpected account index or package base.");
                    }

                    io.Position = 0x395;
                    STFS.STFSDescriptor stfsDescriptor = new STFS.STFSDescriptor(descriptorBytes, io.ReadUInt32(), io.ReadUInt32(), accountIndex);

                    int dataOffset = (int)stfsDescriptor.GenerateDataOffset(stfsDescriptor.DirectoryBlock);
                    uint currentBlock = stfsDescriptor.DirectoryBlock;

                    while (dataOffset != -1)
                    {
                        for (int i = 0; i < 0x40; i++)
                        {
                            if (dataOffset == -1)
                            {
                                break;
                            }

                            io.Position = dataOffset + 0x28 + (0x40 * i);
                            byte nameLength = (byte)(io.ReadByte() & 0x3F);
                            nameLength = nameLength > 0x28 ? (byte)0x28 : nameLength;

                            io.Position = dataOffset + (0x40 * i);
                            if (io.ReadString(StringForm.ASCII, nameLength) == "Account")
                            {
                                io.Position = dataOffset + (0x40 * i) + 0x2F;
                                List<byte> blockBytes = new List<byte>(io.ReadBytes(3)) { 0 };
                                currentBlock = BitConv.ToUInt32(blockBytes.ToArray(), false);
                                dataOffset = -1;
                                break;
                            }
                        }

                        if (dataOffset != -1)
                        {
                            byte shift = stfsDescriptor.TopRecord.Index;

                            // Resolve block using hash tables
                            if (stfsDescriptor.BlockCount >= Constants.BlockLevel[1])
                            {
                                io.Position = (int)stfsDescriptor.GenerateHashOffset(currentBlock, STFS.TreeLevel.L2) + 0x14 + (shift << 0xC);
                                shift = (byte)((io.ReadByte() >> 6) & 1);
                            }

                            if (stfsDescriptor.BlockCount >= Constants.BlockLevel[0])
                            {
                                io.Position = (int)stfsDescriptor.GenerateHashOffset(currentBlock, STFS.TreeLevel.L1) + 0x14 +
                                              (stfsDescriptor.ThisType == STFS.STFSType.Type0 ? 0 : (shift << 0xC));
                                shift = (byte)((io.ReadByte() >> 6) & 1);
                            }

                            io.Position = (int)stfsDescriptor.GenerateHashOffset(currentBlock, STFS.TreeLevel.L0) + 0x15 +
                                          (stfsDescriptor.ThisType == STFS.STFSType.Type0 ? 0 : (shift << 0xC));
                            List<byte> hashBytes = new List<byte>(io.ReadBytes(3));
                            hashBytes.Reverse();
                            hashBytes.Insert(0, 3);
                            currentBlock = BitConv.ToUInt32(hashBytes.ToArray(), true);

                            if (currentBlock == Constants.STFSEnd)
                            {
                                dataOffset = -1;
                            }
                        }
                    }

                    if (currentBlock == 0xFFFFFF)
                    {
                        throw new Exception("Invalid block encountered. Operation aborted.");
                    }

                    io.Position = (int)stfsDescriptor.GenerateDataOffset(currentBlock);
                    byte[] userData = io.ReadBytes(404);
                    Profile.UserAccount userAccount = new Profile.UserAccount(new FXIO(userData, true), Profile.AccountType.Stock, false);

                    if (!userAccount.Success)
                    {
                        userAccount = new Profile.UserAccount(new FXIO(userData, true), Profile.AccountType.Kits, false);
                        if (!userAccount.Success)
                        {
                            throw new Exception("Failed to load user account data.");
                        }
                    }

                    extractedGamertag = userAccount.GetGamertag();
                }
                finally
                {
                    io.Close();
                }

                FXEDrive.Active = false;
                return extractedGamertag;
            }
            catch (Exception ex)
            {
                FXEDrive.Active = false;
                throw new Exception("Error while extracting STFS name: " + ex.Message, ex);
            }
        }
    }

    /// <summary>
    /// Object to hold contents of a read folder
    /// </summary>
    public sealed class FATXReadContents
    {
        internal List<FATXFolderEntry> FXFEFolders;
        internal List<FATXFileEntry> FXFEFiles;
        internal List<FATXPartition> FXFESubParts = new List<FATXPartition>();

        /// <summary>
        /// Files
        /// </summary>
        public FATXFileEntry[] Files => FXFEFiles.ToArray();
        /// <summary>
        /// Folders
        /// </summary>
        public FATXFolderEntry[] Folders => FXFEFolders.ToArray();
        /// <summary>
        /// Subpartitions
        /// </summary>
        public FATXPartition[] SubPartitions => FXFESubParts.ToArray();

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
            if (FXEDrive.ActiveCheck())
            {
                return null;
            }

            FATXReadContents xReturn = ReadContents();
            FXEDrive.Active = false;
            return xReturn;
        }

        internal FATXReadContents ReadContents()
        {
            FATXReadContents readResult = new FATXReadContents();

            try
            {
                FXEDrive.GetIO();
                List<FATXEntry> fatxEntries = new List<FATXEntry>();
                uint[] allocatedBlocks = Partition.FXPAllocTable.GetBlocks(StartBlock);

                for (int blockIndex = 0; blockIndex < allocatedBlocks.Length; blockIndex++)
                {
                    long blockOffset = Partition.BlockToOffset(allocatedBlocks[blockIndex]);

                    if (blockOffset == -1)
                    {
                        break;
                    }

                    for (int entryIndex = 0; entryIndex < Partition.xEntryCount; entryIndex++)
                    {
                        FXEDrive.FXDIO.Position = blockOffset + (0x40 * entryIndex);
                        FATXEntry entry = new FATXEntry(
                            (blockOffset + (0x40 * entryIndex)),
                            FXEDrive.FXDIO.ReadBytes(0x40),
                            ref FXEDrive
                        );

                        entry.SetAttributes(Partition);

                        if (entry.FXEIsValid)
                        {
                            fatxEntries.Add(entry);
                        }                         
                        else if (entry.FXENameLength != 0xE5)
                        {
                            break;
                        }   
                    }
                }

                readResult.FXFEFolders = new List<FATXFolderEntry>();
                readResult.FXFEFiles = new List<FATXFileEntry>();

                for (int entryIndex = 0; entryIndex < fatxEntries.Count; entryIndex++)
                {
                    if (fatxEntries[entryIndex].IsFolder)
                    {
                        readResult.FXFEFolders.Add(new FATXFolderEntry(fatxEntries[entryIndex], ref FXEDrive));
                    }
                    else
                    {
                        readResult.FXFEFiles.Add(new FATXFileEntry(fatxEntries[entryIndex], ref FXEDrive));
                    }
                }

                return readResult;
            }
            catch
            {
                readResult = null;
                return readResult;
            }
        }

        /// <summary>
        /// Gets a location for a new entry
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        long GetNewEntryPos(out uint block)
        {
            block = 0;
            List<uint> xFileBlocks = new List<uint>(Partition.FXPAllocTable.GetBlocks(StartBlock));
            FXEDrive.GetIO();
            // Searches current allocated blocks
            for (int x = 0; x < xFileBlocks.Count; x++)
            {
                long xCurOffset = Partition.BlockToOffset(xFileBlocks[x]);
                for (int i = 0; i < Partition.xEntryCount; i++)
                {
                    FXEDrive.FXDIO.Position = xCurOffset + (0x40 * i);
                    byte xCheck = FXEDrive.FXDIO.ReadByte();
                    if (xCheck == 0 || xCheck > 0x2A || xCheck == 0xFF)
                        return --FXEDrive.FXDIO.Position;
                }
            }
            uint[] xBlock = Partition.FXPAllocTable.GetNewBlockChain(1, 1);
            if (xBlock.Length > 0)
            {
                // Nulls out a new block and returns the start of the new block
                FXEDrive.FXDIO.Position = Partition.BlockToOffset(xBlock[0]);
                //List<byte> xbuff = new List<byte>();
                byte[] xnull = new byte[Partition.xBlockSize];
                FXEDrive.FXDIO.Write(xnull);
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
            if (FXEDrive.ActiveCheck())
                return false;
            try
            {
                FATXReadContents xconts = ReadContents();
                foreach (FATXFolderEntry x in xconts.FXFEFolders)
                {
                    if (x.Name == FolderName)
                        return (FXEDrive.Active = false);
                }
                FXIO xIOIn = new FXIO(new byte[Partition.xBlockSize], true);
                uint xnew = 0;
                long xpos = GetNewEntryPos(out xnew);
                if (xpos == -1)
                    return (FXEDrive.Active = false);
                uint[] blocks = Partition.FXPAllocTable.GetNewBlockChain(xIOIn.BlockCountFATX(Partition), xnew + 1);
                if (blocks.Length == 0)
                    return (FXEDrive.Active = false);
                if (!Partition.WriteFile(blocks, ref xIOIn))
                    return (FXEDrive.Active = false);
                FATXEntry y = new FATXEntry(FolderName, blocks[0], (int)xIOIn.Length, xpos, true, ref FXEDrive);
                if (!y.WriteEntryInternal())
                    return (FXEDrive.Active = false);
                if (xnew > 0)
                {
                    List<uint> fileblocks = new List<uint>(Partition.FXPAllocTable.GetBlocks(StartBlock));
                    fileblocks.Add(xnew);
                    uint[] xtemp = fileblocks.ToArray();
                    if (!Partition.FXPAllocTable.WriteChain(ref xtemp))
                        return (FXEDrive.Active = false);
                }
                if (!Partition.FXPAllocTable.WriteChain(ref blocks))
                    return (FXEDrive.Active = false);
                if (Partition.WriteAllocTable())
                    return !(FXEDrive.Active = false);
                return (FXEDrive.Active = false);
            }
            catch { return FXEDrive.Active = false; }
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
            if (FXEDrive.ActiveCheck())
                return false;
            FXIO xIOIn = null;
            try { xIOIn = new FXIO(FileLocation, DJFileMode.Open, true); }
            catch { return (FXEDrive.Active = false); }
            try
            {
                FATXReadContents xconts = ReadContents();
                foreach (FATXFileEntry x in xconts.FXFEFiles)
                {
                    if (x.Name == FileName)
                    {
                        bool xreturn = false;
                        if (xType == AddType.NoOverWrite)
                            return (FXEDrive.Active = false);
                        else if (xType == AddType.Inject)
                            xreturn = x.InjectInternal(xIOIn);
                        else xreturn = x.ReplaceInternal(xIOIn);
                        return (xreturn & !(FXEDrive.Active = false));
                    }
                }
                uint xnew = 0;
                long xpos = GetNewEntryPos(out xnew);
                if (xpos == -1)
                    return (FXEDrive.Active = false);
                uint[] blocks = Partition.FXPAllocTable.GetNewBlockChain(xIOIn.BlockCountFATX(Partition), xnew + 1);
                if (blocks.Length == 0)
                    return (FXEDrive.Active = false);
                if (!Partition.WriteFile(blocks, ref xIOIn))
                    return (FXEDrive.Active = false);
                FATXEntry y = new FATXEntry(FileName, blocks[0], (int)xIOIn.Length, xpos, false, ref FXEDrive);
                if (!y.WriteEntryInternal())
                    return (FXEDrive.Active = false);
                if (xnew > 0)
                {
                    List<uint> fileblocks = new List<uint>(Partition.FXPAllocTable.GetBlocks(StartBlock));
                    fileblocks.Add(xnew);
                    uint[] xtemp = fileblocks.ToArray();
                    if (!Partition.FXPAllocTable.WriteChain(ref xtemp))
                        return (FXEDrive.Active = false);
                }
                if (!Partition.FXPAllocTable.WriteChain(ref blocks))
                    return (FXEDrive.Active = false);
                if (Partition.WriteAllocTable())
                    return !(FXEDrive.Active = false);
                return (FXEDrive.Active = false);
            }
            catch { xIOIn.Close(); return (FXEDrive.Active = false); }
        }

        bool xExtract(string xOut, bool Sub)
        {
            if (!VariousFunctions.xCheckDirectory(xOut))
                return false;
            FATXReadContents xread = ReadContents();
            if (xread == null)
                return false;
            foreach (FATXFileEntry x in xread.Files)
            {
                FXIO xIOOut = new FXIO(xOut + "/" + x.Name, DJFileMode.Create, true);
                if (!xIOOut.Accessed)
                    continue;
                x.Extract(ref xIOOut);
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
            if (FXEDrive.ActiveCheck())
                return false;
            xOutPath = xOutPath.Replace('\\', '/');
            if (xOutPath[xOutPath.Length - 1] == '/')
                xOutPath += FXEEntryName;
            else xOutPath += "/" + FXEEntryName;
            return (xExtract(xOutPath, IncludeSubFolders) &
                !(FXEDrive.Active = false));
        }
    }

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
