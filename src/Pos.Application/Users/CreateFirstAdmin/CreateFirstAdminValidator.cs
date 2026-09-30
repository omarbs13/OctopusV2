using FluentValidation;

namespace Pos.Application.Users.CreateFirstAdmin;

public sealed class CreateFirstAdminValidator : AbstractValidator<CreateFirstAdminCommand>
{
    public CreateFirstAdminValidator()
    {
        UserRules.FullName(this, c => c.FullName);
        UserRules.UserName(this, c => c.UserName);
        UserRules.NewPassword(this, c => c.Password, c => c.ConfirmPassword);
    }
}
