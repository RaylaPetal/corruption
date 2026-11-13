using System;
using Corruption.Core;
using Corruption.Scene;
using Dalamud.Plugin.Ipc;

namespace Corruption.Ipc;

/// The events. The engine raises them on the framework thread; each is forwarded with failing subscribers logged, never
/// thrown back into the engine. They follow the engine across character changes.
public sealed partial class CorruptionApi
{
    private ICallGateProvider<object> onClimax = null!;
    private ICallGateProvider<string, string, object> onPartnerClimax = null!;
    private ICallGateProvider<string, string, bool, object> onSceneStarted = null!;
    private ICallGateProvider<double, double, object> onSceneEnded = null!;
    private ICallGateProvider<int, object> onLevelUp = null!;
    private ICallGateProvider<string, object> onCyclePhaseChanged = null!;
    private ICallGateProvider<string, string, string, object> onConceived = null!;
    private ICallGateProvider<string, object> onTrimesterChanged = null!;
    private ICallGateProvider<string, string, object> onBirth = null!;
    private ICallGateProvider<object> onRecoveryEnded = null!;
    private ICallGateProvider<int, object> onArousalChanged = null!;
    private ICallGateProvider<object> onStateChanged = null!;

    private SceneEngine? hooked;
    private string lastFingerprint = "";

    private void RegisterEvents()
    {
        var pi = Plugin.PluginInterface;
        onClimax = pi.GetIpcProvider<object>("Corruption.OnClimax");
        onPartnerClimax = pi.GetIpcProvider<string, string, object>("Corruption.OnPartnerClimax");
        onSceneStarted = pi.GetIpcProvider<string, string, bool, object>("Corruption.OnSceneStarted");
        onSceneEnded = pi.GetIpcProvider<double, double, object>("Corruption.OnSceneEnded");
        onLevelUp = pi.GetIpcProvider<int, object>("Corruption.OnLevelUp");
        onCyclePhaseChanged = pi.GetIpcProvider<string, object>("Corruption.OnCyclePhaseChanged");
        onConceived = pi.GetIpcProvider<string, string, string, object>("Corruption.OnConceived");
        onTrimesterChanged = pi.GetIpcProvider<string, object>("Corruption.OnTrimesterChanged");
        onBirth = pi.GetIpcProvider<string, string, object>("Corruption.OnBirth");
        onRecoveryEnded = pi.GetIpcProvider<object>("Corruption.OnRecoveryEnded");
        onArousalChanged = pi.GetIpcProvider<int, object>("Corruption.OnArousalChanged");
        onStateChanged = pi.GetIpcProvider<object>("Corruption.OnStateChanged");

        scene.EngineChanged += Hook;
        scene.Sampled += OnSampled;
        Hook(scene.Engine);
    }

    private void UnhookEvents()
    {
        scene.EngineChanged -= Hook;
        scene.Sampled -= OnSampled;
        Hook(null);
    }

    private void Hook(SceneEngine? engine)
    {
        if (hooked is { } old)
        {
            old.Climaxed -= OnClimax;
            old.PartnerClimaxed -= OnPartnerClimax;
            old.SceneStarted -= OnSceneStarted;
            old.SceneFinished -= OnSceneEnded;
            old.LeveledUp -= OnLevelUp;
            old.CyclePhaseChanged -= OnCyclePhaseChanged;
            old.Conceived -= OnConceived;
            old.TrimesterChanged -= OnTrimesterChanged;
            old.Born -= OnBirth;
            old.RecoveryEnded -= OnRecoveryEnded;
            old.ArousalShown -= OnArousalChanged;
        }
        hooked = engine;
        lastFingerprint = "";
        if (engine is null) return;
        engine.Climaxed += OnClimax;
        engine.PartnerClimaxed += OnPartnerClimax;
        engine.SceneStarted += OnSceneStarted;
        engine.SceneFinished += OnSceneEnded;
        engine.LeveledUp += OnLevelUp;
        engine.CyclePhaseChanged += OnCyclePhaseChanged;
        engine.Conceived += OnConceived;
        engine.TrimesterChanged += OnTrimesterChanged;
        engine.Born += OnBirth;
        engine.RecoveryEnded += OnRecoveryEnded;
        engine.ArousalShown += OnArousalChanged;
    }

    private void OnClimax() => Send("Corruption.OnClimax", () => onClimax.SendMessage());

    private void OnPartnerClimax(PeerRecord peer) => Send("Corruption.OnPartnerClimax", () => onPartnerClimax.SendMessage(peer.Name, peer.World));

    private void OnSceneStarted(PeerRecord? partner, bool solo)
        => Send("Corruption.OnSceneStarted", () => onSceneStarted.SendMessage(partner?.Name ?? "", partner?.World ?? "", solo));

    private void OnSceneEnded(double seconds, double xp) => Send("Corruption.OnSceneEnded", () => onSceneEnded.SendMessage(seconds, xp));

    private void OnLevelUp(int level) => Send("Corruption.OnLevelUp", () => onLevelUp.SendMessage(level));

    private void OnCyclePhaseChanged(CyclePhase phase)
        => Send("Corruption.OnCyclePhaseChanged", () => onCyclePhaseChanged.SendMessage(ApiSnapshots.Lower(phase)));

    private void OnConceived(string name, string world, string source)
        => Send("Corruption.OnConceived", () => onConceived.SendMessage(name, world, source));

    private void OnTrimesterChanged(PregnancyStage stage)
        => Send("Corruption.OnTrimesterChanged", () => onTrimesterChanged.SendMessage(ApiSnapshots.Lower(stage)));

    private void OnBirth(string name, string world) => Send("Corruption.OnBirth", () => onBirth.SendMessage(name, world));

    private void OnRecoveryEnded() => Send("Corruption.OnRecoveryEnded", () => onRecoveryEnded.SendMessage());

    private void OnArousalChanged(int arousal) => Send("Corruption.OnArousalChanged", () => onArousalChanged.SendMessage(arousal));

    /// Once a second, after the engine's sample: "state changed" when any read's fingerprint moved.
    private void OnSampled(DateTime now)
    {
        var fingerprint = Fingerprint(now);
        if (fingerprint == lastFingerprint) return;
        var first = lastFingerprint.Length == 0;
        lastFingerprint = fingerprint;
        if (!first) Send("Corruption.OnStateChanged", () => onStateChanged.SendMessage());
    }
}
