using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Purchasing.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Purchasing.Controllers;

[ApiController]
[Route("api/purchases")]
public class PurchasesController : ControllerBase
{
    private readonly PurchaseService _service;
    public PurchasesController(PurchaseService service)=>_service=service;
    [HttpGet] public async Task<IActionResult> GetAll()=>Ok(await _service.GetAllAsync());
    [HttpGet("{id:int}")] public async Task<IActionResult> Detail(int id)=>Ok(await _service.GetDetailAsync(id));
    [HttpPost] public async Task<IActionResult> Create(PurchaseCreateRequest r)=>Ok(await _service.CreateAsync(r));
    [HttpPost("{id:int}/void")] public async Task<IActionResult> Void(int id)=>Ok(await _service.VoidAsync(id));
}
