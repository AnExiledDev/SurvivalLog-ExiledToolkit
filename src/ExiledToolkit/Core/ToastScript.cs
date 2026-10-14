using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ExiledToolkit.Core;

/// <summary>
/// The JavaScript the game's web view runs for the toast stack. Every piece of a mod's text goes in as a string literal
/// built here, and the page puts it on screen with textContent only, so no toast text can run as script or markup.
/// </summary>
internal static class ToastScript
{
    public const string Global = "window.__exiledtoolkit";

    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    /// <summary>The page's answer when the script is missing, which is how a reloaded web view is noticed.</summary>
    public const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + PageCommand.Prefix + "noscript')";

    /// <summary>Run every couple of seconds; silent while the script is installed.</summary>
    public const string Probe = Global + "||" + NoScriptReply;

    /// <summary>One frame's changes to the stack, in order.</summary>
    public static string Apply(IEnumerable<PageOp> ops) => $"{Global}?{Global}.apply({Json(ops)}):{NoScriptReply}";

    public static string Json(IEnumerable<PageOp> ops) => "[" + string.Join(",", ops.Select(OpJson)) + "]";

    /// <summary>A JavaScript string literal, safe to paste into a script.</summary>
    public static string Str(string? text)
    {
        var literal = new StringBuilder((text?.Length ?? 0) + 2).Append('"');

        foreach (var character in text ?? "")
        {
            switch (character)
            {
                case '"':
                    literal.Append('\\').Append('"');
                    break;
                case '\\':
                    literal.Append('\\').Append('\\');
                    break;
                // Line and paragraph separators end a line in older JavaScript, and '<' keeps "</script>" inert.
                case < ' ' or LineSeparator or ParagraphSeparator or '<':
                    literal.Append('\\').Append('u').Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    literal.Append(character);
                    break;
            }
        }

        return literal.Append('"').ToString();
    }

    public static string KindName(ToastKind kind) => kind switch
    {
        ToastKind.Info => "info",
        ToastKind.Success => "success",
        ToastKind.Warning => "warning",
        ToastKind.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string PositionName(ToastPosition position) => position switch
    {
        ToastPosition.TopRight => "top-right",
        ToastPosition.TopLeft => "top-left",
        ToastPosition.BottomRight => "bottom-right",
        ToastPosition.BottomLeft => "bottom-left",
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
    };

    private static string OpJson(PageOp op) => op switch
    {
        PageOp.Place place => $"{{\"op\":\"place\",\"pos\":{Str(PositionName(place.Position))}}}",
        PageOp.Show show => $"{{\"op\":\"show\",{ToastFields(show.Toast)}}}",
        PageOp.Merge merge => $"{{\"op\":\"merge\",\"id\":{Number(merge.Id.Value)},\"count\":{Number(merge.Count)},\"ms\":{Number(merge.Milliseconds)},\"click\":{Bool(merge.HasClick)}}}",
        PageOp.Replace replace => $"{{\"op\":\"replace\",\"old\":{Number(replace.OldId.Value)},{ToastFields(replace.Toast)}}}",
        PageOp.Remove remove => $"{{\"op\":\"remove\",\"id\":{Number(remove.Id.Value)}}}",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    private static string ToastFields(ToastView toast)
    {
        var content = toast.Content;

        return $"\"id\":{Number(toast.Id.Value)},\"source\":{Str(content.Source)},\"title\":{Str(content.Title)},\"body\":{Str(content.Body)}," +
            $"\"kind\":{Str(KindName(content.Kind))},\"icon\":{Str(content.Icon)},\"count\":{Number(toast.Count)},\"ms\":{Number(toast.Milliseconds)},\"click\":{Bool(toast.HasClick)}";
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";
}
