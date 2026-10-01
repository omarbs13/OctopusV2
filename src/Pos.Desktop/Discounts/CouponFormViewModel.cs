using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Discounts.Coupons.GetCoupon;
using Pos.Application.Discounts.Coupons.SaveCoupon;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Desktop.Sales;
using Pos.Domain.Discounts;

namespace Pos.Desktop.Discounts;

/// <summary>
/// Alta y edición de un cupón (015, FR-009, FR-010): código, modalidad, valor, vigencia y límite de usos.
/// Con usos registrados, código, modalidad y valor quedan de solo lectura.
/// </summary>
public sealed partial class CouponFormViewModel : FormViewModel<Guid>
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    private Guid? _couponId;
    private int _expectedVersion;

    public CouponFormViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        Modes =
        [
            new DiscountModeOption(DiscountMode.Percent, Strings.Discount_ModePercent),
            new DiscountModeOption(DiscountMode.Amount, Strings.Discount_ModeAmount),
        ];
        SelectedMode = Modes[0];
        StartsOn = DateTime.Today;
        EndsOn = DateTime.Today.AddDays(30);
        ResetOriginalState();
    }

    public IReadOnlyList<DiscountModeOption> Modes { get; }

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DiscountModeOption SelectedMode { get; set; }

    [ObservableProperty]
    public partial string ValueText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime? StartsOn { get; set; }

    [ObservableProperty]
    public partial DateTime? EndsOn { get; set; }

    [ObservableProperty]
    public partial string UsageLimitText { get; set; } = string.Empty;

    /// <summary>Con usos registrados no se cambian código, modalidad ni valor (FR-010).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditValue))]
    public partial bool HasUses { get; private set; }

    public bool CanEditValue => !HasUses;

    [ObservableProperty]
    public partial string? CodeError { get; private set; }

    [ObservableProperty]
    public partial string? ValueError { get; private set; }

    [ObservableProperty]
    public partial string? DatesError { get; private set; }

    [ObservableProperty]
    public partial string? UsageLimitError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsEditMode { get; private set; }

    public override string Title => IsEditMode ? Strings.Coupon_EditTitle : Strings.Coupon_NewTitle;

    public async Task<bool> LoadAsync(Guid couponId)
    {
        var (completed, result) = await _runner.RunAsync(
            "CargarCupon",
            () => _useCases.RunAsync<GetCouponHandler, Result<CouponDetailDto>>(h => h.HandleAsync(new GetCouponQuery(couponId), CancellationToken.None)),
            new Dictionary<string, object?> { ["CouponId"] = couponId });
        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, result.Error is Forbidden ? Strings.Common_Forbidden : Strings.Coupon_NotFound);
            return false;
        }

        var coupon = result.Value;
        _couponId = coupon.Id;
        _expectedVersion = coupon.Version;
        IsEditMode = true;
        HasUses = coupon.HasUses;
        Code = coupon.Code;
        SelectedMode = Modes.First(m => m.Mode == coupon.Mode);
        ValueText = DiscountValue.Create(coupon.Mode, coupon.Value).ToEditableString();
        StartsOn = coupon.StartsOn.ToDateTime(TimeOnly.MinValue);
        EndsOn = coupon.EndsOn.ToDateTime(TimeOnly.MinValue);
        UsageLimitText = coupon.UsageLimit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        ClearErrors();
        ResetOriginalState();
        return true;
    }

    protected override async Task<bool> SaveCoreAsync()
    {
        ClearErrors();
        if (StartsOn is not { } starts || EndsOn is not { } ends)
        {
            DatesError = Strings.Coupon_DatesRequired;
            return false;
        }

        int? limit = null;
        if (!string.IsNullOrWhiteSpace(UsageLimitText))
        {
            if (!int.TryParse(UsageLimitText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            {
                UsageLimitError = Strings.Coupon_UsageLimitInvalid;
                FocusField = DiscountFields.UsageLimit;
                return false;
            }

            limit = parsed;
        }

        var command = new SaveCouponCommand(
            _couponId,
            Code,
            SelectedMode.Mode,
            ValueText,
            DateOnly.FromDateTime(starts),
            DateOnly.FromDateTime(ends),
            limit,
            _expectedVersion);
        var (completed, result) = await _runner.RunAsync(
            "GuardarCupon",
            () => _useCases.RunAsync<SaveCouponHandler, Result<Guid>>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["CouponId"] = _couponId });
        if (!completed || result is null)
        {
            return false;
        }

        if (result.Error is null)
        {
            OnSaved(result.Value);
            return true;
        }

        await ShowErrorAsync(result.Error);
        return false;
    }

    protected override object CaptureState() => new CouponFormState(
        Code.Trim().ToUpperInvariant(),
        SelectedMode.Mode,
        ValueText.Trim(),
        StartsOn,
        EndsOn,
        UsageLimitText.Trim());

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
                CodeError = DiscountMessages.CodeDuplicated;
                FocusField = DiscountFields.Code;
                break;
            case CodeCollidesWithProduct:
                CodeError = DiscountMessages.CodeCollidesWithProduct;
                FocusField = DiscountFields.Code;
                break;
            case CouponHasUses:
                ErrorMessage = DiscountMessages.CouponHasUses;
                break;
            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;
            case ModuleNotLicensed:
                ErrorMessage = Strings.License_ModuleNotLicensed;
                break;
            case Conflict when _couponId is { } id:
                if (await Dialogs.ConfirmAsync(Strings.Coupon_ConflictTitle, Strings.Coupon_Conflict, Strings.Editor_Reload))
                {
                    await LoadAsync(id);
                }

                break;
            case NotFound:
                await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Coupon_NotFound);
                RaiseClosed();
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
            case DiscountFields.Code:
                CodeError = message;
                break;
            case DiscountFields.Value:
            case DiscountFields.Mode:
                ValueError = message;
                break;
            case DiscountFields.StartsOn:
            case DiscountFields.EndsOn:
                DatesError = message;
                break;
            case DiscountFields.UsageLimit:
                UsageLimitError = message;
                break;
            default:
                ErrorMessage = message;
                break;
        }
    }

    private void ClearErrors()
    {
        CodeError = ValueError = DatesError = UsageLimitError = ErrorMessage = null;
        FocusField = null;
    }
}

/// <summary>Estado normalizado del formulario de cupón, para detectar cambios.</summary>
internal sealed record CouponFormState(string Code, DiscountMode Mode, string Value, DateTime? StartsOn, DateTime? EndsOn, string UsageLimit);
