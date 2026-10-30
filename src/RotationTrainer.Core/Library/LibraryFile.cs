namespace RotationTrainer.Core;

/// <summary>
/// Reads and writes the rotation library file. A load that drops rotations first copies the
/// original to a new timestamped backup (never overwriting an older one). If the file cannot be
/// read or backed up, saving is disabled so the original is never overwritten.
/// </summary>
public sealed class LibraryFile
{
    private readonly string _path;
    private readonly IAbilityCatalog _catalog;
    private readonly Action<string> _warn;
    private bool _saveBlocked;

    public LibraryFile(string path, IAbilityCatalog catalog, Action<string> warn)
    {
        _path = path;
        _catalog = catalog;
        _warn = warn;
    }

    /// <summary>True when the last load could not read or back up the file, so saves are skipped.</summary>
    public bool SaveBlocked => _saveBlocked;

    /// <summary>Reads the library (empty if there is no file yet) and saves it whenever it changes.</summary>
    public RotationLibrary Load()
    {
        _saveBlocked = false;
        var rotations = new List<Rotation>();
        if (File.Exists(_path))
        {
            string json;
            try
            {
                json = File.ReadAllText(_path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Blocked($"Could not read {_path}: {ex.Message}. Changes will not be saved this session.");
            }

            var result = RotationJson.Import(json, _catalog, NewId);
            foreach (var e in result.Errors)
                _warn($"{Path.GetFileName(_path)}: {e.Rotation}{(e.Step is { } s ? $" step {s}" : "")}: {e.Message}");

            if (result.Errors.Count > 0)
            {
                try
                {
                    var backup = Backup();
                    _warn($"{Path.GetFileName(_path)} had errors; the original was copied to {backup}.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return Blocked($"Could not back up {_path}: {ex.Message}. Changes will not be saved this session.",
                        result.Rotations);
                }
            }
            rotations.AddRange(result.Rotations);
        }

        var library = new RotationLibrary(rotations);
        library.Changed += () => Save(library);
        return library;
    }

    public void Save(RotationLibrary library)
    {
        if (_saveBlocked)
        {
            _warn($"Not saving {_path}: it could not be loaded safely.");
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, RotationJson.Export(library.All));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _warn($"Could not save {_path}: {ex.Message}");
        }
    }

    private RotationLibrary Blocked(string message, IEnumerable<Rotation>? rotations = null)
    {
        _warn(message);
        _saveBlocked = true;
        return new RotationLibrary(rotations ?? Array.Empty<Rotation>());
    }

    /// <summary>Copies the file to library.&lt;yyyyMMdd-HHmmss&gt;[-n].bak.json beside it; never overwrites.</summary>
    private string Backup()
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(_path))!;
        var stem = Path.GetFileNameWithoutExtension(_path);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        for (var n = 0; ; n++)
        {
            var candidate = Path.Combine(dir, $"{stem}.{stamp}{(n == 0 ? "" : $"-{n}")}.bak.json");
            if (File.Exists(candidate)) continue;
            try
            {
                File.Copy(_path, candidate, overwrite: false);
                return candidate;
            }
            catch (IOException) when (File.Exists(candidate))
            {
                // Lost a race for this name; try the next one.
            }
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}
