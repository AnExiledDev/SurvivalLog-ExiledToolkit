using System.Runtime.CompilerServices;

// Mod Manager draws a full-screen window with the same web plumbing the toasts use. It reaches those internals
// directly instead of through a public API, so nothing here is promised to other mods yet. Anything Mod Manager uses
// is a contract with it: change both together and raise the Toolkit version Mod Manager asks for.
[assembly: InternalsVisibleTo("ModManager")]
