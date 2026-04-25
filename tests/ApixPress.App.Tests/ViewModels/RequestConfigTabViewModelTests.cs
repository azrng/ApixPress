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

    [Fact]
    public void BuildRequestSnapshot_ShouldSerializeOnlyEnabledFormFields()
    {
        var viewModel = new RequestConfigTabViewModel
        {
            SelectedBodyMode = BodyModes.FormUrlEncoded
        };

        viewModel.FormFields.Add(new RequestParameterItemViewModel
        {
            Name = "enabled",
            Value = "yes",
            IsEnabled = true
        });
        viewModel.FormFields.Add(new RequestParameterItemViewModel
        {
            Name = "disabled",
            Value = "no",
            IsEnabled = false
        });

        var snapshot = viewModel.BuildRequestSnapshot("endpoint-1", "POST", "/submit");

        Assert.Equal("enabled=yes", snapshot.BodyContent);
    }

    [Fact]
    public void ApplySnapshot_ShouldRestoreParameterEnabledState()
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.ApplySnapshot(new RequestSnapshotDto
        {
            QueryParameters =
            [
                new RequestKeyValueDto { Name = "enabled", Value = "1", IsEnabled = true },
                new RequestKeyValueDto { Name = "disabled", Value = "0", IsEnabled = false }
            ]
        });

        Assert.True(viewModel.QueryParameters[0].IsEnabled);
        Assert.False(viewModel.QueryParameters[1].IsEnabled);
    }

    private static void AssertResetNotification(NotifyCollectionChangedEventArgs e, ref int count)
    {
        count++;
        Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
    }
}
