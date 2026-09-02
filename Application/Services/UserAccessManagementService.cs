using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces;

namespace Application.Services
{
public class UserAccessManagementService
{
    private const string AdminRole = "Admin";
    private const string SuperAdminRole = "SuperAdmin";
    private const string RealtorRole = "Realtor";
    private const string ClientRole = "Client";
    private static readonly string[] AllowedRoles = [AdminRole, RealtorRole];

        private readonly IIdentityAccountManager _identityAccountManager;

        public UserAccessManagementService(IIdentityAccountManager identityAccountManager)
        {
            _identityAccountManager = identityAccountManager;
        }

        public async Task<List<IdentityUserAccessResponse>> GetUsers(
            string? search,
            string? role,
            int limit,
            bool excludeClients,
            CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var users = await _identityAccountManager.GetUsersWithRoles(
                search,
                role,
                safeLimit,
                excludeClients,
                cancellationToken);

            return users
                .Select(Map)
                .ToList();
    }

    public async Task<IdentityUserAccessResponse> AssignRole(
        Guid actorUserId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = NormalizeAndValidateRole(role);
        await EnsureCanManageRole(actorUserId, normalizedRole, cancellationToken);

        var result = await _identityAccountManager.AddToRole(userId, normalizedRole, cancellationToken);
            if (!result.Succeeded)
            {
                throw new ValidationException(JoinErrors(result.Errors));
            }

            var user = await _identityAccountManager.GetUserWithRoles(userId, cancellationToken);
            if (user is null)
            {
                throw new NotFoundException("Пользователь не найден.");
            }

            return Map(user);
    }

    public async Task<IdentityUserAccessResponse> RemoveRole(
        Guid actorUserId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = NormalizeAndValidateRole(role);
        await EnsureCanManageRole(actorUserId, normalizedRole, cancellationToken);

        var result = await _identityAccountManager.RemoveFromRole(userId, normalizedRole, cancellationToken);
            if (!result.Succeeded)
            {
                throw new ValidationException(JoinErrors(result.Errors));
            }

            var user = await _identityAccountManager.GetUserWithRoles(userId, cancellationToken);
            if (user is null)
            {
                throw new NotFoundException("Пользователь не найден.");
            }

        return Map(user);
    }

    public async Task<IdentityUserAccessResponse> TransferSuperAdmin(
        Guid actorUserId,
        TransferSuperAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed)
            throw new ValidationException("Подтвердите передачу прав главного администратора.");

        if (request.TargetUserId == Guid.Empty)
            throw new ValidationException("Не выбран новый главный администратор.");

        var actor = await _identityAccountManager.GetUserWithRoles(actorUserId, cancellationToken);
        if (actor is null)
            throw new NotFoundException("Текущий пользователь не найден.");

        if (!HasRole(actor, SuperAdminRole))
            throw new ValidationException("Передавать права главного администратора может только текущий главный администратор.");

        if (!await _identityAccountManager.CheckPassword(actorUserId, request.Password, cancellationToken))
            throw new ValidationException("Неверный пароль текущего главного администратора.");

        var target = await _identityAccountManager.GetUserWithRoles(request.TargetUserId, cancellationToken);
        if (target is null)
            throw new NotFoundException("Пользователь для передачи прав не найден.");

        await EnsureIdentityOperation(_identityAccountManager.AddToRole(request.TargetUserId, AdminRole, cancellationToken));
        await EnsureIdentityOperation(_identityAccountManager.AddToRole(request.TargetUserId, SuperAdminRole, cancellationToken));

        if (request.TargetUserId != actorUserId)
        {
            await EnsureIdentityOperation(_identityAccountManager.RemoveFromRole(actorUserId, SuperAdminRole, cancellationToken));
        }

        var updatedTarget = await _identityAccountManager.GetUserWithRoles(request.TargetUserId, cancellationToken);
        if (updatedTarget is null)
            throw new NotFoundException("Пользователь для передачи прав не найден.");

        return Map(updatedTarget);
    }

    public async Task<IdentityUserAccessResponse> SetAccountFrozen(
        Guid actorUserId,
        Guid userId,
        bool isFrozen,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("Не выбран пользователь.");

        if (actorUserId == Guid.Empty)
            throw new ValidationException("Не удалось определить текущего администратора.");

        if (actorUserId == userId)
            throw new ValidationException("Нельзя заморозить или разморозить собственный аккаунт.");

        var actor = await _identityAccountManager.GetUserWithRoles(actorUserId, cancellationToken);
        if (actor is null)
            throw new NotFoundException("Текущий пользователь не найден.");

        var target = await _identityAccountManager.GetUserWithRoles(userId, cancellationToken);
        if (target is null)
            throw new NotFoundException("Пользователь не найден.");

        EnsureCanSetFrozen(actor, target);

        await EnsureIdentityOperation(_identityAccountManager.SetFrozen(
            userId,
            isFrozen,
            isFrozen ? actorUserId : null,
            cancellationToken));

        var updatedUser = await _identityAccountManager.GetUserWithRoles(userId, cancellationToken);
        if (updatedUser is null)
            throw new NotFoundException("Пользователь не найден.");

        return Map(updatedUser);
    }

    private static IdentityUserAccessResponse Map(IdentityUserWithRoles user)
        {
            return new IdentityUserAccessResponse(
                user.UserId,
                user.Email,
                user.DisplayName,
                user.EmailConfirmed,
                user.IsFrozen,
                user.Roles.OrderBy(x => x).ToList());
        }

        private static string NormalizeAndValidateRole(string role)
        {
            var normalizedRole = role?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedRole)
                || !AllowedRoles.Any(x => string.Equals(x, normalizedRole, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ValidationException("Можно управлять только ролями Admin и Realtor.");
            }

            return AllowedRoles.First(x => string.Equals(x, normalizedRole, StringComparison.OrdinalIgnoreCase));
        }

        private static string JoinErrors(IReadOnlyList<string> errors)
        {
            if (errors.Count == 0)
            {
                return "Не удалось изменить роли пользователя.";
            }

            return string.Join("; ", errors);
        }

    private async Task EnsureCanManageRole(Guid actorUserId, string role, CancellationToken cancellationToken)
    {
        if (!string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase))
            return;

        var actor = await _identityAccountManager.GetUserWithRoles(actorUserId, cancellationToken);
        if (actor is null || !HasRole(actor, SuperAdminRole))
        {
            throw new ValidationException("Назначать и снимать роль Admin может только главный администратор.");
        }
    }

    private static bool HasRole(IdentityUserWithRoles user, string role)
    {
        return user.Roles.Any(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureCanSetFrozen(IdentityUserWithRoles actor, IdentityUserWithRoles target)
    {
        if (HasRole(actor, SuperAdminRole))
            return;

        if (!HasRole(actor, AdminRole))
            throw new ValidationException("Замораживать аккаунты может только администратор.");

        if (HasRole(target, AdminRole) || HasRole(target, SuperAdminRole))
            throw new ValidationException("Администраторов может замораживать только главный администратор.");

        if (!HasRole(target, RealtorRole) && !HasRole(target, ClientRole))
            throw new ValidationException("Обычный администратор может замораживать только клиентов и риелторов.");
    }

    private static async Task EnsureIdentityOperation(Task<IdentityOperationResult> operation)
    {
        var result = await operation;
        if (!result.Succeeded)
        {
            throw new ValidationException(JoinErrors(result.Errors));
        }
    }
}
}
