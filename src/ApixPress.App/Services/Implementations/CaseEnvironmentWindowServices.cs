using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Json;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Models.Entities;
using ApixPress.App.Repositories.Interfaces;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.Results;
using Azrng.Core;

namespace ApixPress.App.Services.Implementations;

public sealed class RequestCaseService : IRequestCaseService, ITransientDependency
{
    private readonly IRequestCaseRepository _requestCaseRepository;
    private readonly IJsonSerializer _serializer;

    public RequestCaseService(IRequestCaseRepository requestCaseRepository, IJsonSerializer serializer)
    {
        _requestCaseRepository = requestCaseRepository;
        _serializer = serializer;
    }

    public async Task<IReadOnlyList<RequestCaseDto>> GetCasesAsync(CancellationToken cancellationToken)
    {
        var cases = await _requestCaseRepository.GetCasesAsync(cancellationToken);
        return cases.Select(ToDto).ToList();
    }

    public async Task<IResultModel<RequestCaseDto>> SaveAsync(RequestCaseDto requestCase, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestCase.Name))
        {
            return ResultModel<RequestCaseDto>.Failure("请输入用例名称。", "request_case_name_required");
        }

        var entity = ToEntity(requestCase);
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = Guid.NewGuid().ToString("N");
        }

        if (entity.UpdatedAt == default)
        {
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await _requestCaseRepository.UpsertAsync(entity, cancellationToken);
        return ResultModel<RequestCaseDto>.Success(ToDto(entity));
    }

    public async Task<IResultModel<RequestCaseDto>> DuplicateAsync(string id, CancellationToken cancellationToken)
    {
        var source = await _requestCaseRepository.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            return ResultModel<RequestCaseDto>.Failure("未找到待复制的用例。", "request_case_not_found");
        }

        var duplicate = ToDto(source);
        var duplicateEntity = ToEntity(new RequestCaseDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{duplicate.Name} 副本",
            GroupName = duplicate.GroupName,
            Tags = duplicate.Tags,
            Description = duplicate.Description,
            RequestSnapshot = duplicate.RequestSnapshot,
            UpdatedAt = DateTime.UtcNow
        });

        await _requestCaseRepository.UpsertAsync(duplicateEntity, cancellationToken);
        return ResultModel<RequestCaseDto>.Success(ToDto(duplicateEntity));
    }

    public async Task<IResultModel<bool>> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _requestCaseRepository.DeleteAsync(id, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    private RequestCaseDto ToDto(RequestCaseEntity entity)
    {
        return new RequestCaseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            GroupName = entity.GroupName,
            Tags = _serializer.ToObject<List<string>>(entity.TagsJson) ?? [],
            Description = entity.Description,
            RequestSnapshot = _serializer.ToObject<RequestSnapshotDto>(entity.RequestSnapshotJson) ?? new RequestSnapshotDto(),
            UpdatedAt = entity.UpdatedAt
        };
    }

    private RequestCaseEntity ToEntity(RequestCaseDto dto)
    {
        return new RequestCaseEntity
        {
            Id = dto.Id,
            Name = dto.Name,
            GroupName = dto.GroupName,
            TagsJson = _serializer.ToJson(dto.Tags),
            Description = dto.Description,
            RequestSnapshotJson = _serializer.ToJson(dto.RequestSnapshot),
            UpdatedAt = dto.UpdatedAt
        };
    }
}

public sealed class EnvironmentVariableService : IEnvironmentVariableService, ITransientDependency
{
    private readonly IEnvironmentVariableRepository _environmentVariableRepository;

    public EnvironmentVariableService(IEnvironmentVariableRepository environmentVariableRepository)
    {
        _environmentVariableRepository = environmentVariableRepository;
    }

    public async Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(string environmentName, CancellationToken cancellationToken)
    {
        var items = await _environmentVariableRepository.GetByEnvironmentAsync(environmentName, cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<IResultModel<EnvironmentVariableDto>> SaveAsync(EnvironmentVariableDto variable, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(variable.Key))
        {
            return ResultModel<EnvironmentVariableDto>.Failure("环境变量键不能为空。", "environment_key_required");
        }

        var entity = new EnvironmentVariableEntity
        {
            Id = string.IsNullOrWhiteSpace(variable.Id) ? Guid.NewGuid().ToString("N") : variable.Id,
            EnvironmentName = variable.EnvironmentName,
            Key = variable.Key,
            Value = variable.Value,
            IsEnabled = variable.IsEnabled
        };

        await _environmentVariableRepository.UpsertAsync(entity, cancellationToken);
        return ResultModel<EnvironmentVariableDto>.Success(ToDto(entity));
    }

    public async Task<IResultModel<bool>> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _environmentVariableRepository.DeleteAsync(id, cancellationToken);
        return ResultModel<bool>.Success(true);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetActiveDictionaryAsync(string environmentName, CancellationToken cancellationToken)
    {
        var variables = await GetVariablesAsync(environmentName, cancellationToken);
        return variables.Where(item => item.IsEnabled)
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);
    }

    private static EnvironmentVariableDto ToDto(EnvironmentVariableEntity entity)
    {
        return new EnvironmentVariableDto
        {
            Id = entity.Id,
            EnvironmentName = entity.EnvironmentName,
            Key = entity.Key,
            Value = entity.Value,
            IsEnabled = entity.IsEnabled
        };
    }
}

public sealed class WindowHostService : IWindowHostService, ISingletonDependency
{
    public Window? MainWindow { get; set; }
}

public sealed class FilePickerService : IFilePickerService, ITransientDependency
{
    private readonly IWindowHostService _windowHostService;

    public FilePickerService(IWindowHostService windowHostService)
    {
        _windowHostService = windowHostService;
    }

    public async Task<string?> PickSwaggerJsonFileAsync(CancellationToken cancellationToken)
    {
        if (_windowHostService.MainWindow is null)
        {
            return null;
        }

        var files = await _windowHostService.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Swagger / OpenAPI JSON 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON 文档")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        cancellationToken.ThrowIfCancellationRequested();
        return files.FirstOrDefault() is { } file ? file.TryGetLocalPath() : null;
    }
}
