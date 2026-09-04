using Dapper;
using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Infrastructure.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Billing.Controllers;

[ApiController]
[Route("api/billings")]
public class BillingsController : ControllerBase
{
    private readonly Db _db;
    public BillingsController(Db db)=>_db=db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var c=_db.Open();
        return Ok(await c.QueryAsync(@"SELECT b.*,CASE WHEN b.BalanceAmount>0 AND b.DueDate IS NOT NULL AND date(b.DueDate)<date('now','localtime') THEN 1 ELSE 0 END IsOverdue,
CASE WHEN b.BalanceAmount>0 AND b.DueDate IS NOT NULL AND date(b.DueDate)<date('now','localtime') THEN 'OVERDUE' ELSE b.Status END DisplayStatus,
s.OrderNo,c.Name CustomerName FROM Billing b JOIN SalesOrder s ON s.Id=b.SalesOrderId JOIN Customer c ON c.Id=s.CustomerId ORDER BY b.Id DESC"));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        using var c=_db.Open();
        var billing=await c.QuerySingleOrDefaultAsync(@"SELECT b.*,s.OrderNo,c.Name CustomerName,c.Code CustomerCode,c.Email CustomerEmail,c.Address CustomerAddress
FROM Billing b JOIN SalesOrder s ON s.Id=b.SalesOrderId JOIN Customer c ON c.Id=s.CustomerId WHERE b.Id=@Id",new{Id=id});
        if(billing is null) throw new NotFoundApiException("找不到帳單");
        var payments=await c.QueryAsync("SELECT * FROM Payment WHERE BillingId=@Id ORDER BY PaymentDate DESC,Id DESC",new{Id=id});
        var invoices=await c.QueryAsync("SELECT * FROM Invoice WHERE BillingId=@Id ORDER BY Id DESC",new{Id=id});
        var refunds=await c.QueryAsync("SELECT * FROM Refund WHERE BillingId=@Id ORDER BY RefundDate DESC,Id DESC",new{Id=id});
        var items=await c.QueryAsync(@"SELECT d.Id,d.ProductId,p.Sku,p.Name ProductName,p.Unit,d.Qty,d.UnitPrice,d.Amount
FROM SalesOrderDetail d JOIN Product p ON p.Id=d.ProductId WHERE d.SalesOrderId=(SELECT SalesOrderId FROM Billing WHERE Id=@Id) ORDER BY d.Id",new{Id=id});
        return Ok(new{billing,payments,refunds,invoices,items});
    }
}
