using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System.ComponentModel.Composition;

namespace NinaSequenceDiff;

/// <summary>
/// This small class is N.I.N.A.'s front door into the plugin.
///
/// PluginBase reads the assembly metadata (name, author, version, GUID and
/// descriptions) from AssemblyInfo.cs. The actual Imaging panel is exported
/// separately as an IDockableVM, which keeps registration and user-interface
/// logic independent.
/// </summary>
[Export(typeof(IPluginManifest))]
public sealed class SequenceDiffPlugin : PluginBase {
}
