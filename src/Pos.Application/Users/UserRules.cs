using FluentValidation;
using Pos.Domain.Users;

namespace Pos.Application.Users;

/// <summary>Reglas de entrada de usuario compartidas por los validadores.</summary>
internal static class UserRules
{
    public static void FullName<T>(AbstractValidator<T> validator, Func<T, string?> fullName) =>
        validator.RuleFor(x => (fullName(x) ?? string.Empty).Trim())
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(UserMessages.FullNameRequired)
            .MaximumLength(User.FullNameMaxLength).WithMessage(UserMessages.FullNameTooLong)
            .OverridePropertyName(UserFields.FullName);

    public static void UserName<T>(AbstractValidator<T> validator, Func<T, string?> userName) =>
        validator.RuleFor(x => (userName(x) ?? string.Empty).Trim())
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(UserMessages.UserNameRequired)
            .Must(UserNameRules.IsValid).WithMessage(UserMessages.UserNameFormat)
            .OverridePropertyName(UserFields.UserName);

    /// <summary>Contraseña nueva de al menos 8 caracteres con confirmación igual (no se recorta).</summary>
    public static void NewPassword<T>(
        AbstractValidator<T> validator,
        Func<T, string?> password,
        Func<T, string?> confirmation,
        string passwordField = UserFields.Password)
    {
        validator.RuleFor(x => password(x) ?? string.Empty)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(UserMessages.PasswordRequired)
            .MinimumLength(User.PasswordMinLength).WithMessage(UserMessages.PasswordTooShort)
            .OverridePropertyName(passwordField);

        validator.RuleFor(x => confirmation(x) ?? string.Empty)
            .Must((x, confirm) => string.Equals(confirm, password(x) ?? string.Empty, StringComparison.Ordinal))
            .WithMessage(UserMessages.PasswordMismatch)
            .OverridePropertyName(UserFields.ConfirmPassword);
    }
}
