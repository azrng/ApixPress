namespace ApixPress.App.Services.Interfaces;

public sealed record RequestHistoryPageDto(
    IReadOnlyList<RequestHistoryItemDto> Items,
    bool HasMore);
