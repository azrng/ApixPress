using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IProjectHttpSettingsRepository
{
    Task<ProjectHttpAuthSettingsEntity?> GetAuthSettingsAsync(string projectId, CancellationToken cancellationToken);

    Task UpsertAuthSettingsAsync(ProjectHttpAuthSettingsEntity entity, CancellationToken cancellationToken);
}
