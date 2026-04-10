using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Implementations;

namespace ApixPress.App.Tests.Services;

public sealed class RequestExecutionServiceTests
{
    [Fact]
    public void BuildUrl_ShouldReplaceVariablesAndAppendParameters()
    {
        var request = new RequestSnapshotDto
        {
            Method = "GET",
            Url = "{{baseUrl}}/users/{id}",
            PathParameters =
            [
                new RequestKeyValueDto { Name = "id", Value = "42" }
            ],
            QueryParameters =
            [
                new RequestKeyValueDto { Name = "expand", Value = "{{scope}}" }
            ]
        };

        var url = RequestExecutionService.BuildUrl(request, new Dictionary<string, string>
        {
            ["baseUrl"] = "https://api.example.com",
            ["scope"] = "profile"
        });

        Assert.Equal("https://api.example.com/users/42?expand=profile", url);
    }

    [Fact]
    public void ReplaceVariables_ShouldKeepUnknownPlaceholders()
    {
        var result = RequestExecutionService.ReplaceVariables("Bearer {{token}} {{missing}}", new Dictionary<string, string>
        {
            ["token"] = "abc123"
        });

        Assert.Equal("Bearer abc123 {{missing}}", result);
    }
}
