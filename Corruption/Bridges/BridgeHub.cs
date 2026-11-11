using System;
using Corruption.Core;
using Corruption.Mods;

namespace Corruption.Bridges;

/// Drives tier visuals, toys and Moodles from the engine. Each is off until the player turns it on, fails closed, and
/// never affects scenes. Framework thread.
public sealed class BridgeHub : IDisposable
{
    private readonly Settings settings;
    private readonly ToyController toyController = new();
    private bool leftoversCleared;

    public GlamourerIpc Glamourer { get; }
    public TierVisualService Visuals { get; }
    public IntifaceService Toys { get; } = new();
    public MoodlesService Moodles { get; } = new();

    public BridgeHub(Settings settings, PenumbraIpc penumbra)
    {
        this.settings = settings;
        Glamourer = new GlamourerIpc();
        Visuals = new TierVisualService(Glamourer, penumbra);
        if (settings.ToysEnabled) Toys.Connect(settings.IntifaceAddress);
    }

    public void Dispose()
    {
        EndSession();
        Toys.Dispose();
        Visuals.Dispose();
        Glamourer.Dispose();
    }

    /// The current toy intensity, for Settings.
    public double ToyIntensity => toyController.Current;

    /// Every frame: toys follow the scene closely, and a stop goes out on the frame it's needed.
    public void UpdateToys(DateTime now, SceneEngine engine)
    {
        if (!settings.ToysEnabled || !Toys.IsConnected) return;
        var save = engine.Save;
        var live = engine.IsVerified && !save.Paused && !save.Hidden;
        var climax = save.Paused || save.Hidden ? null : engine.LastOwnClimaxUtc;
        if (toyController.Update(now, live, save.Arousal, settings.ToyMaxIntensity, climax) is { } intensity)
            Toys.VibrateAll(intensity);
    }

    /// Once a second, after the engine's sample.
    public void Update(DateTime now, SceneEngine engine, string selfKey)
    {
        var save = engine.Save;
        Visuals.Update(now,
            settings.TierVisualsEnabled ? TierVisualPlan.Wanted(save, settings.RemoveVisualWhilePaused) : null,
            settings.TierVisualsEnabled ? PregnancyVisualPlan.Wanted(save, now, settings.RemoveVisualWhilePaused) : null);

        if (!settings.MoodlesEnabled) return;
        if (!leftoversCleared && Plugin.ObjectTable.LocalPlayer is not null)
        {
            Moodles.RemoveLeftovers();
            leftoversCleared = true;
        }
        var partner = engine.Status.Partner;
        var p = save.Pregnancy;
        var fertile = PregnancyBook.HasCycle(p, now)
            && CycleClock.PhaseOf(CycleClock.DayAt(p.CycleStartUtc, now)).Phase is CyclePhase.Fertile or CyclePhase.Ovulation;
        var stage = PregnancyBook.StageOf(p, now);
        var pregnant = PregnancyBook.IsPregnant(p);
        var plan = MoodlesPlan.Plan(new MoodlesInput(
            save.Setup is not null && !save.Hidden,
            save.Arousal,
            engine.LayersWorn,
            engine.IsVerified,
            engine.Status.Watchers,
            partner is not null && engine.Bonds.PaysWith(partner.Key),
            Arousal.InRefractory(save, now),
            Progression.IsFading(save, now),
            fertile,
            pregnant ? (int)stage : 0,
            pregnant && p.Craving is not null && !p.CravingMet,
            pregnant && p.Unsatisfied,
            stage == PregnancyStage.Recovering,
            engine.DrippingHoles));
        Moodles.Update(plan, selfKey, save.Arousal, engine.LayersWorn);
    }

    /// The safeword, hiding, or turning toys off: every device stops now, without waiting for a tick.
    public void StopToysNow()
    {
        Toys.StopAll();
        toyController.Stopped();
    }

    public void SetToysEnabled(bool enabled)
    {
        settings.ToysEnabled = enabled;
        settings.Save();
        if (enabled) Toys.Connect(settings.IntifaceAddress);
        else
        {
            StopToysNow();
            Toys.Disconnect();
        }
    }

    public void SetMoodlesEnabled(bool enabled)
    {
        settings.MoodlesEnabled = enabled;
        settings.Save();
        if (!enabled) Moodles.RemoveAll();
    }

    public void SetTierVisualsEnabled(bool enabled)
    {
        settings.TierVisualsEnabled = enabled;
        settings.Save();
        if (!enabled) Visuals.RemoveApplied();
    }

    /// Logout, a character change or unload: take off everything Corruption put on.
    public void EndSession()
    {
        StopToysNow();
        Moodles.RemoveAll();
        Visuals.RemoveApplied();
        leftoversCleared = false;
    }
}
