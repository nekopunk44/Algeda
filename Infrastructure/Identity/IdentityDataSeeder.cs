using Application.Interfaces;
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity
{
    public class IdentityDataSeeder
    {
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IClientRepository _clientRepository;
        private readonly IRealtorRepository _realtorRepository;
        private readonly IOptions<IdentitySeedOptions> _options;
        private readonly ILogger<IdentityDataSeeder> _logger;

        public IdentityDataSeeder(
            RoleManager<IdentityRole<Guid>> roleManager,
            UserManager<ApplicationUser> userManager,
            IClientRepository clientRepository,
            IRealtorRepository realtorRepository,
            IOptions<IdentitySeedOptions> options,
            ILogger<IdentityDataSeeder> logger)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _clientRepository = clientRepository;
            _realtorRepository = realtorRepository;
            _options = options;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            var options = _options.Value;

            if (!options.Enabled)
            {
                _logger.LogInformation("Сидирование Identity отключено.");
                return;
            }
            foreach (var role in AppRoles.All)
            {
                if (await _roleManager.RoleExistsAsync(role))
                {
                    continue;
                }

                var createRole = await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (!createRole.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Не удалось создать роль '{role}': {string.Join("; ", createRole.Errors.Select(e => e.Description))}");
                }
            }

            await EnsureUserAsync(options.Admin, AppRoles.Admin, cancellationToken);
            if (!string.IsNullOrWhiteSpace(options.BootstrapSuperAdminEmail))
            {
                await EnsureExistingBootstrapSuperAdmin(
                    options.BootstrapSuperAdminEmail,
                    cancellationToken);
            }
            var seededRealtorUser = await EnsureUserAsync(options.Realtor, AppRoles.Realtor, cancellationToken);
            await EnsureRealtorProfileAsync(seededRealtorUser, options.Realtor);
            var seededClientUser = await EnsureUserAsync(options.Client, AppRoles.Client, cancellationToken);
            await EnsureClientProfileAsync(seededClientUser, options.Client);
        }

        private async Task EnsureExistingBootstrapSuperAdmin(
            string bootstrapSuperAdminEmail,
            CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(bootstrapSuperAdminEmail.Trim());
            if (user is null)
            {
                _logger.LogWarning(
                    "Главный администратор {Email} не найден. Роль SuperAdmin будет назначена после появления такого аккаунта.",
                    bootstrapSuperAdminEmail);
                return;
            }

            await EnsureRoleAsync(user, AppRoles.Admin);
            await EnsureRoleAsync(user, AppRoles.SuperAdmin);
        }

        private async Task EnsureRoleAsync(ApplicationUser user, string role)
        {
            if (await _userManager.IsInRoleAsync(user, role))
            {
                return;
            }

            var addRole = await _userManager.AddToRoleAsync(user, role);
            if (!addRole.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Не удалось назначить роль '{role}' пользователю '{user.Email}': {string.Join("; ", addRole.Errors.Select(e => e.Description))}");
            }
        }

        private async Task<ApplicationUser> EnsureUserAsync(
            SeedUserOptions userOptions,
            string role,
            CancellationToken cancellationToken)
        {
            var normalizedEmail = userOptions.Email.Trim();
            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existingUser is null)
            {
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = normalizedEmail,
                    Email = normalizedEmail,
                    DisplayName = userOptions.DisplayName,
                    EmailConfirmed = true
                };

                var createUser = await _userManager.CreateAsync(user, userOptions.Password);
                if (!createUser.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Не удалось создать пользователя '{normalizedEmail}': {string.Join("; ", createUser.Errors.Select(e => e.Description))}");
                }

                existingUser = user;
                _logger.LogInformation("Создан пользователь {Email} для роли {Role}.", normalizedEmail, role);
            }
            else if (!string.Equals(existingUser.DisplayName, userOptions.DisplayName, StringComparison.Ordinal))
            {
                existingUser.DisplayName = userOptions.DisplayName;
                await _userManager.UpdateAsync(existingUser);
            }

            await EnsureRoleAsync(existingUser, role);

            return existingUser;
        }

        private async Task EnsureClientProfileAsync(ApplicationUser user, SeedUserOptions userOptions)
        {
            var normalizedEmail = userOptions.Email.Trim().ToLowerInvariant();
            var existingClient = await _clientRepository.GetByEmail(normalizedEmail);
            if (existingClient is not null)
            {
                if (!IsPhoneNumberValid(existingClient.PhoneNumber))
                {
                    existingClient.Update(BuildSyntheticPhoneNumber(user.Id));
                    _clientRepository.Update(existingClient);
                    _logger.LogInformation("Обновлён номер телефона профиля клиента для {Email}.", normalizedEmail);
                }

                return;
            }

            var fullName = BuildClientFullName(userOptions.DisplayName, normalizedEmail);
            var client = new Client(fullName, BuildSyntheticPhoneNumber(user.Id), normalizedEmail);
            await _clientRepository.Add(client);
            _logger.LogInformation("Создан профиль клиента для {Email}.", normalizedEmail);
        }

        private async Task EnsureRealtorProfileAsync(ApplicationUser user, SeedUserOptions userOptions)
        {
            var normalizedEmail = userOptions.Email.Trim();
            var fullName = BuildRealtorFullName(userOptions.DisplayName, normalizedEmail);
            var phone = BuildSyntheticPhoneNumber(user.Id);

            var existingRealtor = await _realtorRepository.GetByPhone(phone);
            if (existingRealtor is not null)
            {
                return;
            }

            var realtors = await _realtorRepository.GetActive();
            var existingByName = realtors.FirstOrDefault(x =>
                string.Equals(x.FullName.ToString(), fullName.ToString(), StringComparison.OrdinalIgnoreCase));

            if (existingByName is not null)
            {
                if (!IsPhoneNumberValid(existingByName.PhoneNumber))
                {
                    existingByName.UpdateProfile(fullName, phone);
                    _realtorRepository.Update(existingByName);
                    _logger.LogInformation("Обновлён номер телефона профиля риелтора для {Email}.", normalizedEmail);
                }

                return;
            }

            var realtor = new Realtor(fullName, phone);
            await _realtorRepository.Add(realtor);
            _logger.LogInformation("Создан профиль риелтора для {Email}.", normalizedEmail);
        }

        private static FullName BuildClientFullName(string? displayName, string email)
        {
            var tokens = displayName?
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];

            if (tokens.Length >= 2)
            {
                var lastName = tokens[0];
                var firstName = tokens[1];
                var middleName = tokens.Length > 2
                    ? string.Join(' ', tokens.Skip(2))
                    : null;

                return new FullName(firstName, lastName, middleName);
            }

            if (tokens.Length == 1)
            {
                return new FullName("Client", tokens[0], null);
            }

            var localPart = email.Split('@', 2)[0].Trim();
            if (!string.IsNullOrWhiteSpace(localPart))
            {
                return new FullName("Client", localPart, null);
            }

            return new FullName("Client", "Account", null);
        }

        private static FullName BuildRealtorFullName(string? displayName, string email)
        {
            var tokens = displayName?
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];

            if (tokens.Length >= 2)
            {
                var lastName = tokens[0];
                var firstName = tokens[1];
                var middleName = tokens.Length > 2
                    ? string.Join(' ', tokens.Skip(2))
                    : null;

                return new FullName(firstName, lastName, middleName);
            }

            if (tokens.Length == 1)
            {
                return new FullName("Realtor", tokens[0], null);
            }

            var localPart = email.Split('@', 2)[0].Trim();
            if (!string.IsNullOrWhiteSpace(localPart))
            {
                return new FullName("Realtor", localPart, null);
            }

            return new FullName("Realtor", "Account", null);
        }

        private static string BuildSyntheticPhoneNumber(Guid seed)
        {
            var raw = BitConverter.ToUInt32(seed.ToByteArray(), 0);
            var sixDigits = (raw % 1_000_000U).ToString("D6");
            return $"+37377{sixDigits}";
        }

        private static bool IsPhoneNumberValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var phone = value.Trim();
            var plusCount = phone.Count(c => c == '+');
            if (plusCount > 1 || (plusCount == 1 && !phone.StartsWith("+", StringComparison.Ordinal)))
            {
                return false;
            }

            if (phone.Any(c => !char.IsDigit(c) && c != '+' && c != ' ' && c != '-' && c != '(' && c != ')'))
            {
                return false;
            }

            var digitCount = phone.Count(char.IsDigit);
            return digitCount is >= 7 and <= 15;
        }
    }
}
