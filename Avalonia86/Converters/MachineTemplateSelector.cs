using Avalonia.Controls.Templates;
using Avalonia.Controls;
using Avalonia.Metadata;
using System;
using System.Collections.Generic;
using Avalonia86.ViewModels;
using Avalonia.Media;
using Avalonia;

namespace Avalonia86.Converters
{
    public class MachineTemplateSelector : AvaloniaObject, IDataTemplate
    {
        public static readonly string FullMachine = "FullMachineTpl";
        public static readonly string CompMachine = "CompactMachineTpl";

        string _tpl = FullMachine;

        /// <summary>
        /// This Dictionary will store child templates. By marking this as [Content], we can add to it from axaml.
        /// </summary>
        [Content]
        public Dictionary<string, IDataTemplate> AvailableTemplates { get; } = new Dictionary<string, IDataTemplate>();

        public static readonly StyledProperty<bool> CompactMachineProperty =
            AvaloniaProperty.Register<MachineTemplateSelector, bool>(nameof(CompactMachine));

        public bool CompactMachine
        { 
            get => GetValue(CompactMachineProperty);
            set => SetValue(CompactMachineProperty, value);
        }

        /// <summary>
        /// Builds the datatemplates
        /// </summary>
        public Control Build(object param)
        {            
            //We build the child control
            return AvailableTemplates[CompactMachine ? CompMachine : FullMachine].Build(param); // finally we look up the provided key and let the System build the DataTemplate for us
        }

        /// <summary>
        /// Check if we can accept the provided data
        /// </summary>
        public bool Match(object data)
        {
            return data is VMVisual;
        }
    }
}
