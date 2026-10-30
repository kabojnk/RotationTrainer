using Dalamud.Plugin.Services;
using RotationTrainer.Core;

namespace RotationTrainer.Services;

/// <summary>Persists the rotation library as library.json in the plugin's config directory.</summary>
public sealed class LibraryStore
{
    private const string FileName = "library.json";

    private readonly string _path;
    private readonly IAbilityCatalog _catalog;
    private readonly IPluginLog _log;

    public LibraryStore(string directory, IAbilityCatalog catalog, IPluginLog log)
    {
        _path = Path.Combine(directory, FileName);
        _catalog = catalog;
        _log = log;
    }

    /// <summary>Reads the library (empty if there is no file yet) and saves it whenever it changes.</summary>
    public RotationLibrary Load()
    {
        var rotations = new List<Rotation>();
        if (File.Exists(_path))
        {
            var result = RotationJson.Import(File.ReadAllText(_path), _catalog, NewId);
            foreach (var e in result.Errors)
                _log.Warning("library.json: {Rotation}{Step}: {Message}",
                    e.Rotation, e.Step is { } s ? $" step {s}" : "", e.Message);
            if (result.Errors.Count > 0)
            {
                // The next save would drop whatever failed to load; keep the original.
                File.Copy(_path, _path + ".bak", overwrite: true);
                _log.Warning("library.json had errors; the original was copied to {Backup}", _path + ".bak");
            }
            rotations.AddRange(result.Rotations);
        }

        var library = new RotationLibrary(rotations);
        library.Changed += () => Save(library);
        return library;
    }

    public void Save(RotationLibrary library)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, RotationJson.Export(library.All));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not save {Path}", _path);
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}
