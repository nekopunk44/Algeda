using Application.DTOs.DealDocuments;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Primitives;
using ValidationException = Application.Exceptions.ValidationException;

namespace Application.Tests;

public class DealDocumentServiceTests
{
    [Fact]
    public async Task AddRejectsFilesOverFiveMegabytes()
    {
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var service = BuildService([deal]);
        var actor = AdminActor();
        var content = new byte[DealDocumentService.MaxFileBytes + 1];

        var request = new CreateDealDocumentRequest(
            "Договор",
            "contract.pdf",
            "application/pdf",
            content);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.Add(deal.Id, request, actor));
    }

    [Fact]
    public async Task AddRejectsUnsupportedFormats()
    {
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var service = BuildService([deal]);
        var actor = AdminActor();

        var request = new CreateDealDocumentRequest(
            "Архив",
            "archive.zip",
            "application/zip",
            [1, 2, 3]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.Add(deal.Id, request, actor));
    }

    [Fact]
    public async Task OpenDeniesActorsWithoutDealAccess()
    {
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var documentRepository = new FakeDealDocumentRepository();
        var document = await documentRepository.Add(new DealDocument(
            deal.Id,
            "Договор",
            "contract.pdf",
            "application/pdf",
            [1, 2, 3],
            "hash",
            Guid.NewGuid(),
            "Администратор"));

        var service = BuildService([deal], documentRepository);
        var actor = new DealDocumentActorContext(
            Guid.NewGuid(),
            "client@example.com",
            "Клиент",
            IsAdmin: false,
            IsRealtor: false,
            IpAddress: null,
            UserAgent: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.Open(deal.Id, document.Id, actor));
    }

    [Fact]
    public async Task AddStoresDocumentAndWritesAccessLog()
    {
        var deal = new Deal(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var documentRepository = new FakeDealDocumentRepository();
        var logRepository = new FakeDealDocumentAccessLogRepository();
        var service = BuildService([deal], documentRepository, logRepository);
        var actor = AdminActor();

        var result = await service.Add(
            deal.Id,
            new CreateDealDocumentRequest(
                "Договор купли-продажи",
                "contract.pdf",
                "application/pdf",
                [1, 2, 3]),
            actor);

        Assert.Equal("Договор купли-продажи", result.Title);
        Assert.Single(await documentRepository.GetActiveByDeal(deal.Id));
        var log = Assert.Single(logRepository.Items);
        Assert.Equal(DealDocumentAction.Added, log.Action);
        Assert.Equal(result.Id, log.DealDocumentId);
    }

    private static DealDocumentService BuildService(
        IReadOnlyList<Deal> deals,
        FakeDealDocumentRepository? documentRepository = null,
        FakeDealDocumentAccessLogRepository? logRepository = null)
    {
        return new DealDocumentService(
            new FakeDealRepository(deals),
            documentRepository ?? new FakeDealDocumentRepository(),
            logRepository ?? new FakeDealDocumentAccessLogRepository(),
            dealService: null!,
            new FakeIdentityAccountManager());
    }

    private static DealDocumentActorContext AdminActor()
    {
        return new DealDocumentActorContext(
            Guid.NewGuid(),
            "admin@example.com",
            "Администратор",
            IsAdmin: true,
            IsRealtor: false,
            IpAddress: "127.0.0.1",
            UserAgent: "test");
    }

    private sealed class FakeDealRepository : IDealRepository
    {
        private readonly List<Deal> _deals;

        public FakeDealRepository(IReadOnlyList<Deal> deals)
        {
            _deals = deals.ToList();
        }

        public Task<Deal?> GetById(Guid id)
        {
            return Task.FromResult(_deals.FirstOrDefault(x => x.Id == id));
        }

        public Task<List<Deal>> Get(int limit)
        {
            return Task.FromResult(_deals.Take(limit).ToList());
        }

        public Task<Deal> Add(Deal entity)
        {
            _deals.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(Deal entity)
        {
        }

        public bool Delete(Deal entity)
        {
            return _deals.Remove(entity);
        }

        public Task<Deal?> GetByIdWithNotes(Guid id) => GetById(id);
        public Task<List<Deal>> GetIncoming(int limit) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetIncomingOrAssignedToRealtor(Guid realtorId, int limit) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetByRealtor(Guid realtorId) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetByClient(Guid clientId) => Task.FromResult(new List<Deal>());
        public Task<List<Deal>> GetByProperty(Guid propertyId) => Task.FromResult(new List<Deal>());
        public Task<Deal?> GetLatestSaleRequestByProperty(Guid propertyId) => Task.FromResult<Deal?>(null);
        public Task<List<Deal>> GetCompletedByRealtor(Guid realtorId, DateTime fromUtc) => Task.FromResult(new List<Deal>());
        public Task<DealNote?> AddNote(Guid dealId, string text, Guid? authorRealtorId) => Task.FromResult<DealNote?>(null);
        public Task<DealNote?> UpdateNote(Guid dealId, Guid noteId, string text) => Task.FromResult<DealNote?>(null);
        public Task<bool> DeleteNote(Guid dealId, Guid noteId) => Task.FromResult(false);
    }

    private sealed class FakeDealDocumentRepository : IDealDocumentRepository
    {
        private readonly List<DealDocument> _documents = [];

        public Task<IReadOnlyList<DealDocument>> GetActiveByDeal(Guid dealId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DealDocument>>(
                _documents.Where(x => x.DealId == dealId && !x.IsDeleted).ToList());
        }

        public Task<DealDocument?> GetActiveById(Guid dealId, Guid documentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _documents.FirstOrDefault(x => x.DealId == dealId && x.Id == documentId && !x.IsDeleted));
        }

        public Task<DealDocument?> GetById(Guid id)
        {
            return Task.FromResult(_documents.FirstOrDefault(x => x.Id == id));
        }

        public Task<List<DealDocument>> Get(int limit)
        {
            return Task.FromResult(_documents.Take(limit).ToList());
        }

        public Task<DealDocument> Add(DealDocument entity)
        {
            _documents.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(DealDocument entity)
        {
        }

        public bool Delete(DealDocument entity)
        {
            return _documents.Remove(entity);
        }
    }

    private sealed class FakeDealDocumentAccessLogRepository : IDealDocumentAccessLogRepository
    {
        public List<DealDocumentAccessLog> Items { get; } = [];

        public Task<IReadOnlyList<DealDocumentAccessLogSearchRow>> Search(DealDocumentAccessLogFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DealDocumentAccessLogSearchRow>>(
                Items
                    .Select(x => new DealDocumentAccessLogSearchRow(
                        x,
                        x.DealId.ToString(),
                        "Клиент",
                        "Риелтор"))
                    .ToList());
        }

        public Task<DealDocumentAccessLog?> GetById(Guid id)
        {
            return Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        }

        public Task<List<DealDocumentAccessLog>> Get(int limit)
        {
            return Task.FromResult(Items.Take(limit).ToList());
        }

        public Task<DealDocumentAccessLog> Add(DealDocumentAccessLog entity)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public void Update(DealDocumentAccessLog entity)
        {
        }

        public bool Delete(DealDocumentAccessLog entity)
        {
            return Items.Remove(entity);
        }
    }

    private sealed class FakeIdentityAccountManager : IIdentityAccountManager
    {
        public Task<bool> EmailExists(string email, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IdentityUserInfo?> GetUserByEmail(string email, CancellationToken cancellationToken = default) => Task.FromResult<IdentityUserInfo?>(null);
        public Task<IdentityUserInfo?> GetUserById(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IdentityUserInfo?>(null);
        public Task<IdentityCreateResult> CreateUser(string email, string password, string? displayName, bool emailConfirmed, CancellationToken cancellationToken = default) => Task.FromResult(IdentityCreateResult.Success(Guid.NewGuid()));
        public Task<IdentityPasswordHashResult> BuildPasswordHash(string email, string password, string? displayName, CancellationToken cancellationToken = default) => Task.FromResult(IdentityPasswordHashResult.Success("hash"));
        public Task<IdentityCreateResult> CreateUserWithPasswordHash(string email, string passwordHash, string? displayName, bool emailConfirmed, CancellationToken cancellationToken = default) => Task.FromResult(IdentityCreateResult.Success(Guid.NewGuid()));
        public Task<string?> GenerateEmailConfirmationCode(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> GenerateEmailChangeCode(Guid userId, string newEmail, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<IdentityOperationResult> ConfirmEmailByCode(string email, string code, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<IdentityOperationResult> ChangeEmailByCode(Guid userId, string newEmail, string code, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<string?> GeneratePasswordResetCode(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<IdentityOperationResult> ResetPasswordByCode(string email, string code, string newPassword, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<IdentityOperationResult> AddToRole(Guid userId, string role, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<IdentityOperationResult> RemoveFromRole(Guid userId, string role, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<IdentityOperationResult> DeleteUser(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<IdentityOperationResult> UpdateUserProfile(Guid userId, string email, string? displayName, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<IdentityOperationResult> ChangePassword(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<bool> CheckPassword(Guid userId, string password, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> IsFrozen(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IdentityOperationResult> SetFrozen(Guid userId, bool isFrozen, Guid? frozenByUserId, CancellationToken cancellationToken = default) => Task.FromResult(IdentityOperationResult.Success());
        public Task<IdentityUserWithRoles?> GetUserWithRoles(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IdentityUserWithRoles?>(null);
        public Task<IReadOnlyList<IdentityUserWithRoles>> GetUsersWithRoles(string? search, string? roleFilter, int limit, bool excludeClients, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<IdentityUserWithRoles>>([]);
    }
}
