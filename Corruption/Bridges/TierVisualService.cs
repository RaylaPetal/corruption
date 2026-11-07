using System;
using Corruption.Core;
using Corruption.Mods;

namespace Corruption.Bridges;

/// Puts the current tier's visual on the character, and takes off only what it put on. What should be on comes from
/// <see cref="TierVisualPlan"/>; this remembers what was applied and changes it when the plan does. Framework thread.
public sealed class TierVisualService : IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    /// Glamourer events from Corruption's own apply arrive during and just after the call.
    private static readonly TimeSpan OwnApplyWindow = TimeSpan.FromSeconds(2);

    private readonly GlamourerIpc glamourer;
    private readonly PenumbraIpc penumbra;

    private TierVisual? applied;
    private Guid appliedCollection;
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
    public TierVisual? Applied => applied;

    /// Something else changed the character after Corruption's design, so it won't be reverted.
    public bool Superseded => superseded;

    /// The last thing that went wrong, for the Settings page.
    public string? Problem { get; private set; }

    private void OnGlamourerChanged()
    {
        if (applied is { Kind: VisualKind.Glamourer } && DateTime.UtcNow > ownApplyUntil) superseded = true;
    }

    /// Run once a second with what should be on now.
    public void Update(DateTime now, TierVisual? wanted)
    {
        if (wanted is null && applied is null) return;
        if (wanted is not null && wanted.SameAs(applied)) return;
        if (now < nextTry) return;

        RemoveApplied();
        if (wanted is null) return;
        if (!Apply(wanted, now)) nextTry = now + RetryInterval;
    }

    /// Takes off whatever Corruption applied: on logout, unload, or turning the feature off.
    public void RemoveApplied()
    {
        if (applied is not { } visual) return;
        switch (visual.Kind)
        {
            case VisualKind.Glamourer when !superseded:
                glamourer.Revert();
                break;
            case VisualKind.Glamourer:
                Plugin.Log.Information("Tier visual: left the Glamourer design alone, since something else changed the character after it.");
                break;
            case VisualKind.Penumbra:
                if (penumbra.TryRemoveTemporary(appliedCollection, visual.ModDirectory)) penumbra.TryRedrawPlayer();
                break;
        }
        applied = null;
        superseded = false;
    }

    private bool Apply(TierVisual visual, DateTime now)
    {
        Problem = null;
        if (visual.Kind == VisualKind.Glamourer)
        {
            if (!glamourer.IsAvailable)
            {
                Problem = "Glamourer isn't available. Your choice is kept for when it's back.";
                return false;
            }
            ownApplyUntil = now + OwnApplyWindow;
            if (!glamourer.Apply(visual.DesignId))
            {
                Problem = $"Glamourer couldn't apply {visual.Name}. It may have been deleted.";
                return false;
            }
            superseded = false;
            applied = visual;
            return true;
        }

        if (!penumbra.IsAvailable || penumbra.TryGetPlayerCollection() is not { } collection)
        {
            Problem = "Penumbra isn't available. Your choice is kept for when it's back.";
            return false;
        }
        if (!penumbra.TrySetTemporary(collection, visual.ModDirectory, visual.Options))
        {
            Problem = $"Penumbra couldn't apply {visual.Name}. It may have been removed.";
            return false;
        }
        penumbra.TryRedrawPlayer();
        appliedCollection = collection;
        applied = visual;
        return true;
    }
}
