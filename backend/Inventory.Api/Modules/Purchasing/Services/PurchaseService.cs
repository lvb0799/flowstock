using Dapper;
using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Infrastructure.Errors;
using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Audit.Services;

namespace Inventory.Api.Modules.Purchasing.Services;

public class PurchaseService
{
    private sealed record ProductStockRow(decimal StockQty,string Name,decimal CostPrice);
    private readonly Db _db;
    private readonly HistoryService _history;
    public PurchaseService(Db db,HistoryService history){_db=db;_history=history;}
    private static string No(string prefix)=>$"{prefix}{DateTime.Now:yyyyMMddHHmmssfff}";

    public async Task<IEnumerable<dynamic>> GetAllAsync()
    {
        using var c=_db.Open();
        return await c.QueryAsync(@"SELECT o.*,s.Name SupplierName,COUNT(d.Id) ItemCount
FROM PurchaseOrder o JOIN Supplier s ON s.Id=o.SupplierId
LEFT JOIN PurchaseOrderDetail d ON d.PurchaseOrderId=o.Id
GROUP BY o.Id ORDER BY o.Id DESC");
    }

    public async Task<object> GetDetailAsync(int id)
    {
        using var c=_db.Open();
        var header=await c.QuerySingleOrDefaultAsync(@"SELECT o.*,s.Code SupplierCode,s.Name SupplierName
FROM PurchaseOrder o JOIN Supplier s ON s.Id=o.SupplierId WHERE o.Id=@Id",new{Id=id});
        if(header is null) throw new NotFoundApiException("找不到進貨單");
        var items=await c.QueryAsync(@"SELECT d.Id,d.ProductId,p.Sku,p.Name ProductName,p.Unit,d.Qty,d.UnitPrice,d.Amount
FROM PurchaseOrderDetail d JOIN Product p ON p.Id=d.ProductId
WHERE d.PurchaseOrderId=@Id ORDER BY d.Id",new{Id=id});
        return new{header,items};
    }

    public async Task<object> CreateAsync(PurchaseCreateRequest req)
    {
        ValidateItems(req.Items);
        using var c=_db.Open(); using var tx=c.BeginTransaction();
        try{
            var exists=await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Supplier WHERE Id=@Id",new{Id=req.SupplierId},tx);
            if(exists==0) throw new ValidationApiException("供應商不存在");
            var total=req.Items.Sum(x=>x.Qty*x.UnitPrice); var orderNo=No("PO");
            var id=await c.ExecuteScalarAsync<long>(@"INSERT INTO PurchaseOrder(OrderNo,SupplierId,OrderDate,TotalAmount,Note)
VALUES(@OrderNo,@SupplierId,@OrderDate,@Total,@Note); SELECT last_insert_rowid();",new{OrderNo=orderNo,req.SupplierId,OrderDate=req.OrderDate??DateTime.Today.ToString("yyyy-MM-dd"),Total=total,req.Note},tx);
            foreach(var i in req.Items){
                var p=await c.QuerySingleOrDefaultAsync<ProductStockRow>("SELECT StockQty,Name,CostPrice FROM Product WHERE Id=@Id AND IsActive=1",new{Id=i.ProductId},tx);
                if(p is null) throw new ValidationApiException($"商品 {i.ProductId} 不存在或已停用");
                await c.ExecuteAsync(@"INSERT INTO PurchaseOrderDetail(PurchaseOrderId,ProductId,Qty,UnitPrice,Amount)
VALUES(@OrderId,@ProductId,@Qty,@UnitPrice,@Amount)",new{OrderId=id,i.ProductId,i.Qty,i.UnitPrice,Amount=i.Qty*i.UnitPrice},tx);
                var before=p.StockQty; var after=before+i.Qty; var movingCost=after<=0?i.UnitPrice:((before*p.CostPrice)+(i.Qty*i.UnitPrice))/after;
                await c.ExecuteAsync("UPDATE Product SET StockQty=@After,CostPrice=@Cost WHERE Id=@Id",new{After=after,Cost=movingCost,Id=i.ProductId},tx);
                await c.ExecuteAsync(@"INSERT INTO InventoryTransaction(ProductId,TxType,Qty,BeforeQty,AfterQty,ReferenceType,ReferenceId)
VALUES(@ProductId,'PURCHASE',@Qty,@Before,@After,'PURCHASE',@RefId)",new{i.ProductId,i.Qty,Before=before,After=after,RefId=id},tx);
            }
            await _history.AddAsync("CREATE","進貨單",id,orderNo,$"建立進貨單 {orderNo}，共 {req.Items.Count} 筆明細，金額 {total:0.##}",c,tx);
            tx.Commit(); return new{id,orderNo,itemCount=req.Items.Count,total};
        }catch{try{tx.Rollback();}catch{} throw;}
    }

    public async Task<object> VoidAsync(int id)
    {
        using var c=_db.Open(); using var tx=c.BeginTransaction();
        try{
            var order=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM PurchaseOrder WHERE Id=@Id",new{Id=id},tx);
            if(order is null) throw new NotFoundApiException("找不到進貨單");
            if((string)order.Status=="VOID") throw new ValidationApiException("此進貨單已作廢");
            var items=(await c.QueryAsync<dynamic>("SELECT * FROM PurchaseOrderDetail WHERE PurchaseOrderId=@Id",new{Id=id},tx)).ToList();
            foreach(var d in items){
                var productId=Convert.ToInt64(d.ProductId); var qty=Convert.ToDecimal(d.Qty);
                var before=await c.ExecuteScalarAsync<decimal>("SELECT StockQty FROM Product WHERE Id=@Id",new{Id=productId},tx);
                if(before<qty) throw new ValidationApiException($"商品庫存不足以反沖進貨，ProductId={productId}，目前 {before:0.##}，需反沖 {qty:0.##}");
                var after=before-qty;
                await c.ExecuteAsync("UPDATE Product SET StockQty=@After WHERE Id=@Id",new{After=after,Id=productId},tx);
                await c.ExecuteAsync(@"INSERT INTO InventoryTransaction(ProductId,TxType,Qty,BeforeQty,AfterQty,ReferenceType,ReferenceId,Note)
VALUES(@ProductId,'PURCHASE_VOID',@Qty,@Before,@After,'PURCHASE',@RefId,@Note)",new{ProductId=productId,Qty=qty,Before=before,After=after,RefId=id,Note=$"作廢進貨單 {(string)order.OrderNo}"},tx);
            }
            await c.ExecuteAsync("UPDATE PurchaseOrder SET Status='VOID' WHERE Id=@Id",new{Id=id},tx);
            await _history.AddAsync("VOID","進貨單",id,(string)order.OrderNo,$"作廢進貨單 {(string)order.OrderNo}，庫存已反沖",c,tx);
            tx.Commit(); return new{message="進貨單已作廢，庫存已反沖"};
        }catch{try{tx.Rollback();}catch{} throw;}
    }

    private static void ValidateItems(List<OrderItemRequest>? items)
    {
        if(items is null||items.Count==0) throw new ValidationApiException("單據明細不可為空");
        if(items.Any(x=>x.ProductId<=0)) throw new ValidationApiException("明細商品不可為空");
        if(items.Any(x=>x.Qty<=0)) throw new ValidationApiException("明細數量必須大於 0");
        if(items.Any(x=>x.UnitPrice<0)) throw new ValidationApiException("明細單價不可小於 0");
        var dup=items.GroupBy(x=>x.ProductId).FirstOrDefault(g=>g.Count()>1);
        if(dup is not null) throw new ValidationApiException($"同一張單不可重複加入相同商品（ProductId={dup.Key}）");
    }
}
