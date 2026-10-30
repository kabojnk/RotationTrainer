using Dalamud.Plugin.Services;
using RotationTrainer.Core;

namespace RotationTrainer.Services;

/// <summary>Persists the rotation library as library.json in the plugin's config directory.</summary>
public sealed class LibraryStore
{
    private const string FileName = "library.json";

    private readonly LibraryFile _file;

    public LibraryStore(string directory, IAbilityCatalog catalog, IPluginLog log) =>
        _file = new LibraryFile(Path.Combine(directory, FileName), catalog, message => log.Warning("{Message}", message));

    /// <summary>Reads the library (empty if missing or unreadable) and saves it whenever it changes.</summary>
    public RotationLibrary Load() => _file.Load();

    public void Save(RotationLibrary library) => _file.Save(library);
}
