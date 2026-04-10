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

    // Computed badge classes
    public string MethodBadgeClass => $"MethodBadge_{Method}";

    public string StatusBadgeClass =>
        !HasResponse ? "StatusBadge_Error" :
        int.TryParse(StatusText, out var code) ? $"StatusBadge_{code / 100}xx" :
        "StatusBadge_Error";

    // Computed colors for UI
    public string MethodColor => Method switch
    {
        "GET" => "#6C757D",
        "POST" => "#0D6EFD",
        "PUT" => "#FD7E14",
        "DELETE" => "#DC3545",
        "PATCH" => "#20C997",
        _ => "#6C757D"
    };

    public string StatusColor =>
        !HasResponse ? "#6C757D" :
        int.TryParse(StatusText, out var code) ? code switch
        {
            >= 200 and < 300 => "#198754",
            >= 300 and < 400 => "#6C757D",
            >= 400 and < 500 => "#FD7E14",
            _ => "#DC3545"
        } : "#6C757D";
}
