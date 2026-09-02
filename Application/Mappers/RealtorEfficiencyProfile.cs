using Application.DTOs.RealtorEfficiency;
using Application.Services;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappers
{
    public class RealtorEfficiencyProfile : Profile
    {
        public RealtorEfficiencyProfile()
        {
            CreateMap<CreateRealtorFeedbackRequest, RealtorFeedback>()
                .ConstructUsing(src => new RealtorFeedback(
                    src.DealId,
                    src.ClientId,
                    src.ServiceRealtorId,
                    src.PropertyId,
                    src.PropertyResponsibleRealtorId,
                    src.ServiceScore,
                    src.FormType,
                    src.CommunicationScore,
                    src.ResponsivenessScore,
                    src.ExpertiseScore,
                    src.TitleAccuracyScore,
                    src.CriteriaAccuracyScore,
                    src.DescriptionAccuracyScore,
                    src.PhotosAccuracyScore,
                    src.Comment));

            CreateMap<RealtorFeedback, RealtorFeedbackResponse>();

            CreateMap<RealtorScoreSnapshot, RealtorScoreBreakdownResponse>()
                .ForCtorParam(nameof(RealtorScoreBreakdownResponse.SnapshotId), o => o.MapFrom(s => s.Id))
                .ForCtorParam(
                    nameof(RealtorScoreBreakdownResponse.ClientTrustBreakdown),
                    o => o.MapFrom(s => new ClientTrustBreakdownResponse(
                        ScoreScaleNormalizer.ToFivePointScale(s.ClientServiceScoreComponent),
                        ScoreScaleNormalizer.ToFivePointScale(s.PropertyAccuracyScoreComponent),
                        ScoreScaleNormalizer.ToFivePointScale(s.ComplaintPenaltyComponent))))
                .ForCtorParam(
                    nameof(RealtorScoreBreakdownResponse.AdminPerformanceBreakdown),
                    o => o.MapFrom(s => new AdminPerformanceBreakdownResponse(
                        ScoreScaleNormalizer.ToFivePointScale(s.PropertyDataQualityComponent),
                        ScoreScaleNormalizer.ToFivePointScale(s.WorkflowDisciplineComponent),
                        ScoreScaleNormalizer.ToFivePointScale(s.BusinessResultComponent),
                        ScoreScaleNormalizer.ToFivePointScale(s.ReputationRiskComponent))))
                .ForCtorParam(
                    nameof(RealtorScoreBreakdownResponse.ClientTrustScore),
                    o => o.MapFrom(s => ScoreScaleNormalizer.ToFivePointScale(s.ClientTrustScore)))
                .ForCtorParam(
                    nameof(RealtorScoreBreakdownResponse.AdminPerformanceScore),
                    o => o.MapFrom(s => ScoreScaleNormalizer.ToFivePointScale(s.AdminPerformanceScore)));
        }
    }
}
