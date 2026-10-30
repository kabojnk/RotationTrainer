using RotationTrainer.Core;
using RotationTrainer.Core.Tests.Fakes;

namespace RotationTrainer.Core.Tests;

public sealed class LibraryFileTests : IDisposable
{
    private const string Good =
        """{"id":"pct-op","job":"PCT","kind":"opener","name":"PCT Opener","steps":[{"action":1}]}""";
    private const string Bad =
        """{"id":"bad","job":"XXX","kind":"opener","name":"Broken","steps":[{"action":1}]}""";

    private readonly string _dir = Directory.CreateTempSubdirectory("rt-libfile-").FullName;
    private readonly List<string> _warnings = new();

    private string LibraryPath => Path.Combine(_dir, "library.json");

    private LibraryFile NewFile() => new(LibraryPath, new FakeCatalog(), _warnings.Add);

    private string[] Backups() => Directory.GetFiles(_dir, "library.*.bak.json").Order().ToArray();

    private static Rotation Loop(string id) =>
        new(id, "PCT", 100, RotationKind.Loop, id, new[] { new Step(StepType.Action, 1, "Rainbow Drip") });

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void MissingFileGivesEmptyLibraryThatSaves()
    {
        var library = NewFile().Load();
        Assert.Empty(library.All);
        library.Save(Loop("l1"));
        Assert.Contains("\"l1\"", File.ReadAllText(LibraryPath));
        Assert.Empty(Backups());
    }

    [Fact]
    public void LoadWithErrorsBacksUpOriginal()
    {
        var original = $"[{Good},{Bad}]";
        File.WriteAllText(LibraryPath, original);

        var library = NewFile().Load();

        Assert.Single(library.All);
        Assert.Equal(original, File.ReadAllText(Assert.Single(Backups())));
        Assert.NotEmpty(_warnings);
    }

    [Fact]
    public void LaterErrorLoadNeverOverwritesEarlierBackup()
    {
        var original = $"[{Good},{Bad}]";
        File.WriteAllText(LibraryPath, original);
        NewFile().Load().Save(Loop("l1"));      // auto-save shrinks library.json

        File.WriteAllText(LibraryPath, File.ReadAllText(LibraryPath).Replace("\"PCT\"", "\"XXX\""));
        NewFile().Load();                        // errors again, in the same second

        var backups = Backups();
        Assert.Equal(2, backups.Length);
        Assert.Contains(backups, b => File.ReadAllText(b) == original);
    }

    [Fact]
    public void UnreadableFileIsNeverOverwritten()
    {
        var original = $"[{Good}]";
        File.WriteAllText(LibraryPath, original);
        LibraryFile file = NewFile();
        RotationLibrary library;
        using (new FileStream(LibraryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            library = file.Load();

        Assert.Empty(library.All);
        Assert.Contains(_warnings, w => w.StartsWith("Could not read"));

        library.Save(Loop("l1"));
        file.Save(library);
        Assert.Equal(original, File.ReadAllText(LibraryPath));
    }
}
