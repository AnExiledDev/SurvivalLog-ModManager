using System.Collections.Generic;
using Jint;
using ModManager.Core;
using Xunit;

namespace ModManager.Tests;

/// <summary>
/// An item list setting is drawn as item cards a player reads at a glance: each item's picture, name and amount, the
/// workbench card of the recipe, what taking the thing down gives back, and why the mod cannot use a list. page.js
/// runs in Jint on PageFake.js with every element it makes swapped for a stand-in that keeps the markup written into
/// it, so the drawn settings can be read back.
/// </summary>
public class ItemFieldPageTests
{
    private const string StandIns = """
        var drawn = [];
        var listeners = {};
        var posted = [];

        function standIn() {
          var own = {};

          return new Proxy(function () {}, {
            get: function (target, key) {
              if (key === Symbol.toPrimitive) return function () { return 0; };
              if (key === 'addEventListener') return function (type, fn) { (listeners[type] = listeners[type] || []).push(fn); };
              if (key === Symbol.iterator) return function () { return [][Symbol.iterator](); };
              if (key === 'then') return undefined;
              if (!(key in own)) own[key] = standIn();

              return own[key];
            },
            set: function (target, key, value) {
              own[key] = value;
              if (key === 'innerHTML') drawn.push(String(value));

              return true;
            },
            apply: function () { return standIn(); },
            construct: function () { return standIn(); }
          });
        }

        document.createElement = function () { return standIn(); };
        window.localStorage = { getItem: function () { return null; }, setItem: function () {} };
        window.vuplex = { postMessage: function (text) { posted.push(text.split('|').map(decodeURIComponent).join('|')); } };

        // An element the page's handlers can read: its tag, classes and data, and what each selector finds up its tree.
        function el(tag, classes, data, up, value) {
          return {
            tagName: tag, type: 'text', value: value || '', dataset: data || {},
            classList: { contains: function (name) { return classes.indexOf(name) >= 0; } },
            closest: function (selector) { return (up || {})[selector] || null; },
            blur: function () {}, focus: function () {}
          };
        }

        function fire(type, event) {
          var e = { preventDefault: function () {}, defaultPrevented: false, shiftKey: false, detail: 1, button: 0 };

          for (var key in event) e[key] = event[key];

          (listeners[type] || []).forEach(function (fn) { fn(e); });
        }

        var firstRow = { dataset: { row: '0' } };

        function act(name) { return el('SPAN', [], { act: name }, { '[data-act]': { dataset: { act: name } }, '[data-row]': firstRow }); }

        function sets() { return posted.filter(function (text) { return text.indexOf('|set|') >= 0; }); }
        """;

    // Steel pipe: 1 kg, worth 10. Copper wire: 0.5 kg, worth 4. The turbine package is what the recipe makes.
    private static readonly CatalogItem[] Items =
    {
        new(20004, new Description("Steel Pipe", "钢管"), "Res/pipe.png", 9) { Grams = 1000, Price = 10, RecipeUses = 40 },
        new(20104, new Description("Copper Wire", "铜线"), "Res/wire.png", 9) { Grams = 500, Price = 4, RecipeUses = 25 },
        new(64001, new Description("Small Turbine Package", "小型风力发电机包裹"), "Res/turbine.png", 14),
    };

    private static Setting Materials(string current) =>
        List("Materials", new SettingKind.ItemCounts(7, 99), "20004:5,20104:3", current) with { Makes = 64001 };

    private static Setting Returns(string current, string? blank = "20004:3,20104:2") =>
        List("Returns", new SettingKind.ItemCounts(7, 99, AutoWhenBlank: true), "", current) with
        {
            ReturnsOf = new SettingId("g", "SmallTurbine", "Materials"),
            Answer = new ModAnswer(false, null, blank, new Description("Follows ReturnPercent: 50% of the materials.", "按返还比例：材料的 50%。")),
        };

