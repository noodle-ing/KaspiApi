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
    
    [HttpPost("getconsignment")]
    public async Task<IActionResult> GetConsignment(string orderId)
    {
        var result = await _orderService.GetConsignment(orderId);
        if (result == "Error occurred")
        {
            return BadRequest("Error occurred");
        }
        return Ok(result);
    }
}