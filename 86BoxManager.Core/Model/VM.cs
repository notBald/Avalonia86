using System;
using _86BoxManager.API;

// ReSharper disable InconsistentNaming

namespace _86BoxManager.Models
{
    public class VM : IVm
    {
        /// <summary>
        /// Unique ID for this VM. Used by the database.
        /// </summary>
        public long UID { get; private set; }

        /// <summary>
        /// Window handle for the VM once it's started
        /// </summary>
        public IntPtr hWnd { get; set; }
        /// <summary>
        /// Name of the virtual machine
        /// </summary>
        public string Name;

        /// <summary>
        /// Title of the 86Box window
        /// </summary>
        public string Title => Name;
        /// <summary>
        /// Description
        /// </summary>
        public string Desc { get; set; }
        /// <summary>
        /// Comment
        /// </summary>
        public string Comment { get; set; }
        /// <summary>
        /// What sort of machine to categorize this as
        /// </summary>
        public string Category;
        /// <summary>
        /// Path to icon used to represent the VM
        /// </summary>
        public string IconPath;

        /// <summary>
        /// Process ID of 86box executable running the VM
        /// </summary>
        public int Pid { get; set; }

        public VM(long uid = 0){
            Name = "defaultName";
            Desc = "defaultDesc";
            Category = "defaultCat";
            hWnd = IntPtr.Zero;
            UID = uid;
        }

        public VM(string name, string desc, string cat, string icon = null, string comment = null)
        {
            Name = name;
            Desc = desc;
            Category = cat;
            IconPath = icon;
            Comment = comment;
            hWnd = IntPtr.Zero;
        }

        public override string ToString()
        {
            return $"Name: {Name}";
        }

        public Action<IVm> OnExit { get; set; }
    }
}