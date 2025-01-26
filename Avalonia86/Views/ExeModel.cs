using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace Avalonia86.Views;
public class ExeModel : ReactiveObject
{
    public long? ID { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }
    public string VMExe { get; set; }
    public string VMRoms { get; set; }
    public string Arch { get; set; }
    public string Build { get; set; }
    public string Comment { get; set; }
}
