using System;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Client;
using Buttplug.Core.Messages;

namespace Corruption.Bridges;

/// Intiface Central over its WebSocket server, adapted from Oathbound. Intiface is a separate app, not a plugin. Every
/// call is fire-and-forget and fails closed: the UI polls the state rather than waiting.
public sealed class IntifaceService : IDisposable
{
    public const string DefaultAddress = "ws://127.0.0.1:12345";

    private readonly ButtplugClient client = new("Corruption");
    private CancellationTokenSource? connecting;

    public bool IsConnected => client.Connected;
    public bool IsConnecting { get; private set; }
    public string? LastError { get; private set; }
    public int DeviceCount => IsConnected ? client.Devices.Length : 0;

    public void Connect(string address)
    {
        if (IsConnecting || IsConnected) return;
        IsConnecting = true;
        LastError = null;
        connecting = new CancellationTokenSource();
        _ = ConnectAsync(address, connecting.Token);
    }

    private async Task ConnectAsync(string address, CancellationToken token)
    {
        try
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                LastError = "That isn't a valid address.";
                return;
            }
            await client.ConnectAsync(new ButtplugWebsocketConnector(uri), token);
            await client.StartScanningAsync(token);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Plugin.Log.Warning($"Couldn't connect to Intiface Central: {ex.Message}");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    public void Disconnect()
    {
        connecting?.Cancel();
        if (!IsConnected) return;
        StopAll();
        _ = DisconnectAsync();
    }

    private async Task DisconnectAsync()
    {
        try { await client.DisconnectAsync(); }
        catch (Exception ex) { Plugin.Log.Warning($"Error disconnecting from Intiface Central: {ex.Message}"); }
    }

    /// 0 to 1 on every vibrate feature of every device. One device failing doesn't stop the others.
    public void VibrateAll(double intensity)
    {
        if (!IsConnected) return;
        if (intensity <= 0)
        {
            StopAll();
            return;
        }
        _ = VibrateAllAsync(Math.Clamp(intensity, 0, 1));
    }

    private async Task VibrateAllAsync(double intensity)
    {
        var command = new DeviceOutputCommand(OutputType.Vibrate, PercentOrSteps.FromPercent(intensity), null);
        foreach (var device in client.Devices)
        {
            foreach (var feature in device.GetFeaturesWithOutput(OutputType.Vibrate))
            {
                try { await feature.RunOutputAsync(command, CancellationToken.None); }
                catch (Exception ex) { Plugin.Log.Warning($"Couldn't vibrate {device.Name}: {ex.Message}"); }
            }
        }
    }

    /// The protocol's own stop for every device.
    public void StopAll()
    {
        if (!IsConnected) return;
        _ = StopAllAsync();
    }

    private async Task StopAllAsync()
    {
        try { await client.StopAllDevicesAsync(CancellationToken.None); }
        catch (Exception ex) { Plugin.Log.Warning($"Couldn't stop devices: {ex.Message}"); }
    }

    public void Dispose()
    {
        connecting?.Cancel();
        StopAll();
        client.Dispose();
    }
}
