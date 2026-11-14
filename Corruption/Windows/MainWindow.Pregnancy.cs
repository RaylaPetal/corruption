using System;
using System.Numerics;
using Corruption.Core;
using Corruption.Scene;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Corruption.Windows;

/// The cycle and pregnancy on Profile, and the Pregnancy section in Settings.
public sealed partial class MainWindow
{
    private bool draftCarry = true;
    private bool draftBreed;
    private int draftCycleDay = PregnancyBook.DefaultStartDay;
    private bool confirmingPregnancyOff;
    private int bellyKind;
    private string bellyFilter = "";

    private static readonly string[] TrimesterNames = ["", "First", "Second", "Third"];

    private static Vector4 PhaseColor(CyclePhase phase) => phase switch
    {
        CyclePhase.Period => Theme.Danger,
        CyclePhase.Fertile => Theme.Pink,
        CyclePhase.Ovulation => Theme.Warning,
        _ => Theme.VioletSoft,
    };

    // ── Profile ──────────────────────────────────────────────────────────────

    private void DrawPregnancyCard(SceneEngine engine)
    {
        var save = engine.Save;
        var p = save.Pregnancy;
        if (!p.Enabled) return;
        var now = DateTime.UtcNow;

        // A secret pregnancy shows nothing here; the header cycle carries on as if it weren't there.
        if (PregnancyBook.IsConfirmed(p)) DrawPregnant(save, now);
        else if (PregnancyBook.IsRecovering(p, now))
            Theme.CardBox("recovering", () =>
            {
                Theme.Heading(FontAwesomeIcon.Heart, "Recovering");
                Theme.MutedText($"Your cycle starts again in {ProfileTimers.Describe(p.RecoveryUntilUtc - now)}. Arousal builds a little slower until then.");
            }, stripe: Theme.PinkSoft);
        else if (!PregnancyBook.ShowsCycle(p, now))
            Theme.CardBox("breeder", () =>
            {
                Theme.Heading(FontAwesomeIcon.Seedling, "Pregnancy");
                Theme.MutedWrapped(ItemBook.Virile(save, now)
                    ? "You can breed, and your Virility Tonic is working: partners who can carry are more likely to conceive from your climaxes."
                    : "You can breed: partners who can carry may conceive when your climax counts in a scene with them.");
            }, stripe: Theme.Violet);
    }

    private void DrawPregnant(CharacterSave save, DateTime now)
    {
        var p = save.Pregnancy;
        var days = PregnancyBook.DaysPregnant(p, now);
        var stage = PregnancyBook.StageOf(p, now);
        var left = (p.DueUtc!.Value - now).TotalDays;

        Theme.CardBox("pregnant", () =>
        {
            BabyIllustration.Draw(150 * Scale, days / PregnancyBook.DaysLong);
            ImGui.SameLine(0, 14 * Scale);
            ImGui.BeginGroup();
            ImGui.SetWindowFontScale(1.2f);
            ImGui.TextColored(Theme.PinkSoft, $"{TrimesterNames[(int)stage]} trimester");
            ImGui.SetWindowFontScale(1f);
            Theme.Pill(left >= 1 ? $"{Math.Ceiling(left):0} days to go" : $"Due in {ProfileTimers.Describe(p.DueUtc.Value - now)}", Theme.Pink);
            Theme.MutedText($"About the size of {BabyIllustration.SizeOn(days)}");
            if (p.FatherKey is { } father) Theme.MutedText($"Father: {NameOf(father)}");

            ImGuiHelpers.ScaledDummy(4f);
            if (p.Craving is { } craving)
            {
                Theme.Icon(p.CravingMet ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Cookie, p.CravingMet ? Theme.Good : Theme.Warning);
                ImGui.SameLine();
                ImGui.TextUnformatted(p.CravingMet ? $"Craving satisfied: {CravingBook.Describe(craving)}" : $"Craving: {CravingBook.Describe(craving)}");
            }
            if (p.Unsatisfied) ImGui.TextColored(Theme.Warning, "Unsatisfied: arousal builds slower until you meet a craving.");
            Theme.MutedText(stage switch
            {
                PregnancyStage.First => "Arousal builds a little slower. Scenes pay ×1.1 XP.",
                PregnancyStage.Second => "Desire: arousal builds ×1.25 as fast. Scenes pay ×1.1 XP.",
                _ => "Arousal builds ×1.1 as fast. Scenes pay ×1.1 XP.",
            });
            ImGui.EndGroup();
        }, stripe: Theme.Pink);
    }

