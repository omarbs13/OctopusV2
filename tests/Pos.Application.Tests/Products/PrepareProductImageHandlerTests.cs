using Pos.Application.Abstractions;
using Pos.Application.Products;
using Pos.Application.Products.PrepareProductImage;
using Pos.Domain.Products;

namespace Pos.Application.Tests.Products;

public sealed class PrepareProductImageHandlerTests
{
    private readonly FakeImageProcessor _processor = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PrepareProductImageHandler Handler => new(_processor);

    [Fact]
    public async Task ArchivoMayorA5MB_TooLargeSinLeerNiProcesar()
    {
        var stream = new ThrowingStream();

        var result = await Handler.HandleAsync(new PrepareProductImageCommand(stream, Product.ImageMaxBytes + 1), Ct);

        Assert.Equal(new InvalidImage(InvalidImageReason.TooLarge), result.Error);
        Assert.Equal(0, _processor.Calls);
    }

    [Theory]
    [InlineData(InvalidImageReason.UnsupportedFormat)]
    [InlineData(InvalidImageReason.Corrupt)]
    [InlineData(InvalidImageReason.DimensionsTooLarge)]
    [InlineData(InvalidImageReason.TooLarge)]
    public async Task RechazoDelProcesador_DevuelveInvalidImage(InvalidImageReason reason)
    {
        _processor.Next = ImageProcessingResult.Failure(reason);

        var result = await Handler.HandleAsync(new PrepareProductImageCommand(new MemoryStream([1, 2, 3]), 3), Ct);

        Assert.Equal(new InvalidImage(reason), result.Error);
    }

    [Fact]
    public async Task Exito_DevuelveLaImagenPreparadaConLosMismosBytes()
    {
        byte[] content = [1, 2, 3];
        byte[] thumbnail = [4, 5];
        _processor.Next = ImageProcessingResult.Success(content, thumbnail, 640, 480);

        var result = await Handler.HandleAsync(new PrepareProductImageCommand(new MemoryStream([9]), 1), Ct);

        Assert.True(result.IsSuccess);
        Assert.Same(content, result.Value.Content);
        Assert.Same(thumbnail, result.Value.Thumbnail);
        Assert.Equal((640, 480), (result.Value.Width, result.Value.Height));
    }

    [Theory]
    [InlineData(InvalidImageReason.TooLarge, ProductMessages.ImageTooLarge)]
    [InlineData(InvalidImageReason.UnsupportedFormat, ProductMessages.ImageUnsupportedFormat)]
    [InlineData(InvalidImageReason.Corrupt, ProductMessages.ImageCorrupt)]
    [InlineData(InvalidImageReason.DimensionsTooLarge, ProductMessages.ImageDimensionsTooLarge)]
    public void CadaMotivo_TieneSuMensaje(InvalidImageReason reason, string message)
    {
        Assert.Equal(message, PrepareProductImageHandler.MessageFor(reason));
    }

    private sealed class FakeImageProcessor : IImageProcessor
    {
        public ImageProcessingResult Next { get; set; } = ImageProcessingResult.Failure(InvalidImageReason.Corrupt);

        public int Calls { get; private set; }

        public ImageProcessingResult Process(Stream content)
        {
            Calls++;
            return Next;
        }
    }

    private sealed class ThrowingStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("No debía leerse el archivo.");
    }
}
