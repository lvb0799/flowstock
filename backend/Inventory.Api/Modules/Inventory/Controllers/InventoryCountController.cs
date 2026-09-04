using Dapper;
using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Audit.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Inventory.Controllers;

[ApiController]
[Route("api/inventory-counts")]
public class InventoryCountController : ControllerBase
{
    private readonly Db _db; private readonly HistoryService _history;
    public InventoryCountController(Db db, HistoryService history){_db=db;_history=history;}
    private static string No()=> $"IC{DateTime.Now:yyyyMMddHHmmssfff}";

    [HttpGet]
    public async Task<IActionResult> List(){using var c=_db.Open();return Ok(await c.QueryAsync(@"SELECT i.*,COUNT(d.Id) ItemCount,COALESCE(SUM(ABS(d.DifferenceQty)),0) DifferenceTotal FROM InventoryCount i LEFT JOIN InventoryCountDetail d ON d.InventoryCountId=i.Id GROUP BY i.Id ORDER BY i.Id DESC"));}

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id){using var c=_db.Open();var h=await c.QuerySingleOrDefaultAsync("SELECT * FROM InventoryCount WHERE Id=@Id",new{Id=id});if(h is null)return NotFound(new{message="找不到盤點單"});var items=await c.QueryAsync(@"SELECT d.*,p.Sku,p.Name ProductName,p.Unit FROM InventoryCountDetail d JOIN Product p ON p.Id=d.ProductId WHERE d.InventoryCountId=@Id ORDER BY p.Sku",new{Id=id});return Ok(new{header=h,items});}

    [HttpPost]
    public async Task<IActionResult> Create(InventoryCountCreateRequest r)
    {
        if(r.Items is null || r.Items.Count==0)return BadRequest(new{message="盤點明細不可為空"});
        if(r.Items.Any(x=>x.ActualQty<0))return BadRequest(new{message="實際庫存不可小於 0"});
        if(r.Items.GroupBy(x=>x.ProductId).Any(g=>g.Count()>1))return BadRequest(new{message="同一商品不可重複盤點"});
        using var c=_db.Open();using var tx=c.BeginTransaction();
        try
        {
            var no=No();var date=string.IsNullOrWhiteSpace(r.CountDate)?DateTime.Today.ToString("yyyy-MM-dd"):r.CountDate!;
            var id=await c.ExecuteScalarAsync<long>(@"INSERT INTO InventoryCount(CountNo,CountDate,Note,Status) VALUES(@No,@Date,@Note,'CONFIRMED');SELECT last_insert_rowid();",new{No=no,Date=date,r.Note},tx);
            int changed=0;
            foreach(var i in r.Items)
            {
                var p=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT Id,Sku,Name,StockQty FROM Product WHERE Id=@Id AND IsActive=1",new{Id=i.ProductId},tx);if(p is null)throw new InvalidOperationException($"商品 {i.ProductId} 不存在或已停用");
                decimal before=Convert.ToDecimal(p.StockQty),diff=i.ActualQty-before;
                await c.ExecuteAsync(@"INSERT INTO InventoryCountDetail(InventoryCountId,ProductId,SystemQty,ActualQty,DifferenceQty) VALUES(@CountId,@ProductId,@SystemQty,@ActualQty,@Diff)",new{CountId=id,i.ProductId,SystemQty=before,i.ActualQty,Diff=diff},tx);
                if(diff!=0){changed++;await c.ExecuteAsync("UPDATE Product SET StockQty=@Qty WHERE Id=@Id",new{Qty=i.ActualQty,Id=i.ProductId},tx);await c.ExecuteAsync(@"INSERT INTO InventoryTransaction(ProductId,TxType,Qty,BeforeQty,AfterQty,ReferenceType,ReferenceId,Note) VALUES(@ProductId,'COUNT_ADJUST',@Diff,@Before,@After,'INVENTORY_COUNT',@RefId,@Note)",new{i.ProductId,Diff=diff,Before=before,After=i.ActualQty,RefId=id,Note=$"盤點單 {no}"},tx);}
            }
            await _history.AddAsync("COUNT","庫存盤點",id,no,$"完成盤點 {no}，{r.Items.Count} 項商品，{changed} 項有差異",c,tx);tx.Commit();return Ok(new{id,countNo=no,itemCount=r.Items.Count,changed,message="盤點完成，差異已調整庫存"});
        }catch(Exception ex){try{tx.Rollback();}catch{}return BadRequest(new{message=ex.Message});}
    }
}
