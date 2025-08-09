using Microsoft.AspNetCore.Mvc;
using Jetqor_kaspi_api.Services;

namespace Jetqor_kaspi_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly KaspiOrderService _orderService;

    public OrdersController(KaspiOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncOrders([FromHeader(Name = "X-Auth-Token")] string token)
    {
        await _orderService.CheckAndSaveOrdersOnceAsync(token);
        return Ok(new { message = "Orders synced" });
    }

    [HttpPost("getconsignment")]
    public async Task<IActionResult> GetConsignment([FromHeader(Name = "X-Auth-Token")] string token, string orderId)
    {
        var result = await _orderService.GetConsignment(token, orderId);
        if (result == "Error occurred")
        {
            return BadRequest("Error occurred");
        }
        return Ok(result);
    }
}