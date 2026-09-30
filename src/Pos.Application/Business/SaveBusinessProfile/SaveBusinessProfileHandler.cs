using FluentValidation;
using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Domain.Business;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Business.SaveBusinessProfile;

/// <summary>
/// Guarda los datos del negocio: crea la fila la primera vez y la actualiza después. Un logotipo
/// inválido se rechaza y conserva el anterior (006, US1 #4).
/// </summary>
public sealed class SaveBusinessProfileHandler
{
    private readonly IAccessControl _access;
    private readonly IBusinessProfileRepository _profiles;
    private readonly IImageProcessor _imageProcessor;
    private readonly IValidator<SaveBusinessProfileCommand> _validator;

    public SaveBusinessProfileHandler(
        IAccessControl access,
        IBusinessProfileRepository profiles,
        IImageProcessor imageProcessor,
        IValidator<SaveBusinessProfileCommand> validator)
    {
        _access = access;
        _profiles = profiles;
        _imageProcessor = imageProcessor;
        _validator = validator;
    }

    public async Task<Result> HandleAsync(SaveBusinessProfileCommand command, CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ManageSettings, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        ArgumentNullException.ThrowIfNull(command);

        var validation = await _validator.ValidateAsync(command, cancellationToken);
        var errors = validation.Errors.Select(e => new FieldError(e.PropertyName, e.ErrorMessage)).ToList();

        byte[]? newLogo = null;
        if (command.Logo is LogoChange.Replace replace)
        {
            if (replace.Length > BusinessProfile.LogoMaxBytes)
            {
                errors.Add(new FieldError(BusinessFields.Logo, BusinessMessages.LogoTooLarge));
            }
            else
            {
                var processed = await Task.Run(() => _imageProcessor.Process(replace.Content), cancellationToken);
                if (processed.Error is { } reason)
                {
                    errors.Add(new FieldError(BusinessFields.Logo, reason == InvalidImageReason.TooLarge
                        ? BusinessMessages.LogoTooLarge
                        : BusinessMessages.LogoInvalid));
                }
                else
                {
                    newLogo = processed.Content;
                }
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure(new ValidationFailed(errors));
        }

        var profile = await _profiles.GetAsync(cancellationToken);
        if (profile is null)
        {
            profile = BusinessProfile.Create(command.TradeName, command.Address, command.Phone, command.TaxId, command.FooterMessage);
            _profiles.Add(profile);
        }
        else
        {
            profile.Update(command.TradeName, command.Address, command.Phone, command.TaxId, command.FooterMessage);
        }

        switch (command.Logo)
        {
            case LogoChange.Replace when newLogo is not null:
                profile.SetLogo(newLogo);
                break;
            case LogoChange.Remove:
                profile.RemoveLogo();
                break;
        }

        var outcome = await _profiles.SaveChangesAsync(cancellationToken);
        return outcome.Status == SaveStatus.Saved ? Result.Success() : Result.Failure(new Conflict());
    }
}