    /// The cycle in the header, under arousal, in its own section: the week as seven dots, today's phase and chance.
    private void DrawHeaderCycle(CharacterSave save, float width)
    {
        var p = save.Pregnancy;
        var now = DateTime.UtcNow;
        if (!PregnancyBook.ShowsCycle(p, now)) return;

        var day = CycleClock.DayAt(p.CycleStartUtc, now);
        var phase = CycleClock.PhaseOf(day);
        var chance = PregnancyBook.ShownConceptionChance(save, now, false, 0, false);

        ImGuiHelpers.ScaledDummy(2f);
        var y = ImGui.GetCursorScreenPos().Y;
        var x = ImGui.GetCursorScreenPos().X;
        ImGui.GetWindowDrawList().AddLine(new Vector2(x, y), new Vector2(x + width, y), ImGui.GetColorU32(Theme.Alpha(Theme.Violet, 0.35f)), 1f);
        ImGuiHelpers.ScaledDummy(4f);

        DrawCycleStrip(day);
        ImGui.SameLine(0, 10f * Scale);
        ImGui.BeginGroup();
        ImGui.TextColored(PhaseColor(phase.Phase), $"Day {day} · {phase.Phase}");
        Theme.MutedText(ItemBook.PreventsConception(save, now)
            ? "Contraceptive working"
            : $"{chance:P0} to conceive · next phase in {ProfileTimers.Describe(CycleClock.NextPhaseAt(p.CycleStartUtc, now) - now)}");
        ImGui.EndGroup();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Arousal ×{phase.ArousalFactor:0.##} today. Only a breeder's creampie in your pussy can make you pregnant.");
    }

    /// Seven dots, one per day, coloured by phase, with today ringed.
    private static void DrawCycleStrip(int today)
    {
        var dl = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var radius = 7f * Scale;
        var gap = 22f * Scale;
        for (var day = 1; day <= CycleClock.Length; day++)
        {
            var at = start + new Vector2(radius + (day - 1) * gap, radius + 2 * Scale);
            var color = PhaseColor(CycleClock.PhaseOf(day).Phase);
            dl.AddCircleFilled(at, radius, ImGui.GetColorU32(day == today ? color : Theme.Alpha(color, 0.35f)), 20);
            if (day == today) dl.AddCircle(at, radius + 3 * Scale, ImGui.GetColorU32(Theme.Text), 20, 1.5f);
        }
        ImGui.Dummy(new Vector2(CycleClock.Length * gap, radius * 2 + 6 * Scale));
    }

#if DEBUG
    // ── Debug ────────────────────────────────────────────────────────────────

    private int debugCycleDay = 4;

    /// Pregnancy runs on real days, so this moves it along. Everything here changes the real save.
    private void DrawPregnancyDebug(SceneEngine engine)
    {
        Theme.Heading(FontAwesomeIcon.Seedling, "Pregnancy");
        var save = engine.Save;
        var p = save.Pregnancy;
        var now = DateTime.UtcNow;
        if (!p.Enabled)
        {
            Theme.MutedText("Off. Turn it on in Settings, or:");
            ImGui.SameLine();
            if (ImGui.SmallButton("Carrier and breeder now")) engine.EnablePregnancy(true, true, 4, now);
            return;
        }

        var stage = PregnancyBook.StageOf(p, now);
        Theme.MutedText($"Roles: {(p.CanCarry ? "carry " : "")}{(p.CanBreed ? "breed" : "")}  Stage: {stage}  Births: {p.Births}  Conceptions: {p.Conceptions}  Fathered: {p.Fathered}");
        if (PregnancyBook.IsSecret(p))
        {
            Theme.MutedText($"Secret pregnancy (from {p.ConceivedSource ?? "?"}): nothing shows until a test or day {PregnancyBook.RevealDay:0}.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Confirm now")) PregnancyBook.Confirm(p, now);
        }
        if (PregnancyBook.HasCycle(p, now))
        {
            var day = CycleClock.DayAt(p.CycleStartUtc, now);
            var chance = PregnancyBook.ConceptionChance(save, now, false, 0, false);
            Theme.MutedText($"Cycle day {day} ({CycleClock.PhaseOf(day).Phase}), chance now {chance:P0} with slider ×{p.ChanceFactor:0.##}, arousal ×{PregnancyBook.ArousalFactor(p, now):0.##}");
            ImGui.SetNextItemWidth(120 * Scale);
            ImGui.SliderInt("##debugCycleDay", ref debugCycleDay, 1, CycleClock.Length, "Day %d");
            ImGui.SameLine();
            if (ImGui.Button("Set cycle day")) p.CycleStartUtc = now.Date.AddDays(-(debugCycleDay - 1));
            ImGui.SameLine();
            if (ImGui.Button("Conceive now")) PregnancyBook.Conceive(p, now, $"{SceneService.DebugName}@{SceneService.DebugWorld}");
        }
        if (PregnancyBook.IsPregnant(p))
        {
            var days = PregnancyBook.DaysPregnant(p, now);
            Theme.MutedText($"Day {days:0.0} of {PregnancyBook.DaysLong:0}, due {p.DueUtc:g} UTC, father {p.FatherKey}");
            Theme.MutedText($"Craving: {p.Craving?.ToString() ?? "none"} (day {p.CravingDay}), met {p.CravingMet}, unsatisfied {p.Unsatisfied}, scene seconds {p.CravingSeconds:0}");
            if (ImGui.Button("-1 day")) Shift(p, 1);
            ImGui.SameLine();
            if (ImGui.Button("+1 day")) Shift(p, -1);
            ImGui.SameLine();
            if (ImGui.Button("Next trimester")) Shift(p, -(stage == PregnancyStage.First ? 5 - days : stage == PregnancyStage.Second ? 10 - days : 0));
            ImGui.SameLine();
            if (ImGui.Button("Give birth now")) p.DueUtc = now;
            if (ImGui.Button("New craving")) p.CravingDay = "";
            ImGui.SameLine();
            if (ImGui.Button("Make unsatisfied")) p.Unsatisfied = true;
            ImGui.SameLine();
            if (ImGui.Button("End without birth")) PregnancyBook.MorningAfter(p, p.ConceivedUtc!.Value);

            ImGuiHelpers.ScaledDummy(2f);
            for (var preview = 0; preview <= 4; preview++)
            {
                if (preview > 0) ImGui.SameLine();
                BabyIllustration.Draw(70 * Scale, preview / 4.0);
            }
            Theme.MutedText("The baby at days 0, 3.5, 7, 10.5 and 14.");
        }
        if (PregnancyBook.IsRecovering(p, now))
        {
            Theme.MutedText($"Recovering until {p.RecoveryUntilUtc:g} UTC");
            ImGui.SameLine();
            if (ImGui.SmallButton("End recovery")) p.RecoveryUntilUtc = now;
        }
        if (ImGui.Button("Save pregnancy changes")) scene.SaveProgress();
    }

