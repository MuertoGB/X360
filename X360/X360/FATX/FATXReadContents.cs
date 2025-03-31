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

namespace X360.FATX
{
    /// <summary>
    /// Object to hold contents of a read folder
    /// </summary>
    public sealed class FATXReadContents
    {
        internal List<FATXFolderEntry> FXRCFolders;
        internal List<FATXFileEntry> FXRCFiles;
        internal List<FATXPartition> FXRCSubParts = new List<FATXPartition>();

        /// <summary>
        /// Files
        /// </summary>
        public FATXFileEntry[] Files => FXRCFiles.ToArray();
        /// <summary>
        /// Folders
        /// </summary>
        public FATXFolderEntry[] Folders => FXRCFolders.ToArray();
        /// <summary>
        /// Subpartitions
        /// </summary>
        public FATXPartition[] SubPartitions => FXRCSubParts.ToArray();

        internal FATXReadContents() { }
    }
}