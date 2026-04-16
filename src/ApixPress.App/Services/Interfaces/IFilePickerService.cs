namespace ApixPress.App.Services.Interfaces;

public interface IFilePickerService
{
    Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken);
}
