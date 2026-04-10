namespace ApixPress.App.Helpers;

public static class WorkspacePaths
{
    public static string ResolveFromBaseDirectory(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
    }
}
