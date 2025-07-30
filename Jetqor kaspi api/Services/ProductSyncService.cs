using Jetqor_kaspi_api;
using Jetqor_kaspi_api.Models;
using Microsoft.EntityFrameworkCore;

public class ProductSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProductSyncService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<Product> SyncProductAsync(string article)
    {
              
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var normalizedArticle = article.Trim().ToLower();

        var product = await context.Products
            .FirstOrDefaultAsync(p => p.article.Trim().ToLower() == normalizedArticle);

        if (product != null)
        {
            Console.WriteLine($"Найден продукт с Id = {product.id}");
        }

        return product;
    }
}