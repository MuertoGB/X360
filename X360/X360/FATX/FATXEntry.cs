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
using System.Text;
using X360.IO;
using X360.Other;

namespace X360.FATX
{
    public class FATXEntry
    {
        /// <summary>
        /// Generic entry for FATX
        /// </summary>
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

            return WriteEntryInternal() & !(FXEDrive.Active = false);
        }
    }
}