using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace ApixPress.App.Views.Controls;

/// <summary>
/// 响应 Body 只读查看器：基于 AvaloniaEdit 只做可视区排版，大正文（界面截断上限 256K 字符）不再整页构建 Run 阻塞 UI 线程。
/// JSON 语法高亮来自内嵌 Json.xshd；颜色与字体在挂树时从 Colors.axaml / Fonts.axaml 的 token 解析，不脱离设计 token 管线。
/// 应用锁定 Dark 主题无运行时切换，token 采用一次性解析（而非动态订阅）。
/// </summary>
public class ResponseBodyEditor : TextEditor
{
    // 下标一一对应：xshd 中的命名颜色 <-> 资源 token
    private static readonly string[] HighlightColorNames =
    [
        "JsonKey",
        "JsonString",
        "JsonNumber",
        "JsonPunctuation"
    ];

    private static readonly string[] HighlightResourceKeys =
    [
        "Brush.Code.Key",
        "Brush.Code.String",
        "Brush.Code.Number",
        "Brush.Code.Punctuation"
    ];

    private const string PlainForegroundKey = "Brush.Code.Plain";
    private const string MonospaceFontKey = "Font.Monospace";
    private const string BodyFontSizeKey = "Font.Size.BodySmall";

    /// <summary>
    /// 展示文本绑定入口：AvaloniaEdit 12 的 TextEditor.Text 是普通 CLR 属性不可绑定，
    /// 这里注册 StyledProperty 并转发到当前文档（有意遮蔽基类同名属性，故用 new）。
    /// </summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ResponseBodyEditor, string?>(nameof(Text));

    public new string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 子类必须回退到基类的样式键，才能命中 AvaloniaEdit 主题的 ControlTheme；
    // 否则查不到模板，TextView 保持 0 尺寸，Body 区整体空白
    protected override Type StyleKeyOverride => typeof(TextEditor);

    public ResponseBodyEditor()
    {
        IsReadOnly = true;
        ShowLineNumbers = false;
        WordWrap = false;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        SyntaxHighlighting = LoadJsonHighlighting();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Document.Text = change.NewValue as string ?? string.Empty;
        }
    }

    /// <summary>解析内嵌 xshd；颜色不带值，统一在挂入逻辑树后从 token 映射。</summary>
    private static IHighlightingDefinition LoadJsonHighlighting()
    {
        var resourceName = "ApixPress.App.Assets.Highlighting.Json.xshd";
        var stream = typeof(ResponseBodyEditor).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"缺少内嵌资源 {resourceName}，JSON 高亮不可用。");
        }

        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        MapTokenBindings();
    }

    private void MapTokenBindings()
    {
        var plainBrush = ResolveBrush(PlainForegroundKey);
        if (plainBrush is not null)
        {
            Foreground = plainBrush;
        }

        if (this.FindResource(MonospaceFontKey) is FontFamily fontFamily)
        {
            FontFamily = fontFamily;
        }

        if (this.FindResource(BodyFontSizeKey) is double fontSize)
        {
            FontSize = fontSize;
        }

        var definition = SyntaxHighlighting;
        if (definition is null)
        {
            return;
        }

        for (var index = 0; index < HighlightColorNames.Length; index++)
        {
            var brush = ResolveBrush(HighlightResourceKeys[index]);
            if (brush is null)
            {
                continue;
            }

            // AvaloniaEdit 12 的高亮色以 Color 为载体；token 均为纯色画刷，取色后映射
            definition.GetNamedColor(HighlightColorNames[index]).Foreground =
                new SimpleHighlightingBrush(brush.Color);
        }
    }

    private ISolidColorBrush? ResolveBrush(string resourceKey)
    {
        return this.FindResource(resourceKey) as ISolidColorBrush;
    }
}
