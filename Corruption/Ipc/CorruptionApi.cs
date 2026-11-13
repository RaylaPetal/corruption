using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;
using Corruption.Scene;
using Dalamud.Plugin.Ipc;

namespace Corruption.Ipc;

/// Corruption's plugin API: reads of the local character and nearby players, events, and three writes the player has to
/// allow. Every gate answers only on the framework thread. Gate names are "Corruption.&lt;Name&gt;"; Corruption.Api/CorruptionIpc.cs
/// is the client other plugins copy, and the README documents every gate.
public sealed partial class CorruptionApi : IDisposable
{
    /// Version 1. Later versions only add gates.
    public const int Version = 1;

    private readonly SceneService scene;
    private readonly List<Action> unregister = [];
    private readonly ICallGateProvider<object> ready;
    private readonly ICallGateProvider<object> disposing;

    public CorruptionApi(SceneService scene)
    {
        this.scene = scene;
        var pi = Plugin.PluginInterface;
        var version = pi.GetIpcProvider<int>("Corruption.ApiVersion");
        version.RegisterFunc(() => Version);
        unregister.Add(version.UnregisterFunc);
        ready = pi.GetIpcProvider<object>("Corruption.Ready");
        disposing = pi.GetIpcProvider<object>("Corruption.Disposing");

        RegisterReads();
        RegisterWrites();
        RegisterEvents();

        Send("Corruption.Ready", () => ready.SendMessage());
    }

    public void Dispose()
    {
        Send("Corruption.Disposing", () => disposing.SendMessage());
        UnhookEvents();
        foreach (var undo in unregister) undo();
    }

    private SceneEngine? Engine => scene.Engine;

    // ── Plumbing ─────────────────────────────────────────────────────────────

    /// Runs <paramref name="body"/> on the framework thread only, never letting an exception reach the caller.
    private static T Guarded<T>(string name, Func<T> body, T offThread, T onError)
    {
        if (!Plugin.Framework.IsInFrameworkUpdateThread) return offThread;
        try
        {
            return body();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"{name} failed.");
            return onError;
        }
    }

    /// Raises an event, logging (never throwing) when a subscriber fails.
    private static void Send(string name, Action send)
    {
        try
        {
            send();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"A {name} subscriber failed.");
        }
    }

    private void Read<TRet>(string name, Func<TRet> body, TRet empty)
    {
        var gate = Plugin.PluginInterface.GetIpcProvider<TRet>(name);
        gate.RegisterFunc(() => Guarded(name, body, empty, empty));
        unregister.Add(gate.UnregisterFunc);
    }

    private void Read<T1, T2, TRet>(string name, Func<T1, T2, TRet> body, TRet empty)
    {
        var gate = Plugin.PluginInterface.GetIpcProvider<T1, T2, TRet>(name);
        gate.RegisterFunc((a, b) => Guarded(name, () => body(a, b), empty, empty));
        unregister.Add(gate.UnregisterFunc);
    }

    private void Outcome<T1>(string name, Func<T1, string> body)
    {
        var gate = Plugin.PluginInterface.GetIpcProvider<T1, string>(name);
        gate.RegisterFunc(a => Guarded(name, () => body(a), ApiOutcomes.WrongThread, ApiOutcomes.Error));
        unregister.Add(gate.UnregisterFunc);
    }

    private void Outcome<T1, T2>(string name, Func<T1, T2, string> body)
    {
        var gate = Plugin.PluginInterface.GetIpcProvider<T1, T2, string>(name);
        gate.RegisterFunc((a, b) => Guarded(name, () => body(a, b), ApiOutcomes.WrongThread, ApiOutcomes.Error));
        unregister.Add(gate.UnregisterFunc);
    }

    private void Outcome<T1, T2, T3>(string name, Func<T1, T2, T3, string> body)
    {
        var gate = Plugin.PluginInterface.GetIpcProvider<T1, T2, T3, string>(name);
        gate.RegisterFunc((a, b, c) => Guarded(name, () => body(a, b, c), ApiOutcomes.WrongThread, ApiOutcomes.Error));
        unregister.Add(gate.UnregisterFunc);
    }

    private static PeerRecord Player(string? name, string? world) => new() { Name = name?.Trim() ?? "", World = world?.Trim() ?? "" };
}
