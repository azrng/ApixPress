using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace ApixPress.App.Tests.ViewModels;

public sealed class InteractionRecoveryTests
{
    [Fact]
    public void ReopenClosedTab_ShouldRestoreUnsavedEditorAndResponse()
    {
        using var workspace = new ProjectWorkspaceTabsViewModel(() => { }, new WeakReferenceMessenger());
        var tab = workspace.CreateWorkspaceTab(true);
        tab.ConfigureAsQuickRequest();
        tab.RequestUrl = "https://example.test/unsaved";
        tab.ConfigTab.RequestBody = "draft";
        tab.ResponseSection.BodyText = "previous response";
        workspace.CloseWorkspaceTabCommand.Execute(tab);

        workspace.RestoreClosedWorkspaceTabCommand.Execute(null);

        Assert.Same(tab, workspace.ActiveWorkspaceTab);
        Assert.True(tab.HasUnsavedChanges);
        Assert.Equal("draft", tab.ConfigTab.RequestBody);
        Assert.Equal("previous response", tab.ResponseSection.BodyText);
        Assert.False(workspace.RestoreClosedWorkspaceTabCommand.CanExecute(null));
    }

    [Fact]
    public void DeletedRequest_ShouldNotBeRestoredFromClosedTabs()
    {
        using var workspace = new ProjectWorkspaceTabsViewModel(() => { }, new WeakReferenceMessenger());
        var tab = workspace.CreateWorkspaceTab(true);
        tab.ApplySavedRequest(new RequestCaseDto { Id = "deleted", EntryType = "quick-request", RequestSnapshot = new() { Url = "https://example.test" } });
        workspace.CloseWorkspaceTabCommand.Execute(tab);
        workspace.CloseTabsForDeletedCases([new RequestCaseDto { Id = "deleted" }]);

        Assert.False(workspace.RestoreClosedWorkspaceTabCommand.CanExecute(null));
    }

    [Fact]
    public void ResponseSearch_ShouldNavigateAndWrapCaseInsensitiveMatches()
    {
        using var response = new ResponseSectionViewModel { BodyText = "Order order ORDER", BodySearchText = "order" };
        Assert.Equal("1 / 3", response.BodySearchResultText);
        Assert.Equal(0, response.CurrentBodySearchMatchOffset);
        response.NextBodySearchMatchCommand.Execute(null);
        Assert.Equal(6, response.CurrentBodySearchMatchOffset);
        response.PreviousBodySearchMatchCommand.Execute(null);
        response.PreviousBodySearchMatchCommand.Execute(null);
        Assert.Equal(12, response.CurrentBodySearchMatchOffset);
        response.BodySearchText = "missing";
        Assert.False(response.HasBodySearchMatches);
        Assert.Equal(-1, response.CurrentBodySearchMatchOffset);
    }

    [Fact]
    public async Task ClearingPendingResponseSearch_ShouldClearMatches()
    {
        using var response = new ResponseSectionViewModel { BodyText = new string('a', 20000), BodySearchText = "a" };
        response.BodySearchText = "";
        await Task.Delay(350);
        Assert.Empty(response.BodySearchMatchOffsets);
        Assert.Empty(response.BodySearchResultText);
        Assert.False(response.NextBodySearchMatchCommand.CanExecute(null));
    }

    [Fact]
    public void ClosedHistory_ShouldKeepOnlyTenAndClearOnReset()
    {
        using var workspace = new ProjectWorkspaceTabsViewModel(() => { }, new WeakReferenceMessenger());
        for (var i = 0; i < 12; i++)
        {
            var tab = workspace.CreateWorkspaceTab(true);
            tab.ConfigureAsQuickRequest();
            tab.RequestUrl = $"https://example.test/{i}";
            workspace.CloseWorkspaceTabCommand.Execute(tab);
        }
        for (var i = 11; i >= 2; i--)
        {
            workspace.RestoreClosedWorkspaceTabCommand.Execute(null);
            Assert.Equal($"https://example.test/{i}", workspace.ActiveWorkspaceTab!.RequestUrl);
        }
        Assert.False(workspace.RestoreClosedWorkspaceTabCommand.CanExecute(null));
        workspace.CloseWorkspaceTabCommand.Execute(workspace.ActiveWorkspaceTab);
        workspace.ResetToLanding();
        Assert.False(workspace.RestoreClosedWorkspaceTabCommand.CanExecute(null));
    }
}
