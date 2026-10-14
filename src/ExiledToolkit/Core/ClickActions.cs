using System;
using System.Collections.Generic;

namespace ExiledToolkit.Core;

/// <summary>
/// The click actions of the most recent toasts that have one, so a toast can still be opened from a history list after
/// it has left the screen. Bounded, because every action holds whatever its mod captured in it.
/// </summary>
internal sealed class ClickActions
{
    public const int DefaultCapacity = 500;

    private readonly int _capacity;
    private readonly Dictionary<ToastId, LinkedListNode<(ToastId Id, Action Action)>> _byId = new();
    private readonly LinkedList<(ToastId Id, Action Action)> _oldestFirst = new();

    public ClickActions(int capacity = DefaultCapacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    public int Count => _byId.Count;

    /// <summary>The toast's latest action, made the newest. A null action forgets the toast's earlier one.</summary>
    public void Set(ToastId id, Action? action)
    {
        Forget(id);

        if (action == null) return;

        _byId[id] = _oldestFirst.AddLast((id, action));

        if (_byId.Count > _capacity && _oldestFirst.First is { } oldest) Forget(oldest.Value.Id);
    }

    public bool TryGet(ToastId id, out Action action)
    {
        if (_byId.TryGetValue(id, out var node))
        {
            action = node.Value.Action;

            return true;
        }

        action = static () => { };

        return false;
    }

    private void Forget(ToastId id)
    {
        if (!_byId.Remove(id, out var node)) return;

        _oldestFirst.Remove(node);
    }
}
