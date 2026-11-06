using System.Runtime.CompilerServices;

// Mod Manager still reaches one internal: LiveSettings.IsWatched (whether a setting applies live). Everything else it
// uses is the public API since 0.19.0. Anything internal it uses is a contract with it: change both together and raise
// the Toolkit version Mod Manager asks for. Mod Manager builds already released bind to these internals too, so moving
// or renaming one breaks them for players who update the Toolkit alone.
[assembly: InternalsVisibleTo("ModManager")]
