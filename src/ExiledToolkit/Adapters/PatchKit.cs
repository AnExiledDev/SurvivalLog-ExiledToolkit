using System;
using System.Reflection;
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
    public bool Patch(Type hooks, Type type, string method, Type[]? parameters = null, string? prefix = null, string? postfix = null, string? finalizer = null, string done = "", string consequence = "") =>
        Report(type.Name + "." + method, done, consequence, () => PatchOrThrow(hooks, type, method, parameters, prefix, postfix, finalizer));

    /// <summary>
    /// The same with a Harmony <paramref name="priority"/> (<see cref="Priority"/>) for every hook given, for a hook
    /// that must run before or after other mods' hooks on the method. It replaces any [HarmonyPriority], [HarmonyBefore] or [HarmonyAfter] on the
    /// hooks. Toolkit 0.20.0 and newer.
    /// </summary>
    public bool Patch(Type hooks, Type type, string method, Type[]? parameters, int priority, string? prefix = null, string? postfix = null, string? finalizer = null, string done = "", string consequence = "") =>
        Report(type.Name + "." + method, done, consequence, () => Place(hooks, Find(type, method, parameters), prefix, postfix, finalizer, priority));

    /// <summary>
    /// Hooks a method the mod found itself, such as one picked by its return type or a property getter. A null
    /// <paramref name="target"/> is logged as a failure under <paramref name="name"/>, so a lookup that comes back empty
    /// after a game update shows in the Mod Manager like any other. Leave <paramref name="priority"/> at -1 to keep the
    /// hooks' own Harmony attributes. Toolkit 0.20.0 and newer.
    /// </summary>
    public bool Patch(MethodBase? target, string name, Type hooks, string? prefix = null, string? postfix = null, string? finalizer = null, int priority = NoPriority, string done = "", string consequence = "") =>
        Report(name, done, consequence, () => Place(hooks, target ?? throw new MissingMethodException(name), prefix, postfix, finalizer, priority));

    /// <summary>The same without the logging: throws when the method is not there or the hook cannot go in, so the caller can give up on its feature.</summary>
    public void PatchOrThrow(Type hooks, Type type, string method, Type[]? parameters = null, string? prefix = null, string? postfix = null, string? finalizer = null) =>
        Place(hooks, Find(type, method, parameters), prefix, postfix, finalizer, NoPriority);

    private static MethodInfo Find(Type type, string method, Type[]? parameters) =>
        (parameters == null ? AccessTools.Method(type, method) : AccessTools.Method(type, method, parameters))
        ?? throw new MissingMethodException(type.Name + "." + method);

    private bool Report(string name, string done, string consequence, Action place)
    {
        try
        {
            place();

            if (done.Length > 0) _log(done);

            return true;
        }
        catch (Exception ex)
        {
            _log($"{HookFailure.Prefix} {name}{(consequence.Length > 0 ? ", " + consequence : "")}: {ex.Message}");

            return false;
        }
    }

    private void Place(Type hooks, MethodBase target, string? prefix, string? postfix, string? finalizer, int priority)
    {
        _harmony.Patch(
            target,
            prefix: Hook(hooks, prefix, priority),
            postfix: Hook(hooks, postfix, priority),
            finalizer: Hook(hooks, finalizer, priority));
    }

    // No priority given: the HarmonyMethod(MethodInfo) constructor keeps the hook's own [HarmonyPriority],
    // [HarmonyBefore] and [HarmonyAfter]. The constructor that takes a priority overwrites all three.
    private const int NoPriority = -1;

    private static HarmonyMethod? Hook(Type hooks, string? name, int priority)
    {
        if (name == null) return null;

        var method = AccessTools.Method(hooks, name) ?? throw new MissingMethodException(hooks.Name + "." + name);

        return priority == NoPriority ? new HarmonyMethod(method) : new HarmonyMethod(method, priority);
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
            // Fail runs inside the game's calls; a logger that throws must not carry the exception into them.
        }
    }
}
