using ExiledToolkit;
using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// Mods report the changes Harmony does not record, so the Mod Manager can find two mods changing the same thing. The
/// list has to say who changed what and which write came last, and drop a change once the mod undoes it, or the Mod
/// Manager would name a clash that is gone.
/// </summary>
public class ClaimsTests
{
    [Fact]
    public void Two_mods_adding_the_same_row_are_both_kept()
    {
        var book = new ClaimBook();

        book.AddRows("com.wind", "Config_Item", new[] { "910001", "910002" });
        book.AddRows("com.defense", "Config_Item", new[] { "910001" });

        Assert.Equal(new[] { new RowClaim("com.defense", "Config_Item", "910001"), new RowClaim("com.wind", "Config_Item", "910001"), new RowClaim("com.wind", "Config_Item", "910002") }, book.All().Rows);
    }

    [Fact]
    public void Rows_a_mod_takes_out_are_no_longer_its()
    {
        var book = new ClaimBook();

        book.AddRows("com.wind", "Config_Item", new[] { "910001", "910002" });
        book.RemoveRows("com.wind", "Config_Item", new[] { "910001" });
        book.RemoveRows("com.other", "Config_Item", new[] { "910002" });

        Assert.Equal(new[] { new RowClaim("com.wind", "Config_Item", "910002") }, book.All().Rows);
    }

    [Fact]
    public void Of_two_mods_writing_one_field_the_later_write_has_the_higher_order()
    {
        var book = new ClaimBook();

        book.Write("com.rules", "Config_Item", "1203", "Capacity", "30");
        book.Write("com.packrat", "Config_Item", "1203", "Capacity", "40");
        book.Write("com.rules", "Config_Item", "1203", "Capacity", "35");

        var fields = book.All().Fields;

        Assert.Equal(new[] { ("com.packrat", "40"), ("com.rules", "35") }, fields.Select(field => (field.Guid, field.Value)));
        Assert.True(fields[1].Order > fields[0].Order);
    }

    [Fact]
    public void A_field_put_back_is_dropped_for_that_mod_only()
    {
        var book = new ClaimBook();

        book.Write("com.rules", "Config_Item", "1203", "Capacity", "30");
        book.Write("com.packrat", "Config_Item", "1203", "Capacity", "40");
        book.PutBack("com.rules", "Config_Item", "1203", "Capacity");

        Assert.Equal("com.packrat", book.All().Fields.Single().Guid);
    }

    [Fact]
    public void A_hook_reported_twice_is_one_hook_and_goes_when_taken_off()
    {
        var book = new ClaimBook();

        book.Hook("com.power", "PowerSystem.Tick");
        book.Hook("com.power", "PowerSystem.Tick");
        book.Hook("com.multi", "PowerSystem.Tick");

        Assert.Equal(2, book.All().Hooks.Count);

        book.Unhook("com.power", "PowerSystem.Tick");

        Assert.Equal(new[] { new HookClaim("com.multi", "PowerSystem.Tick") }, book.All().Hooks);
    }

    [Fact]
    public void Bytes_changed_again_at_one_offset_replace_the_first_report()
    {
        var book = new ClaimBook();

        book.ChangeBytes("com.dev", "Bag.Add", 20, 2);
        book.ChangeBytes("com.dev", "Bag.Add", 20, 6);
        book.ChangeBytes("com.dev", "Bag.Add", 40, 1);
        book.RestoreBytes("com.dev", "Bag.Add", 40);

        Assert.Equal(new[] { new BytesClaim("com.dev", "Bag.Add", 20, 6) }, book.All().Bytes);
    }

    [Fact]
    public void A_copy_does_not_change_when_mods_report_more()
    {
        var book = new ClaimBook();

        book.Hook("com.power", "PowerSystem.Tick");
        var before = book.All();
        book.Hook("com.multi", "PowerSystem.Tick");

        Assert.Single(before.Hooks);
    }

    [Fact]
    public void Number_and_text_ids_name_the_same_row()
    {
        var guid = "com.claims.test." + Guid.NewGuid();

        Claims.AddedRows(guid, "Config_Item", new[] { 910001, 910001 });
        Claims.AddedRows(guid, "Config_Item", new[] { "910001" });
        Claims.Wrote(guid, "Config_Item", 910001L, "Price", 1.5f);

        var all = Claims.All();

        Assert.Equal(new[] { new RowClaim(guid, "Config_Item", "910001") }, all.Rows.Where(row => row.Guid == guid));
        Assert.Equal("1.5", all.Fields.Single(field => field.Guid == guid).Value);
    }

    [Fact]
    public void A_method_is_named_by_its_type_short_name_as_the_Mod_Manager_names_Harmony_patches()
    {
        var guid = "com.claims.test." + Guid.NewGuid();

        Claims.Hooked(guid, typeof(ClaimBook), "Hook");
        Claims.ChangedBytes(guid, typeof(ClaimBook), "Write", 16, 4);

        Assert.Equal("ClaimBook.Hook", Claims.All().Hooks.Single(hook => hook.Guid == guid).Method);
        Assert.Equal("ClaimBook.Write", Claims.All().Bytes.Single(bytes => bytes.Guid == guid).Method);
    }

    [Fact]
    public void A_report_without_a_name_or_with_no_bytes_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Claims.Wrote("", "Config_Item", 1, "Price", 2));
        Assert.Throws<ArgumentException>(() => Claims.AddedRows("com.a", " ", new[] { 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Claims.ChangedBytes("com.a", typeof(ClaimBook), "Write", 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Claims.ChangedBytes("com.a", typeof(ClaimBook), "Write", -1, 2));
    }
}

/// <summary>
/// The game's keys are asked about every frame a mod key is held, so each is read once and kept until the game saves
/// its key settings. A key read before the game made its settings must not be kept, or it would stay unknown.
/// </summary>
public class KeyBookTests
{
    private const int Backpack = 0;
    private const int KeyB = 98;
    private const int KeyI = 105;

    [Fact]
    public void A_key_already_read_is_not_asked_of_the_game_again_until_its_settings_are_saved()
    {
        var game = new Dictionary<int, int> { [Backpack] = KeyB };
        var reads = 0;
        var book = new KeyBook(action => { reads++; return game[action]; }) { IsWatched = true };

        Assert.Equal(KeyB, book.KeyFor(Backpack));
        game[Backpack] = KeyI;
        Assert.Equal(KeyB, book.KeyFor(Backpack));

        book.Forget();

        Assert.Equal(KeyI, book.KeyFor(Backpack));
        Assert.Equal(2, reads);
    }

    [Fact]
    public void A_key_the_game_cannot_tell_yet_is_asked_again_next_time()
    {
        int? key = null;
        var book = new KeyBook(_ => key) { IsWatched = true };

        Assert.Null(book.KeyFor(Backpack));
        key = KeyB;
        Assert.Equal(KeyB, book.KeyFor(Backpack));
    }

    [Fact]
    public void Without_the_save_watched_every_ask_reads_the_game()
    {
        var reads = 0;
        var book = new KeyBook(_ => { reads++; return KeyB; });

        book.KeyFor(Backpack);
        book.KeyFor(Backpack);

        Assert.Equal(2, reads);
    }
}
