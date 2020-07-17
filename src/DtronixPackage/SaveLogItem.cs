using System;

namespace DtronixPackage
{
    public class SaveLogItem
    {
        /// <summary>
        /// Username active while saving.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Active computer name which saved the file.
        /// </summary>
        public string ComputerName { get; set; }

        /// <summary>
        /// Time of the save.
        /// </summary>
        public DateTimeOffset Time { get; set; }

        /// <summary>
        /// If set to true, this indicates the file was auto saved at this point.
        /// If this value is true at any point in the file save history, the file was recovered from an auto save.
        /// </summary>
        public bool AutoSave { get; set; }

        /// <summary>
        /// Contains additional information about this save.
        /// </summary>
        public string Note { get; set; }
        
    }
}