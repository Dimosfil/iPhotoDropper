namespace iPhotoDropper.App;

public sealed class AppPaths
{
    public AppPaths()
    {
        LocalDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "iPhotoDropper");
        LogFolder = Path.Combine(LocalDataFolder, "logs");
        StateFolder = Path.Combine(LocalDataFolder, "state");
    }

    public string LocalDataFolder { get; }

    public string LogFolder { get; }

    public string StateFolder { get; }
}
