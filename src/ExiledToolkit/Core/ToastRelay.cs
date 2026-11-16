using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExiledToolkit.Game;

namespace ExiledToolkit.Core;

/// <summary>
/// Sends a toast to the friend the host is acting for, so it shows on their screen and nobody else's. The click action
/// can't travel, so it stays here under a number; the friend's copy sends the number back when they click, and the
/// action runs here as that friend.
/// </summary>
internal sealed class ToastRelay
{
    public const string ToastChannel = "com.dev.survivallog.exiledtoolkit.toast";
    public const string ClickChannel = "com.dev.survivallog.exiledtoolkit.toastclick";

    /// <summary>The format of a sent toast. A copy that reads a different one shows nothing rather than half a toast.</summary>
    private const byte Format = 1;

    private readonly object _gate = new();
    private readonly Func<ToastRequest?, ToastId> _showHere;
    private readonly Func<bool> _isMainThread;
    private readonly Action<string> _log;
    private readonly Dictionary<long, (long Owner, Action Action)> _clicks = new();
    private readonly Queue<long> _clickOrder = new();
    private long _lastClick;

    public ToastRelay(Func<ToastRequest?, ToastId> showHere, Func<bool> isMainThread, Action<string> log)
    {
        _showHere = showHere;
        _isMainThread = isMainThread;
        _log = log;
    }

    /// <summary>Starts listening for toasts from the host and clicks from friends. Dispose what it returns to stop.</summary>
    public IDisposable Listen() => new Both(OtherPlayers.Listen(ToastChannel, Heard), OtherPlayers.Listen(ClickChannel, Clicked));

    /// <summary>
    /// Shows a toast, or sends it to the friend the host is acting for. Who the host acts for is only known on the main
    /// thread, so a toast from any other thread shows here. If sending fails with an error, the toast shows here too.
    /// </summary>
    public ToastId Show(ToastRequest? request)
    {
        if (request == null || !_isMainThread()) return _showHere(request);

        try
        {
            if (!OtherPlayers.IsHost || OtherPlayers.ActingFor is not { } friend) return _showHere(request);

            SendTo(friend, request);

            return ToastId.None;
        }
        catch (Exception ex)
        {
            _log($"a toast could not be sent to a friend, so it shows here: {ex}");

            return _showHere(request);
        }
    }

    private void SendTo(long friend, ToastRequest request)
    {
        var click = Keep(friend, request.OnClick);

        if (!OtherPlayers.SendTo(friend, ToastChannel, Write(request, click))) _log($"a toast for player {friend} could not be sent, so nobody sees it: {request.Title}");
    }

    private long Keep(long friend, Action? onClick)
    {
        if (onClick == null) return 0;

        lock (_gate)
        {
            var click = ++_lastClick;

            _clicks[click] = (friend, onClick);
            _clickOrder.Enqueue(click);

            if (_clickOrder.Count > ClickActions.DefaultCapacity) _clicks.Remove(_clickOrder.Dequeue());

            return click;
        }
    }

    /// <summary>On a guest: a toast from the host, shown here. A click sends its number back.</summary>
    private void Heard(PlayerMessage message)
    {
        if (!OtherPlayers.IsGuest) return;

        if (!TryRead(message.Body, out var request, out var click))
        {
            _log("a toast from the host could not be read; it was made by a different Toolkit version");

            return;
        }

        if (click != 0) request = request with { OnClick = () => OtherPlayers.SendToHost(ClickChannel, BitConverter.GetBytes(click)) };

        _showHere(request);
    }

    /// <summary>On the host: a friend clicked a toast sent to them. Only that friend may run its action.</summary>
    private void Clicked(PlayerMessage message)
    {
        if (!OtherPlayers.IsHost || message.Body.Length != sizeof(long)) return;

        var click = BitConverter.ToInt64(message.Body, 0);
        Action action;

        lock (_gate)
        {
            if (!_clicks.TryGetValue(click, out var kept) || kept.Owner != message.From) return;

            action = kept.Action;
        }

        try
        {
            if (!OtherPlayers.RunFor(message.From, action)) _log($"player {message.From} clicked a toast, but the game doesn't know their character");
        }
        catch (Exception ex)
        {
            _log($"a toast click for player {message.From} failed: {ex}");
        }
    }

    internal static byte[] Write(ToastRequest request, long click)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(Format);
            writer.Write(request.Source ?? "");
            writer.Write(request.Title ?? "");
            WriteOptional(writer, request.Body);
            writer.Write((int)request.Kind);
            WriteOptional(writer, request.Icon);
            writer.Write(request.Seconds.HasValue);
            writer.Write(request.Seconds ?? 0);
            WriteOptional(writer, request.Key);
            writer.Write(click);
            WriteItems(writer, request.Items);
        }

        return stream.ToArray();
    }

    internal static bool TryRead(byte[] body, out ToastRequest request, out long click)
    {
        request = new ToastRequest("", "");
        click = 0;

        try
        {
            using var reader = new BinaryReader(new MemoryStream(body), Encoding.UTF8);

            if (reader.ReadByte() != Format) return false;

            var source = reader.ReadString();
            var title = reader.ReadString();
            var text = ReadOptional(reader);
            var kind = (ToastKind)reader.ReadInt32();
            var icon = ReadOptional(reader);
            var hasSeconds = reader.ReadBoolean();
            var seconds = reader.ReadDouble();
            var key = ReadOptional(reader);
            click = reader.ReadInt64();
            var items = ReadItems(reader);

            request = new ToastRequest(source, title)
            {
                Body = text,
                Kind = Enum.IsDefined(typeof(ToastKind), kind) ? kind : ToastKind.Info,
                Icon = icon,
                Seconds = hasSeconds ? seconds : null,
                Key = key,
                Items = items,
            };

            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void WriteItems(BinaryWriter writer, IReadOnlyList<ToastItem>? items)
    {
        var count = items == null ? 0 : Math.Min(items.Count, ToastItem.Max);

        writer.Write(count);

        for (var i = 0; i < count; i++)
        {
            var item = items![i];

            writer.Write(item?.Label ?? "");
            WriteOptional(writer, item?.Icon);
            writer.Write(item?.Count ?? 0);
        }
    }

    private static IReadOnlyList<ToastItem>? ReadItems(BinaryReader reader)
    {
        var count = reader.ReadInt32();

        if (count <= 0) return null;
        if (count > ToastItem.Max) throw new IOException("too many items");

        var items = new List<ToastItem>(count);

        for (var i = 0; i < count; i++) items.Add(new ToastItem(reader.ReadString()) { Icon = ReadOptional(reader), Count = reader.ReadInt32() });

        return items;
    }

    private static void WriteOptional(BinaryWriter writer, string? value)
    {
        writer.Write(value != null);
        writer.Write(value ?? "");
    }

    private static string? ReadOptional(BinaryReader reader)
    {
        var hasValue = reader.ReadBoolean();
        var value = reader.ReadString();

        return hasValue ? value : null;
    }

    private sealed class Both : IDisposable
    {
        private readonly IDisposable _first;
        private readonly IDisposable _second;

        public Both(IDisposable first, IDisposable second)
        {
            _first = first;
            _second = second;
        }

        public void Dispose()
        {
            _first.Dispose();
            _second.Dispose();
        }
    }
}
