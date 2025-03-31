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
using X360.IO;
using X360.IO.FATXExtensions;
using X360.Other;

namespace X360.FATX
{
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

            FATXReadContents result = ReadContents();
            FXEDrive.Active = false;
            return result;
        }

        internal FATXReadContents ReadContents()
        {
            FATXReadContents result = new FATXReadContents();

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

                result.FXRCFolders = new List<FATXFolderEntry>();
                result.FXRCFiles = new List<FATXFileEntry>();

                for (int entryIndex = 0; entryIndex < fatxEntries.Count; entryIndex++)
                {
                    if (fatxEntries[entryIndex].IsFolder)
                    {
                        result.FXRCFolders.Add(new FATXFolderEntry(fatxEntries[entryIndex], ref FXEDrive));
                    }
                    else
                    {
                        result.FXRCFiles.Add(new FATXFileEntry(fatxEntries[entryIndex], ref FXEDrive));
                    }
                }

                return result;
            }
            catch
            {
                result = null;
                return result;
            }
        }

        /// <summary>
        /// Gets a location for a new entry
        /// </summary>
        /// <param name="allocatedBlock"></param>
        /// <returns></returns>
        long GetNewEntryPosition(out uint allocatedBlock)
        {
            allocatedBlock = 0;
            List<uint> allocatedBlocks = new List<uint>(Partition.FXPAllocTable.GetBlocks(StartBlock));
            FXEDrive.GetIO();

            // Search existing allocated blocks for available space
            foreach (uint currentBlock in allocatedBlocks)
            {
                long blockOffset = Partition.BlockToOffset(currentBlock);
                for (int entryIndex = 0; entryIndex < Partition.xEntryCount; entryIndex++)
                {
                    FXEDrive.FXDIO.Position = blockOffset + (0x40 * entryIndex);
                    byte entryStatus = FXEDrive.FXDIO.ReadByte();

                    if (entryStatus == 0 || entryStatus > 0x2A || entryStatus == 0xFF)
                    {
                        return --FXEDrive.FXDIO.Position;
                    }
                }
            }

            // Allocate a new block if no space was found
            uint[] newBlockChain = Partition.FXPAllocTable.GetNewBlockChain(1, 1);
            if (newBlockChain.Length > 0)
            {
                FXEDrive.FXDIO.Position = Partition.BlockToOffset(newBlockChain[0]);
                byte[] emptyBlockData = new byte[Partition.xBlockSize];
                FXEDrive.FXDIO.Write(emptyBlockData);

                allocatedBlocks.Add(newBlockChain[0]);
                allocatedBlock = newBlockChain[0];
                return Partition.BlockToOffset(newBlockChain[0]);
            }

            return -1;
        }

        /* Note: Have plans for safer and better manipulation to prevent
         * minimal block loss to human error */

        /// <summary>
        /// Adds a folder
        /// </summary>
        /// <param name="folderName"></param>
        /// <returns></returns>
        public bool AddFolder(string folderName)
        {
            folderName.IsValidXboxName();

            if (FXEDrive.ActiveCheck())
            {
                return false;
            }

            try
            {
                FATXReadContents existingContents = ReadContents();

                foreach (FATXFolderEntry entry in existingContents.FXRCFolders)
                {
                    if (entry.Name == folderName)
                    {
                        return FXEDrive.Active = false;
                    }

                }

                FXIO folderIO = new FXIO(new byte[Partition.xBlockSize], true);
                long entryPosition = GetNewEntryPosition(out uint newBlock);

                if (entryPosition == -1)
                {
                    return FXEDrive.Active = false;
                }

                uint[] allocatedBlocks = Partition.FXPAllocTable.GetNewBlockChain(folderIO.BlockCountFATX(Partition), newBlock + 1);
                if (allocatedBlocks.Length == 0)
                {
                    return FXEDrive.Active = false;
                }


                if (!Partition.WriteFile(allocatedBlocks, ref folderIO))
                {
                    return FXEDrive.Active = false;
                }

                FATXEntry newFolderEntry = new FATXEntry(folderName, allocatedBlocks[0], (int)folderIO.Length, entryPosition, true, ref FXEDrive);
                if (!newFolderEntry.WriteEntryInternal())
                {
                    return FXEDrive.Active = false;
                }

                if (newBlock > 0)
                {
                    List<uint> existingBlocks = new List<uint>(Partition.FXPAllocTable.GetBlocks(StartBlock))
                    {
                        newBlock
                    };

                    uint[] tempBuffer = existingBlocks.ToArray();
                    if (!Partition.FXPAllocTable.WriteChain(ref tempBuffer))
                    {
                        return FXEDrive.Active = false;
                    }
                }

                if (!Partition.FXPAllocTable.WriteChain(ref allocatedBlocks))
                {
                    return FXEDrive.Active = false;
                }
                if (Partition.WriteAllocTable())
                {
                    return !(FXEDrive.Active = false);
                }

                return FXEDrive.Active = false;
            }
            catch
            {
                return FXEDrive.Active = false;
            }
        }

        /// <summary>
        /// Adds a file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileLocation"></param>
        /// <param name="addType"></param>
        /// <returns></returns>
        public bool AddFile(string fileName, string fileLocation, AddType addType)
        {
            fileName.IsValidXboxName();

            if (FXEDrive.ActiveCheck())
            {
                return false;
            }

            FXIO fileIO;

            try
            {
                fileIO = new FXIO(fileLocation, DJFileMode.Open, true);
            }
            catch
            {
                return FXEDrive.Active = false;
            }

            try
            {
                FATXReadContents existingContents = ReadContents();

                foreach (FATXFileEntry existingFile in existingContents.FXRCFiles)
                {
                    if (existingFile.Name == fileName)
                    {
                        bool result = false;

                        if (addType == AddType.NoOverWrite)
                        {
                            return FXEDrive.Active = false;
                        }
                        else if (addType == AddType.Inject)
                        {
                            result = existingFile.InjectInternal(fileIO);
                        }
                        else
                        {
                            result = existingFile.ReplaceInternal(fileIO);
                        }

                        return result & !(FXEDrive.Active = false);
                    }
                }

                long entryPosition = GetNewEntryPosition(out uint newBlock);
                if (entryPosition == -1)
                {
                    return FXEDrive.Active = false;
                }

                uint[] allocatedBlocks = Partition.FXPAllocTable.GetNewBlockChain(fileIO.BlockCountFATX(Partition), newBlock + 1);
                if (allocatedBlocks.Length == 0)
                {
                    return FXEDrive.Active = false;
                }

                if (!Partition.WriteFile(allocatedBlocks, ref fileIO))
                {
                    return FXEDrive.Active = false;
                }

                FATXEntry newFileEntry = new FATXEntry(fileName, allocatedBlocks[0], (int)fileIO.Length, entryPosition, false, ref FXEDrive);
                if (!newFileEntry.WriteEntryInternal())
                {
                    return FXEDrive.Active = false;
                }

                if (newBlock > 0)
                {
                    List<uint> existingBlocks = new List<uint>(Partition.FXPAllocTable.GetBlocks(StartBlock))
                    {
                        newBlock
                    };

                    uint[] tempBuffer = existingBlocks.ToArray();
                    if (!Partition.FXPAllocTable.WriteChain(ref tempBuffer))
                    {
                        return FXEDrive.Active = false;
                    }
                }
                if (!Partition.FXPAllocTable.WriteChain(ref allocatedBlocks))
                {
                    return FXEDrive.Active = false;
                }
                if (Partition.WriteAllocTable())
                {
                    return !(FXEDrive.Active = false);
                }

                return FXEDrive.Active = false;
            }
            catch
            {
                fileIO.Close();
                return FXEDrive.Active = false;
            }
        }

        bool ExtractInternal(string outputPath, bool includeSubDirectories)
        {
            if (!VariousFunctions.xCheckDirectory(outputPath))
            {
                return false;
            }

            FATXReadContents readContents = ReadContents();
            if (readContents == null)
            {
                return false;
            }

            foreach (FATXFileEntry entry in readContents.Files)
            {
                FXIO outputFile = new FXIO(outputPath + "/" + entry.Name, DJFileMode.Create, true);
                if (!outputFile.Accessed)
                {
                    continue;
                }

                entry.Extract(ref outputFile);
                outputFile.Dispose();
            }

            if (!includeSubDirectories)
            {
                return true;
            }

            foreach (FATXFolderEntry subDirectory in readContents.Folders)
            {
                subDirectory.ExtractInternal(outputPath + "/" + subDirectory.Name, includeSubDirectories);
            }

            return true;
        }

        /// <summary>
        /// Extracts a file
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="includeSubDirectories"></param>
        /// <returns></returns>
        public bool Extract(string outputPath, bool includeSubDirectories)
        {
            if (FXEDrive.ActiveCheck())
            {
                return false;
            }

            outputPath = outputPath.Replace('\\', '/');
            if (outputPath[outputPath.Length - 1] == '/')
            {
                outputPath += FXEEntryName;
            }
            else
            {
                outputPath += "/" + FXEEntryName;
            }

            return (ExtractInternal(outputPath, includeSubDirectories) & !(FXEDrive.Active = false));
        }
    }
}