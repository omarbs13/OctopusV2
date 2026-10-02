using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Suppliers;
using Pos.Application.Suppliers.CreateSupplier;
using Pos.Application.Suppliers.GetSupplier;
using Pos.Application.Suppliers.UpdateSupplier;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.Suppliers;

namespace Pos.Desktop.Purchases;

/// <summary>Opción del selector de condiciones de pago.</summary>
public sealed record PaymentTermsOption(PaymentTerms Terms, string Label);

/// <summary>
/// Alta y edición de proveedor (formulario de 002): nombre*, RUC, teléfono, email, dirección y condiciones de
/// pago*. "Días de crédito" se habilita y es obligatorio solo con crédito (Historia 1, escenario 7).
/// </summary>
public sealed partial class SupplierFormViewModel : FormViewModel<Guid>
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    private Guid? _supplierId;
    private int _expectedVersion;

    public SupplierFormViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        TermsOptions =
        [
            new PaymentTermsOption(PaymentTerms.Cash, Strings.Supplier_TermsCash),
            new PaymentTermsOption(PaymentTerms.Credit, Strings.Supplier_TermsCredit),
        ];
        SelectedTerms = TermsOptions[0];
        ResetOriginalState();
    }

    public IReadOnlyList<PaymentTermsOption> TermsOptions { get; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaxId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCredit))]
    public partial PaymentTermsOption SelectedTerms { get; set; }

    [ObservableProperty]
    public partial string CreditDaysText { get; set; } = string.Empty;

    /// <summary>Habilita "Días de crédito".</summary>
    public bool IsCredit => SelectedTerms.Terms == PaymentTerms.Credit;

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? TaxIdError { get; private set; }

    [ObservableProperty]
    public partial string? PhoneError { get; private set; }

    [ObservableProperty]
    public partial string? EmailError { get; private set; }

    [ObservableProperty]
    public partial string? AddressError { get; private set; }

    [ObservableProperty]
    public partial string? CreditDaysError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsEditMode { get; private set; }

    public override string Title => IsEditMode ? Strings.Supplier_EditTitle : Strings.Supplier_NewTitle;

    /// <summary>Carga un proveedor para editarlo; falso si ya no existe (y lo informa).</summary>
    public async Task<bool> LoadAsync(Guid supplierId)
    {
        var (completed, result) = await _runner.RunAsync(
            "CargarProveedor",
            () => _useCases.RunAsync<GetSupplierHandler, Result<SupplierDto>>(h => h.HandleAsync(new GetSupplierQuery(supplierId), CancellationToken.None)),
            new Dictionary<string, object?> { ["SupplierId"] = supplierId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, result.Error is Forbidden ? Strings.Common_Forbidden : Strings.Supplier_NotFound);
            return false;
        }

        Fill(result.Value);
        return true;
    }

    /// <summary>Llena el formulario con la ficha ya leída.</summary>
    public void Fill(SupplierDto supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        _supplierId = supplier.Id;
        _expectedVersion = supplier.Version;
        IsEditMode = true;
        Name = supplier.Name;
        TaxId = supplier.TaxId ?? string.Empty;
        Phone = supplier.Phone ?? string.Empty;
        Email = supplier.Email ?? string.Empty;
        Address = supplier.Address ?? string.Empty;
        SelectedTerms = TermsOptions.First(t => t.Terms == supplier.PaymentTerms);
        CreditDaysText = supplier.CreditDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        ClearErrors();
        ResetOriginalState();
    }

    protected override async Task<bool> SaveCoreAsync()
    {
        ClearErrors();
        var creditDays = IsCredit ? CreditDaysText : null;

        var (completed, result) = await _runner.RunAsync(
            "GuardarProveedor",
            () => _supplierId is { } id ? SaveUpdateAsync(id, creditDays) : SaveCreateAsync(creditDays),
            new Dictionary<string, object?> { ["SupplierId"] = _supplierId });

        if (!completed || result is null)
        {
            return false;
        }

        if (result.Error is null)
        {
            OnSaved(_supplierId ?? result.Value);
            return true;
        }

        await ShowErrorAsync(result.Error);
        return false;
    }

    protected override object CaptureState() => new SupplierFormState(
        Name.Trim(),
        Supplier.NormalizeTaxId(TaxId),
        Phone.Trim(),
        Email.Trim(),
        Address.Trim(),
        SelectedTerms.Terms,
        IsCredit ? CreditDaysText.Trim() : string.Empty);

    private Task<Result<Guid>> SaveCreateAsync(string? creditDays)
    {
        var command = new CreateSupplierCommand(Name, TaxId, Phone, Email, Address, SelectedTerms.Terms, creditDays);
        return _useCases.RunAsync<CreateSupplierHandler, Result<Guid>>(h => h.HandleAsync(command, CancellationToken.None));
    }

    private async Task<Result<Guid>> SaveUpdateAsync(Guid id, string? creditDays)
    {
        var command = new UpdateSupplierCommand(id, _expectedVersion, Name, TaxId, Phone, Email, Address, SelectedTerms.Terms, creditDays);
        var result = await _useCases.RunAsync<UpdateSupplierHandler, Result>(h => h.HandleAsync(command, CancellationToken.None));
        return result.IsSuccess ? Result.Success(id) : Result.Failure<Guid>(result.Error);
    }

    private async Task ShowErrorAsync(Error error)
    {
        switch (error)
        {
            case ValidationFailed validation:
                foreach (var field in validation.Errors)
                {
                    SetError(field.Field, field.Message);
                }

                FocusField = validation.Errors.Count > 0 ? validation.Errors[0].Field : null;
                break;

            case SupplierTaxIdInUse inUse:
                TaxIdError = string.Format(CultureInfo.CurrentCulture, Strings.Supplier_TaxIdInUse, inUse.Name);
                FocusField = SupplierFields.TaxId;
                break;

            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;

            case Conflict when _supplierId is { } id:
                if (await Dialogs.ConfirmAsync(Strings.Supplier_ConflictTitle, Strings.Supplier_Conflict, Strings.Editor_Reload))
                {
                    await LoadAsync(id);
                }

                break;

            case NotFound:
                await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Supplier_NotFound);
                RaiseClosed();
                break;

            case ModuleNotLicensed:
                ErrorMessage = Strings.License_ModuleNotLicensed;
                break;

            default:
                await Dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
                break;
        }
    }

    private void SetError(string field, string message)
    {
        switch (field)
        {
            case SupplierFields.Name:
                NameError = message;
                break;
            case SupplierFields.TaxId:
                TaxIdError = message;
                break;
            case SupplierFields.Phone:
                PhoneError = message;
                break;
            case SupplierFields.Email:
                EmailError = message;
                break;
            case SupplierFields.Address:
                AddressError = message;
                break;
            case SupplierFields.CreditDays:
                CreditDaysError = message;
                break;
            default:
                ErrorMessage = message;
                break;
        }
    }

    private void ClearErrors()
    {
        NameError = TaxIdError = PhoneError = EmailError = AddressError = CreditDaysError = ErrorMessage = null;
        FocusField = null;
    }
}

/// <summary>Estado normalizado del formulario de proveedor, para detectar cambios.</summary>
internal sealed record SupplierFormState(
    string Name,
    string? TaxId,
    string Phone,
    string Email,
    string Address,
    PaymentTerms Terms,
    string CreditDays);
