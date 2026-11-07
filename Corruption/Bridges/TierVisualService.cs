using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;
using Corruption.Mods;

namespace Corruption.Bridges;

/// Puts the tier's visual and, while pregnant, the trimester's on the character, and takes off only what it put on.
/// When either changes, everything Corruption applied comes off and goes on again in order, tier first, so the
/// trimester wins where they overlap. Framework thread.
public sealed class TierVisualService : IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    /// Glamourer events from Corruption's own applies arrive during and just after the call.
    private static readonly TimeSpan OwnApplyWindow = TimeSpan.FromSeconds(2);

    private readonly GlamourerIpc glamourer;
    private readonly PenumbraIpc penumbra;

    private TierVisual? appliedTier;
    private TierVisual? appliedPregnancy;
    private readonly List<(Guid Collection, string Mod)> appliedMods = [];
    private bool appliedDesign;
    private bool superseded;
    private DateTime ownApplyUntil = DateTime.MinValue;
    private DateTime nextTry = DateTime.MinValue;

    public TierVisualService(GlamourerIpc glamourer, PenumbraIpc penumbra)
    {
        this.glamourer = glamourer;
        this.penumbra = penumbra;
        glamourer.LocalPlayerChanged += OnGlamourerChanged;
    }

    public void Dispose()
    {
        glamourer.LocalPlayerChanged -= OnGlamourerChanged;
        RemoveApplied();
    }

    /// What's on the character from Corruption, for the Settings page.
    public TierVisual? Applied => appliedPregnancy ?? appliedTier;

    public string AppliedText => string.Join(" and ", new[] { appliedTier, appliedPregnancy }.OfType<TierVisual>().Select(v => v.Name));

    /// Something else changed the character after Corruption's design, so it won't be reverted.
    public bool Superseded => superseded;

    /// The last thing that went wrong, for the Settings page.
    public string? Problem { get; private set; }

    private void OnGlamourerChanged()
    {
        if (appliedDesign && DateTime.UtcNow > ownApplyUntil) superseded = true;
    }

    /// Run once a second with what should be on now.
    public void Update(DateTime now, TierVisual? tier, TierVisual? pregnancy)
    {
        var same = Same(tier, appliedTier) && Same(pregnancy, appliedPregnancy);
        if (same || now < nextTry) return;

        RemoveApplied();
        Problem = null;
        var ok = true;
        if (tier is not null) ok &= Apply(tier, now, ref appliedTier);
        if (pregnancy is not null) ok &= Apply(pregnancy, now, ref appliedPregnancy);
        if (!ok) nextTry = now + RetryInterval;
    }

    private static bool Same(TierVisual? wanted, TierVisual? applied)
        => wanted is null ? applied is null : wanted.SameAs(applied);

    /// Takes off whatever Corruption applied: on logout, unload, or turning the feature off.
    public void RemoveApplied()
    {
        if (appliedDesign)
        {
            if (!superseded) glamourer.Revert();
            else Plugin.Log.Information("Tier visual: left the Glamourer design alone, since something else changed the character after it.");
        }
        var redraw = appliedMods.Count > 0;
        foreach (var (collection, mod) in appliedMods) penumbra.TryRemoveTemporary(collection, mod);
        if (redraw) penumbra.TryRedrawPlayer();

        appliedMods.Clear();
        appliedDesign = false;
        superseded = false;
        appliedTier = null;
        appliedPregnancy = null;
    }

    private bool Apply(TierVisual visual, DateTime now, ref TierVisual? slot)
    {
        if (visual.Kind == VisualKind.Glamourer)
        {
            if (!glamourer.IsAvailable)
            {
                Problem = "Glamourer isn't available. Your choices are kept for when it's back.";
                return false;
            }
            ownApplyUntil = now + OwnApplyWindow;
            if (!glamourer.Apply(visual.DesignId))
            {
                Problem = $"Glamourer couldn't apply {visual.Name}. It may have been deleted.";
                return false;
            }
            appliedDesign = true;
            slot = visual;
            return true;
        }

        if (!penumbra.IsAvailable || penumbra.TryGetPlayerCollection() is not { } collection)
        {
            Problem = "Penumbra isn't available. Your choices are kept for when it's back.";
            return false;
        }
        if (!penumbra.TrySetTemporary(collection, visual.ModDirectory, visual.Options))
        {
            Problem = $"Penumbra couldn't apply {visual.Name}. It may have been removed.";
            return false;
        }
        penumbra.TryRedrawPlayer();
        appliedMods.Add((collection, visual.ModDirectory));
        slot = visual;
        return true;
    }
}
