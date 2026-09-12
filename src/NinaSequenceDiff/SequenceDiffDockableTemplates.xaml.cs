using System.ComponentModel.Composition;
using System.Windows;

namespace NinaSequenceDiff;

// N.I.N.A. imports this WPF ResourceDictionary through MEF. The XAML file
// contains the visual layout and this tiny class only makes it discoverable.
[Export(typeof(ResourceDictionary))]
public partial class SequenceDiffDockableTemplates : ResourceDictionary {
    public SequenceDiffDockableTemplates() {
        // Loads the XAML resources compiled into this DLL.
        InitializeComponent();
    }
}
