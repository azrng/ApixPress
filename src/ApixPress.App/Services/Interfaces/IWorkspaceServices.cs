using ApixPress.App.Models.DTOs;
using Avalonia.Controls;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Interfaces;

public interface IApiWorkspaceService
{
    Task<IReadOnlyList<ApiDocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiEndpointDto>> GetEndpointsAsync(string documentId, CancellationToken cancellationToken);

    Task<ApiDocumentDto?> GetDocumentAsync(string documentId, CancellationToken cancellationToken);

    Task<IResultModel<ApiDocumentDto>> ImportFromUrlAsync(string url, CancellationToken cancellationToken);

    Task<IResultModel<ApiDocumentDto>> ImportFromFileAsync(string filePath, CancellationToken cancellationToken);
}

public interface IRequestExecutionService
{
    Task<IResultModel<ResponseSnapshotDto>> SendAsync(
        RequestSnapshotDto request,
        string environmentName,
        CancellationToken cancellationToken);
}

public interface IRequestCaseService
{
    Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(CancellationToken cancellationToken);

    Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken);

    Task<IResultModel<RequestCaseDto>> DuplicateAsync(string id, CancellationToken cancellationToken);

    Task<IResultModel<bool>> DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface IEnvironmentVariableService
{
    Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(string environmentName, CancellationToken cancellationToken);

    Task<IResultModel<EnvironmentVariableDto>> SaveAsync(EnvironmentVariableDto variable, CancellationToken cancellationToken);

    Task<IResultModel<bool>> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string>> GetActiveDictionaryAsync(string environmentName, CancellationToken cancellationToken);
}

public interface IWindowHostService
{
    Window? MainWindow { get; set; }
}

public interface IFilePickerService
{
    Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken);
}
