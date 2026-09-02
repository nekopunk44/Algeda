using Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Identity
{
    public class IdentityAccountManager : IIdentityAccountManager
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public IdentityAccountManager(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<bool> EmailExists(string email, CancellationToken cancellationToken = default)
        {
            var normalized = email.Trim();
            var existing = await _userManager.FindByEmailAsync(normalized);
            return existing is not null;
        }

        public async Task<IdentityUserInfo?> GetUserByEmail(
            string email,
            CancellationToken cancellationToken = default)
        {
            var normalized = email.Trim();
            var user = await _userManager.FindByEmailAsync(normalized);
            return user is null ? null : MapUserInfo(user);
        }

        public async Task<IdentityUserInfo?> GetUserById(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            return user is null ? null : MapUserInfo(user);
        }

        public async Task<IdentityCreateResult> CreateUser(
            string email,
            string password,
            string? displayName,
            bool emailConfirmed,
            CancellationToken cancellationToken = default)
        {
            var normalized = email.Trim();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = normalized,
                Email = normalized,
                DisplayName = displayName,
                EmailConfirmed = emailConfirmed
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                return IdentityCreateResult.Failed(createResult.Errors.Select(x => x.Description));
            }

            return IdentityCreateResult.Success(user.Id);
        }

        public async Task<IdentityPasswordHashResult> BuildPasswordHash(
            string email,
            string password,
            string? displayName,
            CancellationToken cancellationToken = default)
        {
            var normalized = email.Trim();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = normalized,
                Email = normalized,
                DisplayName = displayName
            };

            var errors = new List<string>();
            foreach (var validator in _userManager.PasswordValidators)
            {
                var validationResult = await validator.ValidateAsync(_userManager, user, password);
                if (!validationResult.Succeeded)
                {
                    errors.AddRange(validationResult.Errors.Select(x => x.Description));
                }
            }

            if (errors.Count > 0)
            {
                return IdentityPasswordHashResult.Failed(errors);
            }

            var hash = _userManager.PasswordHasher.HashPassword(user, password);
            return IdentityPasswordHashResult.Success(hash);
        }

        public async Task<IdentityCreateResult> CreateUserWithPasswordHash(
            string email,
            string passwordHash,
            string? displayName,
            bool emailConfirmed,
            CancellationToken cancellationToken = default)
        {
            var normalized = email.Trim();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = normalized,
                Email = normalized,
                DisplayName = displayName,
                EmailConfirmed = emailConfirmed,
                PasswordHash = passwordHash
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return IdentityCreateResult.Failed(createResult.Errors.Select(x => x.Description));
            }

            return IdentityCreateResult.Success(user.Id);
        }

        public async Task<IdentityOperationResult> AddToRole(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return IdentityOperationResult.Failed(["Пользователь не найден."]);
            }

            if (await _userManager.IsInRoleAsync(user, role))
            {
                return IdentityOperationResult.Success();
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, role);
            if (!addRoleResult.Succeeded)
            {
                return IdentityOperationResult.Failed(addRoleResult.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<string?> GenerateEmailConfirmationCode(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return null;
            }

            var code = GenerateSixDigitCode();
            user.EmailConfirmationCodeHash = BuildCodeHash(user, code, "email-confirmation");
            user.EmailConfirmationCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded ? code : null;
        }

        public async Task<string?> GenerateEmailChangeCode(
            Guid userId,
            string newEmail,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return null;
            }

            var normalizedEmail = newEmail.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return null;
            }

            if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(normalizedEmail);
                if (existing is not null && existing.Id != user.Id)
                {
                    return null;
                }
            }

            var code = GenerateSixDigitCode();
            user.EmailConfirmationCodeHash = BuildCodeHash(user, code, "email-change", normalizedEmail);
            user.EmailConfirmationCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded ? code : null;
        }

        public async Task<IdentityOperationResult> ConfirmEmailByCode(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user is null)
            {
                return IdentityOperationResult.Failed(["Код подтверждения неверный или устарел."]);
            }

            if (user.EmailConfirmed)
            {
                ClearEmailConfirmationCode(user);
                var alreadyConfirmedResult = await _userManager.UpdateAsync(user);
                return alreadyConfirmedResult.Succeeded
                    ? IdentityOperationResult.Success()
                    : IdentityOperationResult.Failed(alreadyConfirmedResult.Errors.Select(x => x.Description));
            }

            if (!IsCodeValid(
                    user,
                    code,
                    "email-confirmation",
                    user.EmailConfirmationCodeHash,
                    user.EmailConfirmationCodeExpiresAtUtc))
            {
                return IdentityOperationResult.Failed(["Код подтверждения неверный или устарел."]);
            }

            user.EmailConfirmed = true;
            ClearEmailConfirmationCode(user);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return IdentityOperationResult.Failed(result.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<IdentityOperationResult> ChangeEmailByCode(
            Guid userId,
            string newEmail,
            string code,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return IdentityOperationResult.Failed(["Пользователь не найден."]);
            }

            var normalizedEmail = newEmail.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return IdentityOperationResult.Failed(["Email не может быть пустым."]);
            }

            if (!IsCodeValid(
                    user,
                    code,
                    "email-change",
                    user.EmailConfirmationCodeHash,
                    user.EmailConfirmationCodeExpiresAtUtc,
                    normalizedEmail))
            {
                return IdentityOperationResult.Failed(["Код подтверждения неверный или устарел."]);
            }

            if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(normalizedEmail);
                if (existing is not null && existing.Id != user.Id)
                {
                    return IdentityOperationResult.Failed(["Пользователь с таким email уже существует."]);
                }

                user.Email = normalizedEmail;
                user.UserName = normalizedEmail;
                user.NormalizedEmail = _userManager.NormalizeEmail(normalizedEmail);
                user.NormalizedUserName = _userManager.NormalizeName(normalizedEmail);
            }

            user.EmailConfirmed = true;
            user.SecurityStamp = Guid.NewGuid().ToString();
            ClearEmailConfirmationCode(user);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return IdentityOperationResult.Failed(result.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<string?> GeneratePasswordResetCode(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return null;
            }

            var code = GenerateSixDigitCode();
            user.PasswordResetCodeHash = BuildCodeHash(user, code, "password-reset");
            user.PasswordResetCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded ? code : null;
        }

        public async Task<IdentityOperationResult> ResetPasswordByCode(
            string email,
            string code,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user is null)
            {
                return IdentityOperationResult.Failed(["Код восстановления неверный или устарел."]);
            }

            if (!IsCodeValid(
                    user,
                    code,
                    "password-reset",
                    user.PasswordResetCodeHash,
                    user.PasswordResetCodeExpiresAtUtc))
            {
                return IdentityOperationResult.Failed(["Код восстановления неверный или устарел."]);
            }

            var passwordErrors = new List<string>();
            foreach (var validator in _userManager.PasswordValidators)
            {
                var validationResult = await validator.ValidateAsync(_userManager, user, newPassword);
                if (!validationResult.Succeeded)
                {
                    passwordErrors.AddRange(validationResult.Errors.Select(x => x.Description));
                }
            }

            if (passwordErrors.Count > 0)
            {
                return IdentityOperationResult.Failed(passwordErrors);
            }

            user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, newPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();
            ClearPasswordResetCode(user);
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return IdentityOperationResult.Failed(updateResult.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<IdentityOperationResult> RemoveFromRole(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return IdentityOperationResult.Failed(["Пользователь не найден."]);
            }

            if (!await _userManager.IsInRoleAsync(user, role))
            {
                return IdentityOperationResult.Success();
            }

            var removeRoleResult = await _userManager.RemoveFromRoleAsync(user, role);
            if (!removeRoleResult.Succeeded)
            {
                return IdentityOperationResult.Failed(removeRoleResult.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<IdentityOperationResult> DeleteUser(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return IdentityOperationResult.Success();
            }

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                return IdentityOperationResult.Failed(deleteResult.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<IdentityOperationResult> UpdateUserProfile(
            Guid userId,
            string email,
            string? displayName,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return IdentityOperationResult.Failed(["Пользователь не найден."]);
            }

            var normalizedEmail = email.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return IdentityOperationResult.Failed(["Email не может быть пустым."]);
            }

            if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(normalizedEmail);
                if (existing is not null && existing.Id != user.Id)
                {
                    return IdentityOperationResult.Failed(["Пользователь с таким email уже существует."]);
                }

                user.Email = normalizedEmail;
                user.UserName = normalizedEmail;
                user.NormalizedEmail = _userManager.NormalizeEmail(normalizedEmail);
                user.NormalizedUserName = _userManager.NormalizeName(normalizedEmail);
                user.EmailConfirmed = false;
            }

            user.DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? null
                : displayName.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return IdentityOperationResult.Failed(result.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<IdentityOperationResult> ChangePassword(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return IdentityOperationResult.Failed(["Пользователь не найден."]);
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
            {
                return IdentityOperationResult.Failed(result.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<bool> CheckPassword(
            Guid userId,
            string password,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            return await _userManager.CheckPasswordAsync(user, password);
        }

        public async Task<bool> IsFrozen(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _userManager.Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => x.IsFrozen)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IdentityOperationResult> SetFrozen(
            Guid userId,
            bool isFrozen,
            Guid? frozenByUserId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return IdentityOperationResult.Failed(["Пользователь не найден."]);
            }

            user.IsFrozen = isFrozen;
            user.FrozenAtUtc = isFrozen ? DateTime.UtcNow : null;
            user.FrozenByUserId = isFrozen ? frozenByUserId : null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return IdentityOperationResult.Failed(result.Errors.Select(x => x.Description));
            }

            return IdentityOperationResult.Success();
        }

        public async Task<IdentityUserWithRoles?> GetUserWithRoles(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            return new IdentityUserWithRoles
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                EmailConfirmed = user.EmailConfirmed,
                IsFrozen = user.IsFrozen,
                Roles = roles.ToList()
            };
        }

        public async Task<IReadOnlyList<IdentityUserWithRoles>> GetUsersWithRoles(
            string? search,
            string? roleFilter,
            int limit,
            bool excludeClients,
            CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 500);
            var normalizedSearch = search?.Trim().ToLowerInvariant();
            var normalizedRoleFilter = roleFilter?.Trim();

            var usersQuery = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                usersQuery = usersQuery.Where(x =>
                    (x.Email != null && x.Email.ToLower().Contains(normalizedSearch))
                    || (x.DisplayName != null && x.DisplayName.ToLower().Contains(normalizedSearch)));
            }

            var scanLimit = Math.Max(safeLimit * 5, 200);
            var users = await usersQuery
                .OrderBy(x => x.Email)
                .Take(scanLimit)
                .ToListAsync(cancellationToken);

            var result = new List<IdentityUserWithRoles>(safeLimit);

            foreach (var user in users)
            {
                var roles = (await _userManager.GetRolesAsync(user)).ToList();

                if (excludeClients)
                {
                    var nonClientRoles = roles
                        .Where(x => !string.Equals(x, AppRoles.Client, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (roles.Count > 0 && nonClientRoles.Count == 0)
                    {
                        continue;
                    }

                    roles = nonClientRoles;
                }

                if (!string.IsNullOrWhiteSpace(normalizedRoleFilter)
                    && !roles.Any(x => string.Equals(x, normalizedRoleFilter, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(new IdentityUserWithRoles
                {
                    UserId = user.Id,
                    Email = user.Email ?? string.Empty,
                    DisplayName = user.DisplayName,
                    EmailConfirmed = user.EmailConfirmed,
                    IsFrozen = user.IsFrozen,
                    Roles = roles
                });

                if (result.Count >= safeLimit)
                {
                    break;
                }
            }

            return result;
        }

        private static IdentityUserInfo MapUserInfo(ApplicationUser user)
        {
            return new IdentityUserInfo
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                EmailConfirmed = user.EmailConfirmed,
                IsFrozen = user.IsFrozen
            };
        }

        private static string GenerateSixDigitCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        private static bool IsCodeValid(
            ApplicationUser user,
            string code,
            string purpose,
            string? storedHash,
            DateTime? expiresAtUtc)
        {
            var normalizedCode = NormalizeCode(code);
            if (normalizedCode.Length != 6
                || storedHash is null
                || expiresAtUtc is null
                || expiresAtUtc.Value < DateTime.UtcNow)
            {
                return false;
            }

            var actualHash = BuildCodeHash(user, normalizedCode, purpose);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(storedHash),
                Encoding.UTF8.GetBytes(actualHash));
        }

        private static bool IsCodeValid(
            ApplicationUser user,
            string code,
            string purpose,
            string? storedHash,
            DateTime? expiresAtUtc,
            string email)
        {
            var normalizedCode = NormalizeCode(code);
            if (normalizedCode.Length != 6
                || storedHash is null
                || expiresAtUtc is null
                || expiresAtUtc.Value < DateTime.UtcNow)
            {
                return false;
            }

            var actualHash = BuildCodeHash(user, normalizedCode, purpose, email);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(storedHash),
                Encoding.UTF8.GetBytes(actualHash));
        }

        private static string BuildCodeHash(ApplicationUser user, string code, string purpose)
        {
            var normalizedEmail = user.Email?.Trim().ToUpperInvariant() ?? string.Empty;
            return BuildCodeHash(user, code, purpose, normalizedEmail);
        }

        private static string BuildCodeHash(
            ApplicationUser user,
            string code,
            string purpose,
            string email)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();
            var raw = $"{purpose}:{user.Id}:{normalizedEmail}:{user.SecurityStamp}:{NormalizeCode(code)}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }

        private static string NormalizeCode(string code)
        {
            return new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private static void ClearEmailConfirmationCode(ApplicationUser user)
        {
            user.EmailConfirmationCodeHash = null;
            user.EmailConfirmationCodeExpiresAtUtc = null;
        }

        private static void ClearPasswordResetCode(ApplicationUser user)
        {
            user.PasswordResetCodeHash = null;
            user.PasswordResetCodeExpiresAtUtc = null;
        }
    }
}
