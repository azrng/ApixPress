namespace ApixPress.App.Repositories.Interfaces;

public interface ISystemDataRepository
{
    Task ClearAllAsync(CancellationToken cancellationToken);
}
