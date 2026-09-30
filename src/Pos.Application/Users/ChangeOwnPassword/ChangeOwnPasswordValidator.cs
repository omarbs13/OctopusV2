using FluentValidation;

namespace Pos.Application.Users.ChangeOwnPassword;

public sealed class ChangeOwnPasswordValidator : AbstractValidator<ChangeOwnPasswordCommand>
{
    public ChangeOwnPasswordValidator()
    {
        RuleFor(c => c.CurrentPassword ?? string.Empty)
            .NotEmpty().WithMessage(UserMessages.PasswordRequired)
            .OverridePropertyName(UserFields.CurrentPassword)
            .When(c => c.SealedUserId is null);
        UserRules.NewPassword(this, c => c.NewPassword, c => c.ConfirmPassword);
    }
}
