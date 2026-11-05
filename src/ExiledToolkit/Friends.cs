using System.Runtime.CompilerServices;

// Mod Manager still reaches two internals: LiveSettings.IsWatched (whether a setting applies live) and ToolkitLog (its
// log file). Everything else it uses is the public API since 0.19.0. Anything internal it uses is a contract with it:
// change both together and raise the Toolkit version Mod Manager asks for.
[assembly: InternalsVisibleTo("ModManager")]
