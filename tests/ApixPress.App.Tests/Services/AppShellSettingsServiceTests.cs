using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Implementations;
namespace ApixPress.App.Tests.Services;

public sealed class AppShellSettingsServiceTests
{
    [Fact]
    public async Task AppShellSettingsService_ShouldPersistSettingsToJsonFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"app-shell-settings-{Guid.NewGuid():N}.json");
        var service = new AppShellSettingsService(tempFile);

        try
        {
            var saveResult = await service.SaveAsync(new AppShellSettingsDto
            {
                StorageDirectoryPath = @"D:\ApixPressData",
                RequestTimeoutMilliseconds = 45000,
                ValidateSslCertificate = false,
                AutoFollowRedirects = false,
                SendNoCacheHeader = true,
                EnableVerboseLogging = true,
                EnableUpdateReminder = false
            }, CancellationToken.None);

            Assert.True(saveResult.IsSuccess);

            var loadResult = await service.LoadAsync(CancellationToken.None);

            Assert.True(loadResult.IsSuccess);
            Assert.NotNull(loadResult.Data);
            Assert.Equal(@"D:\ApixPressData", loadResult.Data!.StorageDirectoryPath);
            Assert.Equal(45000, loadResult.Data.RequestTimeoutMilliseconds);
            Assert.False(loadResult.Data.ValidateSslCertificate);
            Assert.False(loadResult.Data.AutoFollowRedirects);
            Assert.True(loadResult.Data.SendNoCacheHeader);
            Assert.True(loadResult.Data.EnableVerboseLogging);
            Assert.False(loadResult.Data.EnableUpdateReminder);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task AppShellSettingsService_ShouldReuseCachedSettingsAfterInitialLoad()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"app-shell-settings-cache-{Guid.NewGuid():N}.json");
        var service = new AppShellSettingsService(tempFile);

        try
        {
            await File.WriteAllTextAsync(tempFile, """
                                              {
                                                "requestTimeoutMilliseconds": 30000,
                                                "validateSslCertificate": true,
                                                "autoFollowRedirects": true,
                                                "sendNoCacheHeader": false,
                                                "enableVerboseLogging": false,
                                                "enableUpdateReminder": true
                                              }
                                              """);

            var firstLoad = await service.LoadAsync(CancellationToken.None);
            Assert.True(firstLoad.IsSuccess);
            Assert.Equal(30000, firstLoad.Data!.RequestTimeoutMilliseconds);

            await File.WriteAllTextAsync(tempFile, """
                                              {
                                                "requestTimeoutMilliseconds": 9000,
                                                "validateSslCertificate": false,
                                                "autoFollowRedirects": false,
                                                "sendNoCacheHeader": true,
                                                "enableVerboseLogging": true,
                                                "enableUpdateReminder": false
                                              }
                                              """);

            var secondLoad = await service.LoadAsync(CancellationToken.None);

            Assert.True(secondLoad.IsSuccess);
            Assert.Equal(30000, secondLoad.Data!.RequestTimeoutMilliseconds);
            Assert.True(secondLoad.Data.ValidateSslCertificate);
            Assert.True(secondLoad.Data.AutoFollowRedirects);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
