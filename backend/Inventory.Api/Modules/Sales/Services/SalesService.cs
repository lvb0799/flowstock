using Dapper;
using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Infrastructure.Errors;
using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Audit.Services;

namespace Inventory.Api.Modules.Sales.Services;

public class SalesService
{
    private sealed record ProductStockRow(decimal StockQty,string Name,decimal CostPrice);
    private readonly Db _db;
    private readonly HistoryService _history;
    public SalesService(Db db,HistoryService history){_db=db;_history=history;}
    private static string No(string prefix)=>$"{prefix}{DateTime.Now:yyyyMMddHHmmssfff}";

    public async Task<IEnumerable<dynamic>> GetAllAsync(){using var c=_db.Open();return await c.QueryAsync(@"SELECT o.*,c.Name CustomerName,COUNT(d.Id) ItemCount FROM SalesOrder o JOIN Customer c ON c.Id=o.CustomerId LEFT JOIN SalesOrderDetail d ON d.SalesOrderId=o.Id GROUP BY o.Id ORDER BY o.Id DESC");}

    public async Task<object> GetDetailAsync(int id)
    {
        using var c=_db.Open();
        var header=await c.QuerySingleOrDefaultAsync(@"SELECT o.*,c.Code CustomerCode,c.Name CustomerName FROM SalesOrder o JOIN Customer c ON c.Id=o.CustomerId WHERE o.Id=@Id",new{Id=id});
        if(header is null) throw new NotFoundApiException("找不到銷貨單");
        var items=await c.QueryAsync(@"SELECT d.Id,d.ProductId,p.Sku,p.Name ProductName,p.Unit,d.Qty,d.UnitPrice,d.Amount,d.UnitCost,d.CostAmount,
COALESCE((SELECT SUM(rd.Qty) FROM SalesReturnDetail rd JOIN SalesReturn rr ON rr.Id=rd.SalesReturnId WHERE rd.SalesOrderDetailId=d.Id AND rr.Status='CONFIRMED'),0) ReturnedQty,
d.Qty-COALESCE((SELECT SUM(rd.Qty) FROM SalesReturnDetail rd JOIN SalesReturn rr ON rr.Id=rd.SalesReturnId WHERE rd.SalesOrderDetailId=d.Id AND rr.Status='CONFIRMED'),0) ReturnableQty
FROM SalesOrderDetail d JOIN Product p ON p.Id=d.ProductId WHERE d.SalesOrderId=@Id ORDER BY d.Id",new{Id=id});
        return new{header,items};
    }

    public async Task<object> CreateAsync(SalesCreateRequest req)
    {
        ValidateItems(req.Items);
        using var c=_db.Open(); using var tx=c.BeginTransaction();
        try{
            var exists=await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Customer WHERE Id=@Id",new{Id=req.CustomerId},tx);
            if(exists==0) throw new ValidationApiException("客戶不存在");
            foreach(var i in req.Items){var p=await c.QuerySingleOrDefaultAsync<ProductStockRow>("SELECT StockQty,Name,CostPrice FROM Product WHERE Id=@Id AND IsActive=1",new{Id=i.ProductId},tx);if(p is null) throw new ValidationApiException($"商品 {i.ProductId} 不存在或已停用");if(p.StockQty<i.Qty) throw new ValidationApiException($"商品「{p.Name}」庫存不足，需要 {i.Qty:0.##}，目前 {p.StockQty:0.##}");}
            var total=req.Items.Sum(x=>x.Qty*x.UnitPrice); var orderNo=No("SO");
            var id=await c.ExecuteScalarAsync<long>(@"INSERT INTO SalesOrder(OrderNo,CustomerId,OrderDate,TotalAmount,Note) VALUES(@OrderNo,@CustomerId,@OrderDate,@Total,@Note); SELECT last_insert_rowid();",new{OrderNo=orderNo,req.CustomerId,OrderDate=req.OrderDate??DateTime.Today.ToString("yyyy-MM-dd"),Total=total,req.Note},tx);
            foreach(var i in req.Items){var p=await c.QuerySingleAsync<ProductStockRow>("SELECT StockQty,Name,CostPrice FROM Product WHERE Id=@Id",new{Id=i.ProductId},tx);await c.ExecuteAsync(@"INSERT INTO SalesOrderDetail(SalesOrderId,ProductId,Qty,UnitPrice,Amount,UnitCost,CostAmount) VALUES(@OrderId,@ProductId,@Qty,@UnitPrice,@Amount,@UnitCost,@CostAmount)",new{OrderId=id,i.ProductId,i.Qty,i.UnitPrice,Amount=i.Qty*i.UnitPrice,UnitCost=p.CostPrice,CostAmount=i.Qty*p.CostPrice},tx);var before=p.StockQty;var after=before-i.Qty;await c.ExecuteAsync("UPDATE Product SET StockQty=@After WHERE Id=@Id",new{After=after,Id=i.ProductId},tx);await c.ExecuteAsync(@"INSERT INTO InventoryTransaction(ProductId,TxType,Qty,BeforeQty,AfterQty,ReferenceType,ReferenceId) VALUES(@ProductId,'SALE',@Qty,@Before,@After,'SALE',@RefId)",new{i.ProductId,i.Qty,Before=before,After=after,RefId=id},tx);}
            var billNo=No("BL"); await c.ExecuteAsync(@"INSERT INTO Billing(BillNo,SalesOrderId,BillDate,DueDate,Amount,PaidAmount,ReturnAmount,NetAmount,RefundedAmount,RefundDue,BalanceAmount,Status) VALUES(@BillNo,@SalesOrderId,@BillDate,@DueDate,@Amount,0,0,@Amount,0,0,@Amount,'UNPAID')",new{BillNo=billNo,SalesOrderId=id,BillDate=DateTime.Today.ToString("yyyy-MM-dd"),DueDate=DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),Amount=total},tx);
            await _history.AddAsync("CREATE","銷貨單",id,orderNo,$"建立銷貨單 {orderNo}，帳單 {billNo}，共 {req.Items.Count} 筆明細，金額 {total:0.##}",c,tx);
            tx.Commit(); return new{id,orderNo,billNo,itemCount=req.Items.Count,total};
        }catch{try{tx.Rollback();}catch{} throw;}
    }

