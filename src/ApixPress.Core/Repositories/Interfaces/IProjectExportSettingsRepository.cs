using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IProjectExportSettingsRepository
{
    Task<ProjectExportSettingsEntity?> GetAsync(string projectId, CancellationToken cancellationToken);

    Task UpsertAsync(ProjectExportSettingsEntity entity, CancellationToken cancellationToken);
}
