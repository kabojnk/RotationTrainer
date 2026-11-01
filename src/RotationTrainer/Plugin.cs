using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RotationTrainer.Core;
using RotationTrainer.Game;
using RotationTrainer.GameData;
using RotationTrainer.Services;
using RotationTrainer.Ui;
using RotationTrainer.Ui.Builder;
using RotationTrainer.Ui.Glass;
using RotationTrainer.Ui.Levelling;
using RotationTrainer.Ui.Settings;
using RotationTrainer.Ui.Trail;

namespace RotationTrainer;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/rtrain";

    public IDalamudPluginInterface PluginInterface { get; }
    public ICommandManager Commands { get; }
    public IDataManager Data { get; }
    public ITextureProvider Textures { get; }
    public IClientState ClientState { get; }
    public ICondition Condition { get; }
    public IFramework Framework { get; }
    public IGameInteropProvider Interop { get; }
    public IPlayerState PlayerState { get; }
    public IGamepadState Gamepad { get; }
    public IKeyState Keys { get; }
    public IPluginLog Log { get; }

    public Configuration Config { get; }
    public IAbilityCatalog Catalog { get; }
    public LibraryStore Store { get; }
    public RotationLibrary Library { get; }
    /// <summary>The rotations shipped with the plugin; new ones are added to the library on load.</summary>
    public IReadOnlyList<Rotation> BuiltInRotations { get; }
    public ActionListener Actions { get; }
    public CountdownSource Countdown { get; } = new();
    public PracticeSession Session { get; }
    public LevellingState Levelling { get; }

    private readonly WindowSystem _windows = new("RotationTrainer");
    private readonly SettingsWindow _settings;
    private readonly DebugWindow _debug;
    private readonly TrailWindow _trail;
    private readonly BuilderWindow _builder;
    private readonly LevellingGuideWindow _guide;
    private readonly ReferenceCardWindow _card;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IDataManager data,
        ITextureProvider textures, IClientState clientState, ICondition condition, IFramework framework,
        IGameInteropProvider interop, IPlayerState playerState, IGamepadState gamepad, IKeyState keys, IObjectTable objects,
        IPluginLog log)
    {
        PluginInterface = pluginInterface;
        Commands = commands;
        Data = data;
        Textures = textures;
        ClientState = clientState;
        Condition = condition;
        Framework = framework;
        Interop = interop;
        PlayerState = playerState;
        Gamepad = gamepad;
        Keys = keys;
        Log = log;

        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var catalog = new LuminaCatalog(data.Excel);
        Catalog = catalog;
        Store = new LibraryStore(pluginInterface.GetPluginConfigDirectory(), Catalog, log);
        Library = Store.Load();
        BuiltInRotations = LoadBuiltIns(catalog, log);
        if (Library.AddBuiltIns(BuiltInRotations, Config.BuiltInsAdded) > 0) pluginInterface.SavePluginConfig(Config);

        // The hook is live from here on: if the rest of construction fails, Dispose never runs, so undo it here.
        Actions = new ActionListener(interop, playerState, log);
        try
        {
            Session = new PracticeSession(Config, Library, Catalog, data, clientState, playerState, condition,
                framework, gamepad, keys, log, Actions, Countdown,
                () => pluginInterface.SavePluginConfig(Config), new DummyEnmity(objects, log));
            _debug = new DebugWindow(Session, Actions, Countdown);
            _settings = new SettingsWindow(this);
            _trail = new TrailWindow(Config, Session, textures, clientState, _settings.Toggle,
                () => pluginInterface.SavePluginConfig(Config));
            _builder = new BuilderWindow(this, catalog.Jobs);
            Levelling = new LevellingState(playerState, data, Catalog, LoadLevellingDeck(catalog, log));
            var chips = new CardChips(textures, Session);
            _guide = new LevellingGuideWindow(Config, Levelling, Catalog, textures, chips,
                () => pluginInterface.SavePluginConfig(Config));
            _card = new ReferenceCardWindow(Config, Levelling, clientState, chips, OpenLevellingGuide,
                () => pluginInterface.SavePluginConfig(Config));

            _windows.AddWindow(_trail);
            _windows.AddWindow(_settings);
            _windows.AddWindow(_debug);
            _windows.AddWindow(_builder);
            _windows.AddWindow(_guide);
            _windows.AddWindow(_card);
            pluginInterface.UiBuilder.Draw += DrawUi;
            pluginInterface.UiBuilder.OpenConfigUi += _settings.Toggle;
            pluginInterface.UiBuilder.OpenMainUi += _settings.Toggle;

            commands.AddHandler(Command, new CommandInfo(OnCommand)
            {
                HelpMessage = "Toggle settings. Subcommands: trail, level, builder, debug.",
            });
        }
        catch
        {
            pluginInterface.UiBuilder.Draw -= DrawUi;
            if (_settings != null)
            {
                pluginInterface.UiBuilder.OpenConfigUi -= _settings.Toggle;
                pluginInterface.UiBuilder.OpenMainUi -= _settings.Toggle;
            }
            _trail?.Dispose();
            _debug?.Dispose();
            Session?.Dispose();
            Actions.Dispose();
            throw;
        }
    }

    private static IReadOnlyList<Rotation> LoadBuiltIns(IAbilityCatalog catalog, IPluginLog log)
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("RotationTrainer.presets.json");
        if (stream == null) return Array.Empty<Rotation>();
        var result = RotationJson.Import(new StreamReader(stream).ReadToEnd(), catalog, () => Guid.NewGuid().ToString());
        foreach (var e in result.Errors)
            log.Warning("Built-in rotation {Rotation}: {Message}", e.Rotation, e.Message);
        return result.Rotations;
    }

    private static LevellingDeck LoadLevellingDeck(IAbilityCatalog catalog, IPluginLog log)
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("RotationTrainer.levelling.json");
        if (stream == null) return LevellingDeck.Parse("[]", catalog).Deck;
        var (deck, errors) = LevellingDeck.Parse(new StreamReader(stream).ReadToEnd(), catalog);
        foreach (var e in errors)
            log.Warning("Levelling card {Card}: {Message}", e.Card, e.Message);
        return deck;
    }

    /// <summary>Opens the levelling guide (<c>/rtrain level</c>).</summary>
    public void OpenLevellingGuide()
    {
        _guide.IsOpen = true;
        _guide.BringToFront();
    }

    /// <summary>Saves the configuration and rebuilds the practice session from it.</summary>
    public void SaveConfig()
    {
        PluginInterface.SavePluginConfig(Config);
        Session.Rebuild();
    }

    /// <summary>Opens the rotation builder on a document.</summary>
    public void OpenBuilder(BuilderDocument document) => _builder.Open(document);

    private void DrawUi()
    {
        GlassTheme.Use(Config.Theme);
        Levelling.Update();
        _windows.Draw();
        _settings.DrawDialogs();
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
                _settings.Toggle();
                break;
            case "debug":
                _debug.Toggle();
                break;
            case "level":
                _guide.Toggle();
                break;
            case "trail":
                Config.ShowTrail = !Config.ShowTrail;
                PluginInterface.SavePluginConfig(Config);
                Log.Information("Trail {State}.", Config.ShowTrail ? "shown" : "hidden");
                break;
            case "builder":
                if (Session.Job is { } job)
                    OpenBuilder(new BuilderDocument(job, RotationKind.Opener, "New opener", Catalog));
                else
                    Log.Information("Log in to a combat job first.");
                break;
            default:
                Log.Warning("Unknown subcommand \"{Args}\". Try: /rtrain, /rtrain trail, /rtrain level, /rtrain builder, /rtrain debug.",
                    args.Trim());
                break;
        }
    }

    public void Dispose()
    {
        Commands.RemoveHandler(Command);
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi -= _settings.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= _settings.Toggle;
        _windows.RemoveAllWindows();
        _trail.Dispose();
        _debug.Dispose();
        Session.Dispose();
        Actions.Dispose();
    }
}
