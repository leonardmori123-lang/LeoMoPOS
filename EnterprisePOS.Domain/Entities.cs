using System;
using System.Collections.Generic;

namespace EnterprisePOS.Domain
{
    // Matches Roles.RoleId in PosDB: 1 = Admin, 2 = Manager, 3 = Cashier
    public enum UserRole { Admin = 1, Manager = 2, Cashier = 3 }

    // Matches the CHECK constraint on Orders.PaymentMethod
    public static class PaymentMethods
    {
        public const string Cash = "Cash";
        public const string Card = "Card";
        public const string Mobile = "Mobile";
        public static readonly string[] All = { Cash, Card, Mobile };
    }

    // Table: Users (PasswordHash and Salt stay in the Data layer, not here)
    public sealed class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    // Table: Categories
    public sealed class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    // Table: Products
    public sealed class Product
    {
        public int Id { get; set; }
        public string Sku { get; set; }
        public string Barcode { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }   // filled when a query joins Categories
        public decimal Price { get; set; }         // selling price
        public decimal Cost { get; set; }
        public int StockQty { get; set; }
        public int ReorderLevel { get; set; }
        public byte[] Image { get; set; }          // VARBINARY(MAX), null when no picture
        public bool IsActive { get; set; }

        public bool IsLowStock { get { return StockQty <= ReorderLevel; } }
    }

    // Table: Customers
    public sealed class Customer
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
    }

    // One line in the shopping cart. Also used for rows in OrderDetails.
    public sealed class CartLine
    {
        public int ProductId { get; set; }
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get { return Quantity * UnitPrice; } }
    }

    // Table: Orders (+ its OrderDetails lines)
    public sealed class Order
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int? CustomerId { get; set; }
        public int UserId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ChangeDue { get; set; }
        public string Status { get; set; }         // "Completed" or "Voided"
        public List<CartLine> Lines { get; set; } = new List<CartLine>();
    }

    // ---- Receipt model: what gets printed, so the printer does not depend on the database entity ----

    public sealed class ReceiptLine
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class ReceiptData
    {
        public string BusinessName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string OrderNumber { get; set; }
        public DateTime Date { get; set; }
        public string CashierName { get; set; }
        public List<ReceiptLine> Lines { get; } = new List<ReceiptLine>();
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; }
        public decimal AmountTendered { get; set; }
        public decimal Change { get; set; }
    }

    // ---- Report rows, one class per report stored procedure ----

    // usp_Report_DailySales
    public sealed class DailySales
    {
        public DateTime SaleDate { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    // usp_Report_TopProducts
    public sealed class TopProduct
    {
        public string ProductName { get; set; }
        public int QtySold { get; set; }
        public decimal Revenue { get; set; }
    }

    // usp_Report_SalesByCategory
    public sealed class CategorySales
    {
        public string CategoryName { get; set; }
        public decimal Revenue { get; set; }
    }

    // usp_Report_LowStock
    public sealed class LowStockItem
    {
        public int ProductId { get; set; }
        public string Sku { get; set; }
        public string Name { get; set; }
        public int StockQty { get; set; }
        public int ReorderLevel { get; set; }
    }

    // usp_Report_Dashboard
    public sealed class DashboardStats
    {
        public decimal TodaySales { get; set; }
        public int TodayOrders { get; set; }
        public int LowStockCount { get; set; }
        public int ActiveProducts { get; set; }
    }
}