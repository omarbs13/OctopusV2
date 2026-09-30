using FluentValidation;

namespace Pos.Application.Users.CreateUser;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        UserRules.FullName(this, c => c.FullName);
        UserRules.UserName(this, c => c.UserName);
        UserRules.NewPassword(this, c => c.Password, c => c.ConfirmPassword);
        RuleFor(c => c.Role).IsInEnum().OverridePropertyName(UserFields.Role);
    }
}
