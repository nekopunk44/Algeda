using Application.DTOs.Profile;
using Application.DTOs.PropertyPhotos;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Services;

public sealed class UserProfileService
{
    private readonly IIdentityAccountManager _identityAccountManager;
    private readonly IClientRepository _clientRepository;
    private readonly IRealtorRepository _realtorRepository;
    private readonly IRealtorRegistrationRequestRepository _realtorRegistrationRequestRepository;
    private readonly IDealRepository _dealRepository;
    private readonly IRealtorCommissionSettingsService _commissionSettingsService;
    private readonly IPropertyPhotoStorageService _propertyPhotoStorageService;
    private readonly IApplicationEmailService _applicationEmailService;

    public UserProfileService(
        IIdentityAccountManager identityAccountManager,
        IClientRepository clientRepository,
        IRealtorRepository realtorRepository,
        IRealtorRegistrationRequestRepository realtorRegistrationRequestRepository,
        IDealRepository dealRepository,
        IRealtorCommissionSettingsService commissionSettingsService,
        IPropertyPhotoStorageService propertyPhotoStorageService,
        IApplicationEmailService applicationEmailService)
    {
        _identityAccountManager = identityAccountManager;
        _clientRepository = clientRepository;
        _realtorRepository = realtorRepository;
        _realtorRegistrationRequestRepository = realtorRegistrationRequestRepository;
        _dealRepository = dealRepository;
        _commissionSettingsService = commissionSettingsService;
        _propertyPhotoStorageService = propertyPhotoStorageService;
        _applicationEmailService = applicationEmailService;
    }

    public async Task<UserProfileResponse> GetCurrent(string email, bool isRealtor, bool isClient, bool isAdmin)
    {
        var normalizedEmail = NormalizeEmail(email);
        var identityUser = await _identityAccountManager.GetUserByEmail(normalizedEmail);

        if (isRealtor)
        {
            var realtor = await TryResolveRealtor(normalizedEmail, identityUser?.UserId);
            if (realtor is not null)
                return await BuildRealtorResponse(normalizedEmail, realtor, isClient, isAdmin);
        }

        if (isClient)
        {
            var client = await _clientRepository.GetByEmail(normalizedEmail);
            if (client is not null)
                return BuildClientResponse(normalizedEmail, client, isAdmin);
        }

        if (isAdmin)
        {
            if (identityUser is null)
                throw new NotFoundException("Пользователь не найден.");

            return BuildAdminResponse(identityUser);
        }

        if (isRealtor)
            throw new NotFoundException("Аккаунт риелтора не найден.");

        throw new NotFoundException("Аккаунт клиента не найден.");
    }

