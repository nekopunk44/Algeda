using Domain.Primitives;

namespace Application.Interfaces
{
    public interface IRealtorLevelCalculationService
    {
        Task<RealtorLevel> Recalculate(Guid realtorId);

        Task RecalculateAllAutomatic();
    }
}
