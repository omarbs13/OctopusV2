using FluentValidation;

namespace Pos.Application.Users.UpdateUser;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        UserRules.FullName(this, c => c.FullName);
        UserRules.UserName(this, c => c.UserName);
        RuleFor(c => c.Role).IsInEnum().OverridePropertyName(UserFields.Role);
    }
}