    [Fact]
    public void Each_item_shows_as_a_card_with_its_amount_and_the_recipe_shows_at_the_workbench()
    {
        var pane = Draw(Materials("20004:5,20104:3"));
        var field = Field(pane, "Materials");

        Assert.Contains("data-slot=\"0\"", field);
        Assert.Contains("<img src=\"Res/pipe.png\" alt=\"\"><span class=\"qty\">&times;5</span>", field);
        Assert.Contains("<div class=\"nm\">Copper Wire</div>", field);
        Assert.Contains("<b>2</b> / 7 items", field);
        Assert.Contains("<code data-act=\"editText\"", field);
        Assert.Contains(">20004:5, 20104:3</code>", field);
        Assert.Contains("ADD ITEM", field);

        // The workbench card: 8 pieces, 6.5 kg, worth 5 x 10 + 3 x 4 = 62.
        Assert.Contains("<div class=\"t\">Small Turbine Package</div>", field);
        Assert.Contains("<b>8</b><span>pieces</span>", field);
        Assert.Contains("<b>6.5</b><span>kg to carry</span>", field);
        Assert.Contains("<b>62</b><span>trade value</span>", field);
    }

    [Fact]
    public void A_full_list_offers_no_add_card()
    {
        var full = List("Kit", new SettingKind.ItemCounts(MaxKinds: 2), "", "20004:1,20104:1");

        Assert.DoesNotContain("data-act=\"pick\"", Field(Draw(full), "Kit"));
    }

    [Fact]
    public void A_blank_list_the_mod_fills_in_shows_the_mods_list_with_a_way_to_set_it_by_hand()
    {
        var pane = Draw(Materials("20004:5,20104:3"), Returns(""));
        var field = Field(pane, "Returns");

        Assert.Contains("class=\"slot auto\"", field);
        Assert.Contains("<span class=\"qty\">&times;3</span>", field);
        Assert.Contains("<div class=\"from\">of 5</div>", field);
        Assert.Contains("data-act=\"byHand\"", field);
        Assert.Contains("Follows ReturnPercent: 50% of the materials.", field);
        Assert.Contains("(blank)</code>", field);
        Assert.DoesNotContain("data-act=\"dropItem\"", field);

        // The recipe's card shows what taking it down gives back, chosen by the mod.
        Assert.Contains("Taking it down gives back &middot; auto", Field(pane, "Materials"));
    }

    [Fact]
    public void A_list_emptied_by_hand_stays_empty_and_offers_the_add_card()
    {
        var field = Field(Draw(Materials("20004:5,20104:3"), Returns("none")), "Returns");

        Assert.DoesNotContain("class=\"slot auto\"", field);
        Assert.Contains("data-act=\"pick\"", field);
        Assert.Contains("(nothing)</code>", field);
        Assert.Contains("Set by hand. Reset it to let the mod choose again.", field);
    }

    [Fact]
    public void Giving_back_more_than_the_recipe_is_worth_warns_of_a_profit()
    {
        // 9 pipes back (90) for a recipe worth 62.
        var field = Field(Draw(Materials("20004:5,20104:3"), Returns("20004:9")), "Returns");

        Assert.Contains("Worth more than what it takes to make (90 against 62)", field);
    }

    [Fact]
    public void A_list_the_mod_cannot_use_says_why_in_the_mods_words()
    {
        var refused = Materials("20004:5,20104:3") with { Answer = new ModAnswer(true, new Description("The Medium Wind Turbine already takes exactly these materials.", "中型风力发电机已经用了完全相同的材料。"), null, null) };
        var field = Field(Draw(refused), "Materials");

        Assert.Contains("<div class=\"mf-warn\"><span>&#x26A0;</span><div>The Medium Wind Turbine already takes exactly these materials.</div></div>", field);
        Assert.Contains("The mod cannot use this list as it is.", field);
    }

    [Fact]
    public void A_list_the_mod_checks_and_can_use_says_so()
    {
        var fine = Materials("20004:5,20104:3") with { Answer = new ModAnswer(true, null, null, null) };

        Assert.Contains("The mod can use this list.", Field(Draw(fine), "Materials"));
    }

    // ===== A List setting: entries the mod lists itself =====

    private static readonly SettingKind.EntryList BedKind = new(Choices: new[] { "Auto", "Sleep", "Nap", "Skip" });

    private static Setting Beds(string current) =>
        List("BedModes", BedKind, "", current) with { ChoiceZh = new Dictionary<string, string> { ["Sleep"] = "睡觉" } };

    private static Setting Buffs(string current) => List("Buffs", new SettingKind.EntryList(Min: 0.1, Max: 100), "", current);

