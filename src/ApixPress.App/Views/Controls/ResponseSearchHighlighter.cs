using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ApixPress.App.Views.Controls;

/// <summary>只对可见行绘制搜索高亮，不为大正文生成额外控件。</summary>
internal sealed class ResponseSearchHighlighter(ResponseBodyEditor editor) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        var offsets = editor.SearchMatchOffsets;
        var length = editor.SearchMatchLength;
        if (length <= 0 || offsets.Count == 0) return;
        // 二分定位首个可能与当前行相交的匹配，避免每行重扫全文匹配列表。
        var low = 0;
        var high = offsets.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (offsets[middle] + length <= line.Offset) low = middle + 1;
            else high = middle;
        }
        for (var i = low; i < offsets.Count && offsets[i] < line.EndOffset; i++)
        {
            var start = Math.Max(offsets[i], line.Offset);
            var end = Math.Min(offsets[i] + length, line.EndOffset);
            if (end <= start) continue;
            var active = offsets[i] == editor.CurrentSearchMatchOffset;
            var background = editor.FindResource(active ? "Brush.Brand.Primary" : "Brush.Background.Selected") as IBrush;
            var foreground = editor.FindResource(active ? "Brush.Background.App" : "Brush.Text.Primary") as IBrush;
            ChangeLinePart(start, end, element =>
            {
                if (background is not null) element.TextRunProperties.SetBackgroundBrush(background);
                if (foreground is not null) element.TextRunProperties.SetForegroundBrush(foreground);
            });
        }
    }
}
