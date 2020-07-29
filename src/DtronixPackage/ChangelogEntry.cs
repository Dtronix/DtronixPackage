﻿using System;

namespace DtronixPackage
{
    public class ChangelogEntry
    {
        /// <summary>
        /// Contains the type of change for this entry.
        /// </summary>
        public ChangelogItemType Type { get; set; }

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

        public ChangelogEntry(ChangelogItemType type)
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
                case ChangelogItemType.PackageUpgrade:
                    return "PackageUpgrade: " + Note;
                case ChangelogItemType.ApplicationUpgrade:
                    return "ApplicationUpgrade: " + Note;
                case ChangelogItemType.Save:
                    return "Save: " + Time.ToString("g");
                case ChangelogItemType.AutoSave:
                    return "AutoSave: " + Time.ToString("g");
                default:
                    return Type.ToString();
            }
        }
    }
}