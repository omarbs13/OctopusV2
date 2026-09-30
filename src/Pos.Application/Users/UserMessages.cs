using Pos.Domain.Users;

namespace Pos.Application.Users;

/// <summary>Mensajes de validación de usuarios y seguridad, en español.</summary>
public static class UserMessages
{
    public const string InvalidCredentials = "Usuario o contraseña incorrectos.";
    public const string AuthorizationInvalidCredentials = "Usuario o contraseña incorrectos, o el usuario no es administrador.";
    public const string FullNameRequired = "El nombre completo es obligatorio.";
    public const string UserNameRequired = "El usuario es obligatorio.";
    public const string UserNameFormat = "El usuario admite de 3 a 40 caracteres: letras, dígitos, punto, guion y guion bajo, sin espacios.";
    public const string UserNameDuplicate = "Ya existe un usuario con ese nombre.";
    public const string PasswordRequired = "La contraseña es obligatoria.";
    public const string PasswordTooShort = "La contraseña debe tener al menos 8 caracteres.";
    public const string PasswordMismatch = "La confirmación no coincide con la contraseña.";
    public const string CurrentPasswordIncorrect = "La contraseña actual no es correcta.";
    public const string NewPasswordSameAsCurrent = "La nueva contraseña debe ser distinta de la actual.";
    public const string CannotResetOwnPassword = "Para cambiar tu propia contraseña usa \"Cambiar contraseña\".";
    public const string SetupAlreadyDone = "El sistema ya tiene usuarios. Inicia sesión.";
    public const string DateRangeInvalid = "La fecha inicial no puede ser posterior a la final.";
    public const string IdleLockRange = "El tiempo de inactividad debe estar entre 1 y 240 minutos.";
    public const string NotAuthorizable = "Esta operación no admite autorización de un administrador.";

    public static readonly string FullNameTooLong = $"El nombre completo admite hasta {User.FullNameMaxLength} caracteres.";

    public static string LockedOut(DateTime untilUtc, DateTime nowUtc)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling((untilUtc - nowUtc).TotalMinutes));
        return $"Usuario bloqueado temporalmente por intentos fallidos. Intenta de nuevo en {minutes} minuto(s).";
    }
}
