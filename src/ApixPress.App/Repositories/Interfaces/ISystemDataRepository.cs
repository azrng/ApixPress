namespace ApixPress.App.Repositories.Interfaces;

public interface ISystemDataRepository
{
    Task<bool> ClearProjectAsync(string projectId, CancellationToken cancellationToken);

    Task ClearAllAsync(CancellationToken cancellationToken);
}
