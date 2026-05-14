namespace ApixPress.App.Messages;

[Flags]
public enum WorkspaceStateChangeFlags
{
    None = 0,
    ShellState = 1,
    EditorState = 2,
    BindingsChanged = 4,
    ActiveTabChanged = 8,
    TabMenuChanged = 16
}