    private static string BedEntries(Setting beds) =>
        PageScript.Entries(PageScript.EntriesId(beds.Id), ListEntries.Parse("9076|Wooden Bed|木床|Res/bed.png|Beds|床|Sleeps 8 hours|睡 8 小时\n9080|Sleeping Bag|睡袋||Beds|床\n9100|Sofa|沙发||Seats|座椅"));

    private static string BuffEntries(Setting buffs) =>
        PageScript.Entries(PageScript.EntriesId(buffs.Id), ListEntries.Parse("12|Well Fed|饱食||Good|增益\n40|Hungry|饥饿||Bad|负面"));

    [Fact]
    public void A_List_setting_shows_each_entry_as_a_row_with_its_picture_name_description_and_choice()
    {
        var beds = Beds("9076:Sleep,9100:Skip");
        var field = Field(DrawWith(new[] { BedEntries(beds) }, beds), "BedModes");

        Assert.Contains("<div class=\"el-row\" data-slot=\"0\">", field);
        Assert.Contains("<div class=\"el-ic\"><img src=\"Res/bed.png\" alt=\"\"></div>", field);
        Assert.Contains("<div class=\"el-nm\">Wooden Bed<span>木床</span></div><div class=\"el-ds\">Sleeps 8 hours</div>", field);

        // Every choice is a button on the row, the one it has lit; the Chinese name the mod gave shows only in Chinese.
        Assert.Contains("data-act=\"setChoice\" data-index=\"0\" data-value=\"Sleep\" class=\"on\">Sleep</span>", field);
        Assert.Contains("data-act=\"setChoice\" data-index=\"1\" data-value=\"Skip\" class=\"on\">Skip</span>", field);

        // An entry with no picture shows the first letter of its name; no cards are drawn.
        Assert.Contains("<span class=\"noimg\">S</span>", field);
        Assert.DoesNotContain("class=\"slot", field);
        Assert.Contains("data-act=\"pick\">+ ADD</div>", field);
        Assert.Contains(">9076:Sleep, 9100:Skip</code>", field);
    }

    [Fact]
    public void Clicking_a_choice_on_a_row_sets_that_choice()
    {
        var beds = Beds("9076:Sleep,9100:Skip");
        var engine = OpenWith(new[] { BedEntries(beds) }, beds);

        engine.Execute("fire('click', { target: el('SPAN', [], { act: 'setChoice', index: '1', value: 'Nap' }, { '[data-act]': { dataset: { act: 'setChoice', index: '1', value: 'Nap' } }, '[data-row]': firstRow }) })");

        Assert.EndsWith("|SmallTurbine|BedModes|9076:Sleep,9100:Nap", engine.Evaluate("sets().join('\\n')").AsString());
    }

    [Fact]
    public void A_row_moves_up_and_down_in_the_list_order()
    {
        var beds = Beds("9076:Sleep,9100:Skip,9080:Nap");
        var engine = OpenWith(new[] { BedEntries(beds) }, beds);

        // The first row cannot move up and the last cannot move down.
        var field = Field(Pane(engine), "BedModes");

        Assert.Contains("data-act=\"moveUp\" data-index=\"0\" class=\"off\"", field);
        Assert.Contains("data-act=\"moveDown\" data-index=\"2\" class=\"off\"", field);

        engine.Execute("fire('click', { target: el('SPAN', [], { act: 'moveUp', index: '2' }, { '[data-act]': { dataset: { act: 'moveUp', index: '2' } }, '[data-row]': firstRow }) })");

        Assert.EndsWith("|SmallTurbine|BedModes|9076:Sleep,9080:Nap,9100:Skip", engine.Evaluate("sets().join('\\n')").AsString());
    }

