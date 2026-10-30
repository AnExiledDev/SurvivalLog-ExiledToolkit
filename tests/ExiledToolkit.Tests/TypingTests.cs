using ExiledToolkit.Core;
using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// typing.js exactly as the plugin embeds it, run in Jint on a fake root page with game pages in frames. The plugin
/// keeps the game's keys off between a typing|1 and the next typing|0, so a box the player types in must send the one,
/// and a box that no longer has the keyboard must send the other, or the game's keys stay off.
/// </summary>
public class TypingTests
{
    private readonly Engine _engine = new();

    public TypingTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.TypingFake.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private void LoadScript() => Run(Resource("ExiledToolkit.typing.js"));

    /// <summary>What the plugin heard since the last look, read the way it reads it.</summary>
    private List<bool> Heard()
    {
        var count = (int)_engine.Evaluate("posted.length").AsNumber();
        var messages = Enumerable.Range(0, count).Select(i => _engine.Evaluate($"posted[{i}]").AsString()).ToList();

        _engine.Execute("posted.length = 0");

        return messages.Select(m => Assert.IsType<PageCommand.Typing>(PageCommand.Parse(m)).IsTyping).ToList();
    }

    private void StartWithPage()
    {
        Run("connectVuplex(); var frame = openFrame(); var page = frame.contentDocument; var box = field(page);");
        LoadScript();
    }

    [Fact]
    public void A_text_box_in_a_game_page_taking_focus_tells_the_plugin_typing_has_started()
    {
        StartWithPage();
        Run("box.focus();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void Leaving_the_box_tells_the_plugin_typing_has_stopped()
    {
        StartWithPage();
        Run("box.focus(); box.blur(); runTimers();");

        Assert.Equal(new[] { true, false }, Heard());
    }

    [Fact]
    public void Moving_from_one_box_to_another_keeps_the_keys_off_the_whole_time()
    {
        StartWithPage();
        Run("var other = field(page, 'textarea'); box.focus(); other.focus(); runTimers(); tick();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void Moving_to_a_box_in_another_mods_page_keeps_the_keys_off()
    {
        StartWithPage();
        Run("var second = openFrame(); tick(); var far = field(second.contentDocument, 'input', 'search'); box.focus(); far.focus(); runTimers();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void A_checkbox_or_a_read_only_box_taking_focus_is_not_typing()
    {
        StartWithPage();
        Run("field(page, 'input', 'checkbox').focus(); var locked = field(page); locked.readOnly = true; locked.focus(); field(page, 'button', null).focus(); runTimers(); tick();");

        Assert.Empty(Heard());
    }

    [Fact]
    public void A_box_with_no_type_or_a_contenteditable_element_counts_as_typing()
    {
        StartWithPage();
        Run("var note = field(page, 'div', null); note.isContentEditable = true; note.focus(); note.blur(); runTimers();");

        Assert.Equal(new[] { true, false }, Heard());

        Run("field(page, 'input', '').focus();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void A_box_removed_while_it_has_focus_gives_the_keys_back_on_the_next_poll()
    {
        StartWithPage();
        Run("box.focus(); box.isConnected = false;");

        Assert.Equal(new[] { true }, Heard());

        Run("tick();");

        Assert.Equal(new[] { false }, Heard());
    }

    [Fact]
    public void A_box_hidden_while_it_has_focus_gives_the_keys_back_on_the_next_poll()
    {
        StartWithPage();
        Run("box.focus(); box.isHidden = true; tick();");

        Assert.Equal(new[] { true, false }, Heard());
    }

    [Fact]
    public void A_page_closed_while_its_box_has_focus_gives_the_keys_back_on_the_next_poll()
    {
        StartWithPage();
        Run("box.focus(); removeFrame(frame); tick();");

        Assert.Equal(new[] { true, false }, Heard());
    }

    [Fact]
    public void A_box_that_still_has_focus_keeps_the_keys_off_through_polls()
    {
        StartWithPage();
        Run("box.focus(); tick(); tick();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void A_page_opened_after_the_script_started_is_watched_from_the_next_poll()
    {
        StartWithPage();
        Run("var late = openFrame(); tick(); field(late.contentDocument).focus();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void A_frame_inside_a_game_page_is_watched_too()
    {
        StartWithPage();
        Run("var inner = openFrame(page); tick(); field(inner.contentDocument).focus();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void A_frame_that_loads_another_page_is_watched_again()
    {
        StartWithPage();
        Run("var next = navigate(frame); tick(); field(next).focus();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void A_box_in_the_root_document_itself_counts()
    {
        StartWithPage();
        Run("field(document).focus();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void A_box_that_had_focus_before_the_script_loaded_is_told_at_once()
    {
        Run("connectVuplex(); var frame = openFrame(); var box = field(frame.contentDocument); box.focus(); document.activeElement = frame;");
        LoadScript();

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void A_change_made_before_the_bridge_exists_is_sent_once_it_does()
    {
        Run("var frame = openFrame(); var box = field(frame.contentDocument);");
        LoadScript();
        Run("box.focus();");

        Assert.Empty(Heard());

        Run("connectVuplex(); tick(); tick();");

        Assert.Equal(new[] { true }, Heard());
    }

    [Fact]
    public void The_plugins_blur_takes_the_keyboard_from_the_box_and_gives_the_keys_back()
    {
        StartWithPage();
        Run("box.focus(); " + TypingScript.Blur + "; runTimers(); tick();");

        Assert.Equal(new[] { true, false }, Heard());
        Assert.False(_engine.Evaluate("page.activeElement === box").AsBoolean());
    }

    [Fact]
    public void The_plugins_blur_does_nothing_when_no_box_has_focus_or_the_script_is_missing()
    {
        Run(TypingScript.Blur + ";");
        StartWithPage();
        Run(TypingScript.Blur + "; runTimers();");

        Assert.Empty(Heard());
    }

    [Fact]
    public void Loading_the_script_twice_watches_each_page_once()
    {
        StartWithPage();
        LoadScript();
        Run("box.focus(); box.blur(); runTimers();");

        Assert.Equal(new[] { true, false }, Heard());
    }

    [Theory]
    [InlineData("slmod|toolkit|typing|1", true)]
    [InlineData("slmod|toolkit|typing|0", false)]
    public void The_plugin_reads_typing_messages(string message, bool isTyping)
    {
        Assert.Equal(isTyping, Assert.IsType<PageCommand.Typing>(PageCommand.Parse(message)).IsTyping);
    }

    [Theory]
    [InlineData("slmod|toolkit|typing")]
    [InlineData("slmod|toolkit|typing|yes")]
    [InlineData("slmod|toolkit|typing|1|2")]
    public void A_malformed_typing_message_is_not_acted_on(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(TypingTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
