using ApixPress.App.Tests.ViewModels;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class EnvironmentPanelViewModelTests
{
    [Fact]
    public void EnvironmentItemVariables_ShouldUpdateDerivedState()
    {
        var viewModel = new EnvironmentPanelViewModel(new ViewModelSharedTestDoubles.FakeEnvironmentVariableService());
        var environment = new ProjectEnvironmentItemViewModel { Id = "env-1", Name = "开发" };
        viewModel.Environments.Add(environment);

        Assert.False(environment.HasVariables);
        Assert.Contains("变量 0", environment.VariableCountText);

        environment.Variables.Add(new EnvironmentVariableItemViewModel
        {
            EnvironmentId = "env-1",
            Key = "token",
            Value = "abc"
        });

        Assert.True(environment.HasVariables);
        Assert.Contains("变量 1", environment.VariableCountText);

        environment.Variables.Clear();

        Assert.False(environment.HasVariables);
        Assert.Contains("变量 0", environment.VariableCountText);
    }

    [Fact]
    public async Task ApplyImportedBaseUrlAsync_ShouldUpdateSelectedEnvironmentBaseUrl()
    {
        var service = new ViewModelSharedTestDoubles.FakeEnvironmentVariableService
        {
            ApplyImportedBaseUrlEnabled = true
        };
        var viewModel = new EnvironmentPanelViewModel(service);
        await viewModel.LoadProjectAsync("proj-1");
        Assert.NotNull(viewModel.SelectedEnvironment);

        var applied = await viewModel.ApplyImportedBaseUrlAsync("http://localhost:5000");

        Assert.True(applied);
        Assert.Equal("http://localhost:5000", viewModel.SelectedEnvironment!.BaseUrl);
        Assert.Equal("http://localhost:5000", service.AppliedImportedBaseUrls.Single().BaseUrl);
    }

    [Fact]
    public async Task ApplyImportedBaseUrlAsync_ShouldKeepBaseUrlWhenServiceSkipsFill()
    {
        var service = new ViewModelSharedTestDoubles.FakeEnvironmentVariableService();
        var viewModel = new EnvironmentPanelViewModel(service);
        await viewModel.LoadProjectAsync("proj-1");
        var originalBaseUrl = viewModel.SelectedEnvironment!.BaseUrl;

        var applied = await viewModel.ApplyImportedBaseUrlAsync("http://localhost:5000");

        Assert.False(applied);
        Assert.Equal(originalBaseUrl, viewModel.SelectedEnvironment.BaseUrl);
    }

    [Fact]
    public async Task ApplyImportedBaseUrlAsync_ShouldSkipEmptyBaseUrl()
    {
        var service = new ViewModelSharedTestDoubles.FakeEnvironmentVariableService();
        var viewModel = new EnvironmentPanelViewModel(service);
        await viewModel.LoadProjectAsync("proj-1");

        var applied = await viewModel.ApplyImportedBaseUrlAsync(string.Empty);

        Assert.False(applied);
        Assert.Empty(service.AppliedImportedBaseUrls);
    }
}
