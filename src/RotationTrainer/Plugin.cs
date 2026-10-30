using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RotationTrainer.Core;
using RotationTrainer.GameData;
using RotationTrainer.Services;
using RotationTrainer.Ui.Settings;

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
    public IPluginLog Log { get; }

    public Configuration Config { get; }
    public IAbilityCatalog Catalog { get; }
    public LibraryStore Store { get; }
    public RotationLibrary Library { get; }

    private readonly WindowSystem _windows = new("RotationTrainer");
    private readonly SettingsWindow _settings = new();

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IDataManager data,
        ITextureProvider textures, IClientState clientState, ICondition condition, IFramework framework,
        IGameInteropProvider interop, IPlayerState playerState, IGamepadState gamepad, IPluginLog log)
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
        Log = log;

        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Catalog = new LuminaCatalog(data.Excel);
        Store = new LibraryStore(pluginInterface.GetPluginConfigDirectory(), Catalog, log);
        Library = Store.Load();

        _windows.AddWindow(_settings);
        pluginInterface.UiBuilder.Draw += _windows.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += _settings.Toggle;
        pluginInterface.UiBuilder.OpenMainUi += _settings.Toggle;

        commands.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle settings. Subcommands: trail, builder, debug.",
        });
    }

    public void SaveConfig() => PluginInterface.SavePluginConfig(Config);

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
                _settings.Toggle();
                break;
            case "trail":
            case "builder":
            case "debug":
                Log.Information("{Command} {Sub} is not yet available.", Command, args.Trim());
                break;
            default:
                Log.Warning("Unknown subcommand \"{Args}\". Try: /rtrain, /rtrain trail, /rtrain builder, /rtrain debug.",
                    args.Trim());
                break;
        }
    }

    public void Dispose()
    {
        Commands.RemoveHandler(Command);
        PluginInterface.UiBuilder.Draw -= _windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= _settings.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= _settings.Toggle;
        _windows.RemoveAllWindows();
    }
}
