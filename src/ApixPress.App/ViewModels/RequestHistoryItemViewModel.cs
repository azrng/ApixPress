using CommunityToolkit.Mvvm.ComponentModel;
using ApixPress.App.Models.DTOs;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class RequestHistoryItemViewModel : ViewModelBase
{
    public required string Id { get; init; }
    public required string Method { get; init; }
    public required string Url { get; init; }
    public required DateTime Timestamp { get; init; }

    [ObservableProperty]
    private bool hasResponse;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string durationText = string.Empty;

    [ObservableProperty]
    private string sizeText = string.Empty;

    // Request snapshot for loading/saving
    public required RequestSnapshotDto RequestSnapshot { get; init; }
    public ResponseSnapshotDto? ResponseSnapshot { get; init; }

    public string MethodBadgeClass => Method switch
    {
        "GET" => "Light Tertiary",
        "POST" => "Light Primary",
        "PUT" => "Light Warning",
        "DELETE" => "Light Danger",
        "PATCH" => "Light Success",
        _ => "Light Secondary"
    };

    public string StatusBadgeClass =>
        !HasResponse ? "Light Danger" :
        int.TryParse(StatusText, out var code) ? code switch
        {
            >= 200 and < 300 => "Light Success",
            >= 300 and < 400 => "Light Secondary",
            >= 400 and < 500 => "Light Warning",
            _ => "Light Danger"
        } : "Light Danger";

    public string TimestampText => Timestamp.ToLocalTime().ToString("MM-dd HH:mm");

    partial void OnHasResponseChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusBadgeClass));
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBadgeClass));
    }
}
