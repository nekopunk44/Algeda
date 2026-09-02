using API.Auth;
using Application.DTOs.RealtorEfficiency;
using Application.Interfaces;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers
{
    [ApiController]
    [Route("api/realtor-efficiency")]
    public class RealtorEfficiencyController : ApiControllerBase
    {
        private readonly IRealtorFeedbackService _feedbackService;
        private readonly IRealtorEfficiencyCalculationService _calculationService;
        private readonly IRealtorEligibilitySettingsService _eligibilitySettingsService;
        private readonly IRealtorCommissionSettingsService _commissionSettingsService;
        private readonly IRealtorLevelSettingsService _levelSettingsService;
        private readonly IRealtorLevelCalculationService _levelCalculationService;
        private readonly IRealtorRepository _realtorRepository;
        private readonly IRealtorRegistrationRequestRepository _realtorRegistrationRequestRepository;

        public RealtorEfficiencyController(
            IRealtorFeedbackService feedbackService,
            IRealtorEfficiencyCalculationService calculationService,
            IRealtorEligibilitySettingsService eligibilitySettingsService,
            IRealtorCommissionSettingsService commissionSettingsService,
            IRealtorLevelSettingsService levelSettingsService,
            IRealtorLevelCalculationService levelCalculationService,
            IRealtorRepository realtorRepository,
            IRealtorRegistrationRequestRepository realtorRegistrationRequestRepository)
        {
            _feedbackService = feedbackService;
            _calculationService = calculationService;
            _eligibilitySettingsService = eligibilitySettingsService;
            _commissionSettingsService = commissionSettingsService;
            _levelSettingsService = levelSettingsService;
            _levelCalculationService = levelCalculationService;
            _realtorRepository = realtorRepository;
            _realtorRegistrationRequestRepository = realtorRegistrationRequestRepository;
        }

        [HttpPost("feedback")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> SubmitFeedback([FromBody] SubmitDealFeedbackRequest request)
        {
            return ExecuteAsync(async () =>
            {
                var created = await _feedbackService.SubmitForCurrentClient(GetUserEmailOrEmpty(), request);
                return Ok(created);
            });
        }

        [HttpGet("feedback/deals/{dealId:guid}/state")]
        [Authorize(Roles = AppRoles.Client)]
        public Task<IActionResult> GetFeedbackState(Guid dealId)
        {
            return ExecuteAsync(async () =>
            {
                var state = await _feedbackService.GetStateForCurrentClient(GetUserEmailOrEmpty(), dealId);
                return Ok(state);
            });
        }

        [HttpGet("realtors/{realtorId:guid}/feedback-summary")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetFeedbackSummary(
            Guid realtorId,
            [FromQuery][Range(1, 100)] int limit = 20)
        {
            return ExecuteAsync(async () =>
            {
                var summary = await _feedbackService.GetSummaryForRealtor(realtorId, limit);
                return Ok(summary);
            });
        }

        [HttpGet("realtors/{realtorId:guid}/scores/latest")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetLatestScores(Guid realtorId)
        {
            return ExecuteAsync(async () =>
            {
                var latest = await _calculationService.GetLatestBreakdown(realtorId)
                    ?? await _calculationService.RecalculateAndSave(realtorId);

                return Ok(latest);
            });
        }

        [HttpGet("realtors/{realtorId:guid}/scores/history")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetScoresHistory(
            Guid realtorId,
            [FromQuery][Range(1, 200)] int limit = 20)
        {
            return ExecuteAsync(async () => Ok(await _calculationService.GetHistory(realtorId, limit)));
        }

        [HttpGet("eligibility-settings")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetEligibilitySettings()
        {
            return ExecuteAsync(() => Task.FromResult<IActionResult>(Ok(_eligibilitySettingsService.GetForAdmin())));
        }

        [HttpPut("eligibility-settings")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> UpdateEligibilitySettings([FromBody] UpdateRealtorEligibilitySettingsRequest request)
        {
            return ExecuteAsync(() =>
            {
                _eligibilitySettingsService.UpdateFromAdmin(request);
                return Task.FromResult<IActionResult>(Ok(_eligibilitySettingsService.GetForAdmin()));
            });
        }

        [HttpGet("commission-settings")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetCommissionSettings()
        {
            return ExecuteAsync(() => Task.FromResult<IActionResult>(Ok(_commissionSettingsService.GetForAdmin())));
        }

        [HttpPut("commission-settings")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> UpdateCommissionSettings([FromBody] UpdateRealtorCommissionSettingsRequest request)
        {
            return ExecuteAsync(() =>
            {
                _commissionSettingsService.UpdateFromAdmin(request);
                return Task.FromResult<IActionResult>(Ok(_commissionSettingsService.GetForAdmin()));
            });
        }

        [HttpGet("level-settings")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> GetLevelSettings()
        {
            return ExecuteAsync(() => Task.FromResult<IActionResult>(Ok(_levelSettingsService.GetForAdmin())));
        }

        [HttpPut("level-settings")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public Task<IActionResult> UpdateLevelSettings([FromBody] UpdateRealtorLevelSettingsRequest request)
        {
            return ExecuteAsync(async () =>
            {
                _levelSettingsService.UpdateFromAdmin(request);
                await _levelCalculationService.RecalculateAllAutomatic();
                return Ok(_levelSettingsService.GetForAdmin());
            });
        }

        [HttpGet("me/realtor-id")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> GetMyRealtorId()
        {
            return ExecuteAsync(async () =>
            {
                var realtorId = await ResolveCurrentRealtorId();
                return Ok(new { realtorId });
            });
        }

        [HttpGet("me/scores/latest")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> GetMyLatestScores()
        {
            return ExecuteAsync(async () =>
            {
                var realtorId = await ResolveCurrentRealtorId();
                var latest = await _calculationService.GetLatestBreakdown(realtorId)
                    ?? await _calculationService.RecalculateAndSave(realtorId);

                return Ok(latest);
            });
        }

        [HttpGet("me/scores/history")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> GetMyScoresHistory([FromQuery][Range(1, 200)] int limit = 20)
        {
            return ExecuteAsync(async () =>
            {
                var realtorId = await ResolveCurrentRealtorId();
                return Ok(await _calculationService.GetHistory(realtorId, limit));
            });
        }

        [HttpGet("me/feedback-summary")]
        [Authorize(Roles = AppRoles.Realtor)]
        public Task<IActionResult> GetMyFeedbackSummary([FromQuery][Range(1, 100)] int limit = 20)
        {
            return ExecuteAsync(async () =>
            {
                var realtorId = await ResolveCurrentRealtorId();
                var summary = await _feedbackService.GetSummaryForRealtor(realtorId, limit);
                return Ok(summary);
            });
        }

        private string GetUserEmailOrEmpty()
        {
            return User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? string.Empty;
        }

        private async Task<Guid> ResolveCurrentRealtorId()
        {
            var email = GetUserEmailOrEmpty().Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new Application.Exceptions.ValidationException("Не удалось определить email текущего риелтора.");
            }

            var approvedRequest = await _realtorRegistrationRequestRepository.GetLatestApprovedByEmail(email);
            if (approvedRequest is null)
            {
                throw new Application.Exceptions.NotFoundException("Не найдена одобренная регистрация риелтора.");
            }

            var realtor = await _realtorRepository.GetByPhone(approvedRequest.PhoneNumber);
            if (realtor is null)
            {
                throw new Application.Exceptions.NotFoundException("Профиль риелтора не найден.");
            }

            return realtor.Id;
        }
    }
}
