namespace ApixPress.App.Helpers;

public static class AppStoragePaths
{
    public const string DatabaseFileName = "ApixPress.db";
    private const string AppDirectoryName = "ApixPress";
    private const string DataDirectoryName = "data";

    public static string DefaultApplicationDataDirectory =>
        Path.Combine(ResolveUserApplicationDataRoot(), AppDirectoryName);

    public static string DefaultStorageDirectory =>
        Path.Combine(DefaultApplicationDataDirectory, DataDirectoryName);

    public static string ResolveDatabasePath(string? storageDirectoryPath)
    {
        var directory = ResolveStorageDirectory(storageDirectoryPath);
        return Path.Combine(directory, DatabaseFileName);
    }

    public static string ResolveStorageDirectory(string? storageDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(storageDirectoryPath))
        {
            return DefaultStorageDirectory;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(storageDirectoryPath.Trim());
        return Path.IsPathRooted(expandedPath)
            ? Path.GetFullPath(expandedPath)
            : WorkspacePaths.ResolveFromBaseDirectory(expandedPath);
    }

    private static string ResolveUserApplicationDataRoot()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApplicationData))
        {
            return localApplicationData;
        }

        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return !string.IsNullOrWhiteSpace(applicationData)
            ? applicationData
            : AppContext.BaseDirectory;
    }
}
