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
using X360.IO;
using X360.IO.FATXExtensions;
using X360.Other;

namespace X360.FATX
{
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
}