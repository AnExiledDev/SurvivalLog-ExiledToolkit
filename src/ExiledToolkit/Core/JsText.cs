using System.Globalization;
using System.Text;

namespace ExiledToolkit.Web;

/// <summary>Text for scripts sent to the game's web view. Toolkit 0.19.0 and newer.</summary>
public static class JsText
{
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    /// <summary>
    /// A JavaScript string literal in double quotes, safe to paste into a script: quotes, backslashes and control
    /// characters are escaped, and so are '&lt;' (so "&lt;/script&gt;" stays inert) and the line and paragraph separators,
    /// which end a line in older JavaScript. Null is the empty string.
    /// </summary>
    public static string Literal(string? text)
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
}
