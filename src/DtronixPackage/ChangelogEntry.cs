using System;

namespace DtronixPackage
{
    public class ChangelogEntry
    {
        /// <summary>
        /// Contains the type of change for this entry.
        /// </summary>
        public ChangelogEntryType Type { get; set; }

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
        /// Contains additional information about this save.
        /// </summary>
        public string Note { get; set; }

        public ChangelogEntry(ChangelogEntryType type)
        {
            Type = type;
            ComputerName = Environment.MachineName;
            Username = Environment.UserName;
            Time = DateTimeOffset.Now;
        }

        public ChangelogEntry()
        {
            
        }

        public override string ToString()
        {
            switch (Type)
            {
                case ChangelogEntryType.PackageUpgrade:
                    return "PackageUpgrade: " + Note;
                case ChangelogEntryType.ApplicationUpgrade:
                    return "ApplicationUpgrade: " + Note;
                case ChangelogEntryType.Save:
                    return "Save: " + Time.ToString("g");
                case ChangelogEntryType.AutoSave:
                    return "AutoSave: " + Time.ToString("g");
                default:
                    return Type.ToString();
            }
        }
    }
}