    /// Moves the pregnancy as if <paramref name="days"/> fewer had passed; negative moves it forward.
    private static void Shift(PregnancyState p, double days)
    {
        p.ConceivedUtc = p.ConceivedUtc!.Value.AddDays(days);
        p.DueUtc = p.DueUtc!.Value.AddDays(days);
    }
#endif

    // ── Settings ─────────────────────────────────────────────────────────────

    /// The whole-pregnancy belly, which goes on once a pregnancy is confirmed whether or not tier visuals are on.
    private void DrawBellyPicker(PregnancyState p)
    {
        ImGuiHelpers.ScaledDummy(2f);
        ImGui.TextColored(Theme.Pink, "Pregnancy belly");
        var slot = new VisualSlot(p.Visuals, PregnancyVisualPlan.BellyKey, "Belly", Theme.Pink);
        p.Visuals.TryGetValue(slot.Key, out var choice);
        ImGui.SameLine();
        Theme.MutedWrapped(choice is null ? "Nothing" : $"{(choice.Kind == VisualKind.Glamourer ? "Design" : "Mod")}: {choice.Name}{OptionsText(choice)}");
        Theme.MutedWrapped("On for the whole pregnancy once it's confirmed, and off at the birth. While tier visuals are on, a trimester's own visual there goes on instead during that trimester. Synced players see it like any other change to your look.");

        ImGui.PushID("belly");
        if (Theme.Choice("bellyKind", VisualKindNames, ref bellyKind)) bellyFilter = "";
        switch (bellyKind)
        {
            case 0:
                if (choice is not null && ImGui.Button("Clear the belly")) SetVisual(slot, null);
                break;
            case 1:
                DrawDesignPicker(slot, ref bellyFilter);
                break;
            default:
                DrawModPicker(slot, ref bellyFilter);
                break;
        }
        ImGui.PopID();
    }

