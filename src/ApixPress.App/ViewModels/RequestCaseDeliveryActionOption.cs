namespace ApixPress.App.ViewModels;

public sealed class RequestCaseDeliveryActionOption
{
    public RequestCaseDeliveryActionOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }
}
