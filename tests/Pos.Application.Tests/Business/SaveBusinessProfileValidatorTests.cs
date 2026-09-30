using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Business.SaveBusinessProfile;
using Pos.Application.Products;
using Pos.Domain.Business;
using Pos.Application.Tests.TestSupport;

namespace Pos.Application.Tests.Business;

public sealed class SaveBusinessProfileValidatorTests
{
    private static readonly SaveBusinessProfileValidator Validator = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SaveBusinessProfileCommand Valid() =>
        new("Mi Tienda", "Calle 1 #23", "555 123 4567", "xaxx 010101000", "Gracias", LogoChange.KeepCurrent);

    private static IEnumerable<(string Field, string Message)> Errors(SaveBusinessProfileCommand command) =>
        Validator.Validate(command).Errors.Select(e => (e.PropertyName, e.ErrorMessage));

    [Fact]
    public void ComandoValido_NoTieneErrores()
    {
        Assert.Empty(Errors(Valid()));
        Assert.Empty(Errors(Valid() with { TaxId = null, FooterMessage = null }));
    }

    [Fact]
    public void Nombre_Direccion_Telefono_SonObligatorios()
    {
        Assert.Equal([(BusinessFields.TradeName, BusinessMessages.TradeNameRequired)], Errors(Valid() with { TradeName = "  " }));
        Assert.Equal([(BusinessFields.Address, BusinessMessages.AddressRequired)], Errors(Valid() with { Address = "" }));
        Assert.Equal([(BusinessFields.Phone, BusinessMessages.PhoneRequired)], Errors(Valid() with { Phone = "" }));
    }

    [Fact]
    public void RfcDeMasDe13Caracteres_SeRechaza_PeroLosEspaciosNoCuentan()
    {
        Assert.Equal([(BusinessFields.TaxId, BusinessMessages.TaxIdTooLong)], Errors(Valid() with { TaxId = new string('A', 14) }));
        Assert.Empty(Errors(Valid() with { TaxId = "AAAA 010101 AB1" }));
    }

    [Fact]
    public void PieDeMasDe200Caracteres_SeRechaza()
    {
        Assert.Equal([(BusinessFields.FooterMessage, BusinessMessages.FooterTooLong)], Errors(Valid() with { FooterMessage = new string('x', 201) }));
        Assert.Empty(Errors(Valid() with { FooterMessage = new string('x', 200) }));
    }

    [Fact]
    public async Task LogotipoInvalido_SeRechazaYNoSeTocaElAnterior()
    {
        var repository = new FakeRepository();
        var existing = BusinessProfile.Create("Tienda", "Dirección", "555", null, null);
        existing.SetLogo([1, 2, 3]);
        repository.Current = existing;
        var processor = new FakeProcessor { Next = ImageProcessingResult.Failure(InvalidImageReason.UnsupportedFormat) };
        var handler = new SaveBusinessProfileHandler(new AllowAllAccessControl(), repository, processor, Validator);

        var result = await handler.HandleAsync(Valid() with { Logo = new LogoChange.Replace(new MemoryStream([9]), 1) }, Ct);

        var failed = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(BusinessFields.Logo, Assert.Single(failed.Errors).Field);
        Assert.Equal([1, 2, 3], existing.Logo);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task LogotipoMayorA1MB_SeRechazaSinProcesarlo()
    {
        var repository = new FakeRepository();
        var processor = new FakeProcessor();
        var handler = new SaveBusinessProfileHandler(new AllowAllAccessControl(), repository, processor, Validator);

        var result = await handler.HandleAsync(
            Valid() with { Logo = new LogoChange.Replace(new MemoryStream([9]), BusinessProfile.LogoMaxBytes + 1) },
            Ct);

        var failed = Assert.IsType<ValidationFailed>(result.Error);
        Assert.Equal(BusinessMessages.LogoTooLarge, Assert.Single(failed.Errors).Message);
        Assert.Equal(0, processor.Calls);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task LogotipoValido_CreaLaFilaLaPrimeraVezYLaActualizaDespues()
    {
        var repository = new FakeRepository();
        var processor = new FakeProcessor { Next = ImageProcessingResult.Success([7, 7], [1], 10, 10) };
        var handler = new SaveBusinessProfileHandler(new AllowAllAccessControl(), repository, processor, Validator);

        Assert.True((await handler.HandleAsync(Valid() with { Logo = new LogoChange.Replace(new MemoryStream([9]), 1) }, Ct)).IsSuccess);
        var created = repository.Current!;
        Assert.Equal([7, 7], created.Logo);
        Assert.Equal("XAXX010101000", created.TaxId);

        Assert.True((await handler.HandleAsync(Valid() with { TradeName = "Otra", Logo = new LogoChange.Remove() }, Ct)).IsSuccess);
        Assert.Same(created, repository.Current);
        Assert.Equal("Otra", created.TradeName);
        Assert.Null(created.Logo);
        Assert.Equal(1, repository.Adds);
    }

    [Fact]
    public async Task ConservarLogotipo_NoLoModifica()
    {
        var repository = new FakeRepository();
        var existing = BusinessProfile.Create("Tienda", "Dirección", "555", null, null);
        existing.SetLogo([1, 2, 3]);
        repository.Current = existing;
        var handler = new SaveBusinessProfileHandler(new AllowAllAccessControl(), repository, new FakeProcessor(), Validator);

        Assert.True((await handler.HandleAsync(Valid(), Ct)).IsSuccess);

        Assert.Equal([1, 2, 3], existing.Logo);
    }

    private sealed class FakeProcessor : IImageProcessor
    {
        public ImageProcessingResult Next { get; set; } = ImageProcessingResult.Failure(InvalidImageReason.Corrupt);

        public int Calls { get; private set; }

        public ImageProcessingResult Process(Stream content)
        {
            Calls++;
            return Next;
        }
    }

    private sealed class FakeRepository : IBusinessProfileRepository
    {
        public BusinessProfile? Current { get; set; }

        public int Adds { get; private set; }

        public int Saves { get; private set; }

        public Task<BusinessProfile?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

        public void Add(BusinessProfile profile)
        {
            Adds++;
            Current = profile;
        }

        public Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.FromResult(SaveOutcome.Saved);
        }
    }
}
