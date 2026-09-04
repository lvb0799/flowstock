using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Sales.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Sales.Controllers;

[ApiController]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly SalesService _service;
    public SalesController(SalesService service)=>_service=service;
    [HttpGet] public async Task<IActionResult> GetAll()=>Ok(await _service.GetAllAsync());
    [HttpGet("{id:int}")] public async Task<IActionResult> Detail(int id)=>Ok(await _service.GetDetailAsync(id));
    [HttpPost] public async Task<IActionResult> Create(SalesCreateRequest r)=>Ok(await _service.CreateAsync(r));
    [HttpPost("{id:int}/void")] public async Task<IActionResult> Void(int id)=>Ok(await _service.VoidAsync(id));
}
