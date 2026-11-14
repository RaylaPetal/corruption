using System;
using System.IO;
using Corruption.Bridges;
using Corruption.Core;
using Corruption.Ipc;
using Corruption.Mods;
using Corruption.Plate;
using Corruption.Scene;
using Corruption.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Corruption;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/corr";

    private const string Usage = "/corr, /corr hud, /corr climax, /corr plate, /corr hide, /corr pause, /corr resume, /corr prestige, /corr changelog";

    private readonly WindowSystem windows = new("Corruption");
    private readonly Settings settings;
    private readonly PenumbraIpc penumbra;
    private readonly SimpleHeelsIpc heels;
    private readonly CummyBridge cummy;
    private readonly BridgeHub bridges;
    private readonly PlateRenderer plateRenderer;
    private readonly SceneService scene;
    private readonly CorruptionApi api;
    private readonly MainWindow mainWindow;
    private readonly ChangelogWindow changelog;

    public Plugin()
    {
        var saved = PluginInterface.GetPluginConfig() as Settings;
        settings = saved ?? new Settings();
        if (settings.Migrate()) settings.Save();
        // A fresh install gets the welcome instead of notes about changes it never saw.
        if (saved is null)
        {
            settings.LastSeenChangelog = Changelog.Latest;
            settings.Save();
        }
        ApplyUiHide(settings);

        var configDirectory = PluginInterface.GetPluginConfigDirectory();
        Directory.CreateDirectory(configDirectory);

        var balance = Balance.Load(Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "balance.json"), out var warning);
        if (warning is not null) Log.Warning(warning);

        penumbra = new PenumbraIpc();
        var acts = new ActsService(penumbra, new ModScanner(new EmoteIndex()), configDirectory);
        var detector = new ActDetector(penumbra, acts);
        heels = new SimpleHeelsIpc();
        cummy = new CummyBridge();
        bridges = new BridgeHub(settings, penumbra);
        var plates = new PlateLabels();
        plateRenderer = new PlateRenderer(settings, plates);
        var popup = new RewardPopup(settings);
        windows.AddWindow(popup);
        scene = new SceneService(settings, balance, penumbra, acts, detector, heels, cummy, plates, bridges, popup.Show, configDirectory);
        api = new CorruptionApi(scene);

        mainWindow = new MainWindow(settings, balance, scene, acts, penumbra, heels, cummy, bridges, plateRenderer, detector, popup);
        windows.AddWindow(mainWindow);
        var welcome = new WelcomeWindow(settings, scene, balance, cummy, bridges, () => mainWindow.IsOpen = true);
        windows.AddWindow(welcome);
        mainWindow.OpenWelcome = welcome.Show;
        changelog = new ChangelogWindow(settings);
        windows.AddWindow(changelog);
        mainWindow.OpenChangelog = changelog.ShowAll;
        windows.AddWindow(new HudWindow(settings, scene, balance, () => mainWindow.IsOpen = true));

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = $"Corruption. {Usage}" });
        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.Toggle;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        CommandManager.RemoveHandler(CommandName);
        windows.RemoveAllWindows();
#if DEBUG
        mainWindow.DisposeDebug();
#endif

        api.Dispose();
        scene.Dispose();
        bridges.Dispose();
        plateRenderer.Dispose();
        cummy.Dispose();
        heels.Dispose();
        penumbra.Dispose();
    }

    /// The theme goes around everything at once, so its pushes and pops always match.
    private void DrawUi()
    {
        using var theme = Theme.Push();
        windows.Draw();
    }

    internal static void ApplyUiHide(Settings settings)
    {
        PluginInterface.UiBuilder.DisableCutsceneUiHide = settings.ShowInCutscenesAndGpose;
        PluginInterface.UiBuilder.DisableGposeUiHide = settings.ShowInCutscenesAndGpose;
    }

    /// Asks first: `/corr prestige` says what will happen, and `/corr prestige confirm` does it.
    private void Prestige(string rest)
    {
        if (scene.Engine is not { } engine) return;
        if (PrestigeRules.Refusal(engine.Save, engine.Balance) is { } refusal)
        {
            Notifier.Info(refusal);
            return;
        }
        if (!rest.Equals("confirm", StringComparison.OrdinalIgnoreCase))
        {
            Notifier.Info("Prestige takes you back to level 1 with no XP or arousal, for a star and +5% XP for good. Everything else stays. Type /corr prestige confirm to go ahead.");
            return;
        }
        if (engine.Prestige(DateTime.UtcNow) is { } failed) Notifier.Info(failed);
    }

    private void OnCommand(string command, string args)
    {
        var words = args.Trim();
        var space = words.IndexOf(' ');
        var verb = (space < 0 ? words : words[..space]).ToLowerInvariant();

        switch (verb)
        {
            case "":
                mainWindow.Toggle();
                break;
            case "hud":
                settings.ShowHud = !settings.ShowHud;
                settings.Save();
                break;
            case "climax":
                if (scene.Engine?.TryManualClimax(DateTime.UtcNow) is { } refusal) Notifier.Info(refusal);
                break;
            case "plate":
                settings.PlateShowOwn = !settings.PlateShowOwn;
                settings.Save();
                Notifier.Info(settings.PlateShowOwn ? "Your nameplate text is on." : "Your nameplate text is off.");
                break;
            case "hide":
                if (scene.Engine is { } engine) scene.SetHidden(!engine.Save.Hidden);
                break;
            case "pause":
                scene.Pause();
                break;
            case "prestige":
                Prestige(space < 0 ? "" : words[(space + 1)..].Trim());
                break;
            case "resume":
                scene.Engine?.Resume();
                break;
            case "changelog":
                changelog.ShowAll();
                break;
            default:
                Notifier.Error($"Unknown command '{verb}'. Try: {Usage}");
                break;
        }
    }

}
