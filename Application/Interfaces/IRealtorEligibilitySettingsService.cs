using Application.DTOs.RealtorEfficiency;
using Application.Options;

namespace Application.Interfaces
{
    public interface IRealtorEligibilitySettingsService
    {
        EligibilityOptions GetCurrent();
        RealtorEligibilitySettingsResponse GetForAdmin();
        void UpdateFromAdmin(UpdateRealtorEligibilitySettingsRequest request);
    }
}