    public async Task<UserProfileResponse> UpdateCurrent(
        string currentEmail,
        bool isRealtor,
        bool isClient,
        bool isAdmin,
        UpdateUserProfileRequest request)
    {
        var normalizedCurrentEmail = NormalizeEmail(currentEmail);
        var normalizedNewEmail = NormalizeEmail(request.Email);
        var identityUser = await _identityAccountManager.GetUserByEmail(normalizedCurrentEmail);
        if (identityUser is null)
            throw new NotFoundException("Пользователь не найден.");

        if (!string.Equals(normalizedCurrentEmail, normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Для смены email запросите код подтверждения.");

        var fullName = new FullName(request.FirstName, request.LastName, request.MiddleName);
        var displayName = BuildDisplayName(fullName);

        var identityResult = await _identityAccountManager.UpdateUserProfile(
            identityUser.UserId,
            normalizedCurrentEmail,
            displayName);

        if (!identityResult.Succeeded)
            throw new ValidationException(JoinIdentityErrors(identityResult.Errors));

        if (isRealtor)
        {
            var realtor = await TryResolveRealtor(normalizedCurrentEmail, identityUser.UserId);
            if (realtor is not null)
            {
                realtor.UpdateProfile(fullName, NormalizeRequiredPhone(request.PhoneNumber));
                _realtorRepository.Update(realtor);

                return await BuildRealtorResponse(normalizedCurrentEmail, realtor, isClient, isAdmin);
            }
        }

        if (isClient)
        {
            var client = await _clientRepository.GetByEmail(normalizedCurrentEmail);
            if (client is not null)
            {
                client.UpdateProfile(fullName, NormalizeRequiredPhone(request.PhoneNumber), normalizedCurrentEmail);
                _clientRepository.Update(client);

                return BuildClientResponse(normalizedCurrentEmail, client, isAdmin);
            }
        }

        if (isAdmin)
        {
            return BuildAdminResponse(normalizedCurrentEmail, fullName);
        }

        if (isRealtor)
            throw new NotFoundException("Аккаунт риелтора не найден.");

        throw new NotFoundException("Аккаунт клиента не найден.");
    }

    public async Task RequestEmailChange(
        string currentEmail,
        RequestEmailChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrentEmail = NormalizeEmail(currentEmail);
        var normalizedNewEmail = NormalizeEmail(request.NewEmail);
        if (string.Equals(normalizedCurrentEmail, normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Новый email совпадает с текущим.");

        var identityUser = await _identityAccountManager.GetUserByEmail(normalizedCurrentEmail, cancellationToken);
        if (identityUser is null)
            throw new NotFoundException("Пользователь не найден.");

        if (await _identityAccountManager.EmailExists(normalizedNewEmail, cancellationToken))
            throw new ConflictException("Пользователь с таким email уже существует.");

        var code = await _identityAccountManager.GenerateEmailChangeCode(
            identityUser.UserId,
            normalizedNewEmail,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("Не удалось сформировать код подтверждения email.");

        await _applicationEmailService.SendEmailChangeConfirmationAsync(
            normalizedNewEmail,
            code,
            identityUser.DisplayName,
            cancellationToken);
    }

    public async Task<UserProfileResponse> ConfirmEmailChange(
        string currentEmail,
        bool isRealtor,
        bool isClient,
        bool isAdmin,
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrentEmail = NormalizeEmail(currentEmail);
        var normalizedNewEmail = NormalizeEmail(request.NewEmail);
        var identityUser = await _identityAccountManager.GetUserByEmail(normalizedCurrentEmail, cancellationToken);
        if (identityUser is null)
            throw new NotFoundException("Пользователь не найден.");

        var result = await _identityAccountManager.ChangeEmailByCode(
            identityUser.UserId,
            normalizedNewEmail,
            request.Code,
            cancellationToken);
        if (!result.Succeeded)
            throw new ValidationException(JoinIdentityErrors(result.Errors));

        Realtor? updatedRealtor = null;
        Client? updatedClient = null;

        if (isRealtor)
        {
            var approved = await _realtorRegistrationRequestRepository.GetLatestApprovedByIdentityUserId(identityUser.UserId);
            if (approved is not null)
            {
                approved.UpdateEmail(normalizedNewEmail);
                _realtorRegistrationRequestRepository.Update(approved);

                updatedRealtor = await _realtorRepository.GetByPhone(approved.PhoneNumber);
            }
        }

        if (isClient)
        {
            var client = await _clientRepository.GetByEmail(normalizedCurrentEmail);
            if (client is not null)
            {
                client.UpdateProfile(client.FullName, client.PhoneNumber, normalizedNewEmail);
                _clientRepository.Update(client);
                updatedClient = client;
            }
        }

        if (updatedRealtor is not null)
            return await BuildRealtorResponse(normalizedNewEmail, updatedRealtor, isClient, isAdmin);

        if (updatedClient is not null)
            return BuildClientResponse(normalizedNewEmail, updatedClient, isAdmin);

        if (isAdmin)
        {
            var fullName = SplitDisplayName(identityUser.DisplayName, normalizedNewEmail);
            return BuildAdminResponse(normalizedNewEmail, fullName);
        }

        return await GetCurrent(normalizedNewEmail, isRealtor, isClient, isAdmin);
    }

    public async Task ChangePassword(string email, ChangeMyPasswordRequest request)
    {
        var normalizedEmail = NormalizeEmail(email);
        var identityUser = await _identityAccountManager.GetUserByEmail(normalizedEmail);
        if (identityUser is null)
            throw new NotFoundException("Пользователь не найден.");

        var result = await _identityAccountManager.ChangePassword(
            identityUser.UserId,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
            throw new ValidationException(JoinIdentityErrors(result.Errors));
    }

    public async Task<string> UploadRealtorAvatar(
        string email,
        string fileName,
        string? contentType,
        byte[] content)
    {
        var normalizedEmail = NormalizeEmail(email);
        var realtor = await ResolveRealtor(normalizedEmail);

        var result = await _propertyPhotoStorageService.UploadForRealtors(
        [
            new PropertyPhotoUploadItem(fileName, contentType, content)
        ],
        CancellationToken.None);

        var path = result.FirstOrDefault()?.Path;
        if (string.IsNullOrWhiteSpace(path))
            throw new ValidationException("Не удалось сохранить аватар риелтора.");

        realtor.SetAvatarPath(path);
        _realtorRepository.Update(realtor);
        return path;
    }

    public async Task RemoveRealtorAvatar(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var realtor = await ResolveRealtor(normalizedEmail);
        realtor.RemoveAvatar();
        _realtorRepository.Update(realtor);
    }

    private async Task<Realtor> ResolveRealtor(string email, Guid? identityUserId = null)
    {
        var realtor = await TryResolveRealtor(email, identityUserId);
        if (realtor is null)
            throw new NotFoundException("Аккаунт риелтора не найден.");

        return realtor;
    }

    private async Task<Realtor?> TryResolveRealtor(string email, Guid? identityUserId = null)
    {
        var approved = await _realtorRegistrationRequestRepository.GetLatestApprovedByEmail(email);
        if (approved is null && identityUserId.HasValue)
        {
            approved = await _realtorRegistrationRequestRepository
                .GetLatestApprovedByIdentityUserId(identityUserId.Value);
        }

        if (approved is null)
        {
            var identityUser = await _identityAccountManager.GetUserByEmail(email);
            if (identityUser is not null)
            {
                approved = await _realtorRegistrationRequestRepository
                    .GetLatestApprovedByIdentityUserId(identityUser.UserId);
            }
        }

        if (approved is null)
            return null;

        var realtor = await _realtorRepository.GetByPhone(approved.PhoneNumber);
        return realtor;
    }

    private async Task<UserProfileResponse> BuildRealtorResponse(
        string email,
        Realtor realtor,
        bool isClient,
        bool isAdmin)
    {
        var money = await BuildRealtorMoneySummary(realtor.Id);
        return new UserProfileResponse(
            email,
            realtor.FullName.FirstName,
            realtor.FullName.LastName,
            realtor.FullName.MiddleName,
            realtor.PhoneNumber,
            true,
            realtor.AvatarPath,
            realtor.Level.ToString(),
            realtor.IsLevelManuallyAssigned,
            _commissionSettingsService.GetPercentForLevel(realtor.Level),
            money.ThisMonth,
            money.Total,
            money.Currency,
            isClient,
            isAdmin);
    }

    private static UserProfileResponse BuildClientResponse(string email, Client client, bool isAdmin)
    {
        return new UserProfileResponse(
            email,
            client.FullName.FirstName,
            client.FullName.LastName,
            client.FullName.MiddleName,
            client.PhoneNumber,
            false,
            null,
            IsClient: true,
            IsAdmin: isAdmin);
    }

    private static UserProfileResponse BuildAdminResponse(IdentityUserInfo identityUser)
    {
        var fullName = SplitDisplayName(identityUser.DisplayName, identityUser.Email);
        return BuildAdminResponse(identityUser.Email, fullName);
    }

    private static UserProfileResponse BuildAdminResponse(string email, FullName fullName)
    {
        return new UserProfileResponse(
            email,
            fullName.FirstName,
            fullName.LastName,
            fullName.MiddleName,
            string.Empty,
            false,
            null,
            IsAdmin: true);
    }

    private async Task<(decimal ThisMonth, decimal Total, string Currency)> BuildRealtorMoneySummary(Guid realtorId)
    {
        var deals = await _dealRepository.GetByRealtor(realtorId);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var completed = deals
            .Where(x => x.Status == Domain.Enums.DealStatus.Completed)
            .ToList();

        var total = completed.Sum(x => x.RealtorPayoutAmount);
        var thisMonth = completed
            .Where(x => x.CompletedAt.HasValue && x.CompletedAt.Value >= monthStart)
            .Sum(x => x.RealtorPayoutAmount);

        var currency = completed
            .Select(x => x.RealtorPayoutCurrency)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? "USD";

        return (thisMonth, total, currency);
    }

    private static string NormalizeRequiredPhone(string? phoneNumber)
    {
        var normalized = phoneNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ValidationException("Введите номер телефона.");

        return normalized;
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ValidationException("Email пользователя не найден в токене.");

        return normalized;
    }

    private static FullName SplitDisplayName(string? displayName, string email)
    {
        var parts = (displayName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            >= 3 => new FullName(parts[1], parts[0], string.Join(' ', parts.Skip(2))),
            2 => new FullName(parts[1], parts[0], null),
            1 => new FullName("Администратор", parts[0], null),
            _ => new FullName(GetEmailLocalPart(email), "Администратор", null)
        };
    }

    private static string GetEmailLocalPart(string email)
    {
        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        var localPart = atIndex > 0
            ? email[..atIndex]
            : email;

        return string.IsNullOrWhiteSpace(localPart)
            ? "Admin"
            : localPart;
    }

    private static string BuildDisplayName(FullName fullName)
    {
        return string.Join(' ', new[]
        {
            fullName.LastName,
            fullName.FirstName,
            fullName.MiddleName
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string JoinIdentityErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
            return "Не удалось выполнить операцию с учетной записью.";

        return string.Join("; ", errors);
    }
}
