using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Customers;
using Pos.Application.Customers.CreateCustomer;
using Pos.Application.Customers.GetCustomer;
using Pos.Application.Customers.UpdateCustomer;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.Common;
using Pos.Domain.Customers;
using Pos.Domain.Users;

namespace Pos.Desktop.Customers;

/// <summary>Opción del selector de modalidad.</summary>
public sealed record CreditModeOption(CreditMode Mode, string Label);

/// <summary>
/// Alta y edición de cliente (formulario de 002): nombre*, teléfono*, email y RUC, más la sección
/// "Crédito". Sin <c>ManageCustomerCredit</c> la sección es de solo lectura y no se envía (FR-020).
/// </summary>
public sealed partial class CustomerFormViewModel : FormViewModel<Guid>
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    private Guid? _customerId;
    private int _expectedVersion;

    public CustomerFormViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, ICurrentPermissions? permissions = null)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        CanEditCredit = permissions?.Has(Permission.ManageCustomerCredit) ?? true;
        Modes =
        [
            new CreditModeOption(CreditMode.CashOnly, Strings.Customer_ModeCashOnly),
            new CreditModeOption(CreditMode.Credit, Strings.Customer_ModeCredit),
        ];
        SelectedMode = Modes[0];
        ResetOriginalState();
    }

    public IReadOnlyList<CreditModeOption> Modes { get; }

    /// <summary>Solo el Administrador asigna límite y modalidad.</summary>
    public bool CanEditCredit { get; }

    public bool IsCreditReadOnly => !CanEditCredit;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaxId { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreditReadOnlyText))]
    public partial CreditModeOption SelectedMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreditReadOnlyText))]
    public partial string LimitText { get; set; } = "0.00";

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? PhoneError { get; private set; }

    [ObservableProperty]
    public partial string? EmailError { get; private set; }

    [ObservableProperty]
    public partial string? TaxIdError { get; private set; }

    [ObservableProperty]
    public partial string? LimitError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsEditMode { get; private set; }

    public override string Title => IsEditMode ? Strings.Customer_EditTitle : Strings.Customer_NewTitle;

    /// <summary>"Solo efectivo, límite $0.00" para quien no puede asignar crédito.</summary>
    public string CreditReadOnlyText => string.Format(
        MoneyConverter.Culture,
        Strings.Customer_CreditReadOnly,
        SelectedMode.Label,
        MoneyConverter.Format(Money.TryParse(LimitText, out var limit) ? limit.Cents : 0));

    /// <summary>Carga un cliente para editarlo; falso si ya no existe (y lo informa).</summary>
    public async Task<bool> LoadAsync(Guid customerId)
    {
        var (completed, result) = await _runner.RunAsync(
            "CargarCliente",
            () => _useCases.RunAsync<GetCustomerHandler, Result<CustomerDetailDto>>(h => h.HandleAsync(new GetCustomerQuery(customerId), CancellationToken.None)),
            new Dictionary<string, object?> { ["CustomerId"] = customerId });

        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, result.Error is Forbidden ? Strings.Common_Forbidden : Strings.Customer_NotFound);
            return false;
        }

        Fill(result.Value);
        return true;
    }

    /// <summary>Llena el formulario con la ficha ya leída.</summary>
    public void Fill(CustomerDetailDto customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        _customerId = customer.Id;
        _expectedVersion = customer.Version;
        IsEditMode = true;
        Name = customer.Name;
        Phone = customer.Phone;
        Email = customer.Email ?? string.Empty;
        TaxId = customer.TaxId ?? string.Empty;
        SelectedMode = Modes.First(m => m.Mode == customer.CreditMode);
        LimitText = Money.FromCents(customer.LimitCents).ToEditableString();
        ClearErrors();
        ResetOriginalState();
    }

    protected override async Task<bool> SaveCoreAsync()
    {
        ClearErrors();

        long? limit = null;
        CreditMode? mode = null;
        if (CanEditCredit)
        {
            if (!Money.TryParse(LimitText, out var parsed))
            {
                LimitError = Strings.Customer_LimitInvalid;
                FocusField = CustomerFields.CreditLimit;
                return false;
            }

            limit = parsed.Cents;
            mode = SelectedMode.Mode;
        }

        var (completed, result) = await _runner.RunAsync(
            "GuardarCliente",
            () => _customerId is { } id ? SaveUpdateAsync(id, limit, mode) : SaveCreateAsync(limit, mode),
            new Dictionary<string, object?> { ["CustomerId"] = _customerId });

        if (!completed || result is null)
        {
            return false;
        }

        if (result.Error is null)
        {
            OnSaved(_customerId ?? result.Value);
            return true;
        }

        await ShowErrorAsync(result.Error);
        return false;
    }

    protected override object CaptureState() => new CustomerFormState(
        Name.Trim(),
        Phone.Trim(),
        Email.Trim(),
        Customer.NormalizeTaxId(TaxId),
        SelectedMode.Mode,
        LimitText.Trim());

    private Task<Result<Guid>> SaveCreateAsync(long? limit, CreditMode? mode)
    {
        var command = new CreateCustomerCommand(Name, Phone, Email, TaxId, limit, mode);
        return _useCases.RunAsync<CreateCustomerHandler, Result<Guid>>(h => h.HandleAsync(command, CancellationToken.None));
    }

    private async Task<Result<Guid>> SaveUpdateAsync(Guid id, long? limit, CreditMode? mode)
    {
        var command = new UpdateCustomerCommand(id, _expectedVersion, Name, Phone, Email, TaxId, limit, mode);
        var result = await _useCases.RunAsync<UpdateCustomerHandler, Result>(h => h.HandleAsync(command, CancellationToken.None));
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

            case Duplicate:
                TaxIdError = Strings.Customer_DuplicateTaxId;
                FocusField = CustomerFields.TaxId;
                break;

            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;

            case Conflict when _customerId is { } id:
                if (await Dialogs.ConfirmAsync(Strings.Customer_ConflictTitle, Strings.Customer_Conflict, Strings.Editor_Reload))
                {
                    await LoadAsync(id);
                }

                break;

            case NotFound:
                await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Customer_NotFound);
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
            case CustomerFields.Name:
                NameError = message;
                break;
            case CustomerFields.Phone:
                PhoneError = message;
                break;
            case CustomerFields.Email:
                EmailError = message;
                break;
            case CustomerFields.TaxId:
                TaxIdError = message;
                break;
            case CustomerFields.CreditLimit:
            case CustomerFields.CreditMode:
                LimitError = message;
                break;
            default:
                ErrorMessage = message;
                break;
        }
    }

    private void ClearErrors()
    {
        NameError = PhoneError = EmailError = TaxIdError = LimitError = ErrorMessage = null;
        FocusField = null;
    }
}

/// <summary>Estado normalizado del formulario de cliente, para detectar cambios.</summary>
internal sealed record CustomerFormState(string Name, string Phone, string Email, string? TaxId, CreditMode Mode, string Limit);
