using Application.DTOs.RealtorEfficiency;
using Application.Interfaces;
using Application.Options;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Primitives;
using Domain.ValueObjects;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Application.Tests;

public class RealtorLevelCalculationServiceTests
{
    [Fact]
    public void CalculateAutomaticLevel_ShouldKeepJunior_WhenDealsBelowStandardThreshold()
    {
        var level = RealtorLevelCalculationService.CalculateAutomaticLevel(
            RealtorLevel.Junior,
            completedDealsCount: 9,
            clientTrustScore: 5,
            adminPerformanceScore: 5,
            CreateSettings());

        Assert.Equal(RealtorLevel.Junior, level);
    }

    [Fact]
    public void CalculateAutomaticLevel_ShouldPromoteToStandard_WhenStandardRulesMatch()
    {
        var level = RealtorLevelCalculationService.CalculateAutomaticLevel(
            RealtorLevel.Junior,
            completedDealsCount: 10,
            clientTrustScore: 3.50,
            adminPerformanceScore: 3.50,
            CreateSettings());

        Assert.Equal(RealtorLevel.Standard, level);
    }

    [Fact]
    public void CalculateAutomaticLevel_ShouldPromoteToTop_WhenTopRulesMatch()
    {
        var level = RealtorLevelCalculationService.CalculateAutomaticLevel(
            RealtorLevel.Standard,
            completedDealsCount: 30,
            clientTrustScore: 4.30,
            adminPerformanceScore: 4.20,
            CreateSettings());

        Assert.Equal(RealtorLevel.Top, level);
    }

    [Fact]
    public async Task Recalculate_ShouldKeepJunior_WhenSnapshotIsMissing()
    {
        var realtor = CreateRealtor();
        var service = CreateService(realtor, completedDealsCount: 30, snapshot: null);

        var level = await service.Recalculate(realtor.Id);

        Assert.Equal(RealtorLevel.Junior, level);
        Assert.Equal(RealtorLevel.Junior, realtor.Level);
    }

    [Fact]
    public async Task Recalculate_ShouldNotChangeManualLevel()
    {
        var realtor = CreateRealtor();
        realtor.SetManualLevel(RealtorLevel.Top);
        var service = CreateService(
            realtor,
            completedDealsCount: 0,
            snapshot: CreateSnapshot(realtor.Id, clientTrustScore: 0, adminPerformanceScore: 0));

        var level = await service.Recalculate(realtor.Id);

        Assert.Equal(RealtorLevel.Top, level);
        Assert.Equal(RealtorLevel.Top, realtor.Level);
    }

    [Fact]
    public void CalculateAutomaticLevel_ShouldKeepTop_WhenScoresAreInsideDemotionBuffer()
    {
        var level = RealtorLevelCalculationService.CalculateAutomaticLevel(
            RealtorLevel.Top,
            completedDealsCount: 30,
            clientTrustScore: 4.05,
            adminPerformanceScore: 3.95,
            CreateSettings());

        Assert.Equal(RealtorLevel.Top, level);
    }

    [Fact]
    public void CalculateAutomaticLevel_ShouldDemoteTop_WhenScoresAreBelowDemotionBuffer()
    {
        var level = RealtorLevelCalculationService.CalculateAutomaticLevel(
            RealtorLevel.Top,
            completedDealsCount: 30,
            clientTrustScore: 3.99,
            adminPerformanceScore: 4.20,
            CreateSettings());

        Assert.Equal(RealtorLevel.Standard, level);
    }

    [Fact]
    public void UpdateFromAdmin_ShouldRejectInvalidLevelRules()
    {
        var service = new RealtorLevelSettingsService(
            OptionsFactory.Create(new RealtorEfficiencyOptions()),
            new InMemorySystemSettingRepository());

        Assert.Throws<Application.Exceptions.ValidationException>(() => service.UpdateFromAdmin(
            new UpdateRealtorLevelSettingsRequest(
                DemotionBuffer: 0.30,
                Rules:
                [
                    new UpdateRealtorLevelRuleRequest(RealtorLevel.Junior, 0, 0, 0, 0),
                    new UpdateRealtorLevelRuleRequest(RealtorLevel.Standard, 10, 3.50, 3.50, 1),
                    new UpdateRealtorLevelRuleRequest(RealtorLevel.Top, 5, 4.30, 4.20, 2)
                ])));
    }

    private static RealtorLevelCalculationService CreateService(
        Realtor realtor,
        int completedDealsCount,
        RealtorScoreSnapshot? snapshot)
    {
        var realtorRepository = new RealtorRepositoryStub(realtor);
        var dealRepository = new DealRepositoryStub(realtor.Id, completedDealsCount);
        var snapshotRepository = new SnapshotRepositoryStub(snapshot);
        var settingsService = new RealtorLevelSettingsService(
            OptionsFactory.Create(new RealtorEfficiencyOptions()),
            new InMemorySystemSettingRepository());

        return new RealtorLevelCalculationService(
            realtorRepository,
            dealRepository,
            snapshotRepository,
            settingsService);
    }

