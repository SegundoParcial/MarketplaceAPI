using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MarketplaceAPI.Data;
using MarketplaceAPI.DTOs.Order;
using MarketplaceAPI.Models;
using MarketplaceAPI.Services;

namespace MarketplaceAPI.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db)
    {
        _db = db;
    }

    // ✅ Corregido: Ahora recibe directamente el JSON con CreateOrderDto
    [HttpPost]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
    {
        if (dto == null)
            return BadRequest(new { error = "Invalid request body" });

        var ids = dto.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();

        if (products.Count != ids.Count)
            return BadRequest(new { error = "INVALID_PRODUCTS" });

        var companyId = products.First().CompanyId;

        if (products.Any(p => p.CompanyId != companyId) || dto.CompanyId != companyId)
            return BadRequest(new { error = "MIXED_COMPANIES" });

        foreach (var item in dto.Items)
        {
            var p = products.First(x => x.Id == item.ProductId);
            if (p.Stock < item.Quantity)
                return BadRequest(new { error = $"OUT_OF_STOCK:{p.Name}" });
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerUserId = User.GetUserId(),
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.New
            };

            _db.Orders.Add(order);

            foreach (var it in dto.Items)
            {
                var p = products.First(x => x.Id == it.ProductId);
                p.Stock -= it.Quantity;

                _db.OrderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = p.Id,
                    Quantity = it.Quantity,
                    UnitPrice = p.Price
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                orderId = order.Id,
                order.Status,
                order.CreatedAt
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ✅ Devuelve pedidos del cliente autenticado
    [HttpGet("mine")]
    [Authorize(Policy = "Customer")]
    public async Task<ActionResult<IEnumerable<OrderSummaryDto>>> Mine()
    {
        var uid = User.GetUserId();
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerUserId == uid)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var list = orders.Select(o => new OrderSummaryDto(
            o.Id,
            o.CreatedAt,
            o.Status.ToString(),
            o.Items.Sum(i => i.UnitPrice * i.Quantity)
        ));

        return Ok(list);
    }

    // ✅ Devuelve pedidos recibidos por una empresa
    [HttpGet("received")]
    [Authorize(Policy = "Company")]
    public async Task<ActionResult<IEnumerable<OrderSummaryDto>>> Received()
    {
        var cid = User.GetCompanyId()
            ?? (await _db.Companies.FirstAsync(c => c.OwnerUserId == User.GetUserId())).Id;

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.CompanyId == cid)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var list = orders.Select(o => new OrderSummaryDto(
            o.Id,
            o.CreatedAt,
            o.Status.ToString(),
            o.Items.Sum(i => i.UnitPrice * i.Quantity)
        ));

        return Ok(list);
    }

    // ✅ Actualiza el estado del pedido (empresa)
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Company")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusDto dto)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();

        var cid = User.GetCompanyId()
            ?? (await _db.Companies.FirstAsync(c => c.OwnerUserId == User.GetUserId())).Id;

        if (order.CompanyId != cid)
            return Forbid();

        bool allowed = order.Status switch
        {
            OrderStatus.New => dto.Status is OrderStatus.Shipped or OrderStatus.Canceled,
            OrderStatus.Shipped => dto.Status == OrderStatus.Delivered,
            _ => false
        };

        if (!allowed)
            return BadRequest(new { error = "INVALID_TRANSITION" });

        order.Status = dto.Status;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
