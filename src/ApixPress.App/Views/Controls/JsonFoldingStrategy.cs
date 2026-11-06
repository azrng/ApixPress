using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace ApixPress.App.Views.Controls;

/// <summary>
/// JSON 折叠策略：栈扫描对象/数组括号生成折叠区间（默认全部展开），字符串字面量内的括号不参与配对。
/// 仅当正文首字符为 { 或 [（pretty JSON）时产出折叠，XML 与纯文本自动跳过，调用方据此决定折叠边栏显隐。
/// </summary>
internal sealed class JsonFoldingStrategy
{
    /// <summary>判定正文是否按 JSON 处理：展示正文由缩进格式化保证首字符即根括号。</summary>
    public static bool LooksLikeJson(string text)
    {
        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        return start < text.Length && (text[start] == '{' || text[start] == '[');
    }

    /// <summary>生成折叠区间；只对跨行括号建折叠，单行对象/数组无折叠价值。</summary>
    public IEnumerable<NewFolding> CreateFoldings(TextDocument document)
    {
        var text = document.Text;
        if (text.Length == 0 || !LooksLikeJson(text))
        {
            return [];
        }

        var foldings = new List<NewFolding>();
        var openOffsets = new Stack<int>();
        var inString = false;
        var escaped = false;
        for (var offset = 0; offset < text.Length; offset++)
        {
            var c = text[offset];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c is '{' or '[')
            {
                openOffsets.Push(offset);
            }
            else if (c is '}' or ']' && openOffsets.Count > 0)
            {
                var startOffset = openOffsets.Pop();
                var startLine = document.GetLineByOffset(startOffset).LineNumber;
                var endLine = document.GetLineByOffset(offset).LineNumber;
                if (endLine > startLine)
                {
                    foldings.Add(new NewFolding(startOffset, offset + 1)
                    {
                        Name = c == '}' ? "{…}" : "[…]",
                        DefaultClosed = false
                    });
                }
            }
        }

        // UpdateFoldings 契约要求按起点升序（栈出序为逆序）
        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }
}