    private static Realtor CreateRealtor()
    {
        return new Realtor(new FullName("Test", "Realtor", null), "+37360000000");
    }

    private static RealtorScoreSnapshot CreateSnapshot(Guid realtorId, double clientTrustScore, double adminPerformanceScore)
    {
        return new RealtorScoreSnapshot(
            realtorId,
            clientTrustScore,
            adminPerformanceScore,
            clientServiceScoreComponent: clientTrustScore,
            propertyAccuracyScoreComponent: 0,
            complaintPenaltyComponent: 5,
            propertyDataQualityComponent: adminPerformanceScore,
            workflowDisciplineComponent: adminPerformanceScore,
            businessResultComponent: adminPerformanceScore,
            reputationRiskComponent: 5);
    }

    private static RealtorLevelRulesOptions CreateSettings()
    {
        return new RealtorEfficiencyOptions().LevelRules;
    }

    private sealed class RealtorRepositoryStub : IRealtorRepository
    {
        private readonly Realtor _realtor;

        public RealtorRepositoryStub(Realtor realtor)
        {
            _realtor = realtor;
        }

        public Task<Realtor?> GetById(Guid id) => Task.FromResult(id == _realtor.Id ? _realtor : null);
        public Task<List<Realtor>> Get(int limit) => Task.FromResult(new List<Realtor> { _realtor });
        public Task<Realtor> Add(Realtor entity) => Task.FromResult(entity);
        public void Update(Realtor entity) { }
        public bool Delete(Realtor entity) => true;
        public Task<Realtor?> GetByPhone(string phone) => Task.FromResult<Realtor?>(_realtor);
        public Task<List<Realtor>> GetTopRealtors(int count) => Task.FromResult(new List<Realtor> { _realtor });
        public Task<List<Realtor>> GetActive() => Task.FromResult(new List<Realtor> { _realtor });
    }

    private sealed class DealRepositoryStub : IDealRepository
    {
        private readonly Guid _realtorId;
        private readonly int _completedDealsCount;

        public DealRepositoryStub(Guid realtorId, int completedDealsCount)
        {
            _realtorId = realtorId;
            _completedDealsCount = completedDealsCount;
        }

        public Task<TResult> ExecuteWorkflow<TResult>(Guid dealId, Func<Task<TResult>> operation) => operation();
        public Task<Deal?> GetById(Guid id) => Task.FromResult<Deal?>(null);
        public Task<List<Deal>> Get(int limit) => Task.FromResult(new List<Deal>());
        public Task<Deal> Add(Deal entity) => Task.FromResult(entity);
        public void Update(Deal entity) { }
        public bool Delete(Deal entity) => true;
        public Task<Deal?> GetByIdWithNotes(Guid id) => Task.FromResult<Deal?>(null);
        public Task<List<Deal>> GetIncoming(int limit) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetIncomingOrAssignedToRealtor(Guid realtorId, int limit) => Task.FromResult(new List<Deal>());

        public Task<List<Deal>> GetByRealtor(Guid realtorId)
        {
            var deals = Enumerable.Range(0, _completedDealsCount)
                .Select(_ =>
                {
                    var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), _realtorId);
                    deal.Complete(1000m);
                    return deal;
                })
                .ToList();

            return Task.FromResult(deals);
        }

        public Task<List<Deal>> GetByClient(Guid clientId) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetByProperty(Guid propertyId) => Task.FromResult(new List<Deal>());
        public Task<Deal?> GetLatestSaleRequestByProperty(Guid propertyId) => Task.FromResult<Deal?>(null);
        public Task<List<Deal>> GetCompletedByRealtor(Guid realtorId, DateTime fromUtc) => GetByRealtor(realtorId);
        public Task<DealNote?> AddNote(Guid dealId, string text, Guid? authorRealtorId) => Task.FromResult<DealNote?>(null);
        public Task<DealNote?> UpdateNote(Guid dealId, Guid noteId, string text) => Task.FromResult<DealNote?>(null);
        public Task<bool> DeleteNote(Guid dealId, Guid noteId) => Task.FromResult(true);
    }

    private sealed class SnapshotRepositoryStub : IRealtorScoreSnapshotRepository
    {
        private readonly RealtorScoreSnapshot? _snapshot;

        public SnapshotRepositoryStub(RealtorScoreSnapshot? snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<RealtorScoreSnapshot?> GetById(Guid id) => Task.FromResult<RealtorScoreSnapshot?>(null);
        public Task<List<RealtorScoreSnapshot>> Get(int limit) => Task.FromResult(new List<RealtorScoreSnapshot>());
        public Task<RealtorScoreSnapshot> Add(RealtorScoreSnapshot entity) => Task.FromResult(entity);
        public void Update(RealtorScoreSnapshot entity) { }
        public bool Delete(RealtorScoreSnapshot entity) => true;
        public Task<RealtorScoreSnapshot?> GetLatestByRealtor(Guid realtorId) => Task.FromResult(_snapshot);
        public Task<List<RealtorScoreSnapshot>> GetHistoryByRealtor(Guid realtorId, int limit = 50) => Task.FromResult(new List<RealtorScoreSnapshot>());
    }
}
