using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _86BoxManager.ViewModels
{
    public class VMCategory : ReactiveObject
    {
        private bool _is_checked = true;

        public string Name { get; private set; }

        public bool IsChecked { get => _is_checked; set => this.RaiseAndSetIfChanged(ref _is_checked, value); }

        internal int OrderIndex = 0;

        public VMCategory(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
