using ApixPress.App.Helpers;
using ApixPress.App.Services.Implementations;

namespace ApixPress.App.Tests.Services;

public sealed class AppStoragePathsTests
{
    [Fact]
    public void DefaultStorageDirectory_ShouldUseUserApplicationDataDirectory()
    {
        var expectedRoot = ResolveExpectedUserApplicationDataRoot();

        Assert.StartsWith(expectedRoot, AppStoragePaths.DefaultApplicationDataDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("ApixPress", "data"), AppStoragePaths.DefaultStorageDirectory, StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(expectedRoot, AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            Assert.False(AppStoragePaths.DefaultStorageDirectory.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ResolveDatabasePath_WithEmptyStorageDirectory_ShouldUseDefaultStorageDirectory()
    {
        var databasePath = AppStoragePaths.ResolveDatabasePath(null);

        Assert.Equal(
            Path.Combine(AppStoragePaths.DefaultStorageDirectory, AppStoragePaths.DatabaseFileName),
            databasePath);
    }

    [Fact]
    public void ResolveDefaultSettingsFilePath_ShouldUseUserApplicationDataDirectory()
    {
        var settingsFilePath = AppShellSettingsService.ResolveDefaultSettingsFilePath();

        Assert.Equal(
            Path.Combine(AppStoragePaths.DefaultApplicationDataDirectory, "app-shell-settings.json"),
            settingsFilePath);
        if (!string.Equals(ResolveExpectedUserApplicationDataRoot(), AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            Assert.False(settingsFilePath.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string ResolveExpectedUserApplicationDataRoot()
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