    [Fact]
    public void The_picker_lists_each_entry_as_a_row_with_an_Add_button_and_stays_open_after_adding()
    {
        var beds = Beds("9076:Sleep");
        var engine = OpenWith(new[] { BedEntries(beds) }, beds);

        engine.Execute("fire('click', { target: act('pick') })");

        var picker = engine.Evaluate("drawn.join('\\n')").AsString();

        // The mod's groups are the shelves; each row has its picture, name and description, and its button.
        Assert.Contains("data-cat=\"Beds\"><span>Beds</span><i>2</i>", picker);
        Assert.DoesNotContain("Used in recipes", picker);
        Assert.Contains("<div class=\"pk rows\">", picker);
        Assert.Contains("<div class=\"el-nm\">Sleeping Bag<span>睡袋</span></div></div><span class=\"pk-grp\">Beds</span><span class=\"pk-btn\" data-act=\"pkAdd\" data-id=\"9080\">+ ADD</span>", picker);
        Assert.Contains("<span class=\"pk-btn in\" data-act=\"pkDrop\" data-id=\"9076\"><b>&#x2713; ADDED</b>", picker);

        var drawnBefore = engine.Evaluate("drawn.length").AsNumber();

        engine.Execute("fire('click', { target: " + PickerButton("pkAdd", "9080") + " })");

        Assert.EndsWith("|SmallTurbine|BedModes|9076:Sleep,9080:Auto", engine.Evaluate("sets().join('\\n')").AsString());

        // The picker is drawn again, not closed, and shows the new entry as added before the mod answers.
        var after = engine.Evaluate("drawn.slice(" + drawnBefore + ").filter(function (h) { return h.indexOf('pk-btn') >= 0; }).pop() || ''").AsString();

        Assert.Contains("data-act=\"pkDrop\" data-id=\"9080\"", after);
    }

    private static string HeldEntries(Setting life) =>
        PageScript.Entries(PageScript.EntriesId(life.Id), ListEntries.Parse("i12|Bandage|绷带||Meds|药品|Life +10|生命 +10|You have 3|有 3 件\ni13|Pill|药片||Meds|药品|Life +5|生命 +5\ni14|Soup|汤||Food|食物|Life +2|生命 +2|You have 1|有 1 件"));

    [Fact]
    public void Entries_the_player_has_are_marked_and_get_a_shelf_of_their_own()
    {
        var life = List("LifeItems", new SettingKind.EntryList(), "", "i12,i13");
        var engine = OpenWith(new[] { HeldEntries(life) }, life);
        var field = Field(Pane(engine), "LifeItems");

        // In the list, a row the player has says how many; one they do not have is drawn plain.
        Assert.Contains("<div class=\"el-row has\" data-slot=\"0\">", field);
        Assert.Contains("<span class=\"el-have\">You have 3</span>", field);
        Assert.Contains("<div class=\"el-row\" data-slot=\"1\">", field);

        engine.Execute("fire('click', { target: act('pick') })");

        var picker = engine.Evaluate("drawn.join('\\n')").AsString();

        Assert.Contains("data-cat=\"~have\"><span>&#x2605; You have</span><i>2</i>", picker);
        Assert.Contains("<div class=\"pk-row has\">", picker);

        // The shelf lists only what the player has, each with its group.
        var before = engine.Evaluate("drawn.length").AsNumber();

        engine.Execute("fire('click', { target: el('DIV', ['pk-cat'], { cat: '~have' }, { '.pk-host': {}, '.pk-cat': { dataset: { cat: '~have' } } }) })");

        var shelf = engine.Evaluate("drawn.slice(" + before + ").filter(function (h) { return h.indexOf('pk-btn') >= 0; }).pop() || ''").AsString();

        Assert.Contains("data-id=\"i12\"", shelf);
        Assert.Contains("data-id=\"i14\"", shelf);
        Assert.DoesNotContain("data-id=\"i13\"", shelf);
        Assert.Contains("<span class=\"el-have\">You have 1</span><span class=\"pk-grp\">Food</span>", shelf);
    }

    [Fact]
    public void A_list_whose_mod_says_nothing_of_what_the_player_has_gets_no_such_shelf()
    {
        var beds = Beds("9076:Sleep");
        var engine = OpenWith(new[] { BedEntries(beds) }, beds);

        engine.Execute("fire('click', { target: act('pick') })");

        Assert.DoesNotContain("~have", engine.Evaluate("drawn.join('\\n')").AsString());
    }

