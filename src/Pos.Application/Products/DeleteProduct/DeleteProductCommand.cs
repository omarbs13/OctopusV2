namespace Pos.Application.Products.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id, int ExpectedVersion);
