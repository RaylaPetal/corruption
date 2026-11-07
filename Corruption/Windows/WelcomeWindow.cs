using System;
using System.Collections.Generic;
using System.Numerics;
using Corruption.Bridges;
using Corruption.Core;
using Corruption.Ipc;
using Corruption.Scene;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Corruption.Windows;

/// The one-time setup, a step at a time: the locked profile, body and pregnancy, Cummy and safety, and how it shows.
/// Opens by itself for a character that hasn't finished it, and applies everything at once on Finish.
public sealed class WelcomeWindow : Window
{
    private enum Step { Welcome, Profile, Body, Safety, Display, Done }

    private static readonly string[] DifficultyNames = Enum.GetNames<Difficulty>();
    private static readonly string[] DecayNames = Enum.GetNames<DecayMode>();
    private static readonly string[] ClimaxNames = ["Automatic at 100", "By hand only"];
    private static readonly string[] AnatomyNames = ["Not set", "Penis", "Vagina", "Both"];

    private readonly Settings settings;
    private readonly SceneService scene;
    private readonly Balance balance;
    private readonly CummyBridge cummy;
    private readonly BridgeHub bridges;
    private readonly Action openMainWindow;

    private SceneEngine? loadedFor;
    private bool laterThisSession;
    private List<Step> steps = [];
    private int index;
    private string? problem;

    // Drafts, applied on Finish.
    private int difficulty = (int)Difficulty.Normal;
    private int decay = (int)DecayMode.Normal;
    private int climax;
    private float gain = 1f;
    private int anatomy;
    private bool pregnancy;
    private bool carry = true;
    private bool breed;
    private int cycleDay = PregnancyBook.DefaultStartDay;
    private float chance = 1f;
    private bool layered = true;
    private bool hud = true;
    private bool hudInScenes = true;
    private bool plateOwn = true;
    private bool platePartners = true;
    private bool moodles;

    private static float Scale => ImGuiHelpers.GlobalScale;

    public WelcomeWindow(Settings settings, SceneService scene, Balance balance, CummyBridge cummy, BridgeHub bridges, Action openMainWindow)
        : base("Welcome to Corruption###CorruptionWelcome", ImGuiWindowFlags.NoCollapse)
    {
        this.settings = settings;
        this.scene = scene;
        this.balance = balance;
        this.cummy = cummy;
        this.bridges = bridges;
        this.openMainWindow = openMainWindow;
        Size = new Vector2(560, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(480, 440), MaximumSize = new Vector2(1000, 1200) };
    }

    /// Runs every frame, open or not: opens for a character that still needs it, unless put off this session.
    public override void PreOpenCheck()
    {
        if (scene.Engine is not { } engine)
        {
            loadedFor = null;
            return;
        }
        if (engine != loadedFor)
        {
            loadedFor = engine;
            laterThisSession = false;
            Load(engine);
        }
        if (!IsOpen && !laterThisSession && Welcome.IsNeeded(engine.Save)) IsOpen = true;
    }

    /// From the main window: open it now, even if put off.
    public void Show()
    {
        if (scene.Engine is { } engine) Load(engine);
        laterThisSession = false;
        IsOpen = true;
    }

    public override void OnClose()
    {
        if (scene.Engine is { } engine && Welcome.IsNeeded(engine.Save)) laterThisSession = true;
    }

    private void Load(SceneEngine engine)
    {
        var save = engine.Save;
        steps = save.Setup is null
            ? [Step.Welcome, Step.Profile, Step.Body, Step.Safety, Step.Display, Step.Done]
            : [Step.Welcome, Step.Body, Step.Safety, Step.Display, Step.Done];
        index = 0;
        problem = null;
        anatomy = (int)save.Anatomy;
        var p = save.Pregnancy;
        pregnancy = p.Enabled;
        carry = !p.Enabled || p.CanCarry;
        breed = p.Enabled && p.CanBreed;
        chance = (float)p.ChanceFactor;
        layered = settings.LayerOnPartnerClimax;
        hud = settings.ShowHud;
        hudInScenes = settings.HudDuringScenes;
        plateOwn = settings.PlateShowOwn;
        platePartners = settings.PlateShowPartners;
        moodles = settings.MoodlesEnabled;
    }

