using System.Text.RegularExpressions;

namespace ApixPress.App.Helpers;

internal static partial class RequestPathParameterTemplate
{
    private static readonly Regex BracedParameterRegex = new(
        @"(?<!\{)\{(?<name>[^{}]+)\}(?!\})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ColonParameterRegex = new(
        @"(?<prefix>/|^):(?<name>[A-Za-z_][A-Za-z0-9_.-]*)(?=/|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EnvironmentVariableRegex = new(
        @"\{\{[^{}]+\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> ExtractNames(string? url)
    {
        var path = GetPathPart(url ?? string.Empty);
        var names = BracedParameterRegex.Matches(path)
            .Select(match => (match.Index, Name: match.Groups["name"].Value.Trim()))
            .Concat(ColonParameterRegex.Matches(path)
                .Select(match => (match.Index, Name: match.Groups["name"].Value.Trim())))
            .OrderBy(match => match.Index)
            .Select(match => match.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name));

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string ReplaceValues(
        string url,
        IEnumerable<(string Name, string? Value)> parameters,
        Func<string, string>? resolveValue = null,
        bool requireValues = false)
    {
        var pathEnd = FindPathEnd(url);
        var path = url[..pathEnd];
        var suffix = url[pathEnd..];
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(name) && !values.ContainsKey(name.Trim()))
            {
                values[name.Trim()] = value ?? string.Empty;
            }
        }

        string ResolveValue(string name, string originalToken)
        {
            if (!values.TryGetValue(name, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                if (requireValues)
                {
                    throw new InvalidOperationException($"请填写路径参数“{name}”。");
                }

                return originalToken;
            }

            var resolved = resolveValue?.Invoke(rawValue) ?? rawValue;
            if (string.IsNullOrWhiteSpace(resolved) || EnvironmentVariableRegex.IsMatch(resolved))
            {
                if (requireValues)
                {
                    throw new InvalidOperationException($"路径参数“{name}”包含未解析的环境变量。");
                }

                return originalToken;
            }

            return Uri.EscapeDataString(resolved);
        }

        path = BracedParameterRegex.Replace(path, match =>
            ResolveValue(match.Groups["name"].Value.Trim(), match.Value));
        path = ColonParameterRegex.Replace(path, match =>
            match.Groups["prefix"].Value
            + ResolveValue(match.Groups["name"].Value.Trim(), match.Value[match.Groups["prefix"].Length..]));

        return path + suffix;
    }

    private static string GetPathPart(string url) => url[..FindPathEnd(url)];

    private static int FindPathEnd(string url)
    {
        var queryStart = url.IndexOf('?');
        var fragmentStart = url.IndexOf('#');
        if (queryStart < 0) return fragmentStart < 0 ? url.Length : fragmentStart;
        if (fragmentStart < 0) return queryStart;
        return Math.Min(queryStart, fragmentStart);
    }
}
