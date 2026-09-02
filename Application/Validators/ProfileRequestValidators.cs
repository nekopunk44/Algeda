using Application.DTOs.Profile;
using FluentValidation;

namespace Application.Validators;

public sealed class UpdateUserProfileRequestValidator : AbstractValidator<UpdateUserProfileRequest>
{
    public UpdateUserProfileRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MiddleName).MaximumLength(100);
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(32)
            .Must(PhoneNumberValidation.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Номер телефона может содержать только цифры и разделители (+, -, пробелы, скобки), количество цифр — от 7 до 15.");
    }
}

public sealed class ChangeMyPasswordRequestValidator : AbstractValidator<ChangeMyPasswordRequest>
{
    public ChangeMyPasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .Equal(x => x.NewPassword)
            .WithMessage("Пароль и подтверждение пароля не совпадают.");
    }
}

public sealed class RequestEmailChangeRequestValidator : AbstractValidator<RequestEmailChangeRequest>
{
    public RequestEmailChangeRequestValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().MaximumLength(256).EmailAddress();
    }
}

public sealed class ConfirmEmailChangeRequestValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeRequestValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
            .WithMessage("Код должен состоять из 6 цифр.");
    }
}
