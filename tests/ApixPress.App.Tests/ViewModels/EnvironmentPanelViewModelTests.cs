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
}
