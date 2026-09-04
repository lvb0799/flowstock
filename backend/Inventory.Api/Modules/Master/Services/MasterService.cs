using Dapper;
using Inventory.Api.Infrastructure.Data;
using Inventory.Api.Infrastructure.Errors;
using Inventory.Api.Shared.Models;
using Inventory.Api.Modules.Audit.Services;
using Microsoft.Data.Sqlite;

namespace Inventory.Api.Modules.Master.Services;

public class MasterService
{
    private readonly Db _db;
    private readonly HistoryService _history;
    public MasterService(Db db, HistoryService history){ _db=db; _history=history; }

    public async Task<IEnumerable<dynamic>> GetProductsAsync(){ using var c=_db.Open(); return await c.QueryAsync("SELECT * FROM Product ORDER BY Id DESC"); }
    public async Task<IEnumerable<dynamic>> GetCustomersAsync(){ using var c=_db.Open(); return await c.QueryAsync("SELECT * FROM Customer ORDER BY Id DESC"); }
    public async Task<IEnumerable<dynamic>> GetSuppliersAsync(){ using var c=_db.Open(); return await c.QueryAsync("SELECT * FROM Supplier ORDER BY Id DESC"); }
    public async Task<IEnumerable<dynamic>> GetInventoryTransactionsAsync(){ using var c=_db.Open(); return await c.QueryAsync(@"SELECT t.*,p.Sku,p.Name ProductName FROM InventoryTransaction t JOIN Product p ON p.Id=t.ProductId ORDER BY t.Id DESC LIMIT 500"); }

    public async Task<object> CreateProductAsync(ProductCreateRequest r)
    {
        ValidateProduct(r.Sku,r.Name);
        try{
            using var c=_db.Open();
            var id=await c.ExecuteScalarAsync<long>(@"INSERT INTO Product(Sku,Name,Unit,SalePrice,CostPrice) VALUES(@Sku,@Name,@Unit,@SalePrice,@CostPrice); SELECT last_insert_rowid();", new {Sku=r.Sku.Trim(),Name=r.Name.Trim(),Unit=string.IsNullOrWhiteSpace(r.Unit)?"個":r.Unit.Trim(),r.SalePrice,r.CostPrice});
            await _history.AddAsync("CREATE","商品",id,r.Name.Trim(),$"新增商品 {r.Sku.Trim()} / {r.Name.Trim()}");
            return new{id,message="商品新增成功"};
        }catch(SqliteException ex) when(ex.SqliteErrorCode==19){throw new ConflictApiException($"SKU {r.Sku} 已存在");}
    }

    public async Task<object> UpdateProductAsync(long id, ProductUpdateRequest r)
    {
        ValidateProduct(r.Sku,r.Name);
        try{
            using var c=_db.Open();
            var n=await c.ExecuteAsync(@"UPDATE Product SET Sku=@Sku,Name=@Name,Unit=@Unit,SalePrice=@SalePrice,CostPrice=@CostPrice WHERE Id=@Id", new {Id=id,Sku=r.Sku.Trim(),Name=r.Name.Trim(),Unit=string.IsNullOrWhiteSpace(r.Unit)?"個":r.Unit.Trim(),r.SalePrice,r.CostPrice});
            if(n==0) throw new NotFoundApiException("找不到商品");
            await _history.AddAsync("UPDATE","商品",id,r.Name.Trim(),$"修改商品 {r.Sku.Trim()} / {r.Name.Trim()}");
            return new{id,message="商品修改成功"};
        }catch(SqliteException ex) when(ex.SqliteErrorCode==19){throw new ConflictApiException($"SKU {r.Sku} 已存在");}
    }

    public async Task<object> DeleteProductAsync(long id)
    {
        try{
            using var c=_db.Open();
            var old=await c.QuerySingleOrDefaultAsync<dynamic>("SELECT Sku,Name FROM Product WHERE Id=@id",new{id});
            if(old is null) throw new NotFoundApiException("找不到商品");
            await c.ExecuteAsync("DELETE FROM Product WHERE Id=@id",new{id});
            await _history.AddAsync("DELETE","商品",id,(string)old.Name,$"刪除商品 {(string)old.Sku} / {(string)old.Name}");
            return new{id,message="商品刪除成功"};
        }catch(SqliteException ex) when(ex.SqliteErrorCode==19){throw new ConflictApiException("此商品已有進銷存紀錄，無法直接刪除");}
    }

