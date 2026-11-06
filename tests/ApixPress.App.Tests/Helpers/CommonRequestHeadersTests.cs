using ApixPress.App.Helpers;

namespace ApixPress.App.Tests.Helpers;

public sealed class CommonRequestHeadersTests
{
    [Fact]
    public void Names_ShouldCoverMostCommonRequestHeaders()
    {
        Assert.Contains("Authorization", CommonRequestHeaders.Names);
        Assert.Contains("Content-Type", CommonRequestHeaders.Names);
        Assert.Contains("Accept", CommonRequestHeaders.Names);
        Assert.Contains("User-Agent", CommonRequestHeaders.Names);
        Assert.Contains("Cookie", CommonRequestHeaders.Names);
    }

    [Fact]
    public void Names_ShouldBeUniqueIgnoringCase()
    {
        // Header 名大小写不敏感，候选里出现大小写变体会在下拉中表现为重复项
        var distinctCount = CommonRequestHeaders.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count();

        Assert.Equal(CommonRequestHeaders.Names.Count, distinctCount);
    }

    [Fact]
    public void Names_ShouldBeValidHttpFieldNames()
    {
        // RFC 9110 field-name 即 token：选中任一候选发送请求时，不会因非法字符被 HttpClient 拒绝
        Assert.All(CommonRequestHeaders.Names, name => Assert.Matches("^[!#$%&'*+.^_`|~0-9A-Za-z-]+$", name));
    }
}
