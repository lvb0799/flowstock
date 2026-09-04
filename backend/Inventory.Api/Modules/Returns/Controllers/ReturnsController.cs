using Dapper;
using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Audit.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Returns.Controllers;

[ApiController]
[Route("api")]
public class ReturnsController : ControllerBase
{
    private readonly Db _db;
    private readonly HistoryService _history;
    public ReturnsController(Db db, HistoryService history){ _db=db; _history=history; }
    private static string No(string prefix) => $"{prefix}{DateTime.Now:yyyyMMddHHmmssfff}";

    [HttpGet("returns")]
    public async Task<IActionResult> GetReturns()
    {
        using var c=_db.Open();
        return Ok(await c.QueryAsync(@"SELECT r.*,s.OrderNo,c.Name CustomerName,COUNT(d.Id) ItemCount
FROM SalesReturn r JOIN SalesOrder s ON s.Id=r.SalesOrderId JOIN Customer c ON c.Id=r.CustomerId
LEFT JOIN SalesReturnDetail d ON d.SalesReturnId=r.Id
GROUP BY r.Id ORDER BY r.Id DESC"));
    }

    [HttpGet("returns/{id:int}")]
    public async Task<IActionResult> GetReturn(int id)
    {
        using var c=_db.Open();
        var header=await c.QuerySingleOrDefaultAsync(@"SELECT r.*,s.OrderNo,c.Name CustomerName FROM SalesReturn r JOIN SalesOrder s ON s.Id=r.SalesOrderId JOIN Customer c ON c.Id=r.CustomerId WHERE r.Id=@Id",new{Id=id});
        if(header is null) return NotFound(new{message="找不到退貨單"});
        var items=await c.QueryAsync(@"SELECT d.*,p.Sku,p.Name ProductName,p.Unit FROM SalesReturnDetail d JOIN Product p ON p.Id=d.ProductId WHERE d.SalesReturnId=@Id ORDER BY d.Id",new{Id=id});
        return Ok(new{header,items});
    }

    [HttpPost("returns")]
    public async Task<IActionResult> CreateReturn(SalesReturnCreateRequest r)
    {
        if(r.SalesOrderId<=0) return BadRequest(new{message="請選擇銷貨單"});
        if(r.Items is null || r.Items.Count==0) return BadRequest(new{message="退貨明細不可為空"});
        if(r.Items.Any(x=>x.Qty<=0)) return BadRequest(new{message="退貨數量必須大於 0"});
        if(r.Items.GroupBy(x=>x.SalesOrderDetailId).Any(g=>g.Count()>1)) return BadRequest(new{message="同一銷貨明細不可重複退貨"});
        using var c=_db.Open(); using var tx=c.BeginTransaction();
        try
        {
            var sale=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM SalesOrder WHERE Id=@Id",new{Id=r.SalesOrderId},tx);
            if(sale is null) return NotFound(new{message="找不到銷貨單"});
            if((string)sale.Status=="VOID") return BadRequest(new{message="作廢銷貨單不可退貨"});

            var detailRows=new List<(dynamic Detail, decimal Qty, decimal Amount)>(); decimal total=0;
            foreach(var item in r.Items)
            {
                var d=await c.QuerySingleOrDefaultAsync<dynamic>(@"SELECT d.*,p.Name ProductName,p.Sku FROM SalesOrderDetail d JOIN Product p ON p.Id=d.ProductId WHERE d.Id=@DetailId AND d.SalesOrderId=@SalesOrderId",new{DetailId=item.SalesOrderDetailId,SalesOrderId=r.SalesOrderId},tx);
                if(d is null) throw new InvalidOperationException($"找不到銷貨明細 {item.SalesOrderDetailId}");
                var returned=await c.ExecuteScalarAsync<decimal>(@"SELECT COALESCE(SUM(rd.Qty),0) FROM SalesReturnDetail rd JOIN SalesReturn rr ON rr.Id=rd.SalesReturnId WHERE rd.SalesOrderDetailId=@Id AND rr.Status='CONFIRMED'",new{Id=item.SalesOrderDetailId},tx);
                decimal sold=Convert.ToDecimal(d.Qty); decimal remaining=sold-returned;
                if(item.Qty>remaining) throw new InvalidOperationException($"商品「{(string)d.ProductName}」最多還可退 {remaining:0.##}");
                decimal amount=item.Qty*Convert.ToDecimal(d.UnitPrice); total+=amount;
                detailRows.Add((d,item.Qty,amount));
            }

            var returnNo=No("SR"); var returnDate=string.IsNullOrWhiteSpace(r.ReturnDate)?DateTime.Today.ToString("yyyy-MM-dd"):r.ReturnDate!;
            var returnId=await c.ExecuteScalarAsync<long>(@"INSERT INTO SalesReturn(ReturnNo,SalesOrderId,CustomerId,ReturnDate,Reason,TotalAmount,Status)
VALUES(@ReturnNo,@SalesOrderId,@CustomerId,@ReturnDate,@Reason,@TotalAmount,'CONFIRMED'); SELECT last_insert_rowid();",new{ReturnNo=returnNo,SalesOrderId=r.SalesOrderId,CustomerId=Convert.ToInt64(sale.CustomerId),ReturnDate=returnDate,r.Reason,TotalAmount=total},tx);

            foreach(var row in detailRows)
            {
                dynamic d=row.Detail; decimal qty=row.Qty; decimal amount=row.Amount;
                await c.ExecuteAsync(@"INSERT INTO SalesReturnDetail(SalesReturnId,SalesOrderDetailId,ProductId,Qty,UnitPrice,Amount) VALUES(@ReturnId,@DetailId,@ProductId,@Qty,@UnitPrice,@Amount)",new{ReturnId=returnId,DetailId=Convert.ToInt64(d.Id),ProductId=Convert.ToInt64(d.ProductId),Qty=qty,UnitPrice=Convert.ToDecimal(d.UnitPrice),Amount=amount},tx);
                var before=await c.ExecuteScalarAsync<decimal>("SELECT StockQty FROM Product WHERE Id=@Id",new{Id=Convert.ToInt64(d.ProductId)},tx); var after=before+qty;
                await c.ExecuteAsync("UPDATE Product SET StockQty=@After WHERE Id=@Id",new{After=after,Id=Convert.ToInt64(d.ProductId)},tx);
                await c.ExecuteAsync(@"INSERT INTO InventoryTransaction(ProductId,TxType,Qty,BeforeQty,AfterQty,ReferenceType,ReferenceId,Note)
VALUES(@ProductId,'SALE_RETURN',@Qty,@Before,@After,'SALES_RETURN',@ReturnId,@Note)",new{ProductId=Convert.ToInt64(d.ProductId),Qty=qty,Before=before,After=after,ReturnId=returnId,Note=$"退貨單 {returnNo}"},tx);
            }

            var bill=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM Billing WHERE SalesOrderId=@Id",new{Id=r.SalesOrderId},tx);
            if(bill is not null)
            {
                decimal original=Convert.ToDecimal(bill.Amount);
                decimal allReturns=await c.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(TotalAmount),0) FROM SalesReturn WHERE SalesOrderId=@Id AND Status='CONFIRMED'",new{Id=r.SalesOrderId},tx);
                decimal net=Math.Max(0,original-allReturns);
                decimal paid=await c.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(Amount),0) FROM Payment WHERE BillingId=@Id",new{Id=Convert.ToInt64(bill.Id)},tx);
                decimal refunded=await c.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(Amount),0) FROM Refund WHERE BillingId=@Id",new{Id=Convert.ToInt64(bill.Id)},tx);
                decimal retained=paid-refunded; decimal balance=Math.Max(0,net-retained); decimal refundDue=Math.Max(0,retained-net);
                string status=refundDue>0?"REFUND_DUE":net==0?"VOID":balance<=0?"PAID":retained>0?"PARTIALLY_PAID":"UNPAID";
                await c.ExecuteAsync(@"UPDATE Billing SET ReturnAmount=@ReturnAmount,NetAmount=@NetAmount,PaidAmount=@Paid,RefundedAmount=@Refunded,RefundDue=@RefundDue,BalanceAmount=@Balance,Status=@Status,UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@Id",new{ReturnAmount=allReturns,NetAmount=net,Paid=paid,Refunded=refunded,RefundDue=refundDue,Balance=balance,Status=status,Id=Convert.ToInt64(bill.Id)},tx);
                await c.ExecuteAsync("UPDATE SalesOrder SET PaymentStatus=@Status WHERE Id=@Id",new{Status=status,Id=r.SalesOrderId},tx);

                var invoice=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM Invoice WHERE BillingId=@BillingId AND Status='ISSUED' ORDER BY Id DESC LIMIT 1",new{BillingId=Convert.ToInt64(bill.Id)},tx);
                if(invoice is not null)
                {
                    var priorAdjustments=await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM InvoiceAdjustment WHERE InvoiceId=@InvoiceId",new{InvoiceId=Convert.ToInt64(invoice.Id)},tx);
                    var adjustmentType=net==0 && priorAdjustments==0?"VOID":"ALLOWANCE"; var adjNo=No(adjustmentType=="VOID"?"IV":"AL");
                    await c.ExecuteAsync(@"INSERT INTO InvoiceAdjustment(AdjustmentNo,InvoiceId,SalesReturnId,AdjustmentType,Amount,Status,Note)
VALUES(@No,@InvoiceId,@ReturnId,@Type,@Amount,'DRAFT',@Note)",new{No=adjNo,InvoiceId=Convert.ToInt64(invoice.Id),ReturnId=returnId,Type=adjustmentType,Amount=total,Note=$"由退貨單 {returnNo} 自動建立；尚未串電子發票平台"},tx);
                    if(adjustmentType=="VOID") await c.ExecuteAsync("UPDATE Invoice SET Status='VOID_PENDING',UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@Id",new{Id=Convert.ToInt64(invoice.Id)},tx);
                }
            }

            await _history.AddAsync("RETURN","退貨單",returnId,returnNo,$"建立退貨單 {returnNo}，共 {r.Items.Count} 筆明細，退貨金額 {total:0.##}，庫存已回補",c,tx);
            tx.Commit(); return Ok(new{returnId,returnNo,itemCount=r.Items.Count,total,message="退貨完成，庫存與應收已同步調整"});
        }
        catch(Exception ex){try{tx.Rollback();}catch{}return BadRequest(new{message=ex.Message});}
    }

    [HttpGet("billings/{id:int}/refunds")]
    public async Task<IActionResult> Refunds(int id){using var c=_db.Open();return Ok(await c.QueryAsync("SELECT * FROM Refund WHERE BillingId=@Id ORDER BY RefundDate DESC,Id DESC",new{Id=id}));}

    [HttpPost("billings/{id:int}/refunds")]
    public async Task<IActionResult> AddRefund(int id, RefundCreateRequest r)
    {
        if(r.Amount<=0) return BadRequest(new{message="退款金額必須大於 0"});
        if(string.IsNullOrWhiteSpace(r.RefundMethod)) return BadRequest(new{message="請選擇退款方式"});
        using var c=_db.Open();using var tx=c.BeginTransaction();
        try{
            var bill=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM Billing WHERE Id=@Id",new{Id=id},tx); if(bill is null)return NotFound(new{message="找不到帳單"});
            decimal refundDue=Convert.ToDecimal(bill.RefundDue); if(r.Amount>refundDue)return BadRequest(new{message=$"退款金額不可超過待退款 {refundDue:0.##}"});
            if(r.SalesReturnId.HasValue){var ok=await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM SalesReturn WHERE Id=@Id AND SalesOrderId=@SalesOrderId",new{Id=r.SalesReturnId,SalesOrderId=Convert.ToInt64(bill.SalesOrderId)},tx);if(ok==0)return BadRequest(new{message="退貨單與此帳單不相符"});}
            var refundDate=string.IsNullOrWhiteSpace(r.RefundDate)?DateTime.Today.ToString("yyyy-MM-dd"):r.RefundDate!;
            var refundId=await c.ExecuteScalarAsync<long>(@"INSERT INTO Refund(BillingId,SalesReturnId,Amount,RefundMethod,RefundDate,ReferenceNo,Note) VALUES(@BillingId,@SalesReturnId,@Amount,@Method,@Date,@ReferenceNo,@Note); SELECT last_insert_rowid();",new{BillingId=id,r.SalesReturnId,r.Amount,Method=r.RefundMethod.Trim(),Date=refundDate,r.ReferenceNo,r.Note},tx);
            decimal paid=await c.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(Amount),0) FROM Payment WHERE BillingId=@Id",new{Id=id},tx); decimal refunded=await c.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(Amount),0) FROM Refund WHERE BillingId=@Id",new{Id=id},tx); decimal net=Convert.ToDecimal(bill.NetAmount); decimal retained=paid-refunded; decimal balance=Math.Max(0,net-retained); decimal newRefundDue=Math.Max(0,retained-net); string status=newRefundDue>0?"REFUND_DUE":net==0?"VOID":balance<=0?"PAID":retained>0?"PARTIALLY_PAID":"UNPAID";
            await c.ExecuteAsync("UPDATE Billing SET RefundedAmount=@Refunded,RefundDue=@RefundDue,BalanceAmount=@Balance,Status=@Status,UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@Id",new{Refunded=refunded,RefundDue=newRefundDue,Balance=balance,Status=status,Id=id},tx);
            await c.ExecuteAsync("UPDATE SalesOrder SET PaymentStatus=@Status WHERE Id=@Id",new{Status=status,Id=Convert.ToInt64(bill.SalesOrderId)},tx);
            await _history.AddAsync("REFUND","帳單",id,(string)bill.BillNo,$"帳單 {(string)bill.BillNo} 退款 {r.Amount:0.##}，剩餘待退款 {newRefundDue:0.##}",c,tx);
            tx.Commit();return Ok(new{refundId,refundedAmount=refunded,refundDue=newRefundDue,status,message="退款登錄完成"});
        }catch(Exception ex){try{tx.Rollback();}catch{}return BadRequest(new{message=ex.Message});}
    }

    [HttpGet("invoice-adjustments")]
    public async Task<IActionResult> Adjustments(){using var c=_db.Open();return Ok(await c.QueryAsync(@"SELECT a.*,i.InvoiceNo,r.ReturnNo FROM InvoiceAdjustment a JOIN Invoice i ON i.Id=a.InvoiceId JOIN SalesReturn r ON r.Id=a.SalesReturnId ORDER BY a.Id DESC"));}

    [HttpPost("invoice-adjustments/{id:int}/apply")]
    public async Task<IActionResult> ApplyAdjustment(int id)
    {
        using var c=_db.Open();using var tx=c.BeginTransaction();
        try{var a=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM InvoiceAdjustment WHERE Id=@Id",new{Id=id},tx);if(a is null)return NotFound(new{message="找不到發票調整"});if((string)a.Status!="DRAFT")return BadRequest(new{message="只有草稿調整可執行"});await c.ExecuteAsync("UPDATE InvoiceAdjustment SET Status='APPLIED',UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@Id",new{Id=id},tx);if((string)a.AdjustmentType=="VOID")await c.ExecuteAsync("UPDATE Invoice SET Status='VOID',UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@Id",new{Id=Convert.ToInt64(a.InvoiceId)},tx);await _history.AddAsync("INVOICE_ADJUST","發票調整",id,(string)a.AdjustmentNo,$"內部完成發票{((string)a.AdjustmentType=="VOID"?"作廢":"折讓")}流程；尚未串電子發票平台",c,tx);tx.Commit();return Ok(new{message="發票調整已套用（內部流程）"});}catch(Exception ex){try{tx.Rollback();}catch{}return BadRequest(new{message=ex.Message});}
    }
}
