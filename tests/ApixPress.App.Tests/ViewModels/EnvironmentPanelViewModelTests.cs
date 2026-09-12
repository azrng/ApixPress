using ApixPress.App.Tests.ViewModels;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class EnvironmentPanelViewModelTests
{
    [Fact]
    public void EnvironmentVariablesState_ShouldTrackCollectionChanges()
    {
        var viewModel = new EnvironmentPanelViewModel(new ViewModelSharedTestDoubles.FakeEnvironmentVariableService());

        Assert.False(viewModel.HasEnvironmentVariables);
        Assert.True(viewModel.ShowEnvironmentVariablesEmptyState);

        viewModel.EnvironmentVariables.Add(new EnvironmentVariableItemViewModel
        {
            Key = "token",
            Value = "abc"
        });

        Assert.True(viewModel.HasEnvironmentVariables);
        Assert.False(viewModel.ShowEnvironmentVariablesEmptyState);

        viewModel.EnvironmentVariables.Clear();

        Assert.False(viewModel.HasEnvironmentVariables);
        Assert.True(viewModel.ShowEnvironmentVariablesEmptyState);
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
