using System.Collections.Specialized;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Tests.ViewModels;

public sealed class RequestConfigTabViewModelTests
{
    [Fact]
    public void ApplySnapshot_ShouldReplaceParameterCollectionsWithSingleResetNotification()
    {
        var viewModel = new RequestConfigTabViewModel();
        var queryChanges = 0;
        var pathChanges = 0;
        var headerChanges = 0;

        viewModel.QueryParameters.CollectionChanged += (_, e) => AssertResetNotification(e, ref queryChanges);
        viewModel.PathParameters.CollectionChanged += (_, e) => AssertResetNotification(e, ref pathChanges);
        viewModel.Headers.CollectionChanged += (_, e) => AssertResetNotification(e, ref headerChanges);

        viewModel.ApplySnapshot(new RequestSnapshotDto
        {
            QueryParameters =
            [
                new RequestKeyValueDto { Name = "page", Value = "1" },
                new RequestKeyValueDto { Name = "size", Value = "20" }
            ],
            PathParameters =
            [
                new RequestKeyValueDto { Name = "id", Value = "1001" }
            ],
            Headers =
            [
                new RequestKeyValueDto { Name = "Authorization", Value = "Bearer token" },
                new RequestKeyValueDto { Name = "X-Trace-Id", Value = "trace-1" }
            ]
        });

        Assert.Equal(1, queryChanges);
        Assert.Single(viewModel.PathParameters);
        Assert.Equal(1, pathChanges);
        Assert.Equal(1, headerChanges);
        Assert.Equal(2, viewModel.QueryParameters.Count);
        Assert.Equal(2, viewModel.Headers.Count);
    }

    private static void AssertResetNotification(NotifyCollectionChangedEventArgs e, ref int count)
    {
        count++;
        Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
    }
}
