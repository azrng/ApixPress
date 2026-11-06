using ApixPress.App.Views.Controls;
using AvaloniaEdit.Document;

namespace ApixPress.App.Tests.Views;

public sealed class JsonFoldingStrategyTests
{
    private readonly JsonFoldingStrategy _strategy = new();

    [Theory]
    [InlineData("{\"a\":1}", true)]
    [InlineData("[1,2,3]", true)]
    [InlineData("  {\n  \"a\": 1\n}", true)]
    [InlineData("<root><a>1</a></root>", false)]
    [InlineData("plain text", false)]
    [InlineData("", false)]
    public void LooksLikeJson_ShouldDetectJsonByFirstSignificantCharacter(string text, bool expected)
    {
        Assert.Equal(expected, JsonFoldingStrategy.LooksLikeJson(text));
    }

    [Fact]
    public void CreateFoldings_ShouldFoldMultiLineObjectsAndArrays()
    {
        var document = new TextDocument("{\n  \"list\": [\n    1,\n    2\n  ],\n  \"ok\": true\n}");

        var foldings = _strategy.CreateFoldings(document).ToArray();

        // 根对象 + list 数组各一个折叠（跨行才折叠）
        Assert.Equal(2, foldings.Length);
        Assert.Equal(0, foldings[0].StartOffset);
        Assert.False(foldings[0].DefaultClosed);
        Assert.Equal(document.Text.IndexOf('['), foldings[1].StartOffset);
    }

    [Fact]
    public void CreateFoldings_ShouldSkipSingleLineBraces()
    {
        var document = new TextDocument("{\n  \"a\": { \"b\": 1 },\n  \"c\": 2\n}");

        var foldings = _strategy.CreateFoldings(document).ToArray();

        // 内层单行对象不生成折叠，仅根对象跨行
        Assert.Single(foldings);
        Assert.Equal(0, foldings[0].StartOffset);
    }

    [Fact]
    public void CreateFoldings_ShouldIgnoreBracesInsideStrings()
    {
        var document = new TextDocument("{\n  \"tpl\": \"a{b[1]}c\",\n  \"esc\": \"x\\\"y\",\n  \"n\": 1\n}");

        var foldings = _strategy.CreateFoldings(document).ToArray();

        // 字符串内的 { [ } ] 与转义引号不参与配对，仅根对象折叠
        Assert.Single(foldings);
    }

    [Fact]
    public void CreateFoldings_ShouldReturnEmpty_ForXmlOrPlainText()
    {
        Assert.Empty(_strategy.CreateFoldings(new TextDocument("<root>\n  <a>1</a>\n</root>")));
        Assert.Empty(_strategy.CreateFoldings(new TextDocument("hello\nworld")));
    }

    [Fact]
    public void CreateFoldings_ShouldSortByStartOffset_ForNestedStructures()
    {
        var document = new TextDocument("[\n  {\"a\": 1},\n  {\"b\": {\"c\": 2}}\n]");

        var offsets = _strategy.CreateFoldings(document)
            .Select(folding => folding.StartOffset)
            .ToArray();

        Assert.Equal(offsets.OrderBy(offset => offset), offsets);
    }
}
