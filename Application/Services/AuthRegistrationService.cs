using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using System.Security.Cryptography;
using System.Text;

namespace Application.Services
{
    public class AuthRegistrationService
    {
        private const int CodeLifetimeMinutes = 15;
        private const int ResendCooldownSeconds = 30;

        private readonly IIdentityAccountManager _identityAccountManager;
        private readonly IClientRepository _clientRepository;
        private readonly IRealtorRegistrationRequestRepository _realtorRegistrationRequestRepository;
        private readonly IPendingRegistrationRepository _pendingRegistrationRepository;
        private readonly IApplicationEmailService _applicationEmailService;

        public AuthRegistrationService(
            IIdentityAccountManager identityAccountManager,
            IClientRepository clientRepository,
            IRealtorRegistrationRequestRepository realtorRegistrationRequestRepository,
            IPendingRegistrationRepository pendingRegistrationRepository,
            IApplicationEmailService applicationEmailService)
        {
            _identityAccountManager = identityAccountManager;
            _clientRepository = clientRepository;
            _realtorRegistrationRequestRepository = realtorRegistrationRequestRepository;
            _pendingRegistrationRepository = pendingRegistrationRepository;
            _applicationEmailService = applicationEmailService;
        }

        public Task<AuthRegistrationResponse> RegisterClient(
            RegisterClientRequest request,
            CancellationToken cancellationToken = default)
        {
            return StartPendingRegistration(
                PendingRegistrationType.Client,
                request.FirstName,
                request.LastName,
                request.MiddleName,
                request.Email,
                request.PhoneNumber,
                request.Password,
                request.ConfirmPassword,
                cancellationToken);
        }

        public Task<AuthRegistrationResponse> RegisterRealtorRequest(
            RegisterRealtorRequest request,
            CancellationToken cancellationToken = default)
        {
            return StartPendingRegistration(
                PendingRegistrationType.Realtor,
                request.FirstName,
                request.LastName,
                request.MiddleName,
                request.Email,
                request.PhoneNumber,
                request.Password,
                request.ConfirmPassword,
                cancellationToken);
        }

        public async Task<AuthRegistrationResponse?> ConfirmPendingRegistration(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(email);
            var pending = await _pendingRegistrationRepository.GetByEmail(normalizedEmail, cancellationToken);
            if (pending is null)
            {
                return null;
            }

            if (pending.CodeExpiresAtUtc < DateTime.UtcNow
                || !IsCodeValid(pending, code))
            {
                throw new ValidationException("Код подтверждения неверный или устарел. Запросите новый код.");
            }

            var fullName = new FullName(pending.FirstName, pending.LastName, pending.MiddleName);
            var displayName = fullName.ToString();
            var createIdentityResult = await _identityAccountManager.CreateUserWithPasswordHash(
                pending.Email,
                pending.PasswordHash,
                displayName,
                emailConfirmed: true,
                cancellationToken);

            if (!createIdentityResult.Succeeded || createIdentityResult.UserId is null)
                throw new ValidationException(JoinIdentityErrors(createIdentityResult.Errors));

            var userId = createIdentityResult.UserId.Value;

            try
            {
                if (pending.Type == PendingRegistrationType.Client)
                {
                    await CompleteClientRegistration(pending, userId, fullName, cancellationToken);
                    await _pendingRegistrationRepository.Delete(pending, cancellationToken);
                    return new AuthRegistrationResponse(
                        "Email подтвержден. Регистрация клиента завершена.",
                        "Client",
                        "Registered");
                }

                await CompleteRealtorRegistration(pending, userId, cancellationToken);
                await _pendingRegistrationRepository.Delete(pending, cancellationToken);
                return new AuthRegistrationResponse(
                    "Email подтвержден. Заявка риелтора отправлена администратору.",
                    "Realtor",
                    "Pending");
            }
            catch
            {
                await _identityAccountManager.DeleteUser(userId, cancellationToken);
                throw;
            }
        }

        public async Task ResendPendingCode(
            string email,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(email);
            var pending = await _pendingRegistrationRepository.GetByEmail(normalizedEmail, cancellationToken);
            if (pending is null)
            {
                return;
            }

            var secondsLeft = ResendCooldownSeconds
                - (int)(DateTime.UtcNow - pending.LastCodeSentAtUtc).TotalSeconds;
            if (secondsLeft > 0)
            {
                throw new ValidationException($"Новый код можно запросить через {secondsLeft} сек.");
            }

            var code = GenerateSixDigitCode();
            pending.RefreshCode(
                BuildCodeHash(pending.Email, pending.Type, code),
                DateTime.UtcNow.AddMinutes(CodeLifetimeMinutes),
                DateTime.UtcNow);

            await _pendingRegistrationRepository.SaveChanges(cancellationToken);

            await _applicationEmailService.SendEmailConfirmationAsync(
                pending.Email,
                code,
                BuildDisplayName(pending.FirstName, pending.LastName, pending.MiddleName),
                cancellationToken);
        }