    private void DrawPregnancySettings(SceneEngine engine)
    {
        var p = engine.Save.Pregnancy;
        Theme.CardBox("pregnancy", () =>
        {
            Theme.Heading(FontAwesomeIcon.Seedling, "Pregnancy");
            if (!p.Enabled)
            {
                Theme.MutedWrapped("Off. Turn it on for a 7-day cycle, conception in scenes, a 14-day pregnancy with cravings, and pregnancy items in the Shop. Nothing about it shows to others unless you choose Moodles, except to synced Corruption players.");
                ImGui.Checkbox("I can carry", ref draftCarry);
                ImGui.SameLine();
                ImGui.Checkbox("I can breed", ref draftBreed);
                if (draftCarry)
                {
                    ImGui.SetNextItemWidth(160 * Scale);
                    ImGui.SliderInt("Today's cycle day", ref draftCycleDay, 1, CycleClock.Length);
                    ImGui.SameLine();
                    Theme.MutedWrapped(CycleClock.PhaseOf(draftCycleDay).Phase.ToString());
                }
                if (Theme.PrimaryButton(FontAwesomeIcon.Check, "Turn on")
                    && engine.EnablePregnancy(draftCarry, draftBreed, draftCycleDay, DateTime.UtcNow) is { } refusal)
                    Notifier.Info(refusal);
                return;
            }

            Theme.Pill(p.CanCarry && p.CanBreed ? "Can carry and breed" : p.CanCarry ? "Can carry" : "Can breed", Theme.Pink);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.MutedWrapped($"{p.Births} {(p.Births == 1 ? "birth" : "births")} so far");
            if (p.CanCarry)
            {
                var factor = (float)p.ChanceFactor;
                ImGui.SetNextItemWidth(200 * Scale);
                if (ImGui.SliderFloat("Conception chance", ref factor, (float)PregnancyBook.MinChanceFactor, (float)PregnancyBook.MaxChanceFactor, "×%.2f",
                        ImGuiSliderFlags.Logarithmic))
                {
                    p.ChanceFactor = Math.Round(factor, 2);
                    scene.SaveProgress();
                }
                if (ImGui.IsItemDeactivatedAfterEdit()) scene.SaveProgress();
                Theme.MutedWrapped($"×1 is normal: up to {CycleClock.PhaseOf(5).ConceptionChance:P0} on ovulation day. Only a partner's creampie in your pussy can make you pregnant, one roll each time (from a scene climax, or sent straight from Cummy's menu, at most once a minute each), so it needs Cummy's plugin API 6, \"Get layered when a partner climaxes\" on, and \"Creampie: pussy\" not blocked in Cummy. A shot outside, even on your groin, or a creampie in your ass never can.");
                if (!cummy.HasV6)
                    ImGui.TextColored(Theme.Warning, cummy.IsAvailable
                        ? "Your Cummy is too old for creampies, so you can't conceive until it's updated."
                        : "Cummy isn't connected, so you can't conceive until it's installed and updated.");
                DrawBellyPicker(p);
            }
            // A secret pregnancy can't lock the roles, or the lock would give it away.
            if (!PregnancyBook.IsConfirmed(p))
            {
                var carry = p.CanCarry;
                var breed = p.CanBreed;
                if (ImGui.Checkbox("I can carry##now", ref carry) | ImGui.Checkbox("I can breed##now", ref breed))
                {
                    if (engine.EnablePregnancy(carry, breed, carry && !p.CanCarry ? PregnancyBook.DefaultStartDay
                            : CycleClock.DayAt(p.CycleStartUtc, DateTime.UtcNow), DateTime.UtcNow) is { } refusal)
                        Notifier.Info(refusal);
                }
            }

            if (!confirmingPregnancyOff)
            {
                if (ImGui.Button("Turn pregnancy off")) confirmingPregnancyOff = true;
                return;
            }
            ImGui.TextColored(Theme.Warning, PregnancyBook.IsConfirmed(p)
                ? "This ends your pregnancy with no birth, and stops your cycle."
                : "This stops your cycle and cravings.");
            if (Theme.DangerButton(FontAwesomeIcon.Ban, "Yes, turn it off"))
            {
                engine.DisablePregnancy();
                confirmingPregnancyOff = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel##pregnancyOff")) confirmingPregnancyOff = false;
        }, stripe: p.Enabled ? Theme.Pink : null);
    }
}