    public async Task<object> VoidAsync(int id)
    {
        using var c=_db.Open(); using var tx=c.BeginTransaction();
        try{
            var order=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM SalesOrder WHERE Id=@Id",new{Id=id},tx);
            if(order is null) throw new NotFoundApiException("找不到銷貨單"); if((string)order.Status=="VOID") throw new ValidationApiException("此銷貨單已作廢");
            var bill=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM Billing WHERE SalesOrderId=@Id",new{Id=id},tx);
            if(bill is not null){var paymentCount=await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Payment WHERE BillingId=@Id",new{Id=Convert.ToInt64(bill.Id)},tx);var invoiceCount=await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Invoice WHERE BillingId=@Id AND Status<>'VOID'",new{Id=Convert.ToInt64(bill.Id)},tx);if(paymentCount>0) throw new ValidationApiException("此銷貨單已有收款紀錄，不可直接作廢；請走退貨/退款流程");if(invoiceCount>0) throw new ValidationApiException("此銷貨單已有發票，不可直接作廢；請走退貨及發票調整流程");}
            var returnCount=await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM SalesReturn WHERE SalesOrderId=@Id AND Status='CONFIRMED'",new{Id=id},tx); if(returnCount>0) throw new ValidationApiException("此銷貨單已有退貨紀錄，不可直接作廢");
            var items=await c.QueryAsync<dynamic>("SELECT * FROM SalesOrderDetail WHERE SalesOrderId=@Id",new{Id=id},tx); foreach(var d in items){var productId=Convert.ToInt64(d.ProductId);var before=await c.ExecuteScalarAsync<decimal>("SELECT StockQty FROM Product WHERE Id=@Id",new{Id=productId},tx);var qty=Convert.ToDecimal(d.Qty);var after=before+qty;await c.ExecuteAsync("UPDATE Product SET StockQty=@After WHERE Id=@Id",new{After=after,Id=productId},tx);await c.ExecuteAsync(@"INSERT INTO InventoryTransaction(ProductId,TxType,Qty,BeforeQty,AfterQty,ReferenceType,ReferenceId,Note) VALUES(@ProductId,'SALE_VOID',@Qty,@Before,@After,'SALE',@RefId,@Note)",new{ProductId=productId,Qty=qty,Before=before,After=after,RefId=id,Note=$"作廢銷貨單 {(string)order.OrderNo}"},tx);}
            await c.ExecuteAsync("UPDATE SalesOrder SET Status='VOID',PaymentStatus='VOID' WHERE Id=@Id",new{Id=id},tx);if(bill is not null) await c.ExecuteAsync("UPDATE Billing SET Status='VOID',BalanceAmount=0,UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@Id",new{Id=Convert.ToInt64(bill.Id)},tx);
            await _history.AddAsync("VOID","銷貨單",id,(string)order.OrderNo,$"作廢銷貨單 {(string)order.OrderNo}，庫存已反沖",c,tx);tx.Commit();return new{message="銷貨單已作廢，庫存已反沖，帳單已作廢"};
        }catch{try{tx.Rollback();}catch{} throw;}
    }

    private static void ValidateItems(List<OrderItemRequest>? items){if(items is null||items.Count==0) throw new ValidationApiException("單據明細不可為空");if(items.Any(x=>x.ProductId<=0)) throw new ValidationApiException("明細商品不可為空");if(items.Any(x=>x.Qty<=0)) throw new ValidationApiException("明細數量必須大於 0");if(items.Any(x=>x.UnitPrice<0)) throw new ValidationApiException("明細單價不可小於 0");var dup=items.GroupBy(x=>x.ProductId).FirstOrDefault(g=>g.Count()>1);if(dup is not null) throw new ValidationApiException($"同一張單不可重複加入相同商品（ProductId={dup.Key}）");}
}
