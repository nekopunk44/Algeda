namespace Application.Interfaces
{
    public interface IIdentityAccountManager
    {
        Task<bool> EmailExists(string email, CancellationToken cancellationToken = default);
        Task<IdentityUserInfo?> GetUserByEmail(string email, CancellationToken cancellationToken = default);
        Task<IdentityUserInfo?> GetUserById(Guid userId, CancellationToken cancellationToken = default);

        Task<IdentityCreateResult> CreateUser(
            string email,
            string password,
            string? displayName,
            bool emailConfirmed,
            CancellationToken cancellationToken = default);

        Task<IdentityPasswordHashResult> BuildPasswordHash(
            string email,
            string password,
            string? displayName,
            CancellationToken cancellationToken = default);

        Task<IdentityCreateResult> CreateUserWithPasswordHash(
            string email,
            string passwordHash,
            string? displayName,
            bool emailConfirmed,
            CancellationToken cancellationToken = default);

        Task<string?> GenerateEmailConfirmationCode(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<string?> GenerateEmailChangeCode(
            Guid userId,
            string newEmail,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> ConfirmEmailByCode(
            string email,
            string code,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> ChangeEmailByCode(
            Guid userId,
            string newEmail,
            string code,
            CancellationToken cancellationToken = default);

        Task<string?> GeneratePasswordResetCode(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> ResetPasswordByCode(
            string email,
            string code,
            string newPassword,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> AddToRole(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> RemoveFromRole(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> DeleteUser(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> UpdateUserProfile(
            Guid userId,
            string email,
            string? displayName,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> ChangePassword(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default);

        Task<bool> CheckPassword(
            Guid userId,
            string password,
            CancellationToken cancellationToken = default);

        Task<bool> IsFrozen(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<IdentityOperationResult> SetFrozen(
            Guid userId,
            bool isFrozen,
            Guid? frozenByUserId,
            CancellationToken cancellationToken = default);

        Task<IdentityUserWithRoles?> GetUserWithRoles(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IdentityUserWithRoles>> GetUsersWithRoles(
            string? search,
            string? roleFilter,
            int limit,
            bool excludeClients,
            CancellationToken cancellationToken = default);
    }

    public class IdentityOperationResult
    {
        public bool Succeeded { get; init; }

        public IReadOnlyList<string> Errors { get; init; } = [];

        public static IdentityOperationResult Success()
        {
            return new IdentityOperationResult { Succeeded = true };
        }

        public static IdentityOperationResult Failed(IEnumerable<string> errors)
        {
            return new IdentityOperationResult
            {
                Succeeded = false,
                Errors = errors.ToArray()
            };
        }
    }

    public class IdentityCreateResult : IdentityOperationResult
    {
        public Guid? UserId { get; init; }

        public static IdentityCreateResult Success(Guid userId)
        {
            return new IdentityCreateResult
            {
                Succeeded = true,
                UserId = userId
            };
        }

        public static new IdentityCreateResult Failed(IEnumerable<string> errors)
        {
            return new IdentityCreateResult
            {
                Succeeded = false,
                Errors = errors.ToArray()
            };
        }
    }

    public class IdentityPasswordHashResult : IdentityOperationResult
    {
        public string? PasswordHash { get; init; }

        public static IdentityPasswordHashResult Success(string passwordHash)
        {
            return new IdentityPasswordHashResult
            {
                Succeeded = true,
                PasswordHash = passwordHash
            };
        }

        public static new IdentityPasswordHashResult Failed(IEnumerable<string> errors)
        {
            return new IdentityPasswordHashResult
            {
                Succeeded = false,
                Errors = errors.ToArray()
            };
        }
    }

    public class IdentityUserWithRoles
    {
        public Guid UserId { get; init; }

        public string Email { get; init; } = string.Empty;

        public string? DisplayName { get; init; }

        public bool EmailConfirmed { get; init; }

        public bool IsFrozen { get; init; }

        public IReadOnlyList<string> Roles { get; init; } = [];
    }

    public class IdentityUserInfo
    {
        public Guid UserId { get; init; }

        public string Email { get; init; } = string.Empty;

        public string? DisplayName { get; init; }

        public bool EmailConfirmed { get; init; }

        public bool IsFrozen { get; init; }
    }
}