    public override void Draw()
    {
        if (scene.Engine is not { } engine)
        {
            Theme.MutedText("Log in to a character to set up Corruption.");
            return;
        }

        DrawProgress();
        var footer = ImGui.GetFrameHeightWithSpacing() + 12f * Scale;
        ImGui.BeginChild("##welcomeStep", new Vector2(0, -footer));
        switch (steps[index])
        {
            case Step.Welcome: DrawWelcome(engine); break;
            case Step.Profile: DrawProfile(); break;
            case Step.Body: DrawBody(engine); break;
            case Step.Safety: DrawSafety(); break;
            case Step.Display: DrawDisplay(); break;
            default: DrawDone(engine); break;
        }
        ImGui.EndChild();
        DrawFooter(engine);
    }

    /// One dot per step, the current one filled.
    private void DrawProgress()
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var radius = 5f * Scale;
        var gap = 20f * Scale;
        for (var i = 0; i < steps.Count; i++)
        {
            var at = start + new Vector2(radius + i * gap, radius + 2 * Scale);
            var color = i < index ? Theme.Violet : i == index ? Theme.Pink : Theme.Faint;
            dl.AddCircleFilled(at, i == index ? radius + 1.5f * Scale : radius, ImGui.GetColorU32(color), 16);
        }
        ImGui.Dummy(new Vector2(steps.Count * gap, radius * 2 + 8 * Scale));
    }

    private void DrawFooter(SceneEngine engine)
    {
        ImGuiHelpers.ScaledDummy(4f);
        if (problem is { } text) ImGui.TextColored(Theme.Warning, text);
        if (index > 0 && ImGui.Button("Back")) Move(-1);
        if (index == 0)
        {
            if (ImGui.Button("Later")) IsOpen = false;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Closes this until your next login. Scenes can't earn until your profile is set up.");
        }

        var last = index == steps.Count - 1;
        var label = last ? "Finish" : "Next";
        Theme.AlignRight(Theme.IconButtonWidth(last ? FontAwesomeIcon.Check : FontAwesomeIcon.ArrowRight, label));
        if (Theme.PrimaryButton(last ? FontAwesomeIcon.Check : FontAwesomeIcon.ArrowRight, label))
        {
            if (!last) Move(1);
            else Finish(engine);
        }
    }

    private void Move(int by)
    {
        problem = null;
        if (by > 0 && steps[index] == Step.Body && pregnancy && !carry && !breed)
        {
            problem = "Choose at least one pregnancy role, or leave pregnancy off.";
            return;
        }
        index = Math.Clamp(index + by, 0, steps.Count - 1);
    }

    private void Finish(SceneEngine engine)
    {
        var setup = engine.Save.Setup is null
            ? new ProfileSetup
            {
                Difficulty = (Difficulty)difficulty,
                Decay = (DecayMode)decay,
                AutoClimax = climax == 0,
                ArousalGain = Math.Round(gain, 2),
            }
            : null;
        var choices = new WelcomeChoices(setup, (Anatomy)anatomy, new PregnancyChoice(pregnancy, carry, breed, cycleDay, chance));
        if (engine.CompleteWelcome(choices, DateTime.UtcNow) is { } refusal)
        {
            problem = refusal;
            return;
        }

        settings.LayerOnPartnerClimax = layered;
        settings.ShowHud = hud;
        settings.HudDuringScenes = hudInScenes;
        settings.PlateShowOwn = plateOwn;
        settings.PlateShowPartners = platePartners;
        settings.Save();
        bridges.SetMoodlesEnabled(moodles);
        IsOpen = false;
        openMainWindow();
    }

    // ── Steps ────────────────────────────────────────────────────────────────

    private void DrawWelcome(SceneEngine engine)
    {
        Theme.CardBox("hello", () =>
        {
            ImGui.SetWindowFontScale(1.3f);
            ImGui.TextColored(Theme.PinkSoft, engine.Save.Setup is null ? "Welcome to Corruption" : "A few new things to set up");
            ImGui.SetWindowFontScale(1f);
            Theme.MutedWrapped(engine.Save.Setup is null
                ? "Two players with Corruption earn XP and arousal while each plays an animation from mods they picked, standing close together. Levels take you through corruption tiers, climaxes layer your partner through Cummy, and everything runs on your own PC."
                : "Your profile is set up. Since then Corruption has gained anatomy, pregnancy and a few display choices. This takes a minute.");
        }, stripe: Theme.Pink);

        Theme.CardBox("consent", () =>
        {
            Theme.Heading(FontAwesomeIcon.HandPaper, "Before you start");
            Bullet("18+ only.");
            Bullet("Being in an animation next to someone with Corruption is the opt-in. There's no request to accept.");
            Bullet($"The safeword ({SafewordName()}) or /corr pause stops your XP, arousal, toys and climaxes at once.");
            Bullet("Hide me, in the Nearby tab or /corr hide, stops other players seeing that you have Corruption.");
        });
    }

    private void DrawProfile()
    {
        Theme.CardBox("lockNote", () =>
        {
            Theme.Icon(FontAwesomeIcon.Lock, Theme.Warning);
            ImGui.SameLine();
            ImGui.TextColored(Theme.Warning, "These are locked once you finish, until you reset your profile.");
        });
        Theme.CardBox("difficulty", () =>
        {
            Theme.Heading(FontAwesomeIcon.Fire, "Difficulty");
            Theme.Choice("difficulty", DifficultyNames, ref difficulty);
            Theme.MutedText(ProfileSetup.Describe((Difficulty)difficulty));
        });
        Theme.CardBox("decay", () =>
        {
            Theme.Heading(FontAwesomeIcon.HourglassHalf, "Decay");
            Theme.Choice("decay", DecayNames, ref decay);
            Theme.MutedText(ProfileSetup.Describe((DecayMode)decay));
            Theme.MutedText("Fading never drops you below the start of your current tier.");
        });
        Theme.CardBox("climax", () =>
        {
            Theme.Heading(FontAwesomeIcon.Heart, "Climax");
            Theme.Choice("climax", ClimaxNames, ref climax);
            Theme.MutedText(climax == 0 ? "You climax when arousal reaches 100, or earlier with /corr climax." : "Only /corr climax does it, from 70 arousal. Arousal holds at 100 until you do.");
        });
        Theme.CardBox("gain", () =>
        {
            Theme.Heading(FontAwesomeIcon.Bolt, "Arousal gain");
            ImGui.SetNextItemWidth(220 * Scale);
            ImGui.SliderFloat("##gain", ref gain, (float)ProfileSetup.MinArousalGain, (float)ProfileSetup.MaxArousalGain, "%.2fx");
            Theme.MutedText($"About {100 / (balance.ArousalPerTick * gain) * balance.TickSeconds / 60:0.#} minutes to climax in a quiet scene.");
        });
    }

    private void DrawBody(SceneEngine engine)
    {
        Theme.CardBox("anatomy", () =>
        {
            Theme.Heading(FontAwesomeIcon.User, "Your anatomy");
            Theme.Choice("anatomy", AnatomyNames, ref anatomy);
            Theme.MutedWrapped("When you climax, partners' clients aim from here: your penis tip or your vagina. Both shoots from the penis. Not set guesses from your skeleton's bones.");
        });

        Theme.CardBox("pregnancy", () =>
        {
            Theme.Heading(FontAwesomeIcon.Seedling, "Pregnancy");
            ImGui.Checkbox("Turn on pregnancy", ref pregnancy);
            Theme.MutedWrapped("A 7-day cycle, conception from a breeder's cum shot at the groin, a 14-day pregnancy with cravings and a baby that grows on your profile, then a birth. Turn it on or off any time in Settings.");
            if (!pregnancy) return;

            var pregnant = PregnancyBook.IsPregnant(engine.Save.Pregnancy);
            if (pregnant) ImGui.BeginDisabled();
            ImGui.Checkbox("I can carry", ref carry);
            ImGui.SameLine();
            ImGui.Checkbox("I can breed", ref breed);
            if (pregnant)
            {
                ImGui.EndDisabled();
                Theme.MutedText("You're pregnant, so your roles stay as they are until the birth.");
            }
            if (carry && !engine.Save.Pregnancy.Enabled)
            {
                ImGui.SetNextItemWidth(160 * Scale);
                ImGui.SliderInt("Today's cycle day", ref cycleDay, 1, CycleClock.Length);
                ImGui.SameLine();
                Theme.MutedText(CycleClock.PhaseOf(cycleDay).Phase.ToString());
            }
            if (carry)
            {
                ImGui.SetNextItemWidth(200 * Scale);
                ImGui.SliderFloat("Conception chance", ref chance, (float)PregnancyBook.MinChanceFactor, (float)PregnancyBook.MaxChanceFactor, "×%.2f",
                    ImGuiSliderFlags.Logarithmic);
                Theme.MutedText($"×1 is normal: up to {CycleClock.PhaseOf(5).ConceptionChance:P0} on ovulation day.");
            }
        });
    }

    private void DrawSafety()
    {
        Theme.CardBox("cummy", () =>
        {
            Theme.Heading(FontAwesomeIcon.Tint, "Cummy");
            ImGui.Checkbox("Get layered when a partner climaxes", ref layered);
            Theme.MutedWrapped("Your own Cummy layers the part of you nearest your partner's genitals when their climax counts. No pairing is needed, and the parts you block in Cummy are skipped. Pregnancy needs this on.");
            ImGui.TextColored(cummy.CanLayerSelf ? Theme.Good : Theme.Warning, $"Cummy: {cummy.Status}");
        });
        Theme.CardBox("safeword", () =>
        {
            Theme.Heading(FontAwesomeIcon.HandPaper, "Safeword");
            ImGui.TextUnformatted($"{SafewordName()}, or /corr pause");
            Theme.MutedWrapped("Stops scene XP, arousal, climaxes and toys at once, and shows nearby players you've paused. /corr resume continues. Change the key in Settings.");
        });
        Theme.CardBox("hide", () =>
        {
            Theme.Heading(FontAwesomeIcon.EyeSlash, "Privacy");
            Theme.MutedWrapped("Only players with Corruption who are synced with you through Lightless or similar see your level, arousal and pregnancy. Hide me, in Nearby, hides you from them too, and scenes can't earn while you're hidden.");
        });
    }

    private void DrawDisplay()
    {
        Theme.CardBox("display", () =>
        {
            Theme.Heading(FontAwesomeIcon.Eye, "How it shows");
            ImGui.Checkbox("Show the HUD", ref hud);
            ImGui.SameLine();
            ImGui.Checkbox("Always during scenes", ref hudInScenes);
            Theme.MutedText("A small window with your level, arousal and scene. /corr hud toggles it.");
            ImGui.Checkbox("Text above my nameplate", ref plateOwn);
            ImGui.SameLine();
            ImGui.Checkbox("Above other players' nameplates", ref platePartners);
            Theme.MutedText("Your tier, level and arousal. Only synced Corruption players see it.");
            ImGui.Checkbox("Show my state as Moodles statuses", ref moodles);
            Theme.MutedWrapped("Aroused, Marked, Pregnant and more, visible to synced players with Moodles. Needs Moodles' \"Allow other plugins\" setting.");
        });
        Theme.MutedWrapped("Tier visuals, toys through Intiface and the rest are in Settings, under Bridges.");
    }

    private void DrawDone(SceneEngine engine)
    {
        Theme.CardBox("summary", () =>
        {
            Theme.Heading(FontAwesomeIcon.Check, "Ready");
            if (engine.Save.Setup is null)
            {
                Line("Difficulty", ((Difficulty)difficulty).ToString());
                Line("Decay", ((DecayMode)decay).ToString());
                Line("Climax", ClimaxNames[climax]);
                Line("Arousal gain", $"{gain:0.##}x");
            }
            Line("Anatomy", AnatomyNames[anatomy]);
            Line("Pregnancy", !pregnancy ? "Off" : carry && breed ? "Can carry and breed" : carry ? "Can carry" : "Can breed");
            Line("Layered by partners", layered ? "On" : "Off");
            Line("HUD and nameplate", $"{(hud ? "HUD" : "No HUD")}, {(plateOwn ? "your text on" : "your text off")}");
            Line("Moodles", moodles ? "On" : "Off");
        }, stripe: Theme.Good);
        Theme.MutedWrapped("Next: tick the folders and mods that count in the Acts tab, then play an animation next to someone with Corruption.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Bullet(string text)
    {
        Theme.Icon(FontAwesomeIcon.Heart, Theme.PinkSoft);
        ImGui.SameLine();
        ImGui.PushTextWrapPos(0);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    private static void Line(string label, string value)
    {
        Theme.MutedText(label);
        ImGui.SameLine(170 * Scale);
        ImGui.TextUnformatted(value);
    }

    private string SafewordName()
    {
        var parts = new List<string>();
        if (settings.SafewordCtrl) parts.Add("Ctrl");
        if (settings.SafewordShift) parts.Add("Shift");
        if (settings.SafewordAlt) parts.Add("Alt");
        parts.Add(settings.SafewordKey.ToString());
        return string.Join("+", parts);
    }
}
