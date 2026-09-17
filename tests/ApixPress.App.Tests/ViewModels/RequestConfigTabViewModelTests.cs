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
    public void FormFieldsState_ShouldTrackBodyModeAndCollectionChanges()
    {
        var viewModel = new RequestConfigTabViewModel();

        Assert.False(viewModel.HasFormFields);
        Assert.False(viewModel.ShowFormDataEmptyState);

        viewModel.SelectedBodyMode = BodyModes.FormData;

        Assert.False(viewModel.HasFormFields);
        Assert.True(viewModel.ShowFormDataEmptyState);

        viewModel.AddFormFieldCommand.Execute(null);

        Assert.True(viewModel.HasFormFields);
        Assert.False(viewModel.ShowFormDataEmptyState);

        viewModel.FormFields.Clear();

        Assert.False(viewModel.HasFormFields);
        Assert.True(viewModel.ShowFormDataEmptyState);

        viewModel.SelectedBodyMode = BodyModes.RawJson;

        Assert.False(viewModel.ShowFormDataEmptyState);
    }

    [Fact]
    public void ConfigurationEmptyStates_ShouldTrackParameterCollections()
    {
        var viewModel = new RequestConfigTabViewModel();

        Assert.True(viewModel.ShowQueryParametersEmptyState);
        Assert.True(viewModel.ShowHeadersEmptyState);

        viewModel.AddQueryParameterCommand.Execute(null);
        viewModel.AddHeaderCommand.Execute(null);

        Assert.True(viewModel.HasQueryParameters);
        Assert.True(viewModel.HasHeaders);
        Assert.False(viewModel.ShowQueryParametersEmptyState);
        Assert.False(viewModel.ShowHeadersEmptyState);
    }

    [Theory]
    [InlineData(BodyModes.None, true, "不发送请求体")]
    [InlineData(BodyModes.FormData, false, "multipart/form-data")]
    [InlineData(BodyModes.FormUrlEncoded, false, "URL 编码")]
    [InlineData(BodyModes.RawJson, false, "application/json")]
    public void BodyModeHint_ShouldDescribeSelectedMode(string mode, bool showEmptyState, string hintFragment)
    {
        var viewModel = new RequestConfigTabViewModel
        {
            SelectedBodyMode = mode
        };

        Assert.Equal(showEmptyState, viewModel.ShowBodyModeEmptyState);
        Assert.Contains(hintFragment, viewModel.BodyModeHint);
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

    [Fact]
    public void QueryParametersSelectionState_ShouldToggleAllQueryParameters()
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.QueryParameters.Add(new RequestParameterItemViewModel
        {
            Name = "pageIndex",
            IsEnabled = false
        });
        viewModel.QueryParameters.Add(new RequestParameterItemViewModel
        {
            Name = "pageSize",
            IsEnabled = false
        });

        viewModel.QueryParametersSelectionState = true;

        Assert.All(viewModel.QueryParameters, item => Assert.True(item.IsEnabled));
        Assert.True(viewModel.QueryParametersSelectionState);

        viewModel.QueryParametersSelectionState = false;

        Assert.All(viewModel.QueryParameters, item => Assert.False(item.IsEnabled));
        Assert.False(viewModel.QueryParametersSelectionState);
    }

    [Fact]
    public void QueryParametersSelectionState_ShouldExposeMixedStateWhenPartiallySelected()
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.QueryParameters.Add(new RequestParameterItemViewModel
        {
            Name = "keyword",
            IsEnabled = true
        });
        viewModel.QueryParameters.Add(new RequestParameterItemViewModel
        {
            Name = "pageSize",
            IsEnabled = false
        });

        Assert.Null(viewModel.QueryParametersSelectionState);

        viewModel.QueryParameters[1].IsEnabled = true;

        Assert.True(viewModel.QueryParametersSelectionState);
    }

    [Fact]
    public void ConfigPanelMaxHeight_ShouldStayCompactForEmptyParamsAndHeaders()
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.SelectedTabIndex = 0;
        Assert.Equal(288, viewModel.ConfigPanelMaxHeight);

        viewModel.SelectedTabIndex = 2;
        Assert.Equal(288, viewModel.ConfigPanelMaxHeight);
    }

    [Fact]
    public void ConfigPanelMaxHeight_ShouldExpandForBodyWithEditorAndPopulatedParameterTabs()
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.SelectedTabIndex = 1;
        Assert.Equal(288, viewModel.ConfigPanelMaxHeight);

        viewModel.SelectedBodyMode = BodyModes.RawJson;
        Assert.Equal(420, viewModel.ConfigPanelMaxHeight);

        viewModel.SelectedTabIndex = 0;
        viewModel.QueryParameters.Add(new RequestParameterItemViewModel());
        Assert.Equal(420, viewModel.ConfigPanelMaxHeight);

        viewModel.SelectedTabIndex = 2;
        Assert.Equal(288, viewModel.ConfigPanelMaxHeight);

        viewModel.Headers.Add(new RequestParameterItemViewModel());
        Assert.Equal(420, viewModel.ConfigPanelMaxHeight);
    }

    [Fact]
    public void ConfigPanelMaxHeight_ShouldStayCompactForEmptyFormBody()
    {
        var viewModel = new RequestConfigTabViewModel
        {
            SelectedTabIndex = 1,
            SelectedBodyMode = BodyModes.FormData
        };

        Assert.Equal(288, viewModel.ConfigPanelMaxHeight);

        viewModel.FormFields.Add(new RequestParameterItemViewModel());

        Assert.Equal(420, viewModel.ConfigPanelMaxHeight);
    }

    [Fact]
    public void PopulateFromEndpoint_ShouldApplyJsonBodyModeAndTemplate()
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.PopulateFromEndpoint(new ApiEndpointDto
        {
            Name = "获取用户列表",
            RequestBodyMode = BodyModes.RawJson,
            RequestBodyTemplate = "{\n  \"pageIndex\": 1\n}"
        });

        Assert.Equal(BodyModes.RawJson, viewModel.SelectedBodyMode);
        Assert.Equal("{\n  \"pageIndex\": 1\n}", viewModel.RequestBody);
        Assert.Empty(viewModel.FormFields);
    }

    [Fact]
    public void PopulateFromEndpoint_ShouldApplyFormBodyModeAndFields()
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.PopulateFromEndpoint(new ApiEndpointDto
        {
            Name = "上传",
            RequestBodyMode = BodyModes.FormData,
            RequestBodyTemplate = "file=&userId=u-1"
        });

        Assert.Equal(BodyModes.FormData, viewModel.SelectedBodyMode);
        Assert.Equal(2, viewModel.FormFields.Count);
        Assert.Contains(viewModel.FormFields, item => item.Name == "file" && item.Value == string.Empty);
        Assert.Contains(viewModel.FormFields, item => item.Name == "userId" && item.Value == "u-1");
    }

    [Theory]
    [InlineData(BodyModes.RawJson, "{\"model\":\"demo\"}", 0, 0, 1)]
    [InlineData(BodyModes.FormUrlEncoded, "field=1", 0, 0, 1)]
    [InlineData(BodyModes.FormUrlEncoded, "", 1, 0, 0)]
    [InlineData(BodyModes.None, "", 2, 0, 0)]
    [InlineData(BodyModes.None, "", 0, 2, 2)]
    [InlineData(BodyModes.None, "", 0, 0, 0)]
    public void ApplySnapshot_ShouldSelectDefaultTabByContent(
        string bodyMode,
        string bodyContent,
        int queryCount,
        int headerCount,
        int expectedTabIndex)
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.ApplySnapshot(new RequestSnapshotDto
        {
            Method = "POST",
            Url = "https://api.example.com",
            BodyMode = bodyMode,
            BodyContent = bodyContent,
            QueryParameters = Enumerable.Range(0, queryCount)
                .Select(i => new RequestKeyValueDto { Name = $"q{i}", Value = $"{i}" })
                .ToList(),
            Headers = Enumerable.Range(0, headerCount)
                .Select(i => new RequestKeyValueDto { Name = $"X-H{i}", Value = $"{i}" })
                .ToList()
        });

        Assert.Equal(expectedTabIndex, viewModel.SelectedTabIndex);
    }

    [Fact]
    public void Reset_ShouldRestoreDefaultParamsTab()
    {
        var viewModel = new RequestConfigTabViewModel();
        viewModel.ApplySnapshot(new RequestSnapshotDto
        {
            Method = "POST",
            BodyMode = BodyModes.RawText,
            BodyContent = "hello"
        });
        Assert.Equal(1, viewModel.SelectedTabIndex);

        viewModel.Reset();

        Assert.Equal(0, viewModel.SelectedTabIndex);
    }

    [Fact]
    public void BodyModes_ShouldIncludeBinaryOption()
    {
        // 对齐原型 03：Body 七模式（含 Binary）
        var viewModel = new RequestConfigTabViewModel();

        Assert.Equal(7, viewModel.BodyModeOptions.Count);
        Assert.Contains(viewModel.BodyModeOptions, option => option.Mode == BodyModes.Binary);
    }

    [Fact]
    public void BuildRequestSnapshot_FormDataWithFiles_ShouldCarryFormFieldsAndJsonBody()
    {
        var viewModel = new RequestConfigTabViewModel
        {
            SelectedBodyMode = BodyModes.FormData
        };
        viewModel.FormFields.Add(new RequestParameterItemViewModel
        {
            Name = "title",
            Value = "hello"
        });
        var fileField = new RequestParameterItemViewModel
        {
            Name = "file",
            FieldType = FormFieldTypes.File
        };
        fileField.Files.Add(new FormFieldFileViewModel { Path = @"C:\temp\a.txt", Name = "a.txt", SizeBytes = 12 });
        viewModel.FormFields.Add(fileField);

        var snapshot = viewModel.BuildRequestSnapshot("endpoint-1", "POST", "/upload");

        Assert.Equal(2, snapshot.FormFields.Count);
        Assert.Equal(FormFieldTypes.File, snapshot.FormFields[1].FieldType);
        Assert.Single(snapshot.FormFields[1].Files);
        // 含文件字段时 BodyContent 落 JSON，执行层按 FormFields 组装 multipart
        Assert.StartsWith("[", snapshot.BodyContent);
    }

    [Fact]
    public void BuildRequestSnapshot_FormUrlEncoded_ShouldExcludeFileFieldsFromLegacyBody()
    {
        var viewModel = new RequestConfigTabViewModel
        {
            SelectedBodyMode = BodyModes.FormUrlEncoded
        };
        viewModel.FormFields.Add(new RequestParameterItemViewModel { Name = "a", Value = "1" });
        var fileField = new RequestParameterItemViewModel { Name = "f", FieldType = FormFieldTypes.File };
        fileField.Files.Add(new FormFieldFileViewModel { Path = @"C:\a.txt", Name = "a.txt" });
        viewModel.FormFields.Add(fileField);

        var snapshot = viewModel.BuildRequestSnapshot("e", "POST", "/submit");

        // 残留的 file 字段不进 urlencoded 编码串；FormFields 保留完整编辑态
        Assert.Equal("a=1", snapshot.BodyContent);
        Assert.Equal(2, snapshot.FormFields.Count);
    }

    [Fact]
    public void ApplySnapshot_ShouldRoundTripFileFieldsThroughSnapshot()
    {
        var viewModel = new RequestConfigTabViewModel();
        var snapshot = new RequestSnapshotDto
        {
            Method = "POST",
            Url = "/upload",
            BodyMode = BodyModes.FormData,
            FormFields =
            [
                new FormFieldDto { Name = "title", Value = "hi", IsEnabled = false },
                new FormFieldDto
                {
                    Name = "file",
                    FieldType = FormFieldTypes.File,
                    Files = [new FormFieldFileDto { Path = @"C:\a.txt", Name = "a.txt", SizeBytes = 9 }]
                }
            ]
        };

        viewModel.ApplySnapshot(snapshot);

        Assert.Equal(2, viewModel.FormFields.Count);
        Assert.False(viewModel.FormFields[0].IsEnabled);
        Assert.True(viewModel.FormFields[1].IsFileField);
        Assert.Equal("a.txt", viewModel.FormFields[1].Files[0].Name);
        Assert.Equal("9 B", viewModel.FormFields[1].Files[0].SizeText);

        // 回写快照保留完整编辑态（禁用字段与文件引用）
        var rebuilt = viewModel.BuildRequestSnapshot("e", "POST", "/upload");
        Assert.Equal(2, rebuilt.FormFields.Count);
        Assert.False(rebuilt.FormFields[0].IsEnabled);
        Assert.Single(rebuilt.FormFields[1].Files);
    }

    [Fact]
    public void ApplySnapshot_LegacyBodyContent_ShouldParseTextFields()
    {
        var viewModel = new RequestConfigTabViewModel();

        viewModel.ApplySnapshot(new RequestSnapshotDto
        {
            Method = "POST",
            Url = "/form",
            BodyMode = BodyModes.FormData,
            BodyContent = "a=1&b=2"
        });

        Assert.Equal(2, viewModel.FormFields.Count);
        Assert.Equal("a", viewModel.FormFields[0].Name);
        Assert.Equal("1", viewModel.FormFields[0].Value);
    }

    [Fact]
    public void ToggleFormFieldType_ShouldOnlyWorkInFormDataMode()
    {
        var viewModel = new RequestConfigTabViewModel
        {
            SelectedBodyMode = BodyModes.FormUrlEncoded
        };
        viewModel.FormFields.Add(new RequestParameterItemViewModel
        {
            ParameterType = RequestParameterKind.FormField,
            Name = "f"
        });

        viewModel.ToggleFormFieldTypeCommand.Execute(viewModel.FormFields[0]);
        Assert.Equal(FormFieldTypes.Text, viewModel.FormFields[0].FieldType);

        viewModel.SelectedBodyMode = BodyModes.FormData;
        viewModel.ToggleFormFieldTypeCommand.Execute(viewModel.FormFields[0]);
        Assert.Equal(FormFieldTypes.File, viewModel.FormFields[0].FieldType);
        Assert.True(viewModel.FormFields[0].IsFileField);
    }

    [Fact]
    public async Task PickBinaryFile_ShouldFillRequestBodyAndFileState()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllBytesAsync(tempFile, new byte[2048]);
            var picker = new ViewModelSharedTestDoubles.FakeFilePickerService
            {
                PickOpenFilesResult =
                [
                    new FormFieldFileDto { Path = tempFile, Name = "demo.bin", SizeBytes = 2048 }
                ]
            };
            var viewModel = new RequestConfigTabViewModel(picker)
            {
                SelectedBodyMode = BodyModes.Binary
            };

            Assert.True(viewModel.ShowBinaryEmptyState);

            await viewModel.PickBinaryFileCommand.ExecuteAsync(null);

            Assert.Equal(tempFile, viewModel.RequestBody);
            Assert.Equal(System.IO.Path.GetFileName(tempFile), viewModel.BinaryFileName);
            Assert.Equal("2 KB", viewModel.BinaryFileSizeText);
            Assert.True(viewModel.HasBinaryFile);
            Assert.False(viewModel.ShowBinaryEmptyState);

            viewModel.ClearBinaryFileCommand.Execute(null);
            Assert.True(viewModel.ShowBinaryEmptyState);
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public void SelectDefaultTabByContent_BinaryWithFile_ShouldOpenBodyTab()
    {
        var viewModel = new RequestConfigTabViewModel
        {
            SelectedBodyMode = BodyModes.Binary,
            RequestBody = @"C:\temp\a.bin"
        };

        viewModel.SelectDefaultTabByContent();

        Assert.Equal(1, viewModel.SelectedTabIndex);
    }

    private static void AssertResetNotification(NotifyCollectionChangedEventArgs e, ref int count)
    {
        count++;
        Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
    }
}
