using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Master.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Master.Controllers;

[ApiController]
[Route("api")]
public class MasterController : ControllerBase
{
    private readonly MasterService _service;
    public MasterController(MasterService service)=>_service=service;

    [HttpGet("products")] public async Task<IActionResult> Products()=>Ok(await _service.GetProductsAsync());
    [HttpPost("products")] public async Task<IActionResult> CreateProduct(ProductCreateRequest r)=>Ok(await _service.CreateProductAsync(r));
    [HttpPut("products/{id:long}")] public async Task<IActionResult> UpdateProduct(long id,ProductUpdateRequest r)=>Ok(await _service.UpdateProductAsync(id,r));
    [HttpDelete("products/{id:long}")] public async Task<IActionResult> DeleteProduct(long id)=>Ok(await _service.DeleteProductAsync(id));

    [HttpGet("customers")] public async Task<IActionResult> Customers()=>Ok(await _service.GetCustomersAsync());
    [HttpPost("customers")] public async Task<IActionResult> CreateCustomer(PartyCreateRequest r)=>Ok(await _service.CreateCustomerAsync(r));
    [HttpPut("customers/{id:long}")] public async Task<IActionResult> UpdateCustomer(long id,PartyUpdateRequest r)=>Ok(await _service.UpdateCustomerAsync(id,r));
    [HttpDelete("customers/{id:long}")] public async Task<IActionResult> DeleteCustomer(long id)=>Ok(await _service.DeleteCustomerAsync(id));

    [HttpGet("suppliers")] public async Task<IActionResult> Suppliers()=>Ok(await _service.GetSuppliersAsync());
    [HttpPost("suppliers")] public async Task<IActionResult> CreateSupplier(PartyCreateRequest r)=>Ok(await _service.CreateSupplierAsync(r));
    [HttpPut("suppliers/{id:long}")] public async Task<IActionResult> UpdateSupplier(long id,PartyUpdateRequest r)=>Ok(await _service.UpdateSupplierAsync(id,r));
    [HttpDelete("suppliers/{id:long}")] public async Task<IActionResult> DeleteSupplier(long id)=>Ok(await _service.DeleteSupplierAsync(id));

    [HttpGet("inventory/transactions")] public async Task<IActionResult> InventoryTransactions()=>Ok(await _service.GetInventoryTransactionsAsync());
}
