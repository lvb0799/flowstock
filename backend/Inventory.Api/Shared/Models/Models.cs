namespace Inventory.Api.Shared.Models;

public record Product(int Id, string Sku, string Name, string Unit, decimal SalePrice, decimal CostPrice, decimal StockQty, bool IsActive, string CreatedAt);
public record Party(int Id, string Code, string Name, string? Phone, string? Email, string? Address, string CreatedAt);
public record OrderItemRequest(int ProductId, decimal Qty, decimal UnitPrice);
public record PurchaseCreateRequest(int SupplierId, string? OrderDate, string? Note, List<OrderItemRequest> Items);
public record SalesCreateRequest(int CustomerId, string? OrderDate, string? Note, List<OrderItemRequest> Items);
public record ProductCreateRequest(string Sku, string Name, string? Unit, decimal SalePrice, decimal CostPrice);
public record ProductUpdateRequest(string Sku, string Name, string? Unit, decimal SalePrice, decimal CostPrice);
public record PartyCreateRequest(string Code, string Name, string? Phone, string? Email, string? Address);
public record PartyUpdateRequest(string Code, string Name, string? Phone, string? Email, string? Address);

public record OrderDetailRow(int Id,int ProductId,string Sku,string ProductName,string Unit,decimal Qty,decimal UnitPrice,decimal Amount);

public record PaymentCreateRequest(decimal Amount, string PaymentMethod, string? PaymentDate, string? ReferenceNo, string? Note);
public record InvoiceCreateRequest(string? InvoiceDate, string? BuyerName, string? BuyerTaxId, string? InvoiceType, decimal? TaxRate, string? Note);

public record SalesReturnItemRequest(int SalesOrderDetailId, decimal Qty);
public record SalesReturnCreateRequest(int SalesOrderId, string? ReturnDate, string? Reason, List<SalesReturnItemRequest> Items);
public record RefundCreateRequest(decimal Amount, string RefundMethod, string? RefundDate, string? ReferenceNo, string? Note, int? SalesReturnId);

public record InventoryCountItemRequest(int ProductId, decimal ActualQty);
public record InventoryCountCreateRequest(string? CountDate, string? Note, List<InventoryCountItemRequest> Items);
