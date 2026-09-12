using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

// These values are read by N.I.N.A. and later copied into the public manifest.
// Keep the public name, version and descriptions in sync with the release ZIP.
[assembly: AssemblyTitle("Sequencer Item Recovery")]
[assembly: AssemblyDescription(
    "Short-term recovery tool for recently removed Advanced Sequencer " +
    "items from loaded or unsaved sequences, with targeted restore.")]
[assembly: AssemblyCompany("Jost Jahn")]
[assembly: AssemblyProduct("Sequencer Item Recovery")]
[assembly: AssemblyCopyright("Copyright © 2026 Jost Jahn")]
[assembly: ComVisible(false)]
// This GUID is the permanent technical identity of the plugin. N.I.N.A. uses
// it to recognise upgrades and uninstallations, so never generate a new one.
[assembly: Guid("3c52a960-96a7-41a6-a06a-4aa54f4c0df9")]
[assembly: AssemblyVersion("0.1.11.0")]
[assembly: AssemblyFileVersion("0.1.11.0")]
// N.I.N.A.'s plugin manager reads the following metadata when a manifest is
// generated. Keep this URL on the public source repository, not a local path.
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/MPL/2.0/")]
[assembly: AssemblyMetadata("Homepage", "https://jostjahn.de/")]
[assembly: AssemblyMetadata(
    "Repository",
    "https://github.com/JostJahn/nina-sequencer-item-recovery")]
[assembly: AssemblyMetadata("Tags", "sequencer,item,recovery,restore,safety")]
[assembly: AssemblyMetadata(
    "ShortDescription",
    "Lists recently removed Advanced Sequencer items and restores one item.")]
[assembly: AssemblyMetadata(
    "LongDescription",
    "Sequencer Item Recovery is a short-term recovery tool for accidental " +
    "Advanced Sequencer deletions. Open the Imaging workspace and enable the " +
    "Sequencer Item Recovery tool to see recently removed instructions or " +
    "blocks from the loaded or unsaved sequence still being edited. Each row " +
    "has a visible 'Restore' button that reinserts the complete item at its " +
    "position. Sequencer Item Recovery is not a full undo history or " +
    "automatic backup.")]
// Unit tests may exercise carefully chosen internal helpers without exposing
// those implementation details as public API for other plugins.
[assembly: InternalsVisibleTo("NinaSequenceDiff.Tests")]