    [Fact]
    public void A_click_on_Added_in_the_picker_takes_the_entry_out()
    {
        var beds = Beds("9076:Sleep,9100:Skip");
        var engine = OpenWith(new[] { BedEntries(beds) }, beds);

        engine.Execute("fire('click', { target: act('pick') })");
        engine.Execute("fire('click', { target: " + PickerButton("pkDrop", "9076") + " })");

        Assert.EndsWith("|SmallTurbine|BedModes|9100:Skip", engine.Evaluate("sets().join('\\n')").AsString());
    }

    [Fact]
    public void A_full_list_offers_no_Add_in_the_field_or_the_picker()
    {
        var beds = List("BedModes", BedKind with { MaxKinds = 1 }, "", "9076:Sleep");
        var engine = OpenWith(new[] { BedEntries(beds) }, beds);

        Assert.Contains("<div class=\"el-add off\" data-act=\"pick\">", Field(Pane(engine), "BedModes"));

        engine.Execute("fire('click', { target: act('pick') })");
        engine.Execute("fire('click', { target: " + PickerButton("pkAdd", "9080") + " })");

        Assert.Equal(0, engine.Evaluate("sets().length").AsNumber());
        Assert.Contains("The list holds 1 already.", engine.Evaluate("drawn.join('\\n')").AsString());
    }

    [Fact]
    public void A_blank_list_the_mod_fills_in_shows_the_mods_line_and_never_the_whole_list()
    {
        var life = List("LifeItems", new SettingKind.EntryList(AutoWhenBlank: true), "", "") with
        {
            Answer = new ModAnswer(false, null, "9076,9080,9100", new Description("Left blank: every bed.", "留空：所有的床。")),
        };
        var field = Field(DrawWith(new[] { BedEntries(life) }, life), "LifeItems");

        Assert.Contains("<div class=\"el-empty\">Left blank: every bed.</div>", field);
        Assert.DoesNotContain("Wooden Bed", field);
        Assert.DoesNotContain("data-slot=", field);
        Assert.Contains("(blank)</code>", field);
    }

    private static string PickerButton(string name, string id) =>
        "el('SPAN', ['pk-btn'], { act: '" + name + "', id: '" + id + "' }, { '.pk-host': {}, '[data-act]': { dataset: { act: '" + name + "', id: '" + id + "' } } })";

    [Fact]
    public void A_number_each_entry_carries_steps_by_a_tenth_and_stays_in_its_range()
    {
        var buffs = Buffs("12:1.5,40:0.1");
        var engine = OpenWithEntries(new[] { BuffEntries(buffs) }, buffs);

        engine.Execute("fire('click', { target: onRow('inc', 0, 0) })");
        engine.Execute("fire('click', { target: onRow('dec', 0, 1) })");
        engine.Execute("runTimers()");

        Assert.EndsWith("|SmallTurbine|Buffs|12:1.6,40:0.1", engine.Evaluate("sets().join('\\n')").AsString());
    }

    [Fact]
    public void A_List_setting_waits_as_text_until_its_entries_arrive_and_says_so_when_the_mod_lists_none()
    {
        var beds = Beds("9076:Sleep");
        var before = Pane(OpenWith(System.Array.Empty<string>(), beds));

        Assert.DoesNotContain("row wide", before);
        Assert.Contains("value=\"9076:Sleep\"", before);

        var empty = Field(DrawWith(new[] { PageScript.Entries(PageScript.EntriesId(beds.Id), ListEntries.Parse("")) }, beds), "BedModes");

        Assert.Contains("The mod has not listed what this can hold yet.", empty);
    }

    private static Engine OpenWithEntries(string[] before, params Setting[] settings)
    {
        var engine = OpenWith(before, settings);

        engine.Execute(HandTimers);

        return engine;
    }

    private static Setting List(string key, SettingKind kind, string defaultValue, string current) =>
        new(new SettingId("g", "SmallTurbine", key), key, new Description("en", "zh"), kind, defaultValue, current, current, false);

    [Fact]
    public void A_second_click_on_an_item_in_the_picker_puts_it_in()
    {
        // The first click redraws the grid, so the browser's double-click never reaches the item; the click count does.
        var engine = Open(Materials("20004:5,20104:3"));
        const string package = "el('DIV', ['it'], { id: '64001' }, { '.pk-host': {}, '.it': { dataset: { id: '64001' } } })";

        engine.Execute("fire('click', { target: act('pick') })");
        engine.Execute("fire('click', { target: " + package + ", detail: 1 })");

        Assert.Equal(0, engine.Evaluate("sets().length").AsNumber());

        engine.Execute("fire('click', { target: " + package + ", detail: 2 })");

        Assert.EndsWith("|SmallTurbine|Materials|20004:5,20104:3,64001:1", engine.Evaluate("sets().join('\\n')").AsString());
    }