    public Task<object> CreateCustomerAsync(PartyCreateRequest r)=>CreatePartyAsync("Customer","客戶",r);
    public Task<object> UpdateCustomerAsync(long id, PartyUpdateRequest r)=>UpdatePartyAsync("Customer","客戶",id,r);
    public Task<object> DeleteCustomerAsync(long id)=>DeletePartyAsync("Customer","客戶",id);
    public Task<object> CreateSupplierAsync(PartyCreateRequest r)=>CreatePartyAsync("Supplier","供應商",r);
    public Task<object> UpdateSupplierAsync(long id, PartyUpdateRequest r)=>UpdatePartyAsync("Supplier","供應商",id,r);
    public Task<object> DeleteSupplierAsync(long id)=>DeletePartyAsync("Supplier","供應商",id);

    private static void ValidateProduct(string sku,string name){ if(string.IsNullOrWhiteSpace(sku)||string.IsNullOrWhiteSpace(name)) throw new ValidationApiException("SKU 與商品名稱不可為空"); }
    private static void ValidateParty(string label,string code,string name){ if(string.IsNullOrWhiteSpace(code)||string.IsNullOrWhiteSpace(name)) throw new ValidationApiException($"{label}代碼與名稱不可為空"); }

    private async Task<object> CreatePartyAsync(string table,string label,PartyCreateRequest r)
    {
        ValidateParty(label,r.Code,r.Name);
        try{
            using var c=_db.Open();
            var id=await c.ExecuteScalarAsync<long>($@"INSERT INTO {table}(Code,Name,Phone,Email,Address) VALUES(@Code,@Name,@Phone,@Email,@Address); SELECT last_insert_rowid();",new{Code=r.Code.Trim(),Name=r.Name.Trim(),r.Phone,r.Email,r.Address});
            await _history.AddAsync("CREATE",label,id,r.Name.Trim(),$"新增{label} {r.Code.Trim()} / {r.Name.Trim()}");
            return new{id,message=$"{label}新增成功"};
        }catch(SqliteException ex) when(ex.SqliteErrorCode==19){throw new ConflictApiException($"{label}代碼 {r.Code} 已存在");}
    }

    private async Task<object> UpdatePartyAsync(string table,string label,long id,PartyUpdateRequest r)
    {
        ValidateParty(label,r.Code,r.Name);
        try{
            using var c=_db.Open();
            var n=await c.ExecuteAsync($@"UPDATE {table} SET Code=@Code,Name=@Name,Phone=@Phone,Email=@Email,Address=@Address WHERE Id=@Id",new{Id=id,Code=r.Code.Trim(),Name=r.Name.Trim(),r.Phone,r.Email,r.Address});
            if(n==0) throw new NotFoundApiException($"找不到{label}");
            await _history.AddAsync("UPDATE",label,id,r.Name.Trim(),$"修改{label} {r.Code.Trim()} / {r.Name.Trim()}");
            return new{id,message=$"{label}修改成功"};
        }catch(SqliteException ex) when(ex.SqliteErrorCode==19){throw new ConflictApiException($"{label}代碼 {r.Code} 已存在");}
    }

    private async Task<object> DeletePartyAsync(string table,string label,long id)
    {
        try{
            using var c=_db.Open();
            var old=await c.QuerySingleOrDefaultAsync<dynamic>($"SELECT Code,Name FROM {table} WHERE Id=@id",new{id});
            if(old is null) throw new NotFoundApiException($"找不到{label}");
            await c.ExecuteAsync($"DELETE FROM {table} WHERE Id=@id",new{id});
            await _history.AddAsync("DELETE",label,id,(string)old.Name,$"刪除{label} {(string)old.Code} / {(string)old.Name}");
            return new{id,message=$"{label}刪除成功"};
        }catch(SqliteException ex) when(ex.SqliteErrorCode==19){throw new ConflictApiException($"此{label}已有單據紀錄，無法直接刪除");}
    }
}