        private async Task<AuthRegistrationResponse> StartPendingRegistration(
            PendingRegistrationType type,
            string firstName,
            string lastName,
            string? middleName,
            string email,
            string phoneNumber,
            string password,
            string confirmPassword,
            CancellationToken cancellationToken)
        {
            EnsurePasswordsMatch(password, confirmPassword);

            var normalizedEmail = NormalizeEmail(email);
            var normalizedPhone = phoneNumber.Trim();
            await _pendingRegistrationRepository.DeleteExpired(DateTime.UtcNow, cancellationToken);
            await EnsureRegistrationAvailable(type, normalizedEmail, normalizedPhone, cancellationToken);

            var fullName = new FullName(firstName.Trim(), lastName.Trim(), middleName?.Trim());
            var displayName = fullName.ToString();
            var passwordHashResult = await _identityAccountManager.BuildPasswordHash(
                normalizedEmail,
                password,
                displayName,
                cancellationToken);

            if (!passwordHashResult.Succeeded || string.IsNullOrWhiteSpace(passwordHashResult.PasswordHash))
                throw new ValidationException(JoinIdentityErrors(passwordHashResult.Errors));

            var existingPending = await _pendingRegistrationRepository.GetByEmail(normalizedEmail, cancellationToken);
            if (existingPending is not null)
            {
                await _pendingRegistrationRepository.Delete(existingPending, cancellationToken);
            }

            var code = GenerateSixDigitCode();
            var nowUtc = DateTime.UtcNow;
            var pending = new PendingRegistration(
                type,
                firstName.Trim(),
                lastName.Trim(),
                middleName?.Trim(),
                normalizedEmail,
                normalizedPhone,
                passwordHashResult.PasswordHash,
                BuildCodeHash(normalizedEmail, type, code),
                nowUtc.AddMinutes(CodeLifetimeMinutes),
                nowUtc);

            await _pendingRegistrationRepository.Add(pending);

            await _applicationEmailService.SendEmailConfirmationAsync(
                normalizedEmail,
                code,
                displayName,
                cancellationToken);

            return new AuthRegistrationResponse(
                type == PendingRegistrationType.Client
                    ? "Мы отправили код подтверждения email. Введите его, чтобы завершить регистрацию."
                    : "Мы отправили код подтверждения email. После ввода кода заявка риелтора будет отправлена администратору.",
                type == PendingRegistrationType.Client ? "Client" : "Realtor",
                "WaitingForEmailConfirmation");
        }

        private async Task EnsureRegistrationAvailable(
            PendingRegistrationType type,
            string normalizedEmail,
            string normalizedPhone,
            CancellationToken cancellationToken)
        {
            if (type == PendingRegistrationType.Client)
            {
                var existingClientByEmail = await _clientRepository.GetByEmail(normalizedEmail);
                if (existingClientByEmail is not null)
                    throw new ConflictException("Клиент с таким email уже существует.");

                var existingClientByPhone = await _clientRepository.GetByPhone(normalizedPhone);
                if (existingClientByPhone is not null)
                    throw new ConflictException("Клиент с таким номером телефона уже существует.");
            }
            else
            {
                var pendingByEmail = await _realtorRegistrationRequestRepository.GetPendingByEmail(normalizedEmail);
                if (pendingByEmail is not null)
                    throw new ConflictException("Заявка риелтора с таким email уже ожидает рассмотрения.");
            }

            if (await _identityAccountManager.EmailExists(normalizedEmail, cancellationToken))
                throw new ConflictException("Пользователь с таким email уже зарегистрирован.");

            var pendingByPhone = await _pendingRegistrationRepository.GetByPhone(normalizedPhone, cancellationToken);
            if (pendingByPhone is not null
                && !string.Equals(pendingByPhone.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("Для этого номера телефона уже ожидается подтверждение регистрации.");
            }
        }

        private async Task CompleteClientRegistration(
            PendingRegistration pending,
            Guid userId,
            FullName fullName,
            CancellationToken cancellationToken)
        {
            var roleResult = await _identityAccountManager.AddToRole(userId, "Client", cancellationToken);
            if (!roleResult.Succeeded)
                throw new ValidationException(JoinIdentityErrors(roleResult.Errors));

            var client = new Client(fullName, pending.PhoneNumber, pending.Email);
            await _clientRepository.Add(client);
        }

        private async Task CompleteRealtorRegistration(
            PendingRegistration pending,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var registrationRequest = new RealtorRegistrationRequest(
                pending.FirstName,
                pending.LastName,
                pending.MiddleName,
                pending.Email,
                pending.PhoneNumber,
                userId);

            await _realtorRegistrationRequestRepository.Add(registrationRequest);
        }

        private static void EnsurePasswordsMatch(string password, string confirmPassword)
        {
            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
                throw new ValidationException("Пароль и подтверждение пароля не совпадают.");
        }

        private static bool IsCodeValid(PendingRegistration pending, string code)
        {
            var normalizedCode = NormalizeCode(code);
            if (normalizedCode.Length != 6)
            {
                return false;
            }

            var actualHash = BuildCodeHash(pending.Email, pending.Type, normalizedCode);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(pending.CodeHash),
                Encoding.UTF8.GetBytes(actualHash));
        }

        private static string BuildCodeHash(string email, PendingRegistrationType type, string code)
        {
            var raw = $"pending-registration:{type}:{NormalizeEmail(email)}:{NormalizeCode(code)}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }

        private static string GenerateSixDigitCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        private static string NormalizeCode(string code)
        {
            return new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private static string BuildDisplayName(string firstName, string lastName, string? middleName)
        {
            return new FullName(firstName, lastName, middleName).ToString();
        }

        private static string JoinIdentityErrors(IReadOnlyList<string> errors)
        {
            if (errors.Count == 0)
                return "Не удалось выполнить операцию с учетной записью.";

            return string.Join("; ", errors);
        }
    }
}
