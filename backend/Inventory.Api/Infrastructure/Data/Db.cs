using Dapper;
using Microsoft.Data.Sqlite;

namespace Inventory.Api.Infrastructure.Data;

public class Db
{
    private readonly IConfiguration _config;
    public Db(IConfiguration config) => _config = config;

    public SqliteConnection Open()
    {
        var cs = _config.GetConnectionString("Default")!;
        var c = new SqliteConnection(cs);
        c.Open();
        return c;
    }

    public async Task InitializeAsync()
    {
        using var c = Open();
        await c.ExecuteAsync(@"
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Product(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Sku TEXT NOT NULL UNIQUE,
    Name TEXT NOT NULL,
    Unit TEXT NOT NULL DEFAULT '個',
    SalePrice REAL NOT NULL DEFAULT 0,
    CostPrice REAL NOT NULL DEFAULT 0,
    StockQty REAL NOT NULL DEFAULT 0,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS Customer(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT NOT NULL UNIQUE,
    Name TEXT NOT NULL,
    Phone TEXT,
    Email TEXT,
    Address TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS Supplier(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Code TEXT NOT NULL UNIQUE,
    Name TEXT NOT NULL,
    Phone TEXT,
    Email TEXT,
    Address TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS PurchaseOrder(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderNo TEXT NOT NULL UNIQUE,
    SupplierId INTEGER NOT NULL,
    OrderDate TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'CONFIRMED',
    TotalAmount REAL NOT NULL DEFAULT 0,
    Note TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(SupplierId) REFERENCES Supplier(Id)
);
CREATE TABLE IF NOT EXISTS PurchaseOrderDetail(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PurchaseOrderId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    Qty REAL NOT NULL,
    UnitPrice REAL NOT NULL,
    Amount REAL NOT NULL,
    FOREIGN KEY(PurchaseOrderId) REFERENCES PurchaseOrder(Id) ON DELETE CASCADE,
    FOREIGN KEY(ProductId) REFERENCES Product(Id)
);
CREATE TABLE IF NOT EXISTS SalesOrder(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderNo TEXT NOT NULL UNIQUE,
    CustomerId INTEGER NOT NULL,
    OrderDate TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'CONFIRMED',
    TotalAmount REAL NOT NULL DEFAULT 0,
    PaymentStatus TEXT NOT NULL DEFAULT 'UNPAID',
    Note TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(CustomerId) REFERENCES Customer(Id)
);
CREATE TABLE IF NOT EXISTS SalesOrderDetail(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SalesOrderId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    Qty REAL NOT NULL,
    UnitPrice REAL NOT NULL,
    Amount REAL NOT NULL,
    UnitCost REAL NOT NULL DEFAULT 0,
    CostAmount REAL NOT NULL DEFAULT 0,
    FOREIGN KEY(SalesOrderId) REFERENCES SalesOrder(Id) ON DELETE CASCADE,
    FOREIGN KEY(ProductId) REFERENCES Product(Id)
);
CREATE TABLE IF NOT EXISTS InventoryTransaction(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId INTEGER NOT NULL,
    TxType TEXT NOT NULL,
    Qty REAL NOT NULL,
    BeforeQty REAL NOT NULL,
    AfterQty REAL NOT NULL,
    ReferenceType TEXT,
    ReferenceId INTEGER,
    Note TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(ProductId) REFERENCES Product(Id)
);
CREATE TABLE IF NOT EXISTS Billing(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BillNo TEXT NOT NULL UNIQUE,
    SalesOrderId INTEGER NOT NULL UNIQUE,
    BillDate TEXT NOT NULL,
    DueDate TEXT,
    Amount REAL NOT NULL,
    PaidAmount REAL NOT NULL DEFAULT 0,
    ReturnAmount REAL NOT NULL DEFAULT 0,
    NetAmount REAL NOT NULL DEFAULT 0,
    RefundedAmount REAL NOT NULL DEFAULT 0,
    RefundDue REAL NOT NULL DEFAULT 0,
    BalanceAmount REAL NOT NULL DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'UNPAID',
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY(SalesOrderId) REFERENCES SalesOrder(Id)
);
CREATE TABLE IF NOT EXISTS Payment(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BillingId INTEGER NOT NULL,
    Amount REAL NOT NULL,
    PaymentMethod TEXT NOT NULL,
    PaymentDate TEXT NOT NULL,
    ReferenceNo TEXT,
    Note TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(BillingId) REFERENCES Billing(Id)
);
CREATE TABLE IF NOT EXISTS Invoice(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    InvoiceNo TEXT UNIQUE,
    BillingId INTEGER NOT NULL,
    SalesOrderId INTEGER NOT NULL,
    InvoiceDate TEXT NOT NULL,
    BuyerName TEXT NOT NULL,
    BuyerTaxId TEXT,
    InvoiceType TEXT NOT NULL DEFAULT 'B2C',
    SalesAmount REAL NOT NULL,
    TaxAmount REAL NOT NULL DEFAULT 0,
    TotalAmount REAL NOT NULL,
    Status TEXT NOT NULL DEFAULT 'DRAFT',
    ExternalInvoiceId TEXT,
    Note TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY(BillingId) REFERENCES Billing(Id),
    FOREIGN KEY(SalesOrderId) REFERENCES SalesOrder(Id)
);
CREATE INDEX IF NOT EXISTS IX_Payment_BillingId ON Payment(BillingId);
CREATE INDEX IF NOT EXISTS IX_Invoice_BillingId ON Invoice(BillingId);

CREATE TABLE IF NOT EXISTS SalesReturn(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReturnNo TEXT NOT NULL UNIQUE,
    SalesOrderId INTEGER NOT NULL,
    CustomerId INTEGER NOT NULL,
    ReturnDate TEXT NOT NULL,
    Reason TEXT,
    TotalAmount REAL NOT NULL DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'CONFIRMED',
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(SalesOrderId) REFERENCES SalesOrder(Id),
    FOREIGN KEY(CustomerId) REFERENCES Customer(Id)
);
CREATE TABLE IF NOT EXISTS SalesReturnDetail(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SalesReturnId INTEGER NOT NULL,
    SalesOrderDetailId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    Qty REAL NOT NULL,
    UnitPrice REAL NOT NULL,
    Amount REAL NOT NULL,
    FOREIGN KEY(SalesReturnId) REFERENCES SalesReturn(Id) ON DELETE CASCADE,
    FOREIGN KEY(SalesOrderDetailId) REFERENCES SalesOrderDetail(Id),
    FOREIGN KEY(ProductId) REFERENCES Product(Id)
);
CREATE TABLE IF NOT EXISTS Refund(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BillingId INTEGER NOT NULL,
    SalesReturnId INTEGER,
    Amount REAL NOT NULL,
    RefundMethod TEXT NOT NULL,
    RefundDate TEXT NOT NULL,
    ReferenceNo TEXT,
    Note TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY(BillingId) REFERENCES Billing(Id),
    FOREIGN KEY(SalesReturnId) REFERENCES SalesReturn(Id)
);
CREATE TABLE IF NOT EXISTS InvoiceAdjustment(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AdjustmentNo TEXT NOT NULL UNIQUE,
    InvoiceId INTEGER NOT NULL,
    SalesReturnId INTEGER NOT NULL,
    AdjustmentType TEXT NOT NULL,
    Amount REAL NOT NULL,
    Status TEXT NOT NULL DEFAULT 'DRAFT',
    Note TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY(InvoiceId) REFERENCES Invoice(Id),
    FOREIGN KEY(SalesReturnId) REFERENCES SalesReturn(Id)
);
CREATE INDEX IF NOT EXISTS IX_SalesReturn_SalesOrderId ON SalesReturn(SalesOrderId);
CREATE INDEX IF NOT EXISTS IX_SalesReturnDetail_OrderDetailId ON SalesReturnDetail(SalesOrderDetailId);
CREATE INDEX IF NOT EXISTS IX_Refund_BillingId ON Refund(BillingId);
CREATE INDEX IF NOT EXISTS IX_InvoiceAdjustment_InvoiceId ON InvoiceAdjustment(InvoiceId);


CREATE TABLE IF NOT EXISTS InventoryCount(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CountNo TEXT NOT NULL UNIQUE,
    CountDate TEXT NOT NULL,
    Note TEXT,
    Status TEXT NOT NULL DEFAULT 'CONFIRMED',
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS InventoryCountDetail(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    InventoryCountId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    SystemQty REAL NOT NULL,
    ActualQty REAL NOT NULL,
    DifferenceQty REAL NOT NULL,
    FOREIGN KEY(InventoryCountId) REFERENCES InventoryCount(Id) ON DELETE CASCADE,
    FOREIGN KEY(ProductId) REFERENCES Product(Id)
);
CREATE INDEX IF NOT EXISTS IX_InventoryCountDetail_CountId ON InventoryCountDetail(InventoryCountId);

CREATE TABLE IF NOT EXISTS ActivityHistory(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Action TEXT NOT NULL,
    EntityType TEXT NOT NULL,
    EntityId INTEGER,
    EntityLabel TEXT,
    Description TEXT,
    Actor TEXT NOT NULL DEFAULT 'SYSTEM',
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS IX_ActivityHistory_CreatedAt ON ActivityHistory(CreatedAt DESC);

CREATE TABLE IF NOT EXISTS ApiActionLog(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TraceId TEXT NOT NULL,
    Method TEXT NOT NULL,
    Path TEXT NOT NULL,
    QueryString TEXT,
    Endpoint TEXT,
    StatusCode INTEGER NOT NULL,
    ElapsedMs INTEGER NOT NULL,
    Actor TEXT NOT NULL DEFAULT 'ANONYMOUS',
    ClientIp TEXT,
    UserAgent TEXT,
    ErrorMessage TEXT,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS IX_ApiActionLog_CreatedAt ON ApiActionLog(CreatedAt DESC);
CREATE INDEX IF NOT EXISTS IX_ApiActionLog_Path ON ApiActionLog(Path);
");


        // v0.8 migration: historical sales cost snapshot for margin reporting.
        var salesDetailColumnsV08 = (await c.QueryAsync<string>("SELECT name FROM pragma_table_info('SalesOrderDetail')")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!salesDetailColumnsV08.Contains("UnitCost")) await c.ExecuteAsync("ALTER TABLE SalesOrderDetail ADD COLUMN UnitCost REAL NOT NULL DEFAULT 0");
        if (!salesDetailColumnsV08.Contains("CostAmount")) await c.ExecuteAsync("ALTER TABLE SalesOrderDetail ADD COLUMN CostAmount REAL NOT NULL DEFAULT 0");
        await c.ExecuteAsync(@"UPDATE SalesOrderDetail SET UnitCost=(SELECT CostPrice FROM Product WHERE Product.Id=SalesOrderDetail.ProductId), CostAmount=Qty*(SELECT CostPrice FROM Product WHERE Product.Id=SalesOrderDetail.ProductId) WHERE UnitCost=0 AND CostAmount=0");

        // v0.6 migration: return/refund fields for existing databases.
        var billingColumnsV06 = (await c.QueryAsync<string>("SELECT name FROM pragma_table_info('Billing')")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!billingColumnsV06.Contains("ReturnAmount")) await c.ExecuteAsync("ALTER TABLE Billing ADD COLUMN ReturnAmount REAL NOT NULL DEFAULT 0");
        if (!billingColumnsV06.Contains("NetAmount")) await c.ExecuteAsync("ALTER TABLE Billing ADD COLUMN NetAmount REAL NOT NULL DEFAULT 0");
        if (!billingColumnsV06.Contains("RefundedAmount")) await c.ExecuteAsync("ALTER TABLE Billing ADD COLUMN RefundedAmount REAL NOT NULL DEFAULT 0");
        if (!billingColumnsV06.Contains("RefundDue")) await c.ExecuteAsync("ALTER TABLE Billing ADD COLUMN RefundDue REAL NOT NULL DEFAULT 0");
        await c.ExecuteAsync("UPDATE Billing SET ReturnAmount=COALESCE(ReturnAmount,0), NetAmount=CASE WHEN NetAmount IS NULL OR NetAmount=0 THEN Amount-COALESCE(ReturnAmount,0) ELSE NetAmount END, RefundedAmount=COALESCE(RefundedAmount,0), RefundDue=COALESCE(RefundDue,0)");

        // v0.5 migration: safely upgrade an existing v0.4 SQLite database.
        var billingColumns = (await c.QueryAsync<string>("SELECT name FROM pragma_table_info('Billing')")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!billingColumns.Contains("PaidAmount")) await c.ExecuteAsync("ALTER TABLE Billing ADD COLUMN PaidAmount REAL NOT NULL DEFAULT 0");
        if (!billingColumns.Contains("BalanceAmount")) await c.ExecuteAsync("ALTER TABLE Billing ADD COLUMN BalanceAmount REAL NOT NULL DEFAULT 0");
        if (!billingColumns.Contains("UpdatedAt")) await c.ExecuteAsync("ALTER TABLE Billing ADD COLUMN UpdatedAt TEXT");
        await c.ExecuteAsync("UPDATE Billing SET PaidAmount=COALESCE(PaidAmount,0), BalanceAmount=CASE WHEN BalanceAmount IS NULL OR (BalanceAmount=0 AND Status='UNPAID') THEN Amount-COALESCE(PaidAmount,0) ELSE BalanceAmount END");
    }
}
