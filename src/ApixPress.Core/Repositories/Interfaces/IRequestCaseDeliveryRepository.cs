using ApixPress.App.Models.Entities;

namespace ApixPress.App.Repositories.Interfaces;

public interface IRequestCaseDeliveryRepository
{
    /// <summary>在单一事务内写入请求用例交付包的接口、参数、用例和环境。</summary>
    Task ImportAsync(RequestCaseDeliveryImportPlan plan, CancellationToken cancellationToken);
}
