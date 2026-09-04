using Dapper;
using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Infrastructure.Errors;
using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Audit.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Modules.Billing.Controllers;

[ApiController]
[Route("api/billings/{billingId:int}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly Db _db; private readonly HistoryService _history;
    public PaymentsController(Db db,HistoryService history){_db=db;_history=history;}

    [HttpGet]
    public async Task<IActionResult> GetAll(int billingId){using var c=_db.Open();return Ok(await c.QueryAsync("SELECT * FROM Payment WHERE BillingId=@Id ORDER BY PaymentDate DESC,Id DESC",new{Id=billingId}));}

    [HttpPost]
    public async Task<IActionResult> Create(int billingId,PaymentCreateRequest r)
    {
        if(r.Amount<=0) throw new ValidationApiException("本次收款金額必須大於 0");
        if(string.IsNullOrWhiteSpace(r.PaymentMethod)) throw new ValidationApiException("請選擇付款方式");
        using var c=_db.Open(); using var tx=c.BeginTransaction();
        try{
            var bill=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT * FROM Billing WHERE Id=@Id",new{Id=billingId},tx);
            if(bill is null) throw new NotFoundApiException("找不到帳單"); if((string)bill.Status=="VOID") throw new ValidationApiException("作廢帳單不可收款");
            decimal balance=Convert.ToDecimal(bill.BalanceAmount);if(r.Amount>balance) throw new ValidationApiException($"收款金額不可超過未收金額 {balance:0.##}");
            var paymentDate=string.IsNullOrWhiteSpace(r.PaymentDate)?DateTime.Today.ToString("yyyy-MM-dd"):r.PaymentDate!;
            var paymentId=await c.ExecuteScalarAsync<long>(@"INSERT INTO Payment(BillingId,Amount,PaymentMethod,PaymentDate,ReferenceNo,Note) VALUES(@BillingId,@Amount,@PaymentMethod,@PaymentDate,@ReferenceNo,@Note); SELECT last_insert_rowid();",new{BillingId=billingId,r.Amount,PaymentMethod=r.PaymentMethod.Trim(),PaymentDate=paymentDate,r.ReferenceNo,r.Note},tx);
            var paid=await c.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(Amount),0) FROM Payment WHERE BillingId=@Id",new{Id=billingId},tx);var refunded=await c.ExecuteScalarAsync<decimal>("SELECT COALESCE(SUM(Amount),0) FROM Refund WHERE BillingId=@Id",new{Id=billingId},tx);
            decimal net=Convert.ToDecimal(bill.NetAmount);if(net<=0&&Convert.ToDecimal(bill.ReturnAmount)==0) net=Convert.ToDecimal(bill.Amount);decimal retained=paid-refunded;var newBalance=Math.Max(0,net-retained);var refundDue=Math.Max(0,retained-net);var status=refundDue>0?"REFUND_DUE":net==0?"VOID":retained<=0?"UNPAID":newBalance>0?"PARTIALLY_PAID":"PAID";
            await c.ExecuteAsync("UPDATE Billing SET PaidAmount=@Paid,RefundedAmount=@Refunded,RefundDue=@RefundDue,BalanceAmount=@Balance,Status=@Status,UpdatedAt=CURRENT_TIMESTAMP WHERE Id=@Id",new{Paid=paid,Refunded=refunded,RefundDue=refundDue,Balance=newBalance,Status=status,Id=billingId},tx);await c.ExecuteAsync("UPDATE SalesOrder SET PaymentStatus=@Status WHERE Id=@Id",new{Status=status,Id=Convert.ToInt64(bill.SalesOrderId)},tx);
            await _history.AddAsync("PAYMENT","帳單",billingId,(string)bill.BillNo,$"帳單 {(string)bill.BillNo} 收款 {r.Amount:0.##}，剩餘應收 {newBalance:0.##}",c,tx);
            tx.Commit();return Ok(new{paymentId,paidAmount=paid,balanceAmount=newBalance,status,message="收款完成"});
        }catch{try{tx.Rollback();}catch{} throw;}
    }
}
