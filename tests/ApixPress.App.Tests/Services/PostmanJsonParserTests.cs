using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Tests.Services;

public sealed class PostmanJsonParserTests
{
    [Fact]
    public void Parse_ShouldImportNestedRequestsAuthQueryAndJsonBody()
    {
        const string json = """
                            {
                              "info": {
                                "name": "AIProvder",
                                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
                              },
                              "variable": [
                                { "key": "baseUrl", "value": "https://api.example.com" }
                              ],
                              "item": [
                                {
                                  "name": "对话",
                                  "item": [
                                    {
                                      "name": "发送消息",
                                      "request": {
                                        "auth": {
                                          "type": "bearer",
                                          "bearer": [{ "key": "token", "value": "{{apiKey}}" }]
                                        },
                                        "method": "POST",
                                        "header": [{ "key": "content-type", "value": "application/json" }],
                                        "url": {
                                          "raw": "{{baseUrl}}/chat/completions?stream=true",
                                          "query": [{ "key": "stream", "value": "true" }]
                                        },
                                        "body": {
                                          "mode": "raw",
                                          "raw": "{\"message\":\"你好呀\"}",
                                          "options": { "raw": { "language": "json" } }
                                        }
                                      }
                                    }
                                  ]
                                }
                              ]
                            }
                            """;

        var result = ImportDocumentParser.Parse(json, "FILE", "AIProvder.postman.json");

        Assert.Equal("AIProvder", result.Document.Name);
        Assert.Equal("POSTMAN", result.Document.SourceType);
        Assert.Equal("https://api.example.com", result.Document.BaseUrl);
        var endpoint = Assert.Single(result.Endpoints);
        Assert.Equal("对话", endpoint.GroupName);
        Assert.Equal("发送消息", endpoint.Name);
        Assert.Equal("POST", endpoint.Method);
        Assert.Equal("/chat/completions", endpoint.Path);
        Assert.Equal(BodyModes.RawJson, endpoint.RequestBodyMode);
        Assert.Contains("你好呀", endpoint.RequestBodyTemplate);
        Assert.Contains(result.Parameters, item => item.ParameterType == "Query" && item.Name == "stream");
        Assert.Contains(result.Parameters, item => item.ParameterType == "Header" && item.Name == "Authorization" && item.DefaultValue == "Bearer {{apiKey}}");
        Assert.Contains(result.Parameters, item => item.ParameterType == "Header" && item.Name == "content-type");
    }

    [Fact]
    public void Parse_ShouldStripLeadingBaseUrlVariableIntoRelativePath()
    {
        const string json = """
                            {
                              "info": {
                                "name": "变量路径集合",
                                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
                              },
                              "variable": [
                                { "key": "baseUrl", "value": "https://api.example.com" }
                              ],
                              "item": [
                                { "name": "无raw的URL对象", "request": { "method": "GET", "url": { "host": ["{{baseUrl}}"], "path": ["v1", "models"] } } },
                                { "name": "独占baseUrl", "request": { "method": "GET", "url": "{{baseUrl}}" } },
                                { "name": "大小写变体", "request": { "method": "GET", "url": "{{BaseURL}}/health" } },
                                { "name": "非开头变量保留", "request": { "method": "GET", "url": "https://api.example.com/{{version}}/chat" } }
                              ]
                            }
                            """;

        var result = PostmanJsonParser.Parse(json, "FILE", "variables.postman.json");

        Assert.Equal("/v1/models", result.Endpoints[0].Path);
        Assert.Equal("/", result.Endpoints[1].Path);
        Assert.Equal("/health", result.Endpoints[2].Path);
        Assert.Equal("https://api.example.com/{{version}}/chat", result.Endpoints[3].Path);
    }

    [Fact]
    public void Parse_ShouldPreserveFormDataFileReferencesAndDisabledFields()
    {
        const string json = """
                            {
                              "info": {
                                "name": "上传集合",
                                "schema": "https://schema.getpostman.com/json/collection/v2.0.0/collection.json"
                              },
                              "item": [
                                {
                                  "name": "上传",
                                  "request": {
                                    "method": "POST",
                                    "url": "https://api.example.com/upload",
                                    "body": {
                                      "mode": "formdata",
                                      "formdata": [
                                        { "key": "title", "value": "中文", "type": "text" },
                                        { "key": "file", "type": "file", "src": "C:/temp/demo.txt" },
                                        { "key": "ignored", "value": "x", "type": "text", "disabled": true }
                                      ]
                                    }
                                  }
                                }
                              ]
                            }
                            """;

        var result = PostmanJsonParser.Parse(json, "FILE", "upload.postman.json");
        var endpoint = Assert.Single(result.Endpoints);

        Assert.Equal(BodyModes.FormData, endpoint.RequestBodyMode);
        Assert.Contains("C:/temp/demo.txt", endpoint.RequestBodyTemplate);
        Assert.Contains("\"IsEnabled\": false", endpoint.RequestBodyTemplate);
    }

    [Fact]
    public void Parse_ShouldReadBinaryFileBodyObject()
    {
        const string json = """
                            {
                              "info": {
                                "name": "二进制集合",
                                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
                              },
                              "item": [
                                {
                                  "name": "上传文件",
                                  "request": {
                                    "method": "PUT",
                                    "url": "https://api.example.com/blob",
                                    "body": {
                                      "mode": "file",
                                      "file": { "src": "C:/temp/blob.bin" }
                                    }
                                  }
                                }
                              ]
                            }
                            """;

        var result = PostmanJsonParser.Parse(json, "FILE", "binary.postman.json");
        var endpoint = Assert.Single(result.Endpoints);

        Assert.Equal(BodyModes.Binary, endpoint.RequestBodyMode);
        Assert.Equal("C:/temp/blob.bin", endpoint.RequestBodyTemplate);
    }
}
