using Dapper;
using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Infrastructure.Errors;
using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Audit.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Billing.Controllers;

[ApiController]
[Route("api")]
public class InvoicesController : ControllerBase
{
    private readonly Db _db; private readonly HistoryService _history;
    public InvoicesController(Db db,HistoryService history){_db=db;_history=history;}
    private static string No(string prefix)=>$"{prefix}{DateTime.Now:yyyyMMddHHmmssfff}";

    [HttpPost("billings/{billingId:int}/invoices")]
    public async Task<IActionResult> CreateDraft(int billingId,InvoiceCreateRequest r)
    {
        using var c=_db.Open();using var tx=c.BeginTransaction();
        try{
            var row=await c.QuerySingleOrDefaultAsync<dynamic>(@"SELECT b.*,s.CustomerId,c.Name CustomerName FROM Billing b JOIN SalesOrder s ON s.Id=b.SalesOrderId JOIN Customer c ON c.Id=s.CustomerId WHERE b.Id=@Id",new{Id=billingId},tx);
            if(row is null) throw new NotFoundApiException("找不到帳單");if((string)row.Status=="VOID") throw new ValidationApiException("作廢帳單不可建立發票");
            var exists=await c.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Invoice WHERE BillingId=@Id AND Status<>'VOID'",new{Id=billingId},tx);if(exists>0) throw new ConflictApiException("此帳單已有未作廢的發票資料");
            var buyer=string.IsNullOrWhiteSpace(r.BuyerName)?(string)row.CustomerName:r.BuyerName!.Trim();var taxRate=r.TaxRate??0.05m;if(taxRate<0||taxRate>1) throw new ValidationApiException("稅率格式錯誤");
            decimal total=Convert.ToDecimal(row.NetAmount);if(total<=0&&Convert.ToDecimal(row.ReturnAmount)==0) total=Convert.ToDecimal(row.Amount);if(total<=0) throw new ValidationApiException("此帳單淨應收為 0，不可建立發票");decimal sales=decimal.Round(total/(1+taxRate),2,MidpointRounding.AwayFromZero);decimal tax=total-sales;var invoiceNo=No("INV-DRAFT-");
            var invoiceId=await c.ExecuteScalarAsync<long>(@"INSERT INTO Invoice(InvoiceNo,BillingId,SalesOrderId,InvoiceDate,BuyerName,BuyerTaxId,InvoiceType,SalesAmount,TaxAmount,TotalAmount,Status,Note) VALUES(@InvoiceNo,@BillingId,@SalesOrderId,@InvoiceDate,@BuyerName,@BuyerTaxId,@InvoiceType,@SalesAmount,@TaxAmount,@TotalAmount,'DRAFT',@Note); SELECT last_insert_rowid();",new{InvoiceNo=invoiceNo,BillingId=billingId,SalesOrderId=Convert.ToInt64(row.SalesOrderId),InvoiceDate=string.IsNullOrWhiteSpace(r.InvoiceDate)?DateTime.Today.ToString("yyyy-MM-dd"):r.InvoiceDate,BuyerName=buyer,r.BuyerTaxId,InvoiceType=string.IsNullOrWhiteSpace(r.InvoiceType)?"B2C":r.InvoiceType,SalesAmount=sales,TaxAmount=tax,TotalAmount=total,r.Note},tx);
            await _history.AddAsync("CREATE","發票草稿",invoiceId,invoiceNo,$"由帳單 {(string)row.BillNo} 建立發票草稿，金額 {total:0.##}",c,tx);tx.Commit();return Ok(new{invoiceId,invoiceNo,status="DRAFT",message="發票草稿建立成功"});
        }catch{try{tx.Rollback();}catch{} throw;}
    }

    [HttpPost("invoices/{id:int}/issue")]
    public async Task<IActionResult> Issue(int id)
    {
        using var c=_db.Open();using var tx=c.BeginTransaction();
        try{var invoice=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM Invoice WHERE Id=@Id",new{Id=id},tx);if(invoice is null) throw new NotFoundApiException("找不到發票");if((string)invoice.Status!="DRAFT") throw new ValidationApiException("只有草稿可開立");await c.ExecuteAsync("UPDATE Invoice SET Status='ISSUED',UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@Id",new{Id=id},tx);await _history.AddAsync("ISSUE","發票",id,(string)invoice.InvoiceNo,$"內部標記發票 {(string)invoice.InvoiceNo} 為已開立（尚未串電子發票平台）",c,tx);tx.Commit();return Ok(new{message="已標記為開立；目前尚未串接電子發票平台"});}catch{try{tx.Rollback();}catch{} throw;}
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetAll(){using var c=_db.Open();return Ok(await c.QueryAsync(@"SELECT i.*,b.BillNo,s.OrderNo,c.Name CustomerName FROM Invoice i JOIN Billing b ON b.Id=i.BillingId JOIN SalesOrder s ON s.Id=i.SalesOrderId JOIN Customer c ON c.Id=s.CustomerId ORDER BY i.Id DESC"));}
}
