using Dapper;
using Inventory.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Reports.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly Db _db;
    public ReportsController(Db db) => _db = db;

    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] string? from, [FromQuery] string? to)
    {
        var end = string.IsNullOrWhiteSpace(to) ? DateTime.Today.ToString("yyyy-MM-dd") : to!;
        var start = string.IsNullOrWhiteSpace(from) ? DateTime.Today.AddDays(-29).ToString("yyyy-MM-dd") : from!;
        using var c = _db.Open();

        var summary = await c.QuerySingleAsync(@"
SELECT
 COALESCE((SELECT SUM(TotalAmount) FROM SalesOrder WHERE OrderDate BETWEEN @From AND @To AND Status<>'VOID'),0) SalesAmount,
 COALESCE((SELECT SUM(TotalAmount) FROM SalesReturn WHERE ReturnDate BETWEEN @From AND @To AND Status='CONFIRMED'),0) ReturnAmount,
 COALESCE((SELECT SUM(TotalAmount) FROM PurchaseOrder WHERE OrderDate BETWEEN @From AND @To AND Status<>'VOID'),0) PurchaseAmount,
 COALESCE((SELECT SUM(Amount) FROM Payment WHERE PaymentDate BETWEEN @From AND @To),0) PaymentAmount,
 COALESCE((SELECT SUM(Amount) FROM Refund WHERE RefundDate BETWEEN @From AND @To),0) RefundAmount,
 COALESCE((SELECT SUM(BalanceAmount) FROM Billing WHERE Status IN ('UNPAID','PARTIALLY_PAID')),0) ReceivableAmount,
 COALESCE((SELECT SUM(RefundDue) FROM Billing WHERE Status='REFUND_DUE'),0) RefundDueAmount,
 COALESCE((SELECT SUM(BalanceAmount) FROM Billing WHERE BalanceAmount>0 AND DueDate IS NOT NULL AND date(DueDate)<date('now','localtime')),0) OverdueAmount,
 COALESCE((SELECT SUM(d.CostAmount-COALESCE((SELECT SUM(rd.Qty*d.UnitCost) FROM SalesReturnDetail rd JOIN SalesReturn rr ON rr.Id=rd.SalesReturnId WHERE rd.SalesOrderDetailId=d.Id AND rr.Status='CONFIRMED'),0)) FROM SalesOrderDetail d JOIN SalesOrder s ON s.Id=d.SalesOrderId WHERE s.OrderDate BETWEEN @From AND @To AND s.Status<>'VOID'),0) SalesCost", new { From = start, To = end });

        var dailySales = await c.QueryAsync(@"
SELECT OrderDate Date, ROUND(SUM(TotalAmount),2) SalesAmount
FROM SalesOrder WHERE OrderDate BETWEEN @From AND @To AND Status<>'VOID'
GROUP BY OrderDate ORDER BY OrderDate", new { From = start, To = end });

        var topProducts = await c.QueryAsync(@"
SELECT p.Sku,p.Name,
 ROUND(SUM(d.Qty-COALESCE((SELECT SUM(rd.Qty) FROM SalesReturnDetail rd JOIN SalesReturn rr ON rr.Id=rd.SalesReturnId WHERE rd.SalesOrderDetailId=d.Id AND rr.Status='CONFIRMED'),0)),2) NetQty,
 ROUND(SUM(d.Amount-COALESCE((SELECT SUM(rd.Amount) FROM SalesReturnDetail rd JOIN SalesReturn rr ON rr.Id=rd.SalesReturnId WHERE rd.SalesOrderDetailId=d.Id AND rr.Status='CONFIRMED'),0)),2) NetAmount,
 ROUND(SUM(d.CostAmount-COALESCE((SELECT SUM(rd.Qty*d.UnitCost) FROM SalesReturnDetail rd JOIN SalesReturn rr ON rr.Id=rd.SalesReturnId WHERE rd.SalesOrderDetailId=d.Id AND rr.Status='CONFIRMED'),0)),2) NetCost,
 ROUND(SUM((d.Amount-d.CostAmount)-COALESCE((SELECT SUM(rd.Amount-rd.Qty*d.UnitCost) FROM SalesReturnDetail rd JOIN SalesReturn rr ON rr.Id=rd.SalesReturnId WHERE rd.SalesOrderDetailId=d.Id AND rr.Status='CONFIRMED'),0)),2) GrossProfit
FROM SalesOrderDetail d JOIN SalesOrder s ON s.Id=d.SalesOrderId JOIN Product p ON p.Id=d.ProductId
WHERE s.OrderDate BETWEEN @From AND @To AND s.Status<>'VOID'
GROUP BY p.Id,p.Sku,p.Name HAVING NetQty<>0 OR NetAmount<>0 ORDER BY NetAmount DESC LIMIT 20", new { From = start, To = end });

        var topCustomers = await c.QueryAsync(@"
SELECT c.Code,c.Name,ROUND(SUM(b.Amount),2) SalesAmount,ROUND(SUM(b.ReturnAmount),2) ReturnAmount,
 ROUND(SUM(CASE WHEN b.NetAmount>0 OR b.ReturnAmount>0 THEN b.NetAmount ELSE b.Amount END),2) NetAmount,
 ROUND(SUM(b.PaidAmount-b.RefundedAmount),2) NetReceived,ROUND(SUM(b.BalanceAmount),2) BalanceAmount
FROM Billing b JOIN SalesOrder s ON s.Id=b.SalesOrderId JOIN Customer c ON c.Id=s.CustomerId
WHERE s.OrderDate BETWEEN @From AND @To
GROUP BY c.Id,c.Code,c.Name ORDER BY NetAmount DESC LIMIT 20", new { From = start, To = end });

        var purchases = await c.QueryAsync(@"
SELECT o.OrderNo,o.OrderDate,s.Code SupplierCode,s.Name SupplierName,o.TotalAmount,o.Status
FROM PurchaseOrder o JOIN Supplier s ON s.Id=o.SupplierId
WHERE o.OrderDate BETWEEN @From AND @To ORDER BY o.OrderDate DESC,o.Id DESC", new { From = start, To = end });

        var inventory = await c.QueryAsync(@"
SELECT Sku,Name,Unit,StockQty,CostPrice,SalePrice,
 ROUND(StockQty*CostPrice,2) CostValue,ROUND(StockQty*SalePrice,2) SaleValue
FROM Product WHERE IsActive=1 ORDER BY StockQty ASC,Name");

        var receivables = await c.QueryAsync(@"
SELECT b.BillNo,b.BillDate,b.DueDate,c.Code CustomerCode,c.Name CustomerName,b.Amount,b.ReturnAmount,
 CASE WHEN b.NetAmount>0 OR b.ReturnAmount>0 THEN b.NetAmount ELSE b.Amount END NetAmount,
 b.PaidAmount,b.RefundedAmount,b.BalanceAmount,b.RefundDue,b.Status,CASE WHEN b.BalanceAmount>0 AND b.DueDate IS NOT NULL AND date(b.DueDate)<date('now','localtime') THEN 1 ELSE 0 END IsOverdue
FROM Billing b JOIN SalesOrder s ON s.Id=b.SalesOrderId JOIN Customer c ON c.Id=s.CustomerId
WHERE b.BalanceAmount>0 OR b.RefundDue>0 ORDER BY COALESCE(b.DueDate,b.BillDate),b.Id");

        return Ok(new { from=start, to=end, summary, dailySales, topProducts, topCustomers, purchases, inventory, receivables });
    }
}
