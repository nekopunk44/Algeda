using Application.Exceptions;
using Application.Interfaces;
using Application.Services;

namespace Application.Tests;

public class UserAccessManagementServiceTests
{
    [Fact]
    public async Task SetAccountFrozen_AdminCanFreezeRealtor()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var manager = new FakeIdentityAccountManager()
            .AddUser(actorId, "Admin")
            .AddUser(targetId, "Realtor");
        var service = new UserAccessManagementService(manager);

        var result = await service.SetAccountFrozen(actorId, targetId, true);

        Assert.True(result.IsFrozen);
        Assert.True(await manager.IsFrozen(targetId));
    }

    [Fact]
    public async Task SetAccountFrozen_AdminCannotFreezeAdmin()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var manager = new FakeIdentityAccountManager()
            .AddUser(actorId, "Admin")
            .AddUser(targetId, "Admin");
        var service = new UserAccessManagementService(manager);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SetAccountFrozen(actorId, targetId, true));
    }

    [Fact]
    public async Task SetAccountFrozen_SuperAdminCanFreezeAdmin()
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var manager = new FakeIdentityAccountManager()
            .AddUser(actorId, "Admin", "SuperAdmin")
            .AddUser(targetId, "Admin");
        var service = new UserAccessManagementService(manager);

        var result = await service.SetAccountFrozen(actorId, targetId, true);

        Assert.True(result.IsFrozen);
    }

    [Fact]
    public async Task SetAccountFrozen_CannotFreezeSelf()
    {
        var actorId = Guid.NewGuid();
        var manager = new FakeIdentityAccountManager()
            .AddUser(actorId, "Admin", "SuperAdmin");
        var service = new UserAccessManagementService(manager);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SetAccountFrozen(actorId, actorId, true));
    }

    private sealed class FakeIdentityAccountManager : IIdentityAccountManager
    {
        private readonly Dictionary<Guid, IdentityUserWithRoles> _users = [];

        public FakeIdentityAccountManager AddUser(Guid userId, params string[] roles)
        {
            _users[userId] = new IdentityUserWithRoles
            {
                UserId = userId,
                Email = $"{userId:N}@test.local",
                DisplayName = "Test User",
                EmailConfirmed = true,
                Roles = roles
            };
            return this;
        }

        public Task<bool> IsFrozen(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_users.TryGetValue(userId, out var user) && user.IsFrozen);
        }

        public Task<IdentityOperationResult> SetFrozen(
            Guid userId,
            bool isFrozen,
            Guid? frozenByUserId,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user))
                return Task.FromResult(IdentityOperationResult.Failed(["User not found."]));

            _users[userId] = new IdentityUserWithRoles
            {
                UserId = user.UserId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                EmailConfirmed = user.EmailConfirmed,
                IsFrozen = isFrozen,
                Roles = user.Roles
            };

            return Task.FromResult(IdentityOperationResult.Success());
        }

        public Task<IdentityUserWithRoles?> GetUserWithRoles(Guid userId, CancellationToken cancellationToken = default)
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<bool> EmailExists(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityUserInfo?> GetUserByEmail(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityUserInfo?> GetUserById(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityCreateResult> CreateUser(string email, string password, string? displayName, bool emailConfirmed, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityPasswordHashResult> BuildPasswordHash(string email, string password, string? displayName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityCreateResult> CreateUserWithPasswordHash(string email, string passwordHash, string? displayName, bool emailConfirmed, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string?> GenerateEmailConfirmationCode(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string?> GenerateEmailChangeCode(Guid userId, string newEmail, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> ConfirmEmailByCode(string email, string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> ChangeEmailByCode(Guid userId, string newEmail, string code, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string?> GeneratePasswordResetCode(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> ResetPasswordByCode(string email, string code, string newPassword, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> AddToRole(Guid userId, string role, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> RemoveFromRole(Guid userId, string role, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> DeleteUser(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> UpdateUserProfile(Guid userId, string email, string? displayName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IdentityOperationResult> ChangePassword(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CheckPassword(Guid userId, string password, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<IdentityUserWithRoles>> GetUsersWithRoles(string? search, string? roleFilter, int limit, bool excludeClients, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
