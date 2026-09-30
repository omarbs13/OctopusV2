using FluentValidation;

namespace Pos.Application.Users.ResetUserPassword;

public sealed class ResetUserPasswordValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordValidator() =>
        UserRules.NewPassword(this, c => c.Password, c => c.ConfirmPassword);
}