    [Fact]
    public void Esc_on_the_text_line_writes_nothing_even_when_the_browser_sends_the_typed_change()
    {
        // Esc ends the edit and redraws the cards; Chromium then sends a change for the line it took away.
        var engine = Open(Materials("20004:5,20104:3"));
        const string line = "el('INPUT', ['mf-in'], { input: 'text' }, { '[data-row]': firstRow }, 'junk')";

        engine.Execute("fire('click', { target: act('editText') })");
        engine.Execute("fire('keydown', { target: " + line + ", key: 'Escape' })");
        engine.Execute("fire('change', { target: " + line + " })");

        Assert.Equal(0, engine.Evaluate("sets().length").AsNumber());

        // Leaving the line any other way still writes it.
        engine.Execute("fire('click', { target: act('editText') })");
        engine.Execute("fire('change', { target: " + line + " })");

        Assert.EndsWith("|SmallTurbine|Materials|junk", engine.Evaluate("sets().join('\\n')").AsString());
    }

    // Timers the test runs by hand, so a count that waits to be written can be written on cue.
    private const string HandTimers = """
        var timers = [];
        setTimeout = function (fn) { timers.push(fn); return timers.length; };
        clearTimeout = function (id) { if (id > 0) timers[id - 1] = null; };
        function runTimers() { var due = timers; timers = []; due.forEach(function (fn) { if (fn) fn(); }); }
        function onRow(name, row, index) {
          return el('SPAN', [], { act: name, index: String(index) }, { '[data-act]': { dataset: { act: name, index: String(index) } }, '[data-row]': { dataset: { row: String(row) } } });
        }
        function stepper(row, slot) {
          var card = { dataset: { slot: String(slot) }, closest: function (s) { return s === '[data-row]' ? { dataset: { row: String(row) } } : null; } };
          var box = { closest: function (s) { return s === '.slot[data-slot]' ? card : null; } };
          return el('SPAN', [], {}, { '.mf .slot[data-slot] .stp': box, '.mf .slot[data-slot]': card });
        }
        """;

    private static Engine OpenWithTimers(params Setting[] settings)
    {
        var engine = Open(settings);

        engine.Execute(HandTimers);

        return engine;
    }

    [Fact]
    public void A_count_stepped_in_one_list_is_written_when_another_list_is_stepped_straight_after()
    {
        var engine = OpenWithTimers(Materials("20004:5,20104:3"), List("Kit", new SettingKind.ItemCounts(7, 99), "", "20004:1"));

        engine.Execute("fire('click', { target: onRow('inc', 0, 0) })");
        engine.Execute("fire('click', { target: onRow('inc', 1, 0) })");
        engine.Execute("runTimers()");

        var written = engine.Evaluate("sets().join('\\n')").AsString();

        Assert.Contains("|SmallTurbine|Materials|20004:6,20104:3", written);
        Assert.Contains("|SmallTurbine|Kit|20004:2", written);
    }

    [Fact]
    public void Removing_a_card_right_after_a_step_is_not_undone_by_the_waiting_count()
    {
        var engine = OpenWithTimers(Materials("20004:5,20104:3"));

        engine.Execute("fire('click', { target: onRow('inc', 0, 0) })");
        engine.Execute("fire('click', { target: onRow('dropItem', 0, 1) })");
        engine.Execute("runTimers()");

        Assert.EndsWith("|SmallTurbine|Materials|20004:6", engine.Evaluate("sets().join('\\n')").AsString());
    }

