namespace ApixPress.App.Helpers;

public static class AppStoragePaths
{
    public const string DatabaseFileName = "ApixPress.db";

    public static string DefaultStorageDirectory =>
        Path.GetDirectoryName(WorkspacePaths.ResolveFromBaseDirectory(Path.Combine("data", DatabaseFileName)))
        ?? WorkspacePaths.ResolveFromBaseDirectory("data");

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
}
