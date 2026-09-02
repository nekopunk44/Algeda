using Application.DTOs.RealtorEfficiency;
using Application.Options;

namespace Application.Interfaces
{
    public interface IRealtorLevelSettingsService
    {
        RealtorLevelRulesOptions GetCurrent();

        RealtorLevelSettingsResponse GetForAdmin();

        void UpdateFromAdmin(UpdateRealtorLevelSettingsRequest request);
    }
}
