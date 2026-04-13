using ApixPress.App.Helpers;

namespace ApixPress.App.Tests.Services;

public sealed class OpenApiJsonParserTests
{
    [Fact]
    public void Parse_ShouldExtractDocumentEndpointAndParameters()
    {
        const string json = """
                            {
                              "openapi": "3.0.1",
                              "info": {
                                "title": "Demo API"
                              },
                              "servers": [
                                {
                                  "url": "https://api.example.com"
                                }
                              ],
                              "paths": {
                                "/users/{id}": {
                                  "get": {
                                    "summary": "获取用户",
                                    "tags": ["用户"],
                                    "parameters": [
                                      {
                                        "name": "id",
                                        "in": "path",
                                        "required": true,
                                        "schema": {
                                          "type": "string"
                                        }
                                      },
                                      {
                                        "name": "expand",
                                        "in": "query",
                                        "schema": {
                                          "default": "profile"
                                        }
                                      }
                                    ]
                                  }
                                }
                              }
                            }
                            """;

        var result = OpenApiJsonParser.Parse(json, "FILE", "demo.json");

        Assert.Equal("Demo API", result.Document.Name);
        Assert.Equal("https://api.example.com", result.Document.BaseUrl);
        Assert.Single(result.Endpoints);
        Assert.Equal("GET", result.Endpoints[0].Method);
        Assert.Equal("/users/{id}", result.Endpoints[0].Path);
        Assert.Equal(2, result.Parameters.Count);
        Assert.Contains(result.Parameters, item => item.ParameterType == "Path" && item.Name == "id");
        Assert.Contains(result.Parameters, item => item.ParameterType == "Query" && item.Name == "expand" && item.DefaultValue == "profile");
    }

    [Fact]
    public void Parse_ShouldFallbackBaseUrlFromImportUrl_WhenDocumentDoesNotDeclareServer()
    {
        const string json = """
                            {
                              "openapi": "3.0.4",
                              "info": {
                                "title": "SwaggerAPI"
                              },
                              "paths": {
                                "/api/users": {
                                  "get": {
                                    "summary": "获取用户列表"
                                  }
                                }
                              }
                            }
                            """;

        var result = OpenApiJsonParser.Parse(json, "URL", "http://localhost:5000/swagger/v1/swagger.json");

        Assert.Equal("http://localhost:5000", result.Document.BaseUrl);
    }
}