    [Fact]
    public void The_wheel_steps_an_amount_only_over_its_buttons_and_Shift_steps_ten()
    {
        var engine = OpenWithTimers(Materials("20004:5,20104:3"));
        const string card = "el('IMG', [], {}, { '.mf .slot[data-slot]': { dataset: { slot: '0' }, closest: function () { return firstRow; } } })";

        // Scrolling the page past a card leaves its amount alone, and a wheel event that turns nothing does nothing.
        engine.Execute("fire('wheel', { target: " + card + ", deltaY: 100 })");
        engine.Execute("fire('wheel', { target: stepper(0, 0), deltaY: 0, deltaX: 0 })");
        engine.Execute("runTimers()");

        Assert.Equal(0, engine.Evaluate("sets().length").AsNumber());

        // Shift turns the wheel sideways in Chromium; it still steps, by ten.
        engine.Execute("fire('wheel', { target: stepper(0, 0), deltaY: 0, deltaX: -100, shiftKey: true })");
        engine.Execute("runTimers()");

        Assert.EndsWith("|SmallTurbine|Materials|20004:15,20104:3", engine.Evaluate("sets().join('\\n')").AsString());
    }

    [Fact]
    public void Typing_an_amount_in_the_picker_leaves_its_button_in_place_for_the_click_that_follows()
    {
        // The box's change fires on the mousedown of the button click; redrawing the panel then would swap the button out.
        var engine = Open(Materials("20004:5,20104:3"));
        const string package = "el('DIV', ['it'], { id: '64001' }, { '.pk-host': {}, '.it': { dataset: { id: '64001' } } })";
        const string box = "el('INPUT', [], { input: 'pkCount' }, {}, '5')";

        engine.Execute("fire('click', { target: act('pick') })");
        engine.Execute("fire('click', { target: " + package + ", detail: 1 })");

        var drawnBefore = engine.Evaluate("drawn.length").AsNumber();

        engine.Execute("fire('change', { target: " + box + " })");

        Assert.Equal(drawnBefore, engine.Evaluate("drawn.length").AsNumber());

        engine.Execute("fire('click', { target: el('DIV', ['go'], { act: 'pkGo' }, { '.pk-host': {}, '[data-act]': { dataset: { act: 'pkGo' } } }) })");

        Assert.EndsWith("|SmallTurbine|Materials|20004:5,20104:3,64001:5", engine.Evaluate("sets().join('\\n')").AsString());
    }

    /// <summary>Opens the screen on one mod with these settings, the item table sent first; returns the drawn settings.</summary>
    private static string Draw(params Setting[] settings) => DrawWith(System.Array.Empty<string>(), settings);

    private static string DrawWith(string[] before, params Setting[] settings) => Pane(OpenWith(before, settings));

    private static string Pane(Engine engine)
    {
        var pane = engine.Evaluate("drawn.filter(function (html) { return html.indexOf('data-row=\"') >= 0; }).pop() || ''").AsString();

        Assert.NotEqual("", pane);

        return pane;
    }

    private static Engine Open(params Setting[] settings) => OpenWith(System.Array.Empty<string>(), settings);

    /// <summary>Opens the screen with these scripts run first, such as the entries of a List setting.</summary>
    private static Engine OpenWith(string[] before, params Setting[] settings)
    {
        var mod = new ModEntry("g", "Mod", "1.0", "Mod.dll", new ModState.Loaded(settings), ModInfo.None, new ModSwitch.Allowed(true, System.Array.Empty<string>()), new UpdateState.NotChecked());
        var engine = new Engine();

        engine.Execute(Resource("ModManager.Tests.PageFake.js"));
        engine.Execute(StandIns);
        engine.Execute(Resource("ModManager.page.js"));
        engine.Execute(PageScript.Items(Items, new[] { new CategoryName(9, new Description("Materials", "材料")) }));

        foreach (var script in before) engine.Execute(script);

        engine.Execute(PageScript.Open(new ModsView(new[] { mod }, System.Array.Empty<string>(), false, "F10")));

        return engine;
    }

    /// <summary>The markup of the one item list row whose label is <paramref name="key"/>.</summary>
    private static string Field(string pane, string key)
    {
        var rows = new List<string>(pane.Split("<div class=\"row wide"));
        var row = rows.Find(part => part.Contains("<div class=\"lab\">" + key));

        Assert.True(row != null, key + " is not drawn as an item list");

        return row!;
    }

    private static string Resource(string name)
    {
        using var stream = typeof(ItemFieldPageTests).Assembly.GetManifestResourceStream(name) ?? throw new System.InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new System.IO.StreamReader(stream);

        return reader.ReadToEnd();
    }
}
