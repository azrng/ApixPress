using Avalonia;

namespace ApixPress.App.Views.Controls;

/// <summary>请求正文编辑器，复用可视区排版与字体资源，并把文档编辑同步回绑定属性。</summary>
public sealed class RequestBodyEditor : ResponseBodyEditor
{
    private bool _isUpdatingDocument;

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<RequestBodyEditor, string?>(nameof(PlaceholderText));

    /// <summary>正文为空时显示当前格式的输入提示。</summary>
    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public RequestBodyEditor()
    {
        IsReadOnly = false;
        // JSON、XML、Text 共用原始正文入口，保持原有无语法高亮的显示语义。
        SyntaxHighlighting = null;
        TextChanged += (_, _) =>
        {
            if (!_isUpdatingDocument)
            {
                SetCurrentValue(TextProperty, Document.Text);
            }
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == PlaceholderTextProperty)
        {
            Watermark = change.NewValue as string ?? string.Empty;
        }

        if (change.Property != TextProperty
            || string.Equals(Document.Text, change.NewValue as string ?? string.Empty, StringComparison.Ordinal))
        {
            base.OnPropertyChanged(change);
            return;
        }

        _isUpdatingDocument = true;
        try
        {
            base.OnPropertyChanged(change);
            // 外部回填替换整份正文时清理旧请求的撤销历史，避免跨接口恢复内容。
            CaretOffset = 0;
            Document.UndoStack.ClearAll();
        }
        finally
        {
            _isUpdatingDocument = false;
        }
    }
}
