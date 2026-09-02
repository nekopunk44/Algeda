using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

public sealed class RussianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError()
    {
        return Error(nameof(DefaultError), "Не удалось выполнить операцию с учетной записью.");
    }

    public override IdentityError ConcurrencyFailure()
    {
        return Error(nameof(ConcurrencyFailure), "Данные учетной записи были изменены. Обновите страницу и повторите попытку.");
    }

    public override IdentityError PasswordMismatch()
    {
        return Error(nameof(PasswordMismatch), "Текущий пароль указан неверно.");
    }

    public override IdentityError InvalidToken()
    {
        return Error(nameof(InvalidToken), "Ссылка или токен недействительны либо устарели.");
    }

    public override IdentityError LoginAlreadyAssociated()
    {
        return Error(nameof(LoginAlreadyAssociated), "Этот внешний вход уже связан с другой учетной записью.");
    }

    public override IdentityError InvalidUserName(string? userName)
    {
        return Error(nameof(InvalidUserName), "Email указан в некорректном формате.");
    }

    public override IdentityError InvalidEmail(string? email)
    {
        return Error(nameof(InvalidEmail), "Введите корректный email.");
    }

    public override IdentityError DuplicateUserName(string userName)
    {
        return Error(nameof(DuplicateUserName), "Пользователь с таким email уже существует.");
    }

    public override IdentityError DuplicateEmail(string email)
    {
        return Error(nameof(DuplicateEmail), "Пользователь с таким email уже существует.");
    }

    public override IdentityError InvalidRoleName(string? role)
    {
        return Error(nameof(InvalidRoleName), "Указана некорректная роль.");
    }

    public override IdentityError DuplicateRoleName(string role)
    {
        return Error(nameof(DuplicateRoleName), "Такая роль уже существует.");
    }

    public override IdentityError UserAlreadyHasPassword()
    {
        return Error(nameof(UserAlreadyHasPassword), "У пользователя уже задан пароль.");
    }

    public override IdentityError UserLockoutNotEnabled()
    {
        return Error(nameof(UserLockoutNotEnabled), "Блокировка для этого пользователя не включена.");
    }

    public override IdentityError UserAlreadyInRole(string role)
    {
        return Error(nameof(UserAlreadyInRole), "Пользователь уже имеет эту роль.");
    }

    public override IdentityError UserNotInRole(string role)
    {
        return Error(nameof(UserNotInRole), "У пользователя нет этой роли.");
    }

    public override IdentityError PasswordTooShort(int length)
    {
        return Error(nameof(PasswordTooShort), $"Пароль должен содержать минимум {length} символов.");
    }

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
    {
        return Error(nameof(PasswordRequiresUniqueChars), $"Пароль должен содержать минимум {uniqueChars} разных символов.");
    }

    public override IdentityError PasswordRequiresNonAlphanumeric()
    {
        return Error(nameof(PasswordRequiresNonAlphanumeric), "Пароль должен содержать хотя бы один специальный символ.");
    }

    public override IdentityError PasswordRequiresDigit()
    {
        return Error(nameof(PasswordRequiresDigit), "Пароль должен содержать хотя бы одну цифру.");
    }

    public override IdentityError PasswordRequiresLower()
    {
        return Error(nameof(PasswordRequiresLower), "Пароль должен содержать хотя бы одну строчную букву.");
    }

    public override IdentityError PasswordRequiresUpper()
    {
        return Error(nameof(PasswordRequiresUpper), "Пароль должен содержать хотя бы одну заглавную букву.");
    }

    public override IdentityError RecoveryCodeRedemptionFailed()
    {
        return Error(nameof(RecoveryCodeRedemptionFailed), "Код восстановления недействителен.");
    }

    private static IdentityError Error(string code, string description)
    {
        return new IdentityError
        {
            Code = code,
            Description = description
        };
    }
}
