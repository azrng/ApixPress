using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;
using Azrng.Core.Results;

namespace ApixPress.App.Tests.ViewModels;

public sealed class RequestWorkspaceTabViewModelTests
{
    [Fact]
    public void ApplySavedRequest_ShouldClearResponseWhenSwitchingToAnotherRequest()
    {
        var viewModel = new RequestWorkspaceTabViewModel();
        viewModel.ApplySavedRequest(CreateInterface("interface-1", "/first"));
        ApplyResponse(viewModel.ResponseSection);

        viewModel.ApplySavedRequest(CreateInterface("interface-2", "/second"));

        Assert.False(viewModel.ResponseSection.HasResponse);
        Assert.True(viewModel.ResponseSection.ShowPlaceholder);
        Assert.Empty(viewModel.ResponseSection.BodyText);
    }

    [Fact]
    public void ApplySavedRequest_ShouldKeepResponseWhenReloadingTheSameRequest()
    {
        var viewModel = new RequestWorkspaceTabViewModel();
        viewModel.ApplySavedRequest(CreateInterface("interface-1", "/first"));
        ApplyResponse(viewModel.ResponseSection);

        viewModel.ApplySavedRequest(CreateInterface("interface-1", "/updated"));

        Assert.True(viewModel.ResponseSection.HasResponse);
        Assert.Contains("old", viewModel.ResponseSection.BodyText);
    }

    private static RequestCaseDto CreateInterface(string id, string url)
    {
        return new RequestCaseDto
        {
            Id = id,
            EntryType = "http-interface",
            Name = id,
            RequestSnapshot = new RequestSnapshotDto
            {
                Method = "GET",
                Url = url
            }
        };
    }

    private static void ApplyResponse(ResponseSectionViewModel responseSection)
    {
        responseSection.ApplyResult(
            ResultModel<ResponseSnapshotDto>.Success(new ResponseSnapshotDto
            {
                StatusCode = 200,
                Content = "{\"message\":\"old\"}"
            }),
            new RequestSnapshotDto());
    }
}
