using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ApixPress.App.Views.Controls;

/// <summary>
/// JSON 语法高亮附加属性：把响应 Body 文本按 key / 字符串 / 数字 / 标点分色渲染到 TextBlock，
/// 颜色取自 Colors.axaml 的 Brush.Code.* 资源；非 JSON 文本退回纯文本展示。
/// </summary>
public static class JsonSyntaxHighlight
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Text", typeof(JsonSyntaxHighlight));

    // 超长响应一次性构建大量 Run 会拖慢渲染，退回纯文本
    private const int MaxHighlightLength = 200_000;

    private const int KindPlain = 0;
    private const int KindKey = 1;
    private const int KindString = 2;
    private const int KindNumber = 3;
    private const int KindPunctuation = 4;

    static JsonSyntaxHighlight()
    {
        TextProperty.Changed.AddClassHandler<TextBlock>((target, e) => Apply(target, e.NewValue as string));
    }

    public static string? GetText(TextBlock element) => element.GetValue(TextProperty);

    public static void SetText(TextBlock element, string? value) => element.SetValue(TextProperty, value);

    private static void Apply(TextBlock target, string? text)
    {
        target.Inlines ??= new InlineCollection();
        target.Inlines.Clear();

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var trimmedStart = text.TrimStart();
        if (text.Length > MaxHighlightLength
            || (trimmedStart.StartsWith('{') == false && trimmedStart.StartsWith('[') == false))
        {
            target.Inlines.Add(new Run(text));
            return;
        }

        AppendHighlighted(target, text);
    }

    private static void AppendHighlighted(TextBlock target, string text)
    {
        var inlines = target.Inlines!;
        var plainStart = 0;
        var index = 0;

        void FlushPlain(int end)
        {
            if (end > plainStart)
            {
                inlines.Add(new Run(text[plainStart..end]));
            }
        }

        while (index < text.Length)
        {
            var current = text[index];
            if (current == '"')
            {
                var close = FindClosingQuote(text, index);
                FlushPlain(index);
                var isKey = IsFollowedByColon(text, close);
                inlines.Add(CreateRun(target, text[index..(close + 1)], isKey ? KindKey : KindString));
                index = close + 1;
                plainStart = index;
            }
            else if (char.IsDigit(current)
                     || (current == '-' && index + 1 < text.Length && char.IsDigit(text[index + 1])))
            {
                FlushPlain(index);
                var end = ScanNumber(text, index);
                inlines.Add(CreateRun(target, text[index..end], KindNumber));
                index = end;
                plainStart = index;
            }
            else if (current is '{' or '}' or '[' or ']' or ':' or ',')
            {
                FlushPlain(index);
                inlines.Add(CreateRun(target, current.ToString(), KindPunctuation));
                index++;
                plainStart = index;
            }
            else
            {
                index++;
            }
        }

        FlushPlain(text.Length);
    }

    private static Run CreateRun(TextBlock target, string value, int kind)
    {
        var run = new Run(value);
        var brushKey = kind switch
        {
            KindKey => "Brush.Code.Key",
            KindString => "Brush.Code.String",
            KindNumber => "Brush.Code.Number",
            KindPunctuation => "Brush.Code.Punctuation",
            _ => null
        };

        if (brushKey is not null
            && target.TryFindResource(brushKey, out var resource)
            && resource is IBrush brush)
        {
            run.Foreground = brush;
        }

        return run;
    }

    private static int FindClosingQuote(string text, int start)
    {
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
                continue;
            }

            if (text[i] == '"')
            {
                return i;
            }
        }

        return text.Length - 1;
    }

    private static bool IsFollowedByColon(string text, int closeQuoteIndex)
    {
        for (var i = closeQuoteIndex + 1; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return c == ':';
        }

        return false;
    }

    private static int ScanNumber(string text, int start)
    {
        var index = start + 1;
        while (index < text.Length)
        {
            var c = text[index];
            if (char.IsDigit(c) || c is '.' or 'e' or 'E' or '+' or '-')
            {
                index++;
                continue;
            }

            break;
        }

        return index;
    }
}
