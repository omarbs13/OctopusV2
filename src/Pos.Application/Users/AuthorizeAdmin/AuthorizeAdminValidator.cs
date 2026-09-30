using FluentValidation;

namespace Pos.Application.Users.AuthorizeAdmin;

public sealed class AuthorizeAdminValidator : AbstractValidator<AuthorizeAdminCommand>
{
    public AuthorizeAdminValidator()
    {
        RuleFor(c => (c.UserName ?? string.Empty).Trim())
            .NotEmpty().WithMessage(UserMessages.UserNameRequired)
            .OverridePropertyName(UserFields.UserName);
        RuleFor(c => c.Password ?? string.Empty)
            .NotEmpty().WithMessage(UserMessages.PasswordRequired)
            .OverridePropertyName(UserFields.Password);
    }
}
