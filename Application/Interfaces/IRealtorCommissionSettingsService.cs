using Application.DTOs.RealtorEfficiency;
using Domain.Primitives;

namespace Application.Interfaces
{
    public interface IRealtorCommissionSettingsService
    {
        decimal GetPercentForLevel(RealtorLevel level);

        RealtorCommissionSettingsResponse GetForAdmin();

        void UpdateFromAdmin(UpdateRealtorCommissionSettingsRequest request);
    }
}
