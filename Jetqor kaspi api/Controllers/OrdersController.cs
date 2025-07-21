using Jetqor_kaspi_api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jetqor_kaspi_api.Controllers;
[ApiController]
[Route("kaspiApi/[controller]")]
public class OrdersController : ControllerBase
{ 
    private readonly AppDbContext _dbContext;
    private readonly KaspiOrderChecker _orderChecker;

    public OrdersController(AppDbContext dbContext, KaspiOrderChecker orderChecker)
    {
        _dbContext = dbContext;
        _orderChecker = orderChecker;
    }
    
    [HttpGet]
    [HttpGet("test")]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _dbContext.Orders.ToListAsync();
        return Ok(orders);
    }

    [HttpPost("update")]
    public async Task<IActionResult> UpdateOrders()
    {
        await _orderChecker.CheckAndSaveOrdersOnceAsync();
        return Ok(new { message = "Обновление заказов выполнено" });
    }

}