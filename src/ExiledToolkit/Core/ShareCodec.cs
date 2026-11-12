using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ExiledToolkit.Kit;

/// <summary>Why <see cref="ShareCodec.Unpack"/> gave no text.</summary>
public enum ShareProblem
{
    /// <summary>Nothing was pasted, or only spaces and line breaks.</summary>
    Empty,

    /// <summary>The pasted text holds no code with the prefix.</summary>
    NotACode,

    /// <summary>The code is cut off or changed: its base64 or its compressed data does not read.</summary>
    Broken,

    /// <summary>The pasted text is longer than the limit, or the code unpacks to more than the limit.</summary>
    TooBig,
}

/// <summary>
/// A share code: one short line of text a player can paste in a chat or a URL. It is a prefix (such as "SLMM1-") and
/// the text, as UTF-8, compressed with deflate, in base64 with "-" for "+", "_" for "/" and no "=" at the end. This is
/// the format Mod Manager's profile codes use, so every "SLMM1-" code already shared still reads. Reading one never
/// trusts its size: each mod passes its own limits. Toolkit 0.21.0 and newer.
/// </summary>
public static class ShareCodec
{
    private const string CodeChars = "([A-Za-z0-9_-]+)";

    /// <summary>The share code for <paramref name="text"/>: <paramref name="prefix"/> and the packed text.</summary>
    public static string Pack(string prefix, string text)
    {
        CheckPrefix(prefix);

        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
        {
            var bytes = Encoding.UTF8.GetBytes(text ?? "");

            deflate.Write(bytes, 0, bytes.Length);
        }

        return prefix + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// The text in the first code with <paramref name="prefix"/> anywhere in <paramref name="pasted"/>, or the problem
    /// that stopped it. Words around the code are fine. Chat apps break long lines, so every space and line break is
    /// taken out before the code is looked for. Pasted text longer than <paramref name="maxPastedLength"/> characters
    /// is refused before anything is decoded, and reading stops as soon as the code unpacks to more than
    /// <paramref name="maxUnpackedBytes"/> bytes.
    /// </summary>
    public static (string? Text, ShareProblem? Problem) Unpack(string? pasted, string prefix, int maxPastedLength, int maxUnpackedBytes)
    {
        CheckPrefix(prefix);

        if (string.IsNullOrWhiteSpace(pasted)) return (null, ShareProblem.Empty);

        if (pasted.Length > maxPastedLength) return (null, ShareProblem.TooBig);

        var joined = Regex.Replace(pasted, "\\s+", "");
        var match = Regex.Match(joined, Regex.Escape(prefix) + CodeChars, RegexOptions.CultureInvariant);

        if (!match.Success) return (null, ShareProblem.NotACode);

        try
        {
            var base64 = match.Groups[1].Value.Replace('-', '+').Replace('_', '/');

            base64 += new string('=', (4 - base64.Length % 4) % 4);

            using var deflate = new DeflateStream(new MemoryStream(Convert.FromBase64String(base64)), CompressionMode.Decompress);
            using var unpacked = new MemoryStream();
            var buffer = new byte[16384];
            int read;

            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (unpacked.Length + read > maxUnpackedBytes) return (null, ShareProblem.TooBig);

                unpacked.Write(buffer, 0, read);
            }

            return (Encoding.UTF8.GetString(unpacked.ToArray()), null);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException)
        {
            return (null, ShareProblem.Broken);
        }
    }

    /// <summary>
    /// For text made of lines of tab-separated fields: a field with "%" written as "%25", a tab as "%09", a line break
    /// as "%0A" and a carriage return as "%0D", so no field can split a line. Mod Manager's profile codes write their
    /// fields this way.
    /// </summary>
    public static string EscapeField(string value) => (value ?? "").Replace("%", "%25").Replace("\t", "%09").Replace("\n", "%0A").Replace("\r", "%0D");

    /// <summary>The field as it was before <see cref="EscapeField"/>.</summary>
    public static string UnescapeField(string value) => (value ?? "").Replace("%0D", "\r").Replace("%0A", "\n").Replace("%09", "\t").Replace("%25", "%");

    private static void CheckPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("A share code needs a prefix.", nameof(prefix));
    }
}
