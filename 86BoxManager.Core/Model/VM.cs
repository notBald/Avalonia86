using System;
using _86BoxManager.API;
using _86BoxManager.Core;

// ReSharper disable InconsistentNaming

namespace _86BoxManager.Models
{
    public class VM : IVm
    {
        private int _status;

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

        public int Status 
        { 
            get => _status; 
            set
            {
                if (value == STATUS_PAUSED)
                    IsPaused = true;
                else if (value != STATUS_WAITING)
                    IsPaused = false;

                _status = value;
            }
        } //Status

        /// <summary>
        /// For cases where the VM is both paused and waiting
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Process ID of 86box executable running the VM
        /// </summary>
        public int Pid { get; set; }

        public const int STATUS_STOPPED = 0; //VM is not running
        public const int STATUS_RUNNING = 1; //VM is running
        public const int STATUS_WAITING = 2; //VM is waiting for user response
        public const int STATUS_PAUSED = 3; //VM is paused

        public VM(long uid = 0){
            Name = "defaultName";
            Desc = "defaultDesc";
            Category = "defaultCat";
            Status = STATUS_STOPPED;
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
            Status = STATUS_STOPPED;
            hWnd = IntPtr.Zero;
        }

        public override string ToString()
        {
            return $"Name: {Name}, status: {Status}";
        }

        //Returns a lovely status string for use in UI
        public string GetStatusString()
        {
            switch (Status)
            {
                case STATUS_STOPPED: return "Stopped";
                case STATUS_RUNNING: return "Running";
                case STATUS_PAUSED: return "Paused";
                case STATUS_WAITING: return "Waiting";
                default: return "Invalid status";
            }
        }

        public Action<IVm> OnExit { get; set; }
    }
}