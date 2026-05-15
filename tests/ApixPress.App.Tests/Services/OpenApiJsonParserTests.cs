using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using System.Text.Json;

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

    [Fact]
    public void Parse_ShouldBuildJsonBodyTemplateFromSchema()
    {
        const string json = """
                            {
                              "openapi": "3.0.1",
                              "info": { "title": "Demo API" },
                              "paths": {
                                "/users/page": {
                                  "post": {
                                    "summary": "获取用户列表",
                                    "requestBody": {
                                      "content": {
                                        "application/json": {
                                          "schema": {
                                            "type": "object",
                                            "properties": {
                                              "pageIndex": { "type": "integer", "default": 1 },
                                              "pageSize": { "type": "integer", "default": 20 },
                                              "keyword": { "type": "string" }
                                            }
                                          }
                                        }
                                      }
                                    }
                                  }
                                }
                              }
                            }
                            """;

        var result = OpenApiJsonParser.Parse(json, "FILE", "demo.json");

        var endpoint = Assert.Single(result.Endpoints);
        Assert.Equal(BodyModes.RawJson, endpoint.RequestBodyMode);
        Assert.Contains("\"pageIndex\": 1", endpoint.RequestBodyTemplate);
        Assert.Contains("\"pageSize\": 20", endpoint.RequestBodyTemplate);
        Assert.Contains("\"keyword\": \"string\"", endpoint.RequestBodyTemplate);
    }

    [Fact]
    public void Parse_ShouldBuildFormDataTemplateFromSchema()
    {
        const string json = """
                            {
                              "openapi": "3.0.1",
                              "info": { "title": "Demo API" },
                              "paths": {
                                "/upload": {
                                  "post": {
                                    "summary": "上传",
                                    "requestBody": {
                                      "content": {
                                        "multipart/form-data": {
                                          "schema": {
                                            "type": "object",
                                            "properties": {
                                              "file": { "type": "string", "format": "binary" },
                                              "userId": { "type": "string", "example": "u-1" }
                                            }
                                          }
                                        }
                                      }
                                    }
                                  }
                                }
                              }
                            }
                            """;

        var result = OpenApiJsonParser.Parse(json, "FILE", "demo.json");

        var endpoint = Assert.Single(result.Endpoints);
        Assert.Equal(BodyModes.FormData, endpoint.RequestBodyMode);
        Assert.Contains("file=", endpoint.RequestBodyTemplate);
        Assert.Contains("userId=u-1", endpoint.RequestBodyTemplate);
    }

    [Fact]
    public void Parse_ShouldBuildJsonBodyTemplateWhenSchemaReferenceIsReusedBySiblingProperties()
    {
        const string json = """
                            {
                              "openapi": "3.0.1",
                              "info": { "title": "Demo API" },
                              "paths": {
                                "/orders": {
                                  "post": {
                                    "requestBody": {
                                      "content": {
                                        "application/json": {
                                          "schema": {
                                            "type": "object",
                                            "properties": {
                                              "billingAddress": { "$ref": "#/components/schemas/Address" },
                                              "shippingAddress": { "$ref": "#/components/schemas/Address" }
                                            }
                                          }
                                        }
                                      }
                                    }
                                  }
                                }
                              },
                              "components": {
                                "schemas": {
                                  "Address": {
                                    "type": "object",
                                    "properties": {
                                      "city": { "type": "string", "example": "上海" }
                                    }
                                  }
                                }
                              }
                            }
                            """;

        var result = OpenApiJsonParser.Parse(json, "FILE", "demo.json");

        var endpoint = Assert.Single(result.Endpoints);
        using var template = JsonDocument.Parse(endpoint.RequestBodyTemplate);
        Assert.Equal("上海", template.RootElement.GetProperty("billingAddress").GetProperty("city").GetString());
        Assert.Equal("上海", template.RootElement.GetProperty("shippingAddress").GetProperty("city").GetString());
    }

    [Fact]
    public void Parse_ShouldStopExpandingCircularSchemaReference()
    {
        const string json = """
                            {
                              "openapi": "3.0.1",
                              "info": { "title": "Demo API" },
                              "paths": {
                                "/users": {
                                  "post": {
                                    "requestBody": {
                                      "content": {
                                        "application/json": {
                                          "schema": { "$ref": "#/components/schemas/User" }
                                        }
                                      }
                                    }
                                  }
                                }
                              },
                              "components": {
                                "schemas": {
                                  "User": {
                                    "type": "object",
                                    "properties": {
                                      "name": { "type": "string", "example": "张三" },
                                      "manager": { "$ref": "#/components/schemas/User" }
                                    }
                                  }
                                }
                              }
                            }
                            """;

        var result = OpenApiJsonParser.Parse(json, "FILE", "demo.json");

        var endpoint = Assert.Single(result.Endpoints);
        using var template = JsonDocument.Parse(endpoint.RequestBodyTemplate);
        Assert.Equal("张三", template.RootElement.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, template.RootElement.GetProperty("manager").ValueKind);
    }
}
