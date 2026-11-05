using System;
using HarmonyLib;

namespace ExiledToolkit.Game;

/// <summary>
/// Hooking and failure logging for a mod's own Harmony hooks. A hook that cannot be placed costs its feature, never
/// the mod: it is logged as "could not hook Type.Method, consequence: why", which the Mod Manager reads as a sign the
/// mod may need an update. Toolkit 0.19.0 and newer.
/// </summary>
public sealed class PatchKit
{
    private readonly Harmony _harmony;
    private readonly Action<string> _log;
    private readonly FailureCounts _failures;

    /// <param name="maxLoggedFailures">How many failures of each kind <see cref="Fail"/> writes: 1 writes each once.</param>
    public PatchKit(Harmony harmony, Action<string> log, int maxLoggedFailures = 1)
    {
        _harmony = harmony ?? throw new ArgumentNullException(nameof(harmony));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _failures = new FailureCounts(maxLoggedFailures);
    }

    /// <summary>
    /// Hooks <paramref name="type"/>.<paramref name="method"/> with the named methods of <paramref name="hooks"/>, and
    /// logs <paramref name="done"/> (when given) or the failure. <paramref name="parameters"/> picks one overload, so a
    /// new overload in a game update cannot take the hook's place. True when the hook is in.
    /// </summary>
    public bool Patch(Type hooks, Type type, string method, Type[]? parameters = null, string? prefix = null, string? postfix = null, string? finalizer = null, string done = "", string consequence = "")
    {
        try
        {
            PatchOrThrow(hooks, type, method, parameters, prefix, postfix, finalizer);

            if (done.Length > 0) _log(done);

            return true;
        }
        catch (Exception ex)
        {
            _log($"{HookFailure.Prefix} {type.Name}.{method}{(consequence.Length > 0 ? ", " + consequence : "")}: {ex.Message}");

            return false;
        }
    }

    /// <summary>The same without the logging: throws when the method is not there or the hook cannot go in, so the caller can give up on its feature.</summary>
    public void PatchOrThrow(Type hooks, Type type, string method, Type[]? parameters = null, string? prefix = null, string? postfix = null, string? finalizer = null)
    {
        var target = (parameters == null ? AccessTools.Method(type, method) : AccessTools.Method(type, method, parameters))
                     ?? throw new MissingMethodException(type.Name + "." + method);

        _harmony.Patch(
            target,
            prefix: prefix == null ? null : new HarmonyMethod(hooks, prefix),
            postfix: postfix == null ? null : new HarmonyMethod(hooks, postfix),
            finalizer: finalizer == null ? null : new HarmonyMethod(hooks, finalizer));
    }

    /// <summary>
    /// For a hook's catch: logs the failure while fewer than the most of its kind were. Give every hook its own
    /// <paramref name="doing"/>, or the first hook to fail hides the others'. Any thread; never throws.
    /// </summary>
    public void Fail(string doing, Exception ex)
    {
        try
        {
            if (_failures.LineFor(doing, ex) is { } line) _log(line);
        }
        catch (Exception)
        {
            // Fail runs inside the game's own calls; a logger that throws must not carry the exception into them.
        }
    }
}
