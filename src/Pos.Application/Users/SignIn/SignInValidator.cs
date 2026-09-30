using FluentValidation;

namespace Pos.Application.Users.SignIn;

public sealed class SignInValidator : AbstractValidator<SignInCommand>
{
    public SignInValidator()
    {
        RuleFor(c => (c.UserName ?? string.Empty).Trim())
            .NotEmpty().WithMessage(UserMessages.UserNameRequired)
            .OverridePropertyName(UserFields.UserName);
        RuleFor(c => c.Password ?? string.Empty)
            .NotEmpty().WithMessage(UserMessages.PasswordRequired)
            .OverridePropertyName(UserFields.Password);
    }
}
