using System.IO.Compression;
using System.Text;
using ExiledToolkit.Kit;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// Share codes are text players paste in chats and wiki links. Every code Mod Manager already handed out has to keep
/// reading, and a pasted code from a stranger must never be able to fill the memory.
/// </summary>
public class ShareCodecTests
{
    private const string Prefix = "SLMM1-";
    private const int MaxPasted = 200_000;
    private const int MaxUnpacked = 1_000_000;

    // A profile in the lines Mod Manager writes before it packs them.
    private const string ProfileText = "n\tNight run\nm\tcom.x\tName%09with tab\t1.0\t1\t1\nm\tcom.y\t\u6a21\u7ec4\t\t0\t0\ns\tcom.x\tShop\tStock\t20004:5%0A%250A,1\ne\t3\n";

    /// <summary>The packing half of Mod Manager's ShareCode.Write, copied unchanged.</summary>
    private static string ModManagerPack(string text)
    {
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
        {
            var bytes = Encoding.UTF8.GetBytes(text);

            deflate.Write(bytes, 0, bytes.Length);
        }

        return "SLMM1-" + Convert.ToBase64String(packed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static (string? Text, ShareProblem? Problem) Read(string? pasted) => ShareCodec.Unpack(pasted, Prefix, MaxPasted, MaxUnpacked);

    private static void Gives(string? text, ShareProblem? problem, (string? Text, ShareProblem? Problem) read)
    {
        Assert.Equal(text, read.Text);
        Assert.Equal(problem, read.Problem);
    }

    [Fact]
    public void A_text_packs_and_reads_back_the_same()
    {
        var code = ShareCodec.Pack(Prefix, ProfileText);

        Assert.StartsWith(Prefix, code);
        Assert.Matches("^[A-Za-z0-9_-]+$", code.Substring(Prefix.Length));
        Gives(ProfileText, null, Read(code));
    }

    [Theory]
    [InlineData(ProfileText)]
    [InlineData("n\tx\ne\t0\n")]
    [InlineData("")]
    public void A_code_is_byte_for_byte_what_Mod_Manager_writes_for_the_same_text(string text)
    {
        Assert.Equal(ModManagerPack(text), ShareCodec.Pack(Prefix, text));
    }

    [Fact]
    public void A_long_code_from_Mod_Manager_with_both_base64_swaps_in_it_still_reads()
    {
        var text = string.Concat(Enumerable.Range(0, 300).Select(i => "m\tcom.mod" + i + "\tMod " + i + "\t1." + i * 7 + "\t1\t1\n"));
        var code = ModManagerPack(text);

        Assert.Contains("-", code.Substring(Prefix.Length));
        Assert.Contains("_", code);
        Assert.Equal(ShareCodec.Pack(Prefix, text), code);
        Assert.Equal(text, Read(code).Text);
    }

    [Fact]
    public void A_code_built_by_hand_reads_without_padding_and_with_the_url_safe_letters()
    {
        // One stored deflate block holding "hi": 01 02 00 FD FF 68 69. In base64 that is "AQIA/f9oaQ==".
        Gives("hi", null, Read("SLMM1-AQIA_f9oaQ"));
    }

    [Fact]
    public void A_code_broken_across_lines_by_a_chat_app_with_words_around_it_still_reads()
    {
        var code = ShareCodec.Pack(Prefix, ProfileText);
        var chat = "here is my setup: " + code.Substring(0, 10) + "\n  " + code.Substring(10) + ". have fun";

        Assert.Equal(ProfileText, Read(chat).Text);
    }

    [Theory]
    [InlineData(null, ShareProblem.Empty)]
    [InlineData("", ShareProblem.Empty)]
    [InlineData(" \n\t ", ShareProblem.Empty)]
    [InlineData("hello there", ShareProblem.NotACode)]
    [InlineData("SLMM1-%%%", ShareProblem.NotACode)]
    [InlineData("SLMM1-AAAAA", ShareProblem.Broken)]
    [InlineData("look: SLMM1-BwAA", ShareProblem.Broken)]
    public void A_text_that_is_not_a_whole_code_says_why(string? pasted, ShareProblem problem)
    {
        // "AAAAA" is not base64 at any padding. "BwAA" is base64 for a deflate block of a type that does not exist.
        Gives(null, problem, Read(pasted));
    }

    [Fact]
    public void A_code_that_unpacks_huge_is_refused_before_it_fills_the_memory()
    {
        // A few kilobytes of code that would unpack to megabytes of one repeated letter.
        var bomb = ShareCodec.Pack(Prefix, new string('a', MaxUnpacked * 3));

        Assert.True(bomb.Length < MaxPasted);
        Gives(null, ShareProblem.TooBig, Read(bomb));
    }

    [Fact]
    public void The_unpacked_limit_lets_through_exactly_its_size_and_not_one_byte_more()
    {
        var full = new string('a', 1000);

        Assert.Equal(full, ShareCodec.Unpack(ShareCodec.Pack(Prefix, full), Prefix, MaxPasted, 1000).Text);
        Assert.Equal(ShareProblem.TooBig, ShareCodec.Unpack(ShareCodec.Pack(Prefix, full + "a"), Prefix, MaxPasted, 1000).Problem);
    }

    [Fact]
    public void Pasted_text_over_the_length_limit_is_refused_before_it_is_decoded()
    {
        var code = ShareCodec.Pack(Prefix, ProfileText);

        Assert.Equal(ShareProblem.TooBig, ShareCodec.Unpack(code, Prefix, code.Length - 1, MaxUnpacked).Problem);
        Assert.Equal(ProfileText, ShareCodec.Unpack(code, Prefix, code.Length, MaxUnpacked).Text);
        Assert.Equal(ShareProblem.TooBig, Read(new string('x', MaxPasted + 1)).Problem);
    }

    [Fact]
    public void Only_a_code_with_this_exact_prefix_is_read()
    {
        var other = ShareCodec.Pack("SLCD1-", "preset");

        Assert.Equal(ShareProblem.NotACode, Read(other).Problem);
        Assert.Equal(ShareProblem.NotACode, Read("slmm1-" + other.Substring(6)).Problem);
        Assert.Equal("preset", ShareCodec.Unpack(other, "SLCD1-", MaxPasted, MaxUnpacked).Text);
        Assert.Equal(ShareProblem.NotACode, ShareCodec.Unpack("AxB-" + other.Substring(6), "A.B-", MaxPasted, MaxUnpacked).Problem);
        Assert.Throws<ArgumentException>(() => ShareCodec.Pack("", "x"));
        Assert.Throws<ArgumentException>(() => ShareCodec.Unpack("x", "", MaxPasted, MaxUnpacked));
    }

    [Fact]
    public void A_field_with_tabs_line_breaks_and_percents_cannot_split_a_line_and_reads_back()
    {
        var value = "50% \"loot\"\tnext\r\nline %0A";
        var escaped = ShareCodec.EscapeField(value);

        Assert.Equal("50%25 \"loot\"%09next%0D%0Aline %250A", escaped);
        Assert.Equal(value, ShareCodec.UnescapeField(escaped));
    }
}
