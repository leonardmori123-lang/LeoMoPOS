using System;
using System.Collections.Generic;
using EnterprisePOS.Data;
using EnterprisePOS.Domain;

namespace EnterprisePOS.Services
{
    public sealed class AuthService
    {
        private readonly UserRepository users = new UserRepository();

        public User Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;
            return users.Login(username.Trim(), password);
        }
    }

    public sealed class UserService
    {
        private readonly UserRepository users = new UserRepository();

        public List<User> List(User actor)
        {
            RequireAdmin(actor);
            return users.List();
        }

        public int Create(User actor, User user, string password)
        {
            RequireAdmin(actor);
            ValidateProfile(user);

            user.Username = (user.Username ?? string.Empty).Trim();
            if (user.Username.Length < 3 || user.Username.Length > 50)
                throw new ArgumentException("Username must be 3 to 50 characters.");
            foreach (char c in user.Username)
            {
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
                    throw new ArgumentException("Username can only use letters, numbers, dot, dash and underscore.");
            }

            CheckPassword(password);

            return users.Create(user, password);
        }

        public void ResetPassword(User actor, int userId, string newPassword)
        {
            RequireAdmin(actor);
            CheckPassword(newPassword);
            users.ResetPassword(userId, newPassword);
        }

        public void Update(User actor, User user)
        {
            RequireAdmin(actor);
            ValidateProfile(user);

            if (user.Id == actor.Id)
            {
                if (!user.IsActive) throw new InvalidOperationException("You cannot deactivate your own account.");
                if (user.Role != actor.Role) throw new InvalidOperationException("You cannot change your own role.");
            }

            users.Update(user);
        }

        private static void RequireAdmin(User actor)
        {
            if (actor == null || actor.Role != UserRole.Admin)
                throw new UnauthorizedAccessException("Only an admin can manage users.");
        }

        private static void CheckPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                throw new ArgumentException("The password must be at least 8 characters.");
            if (password.Length > 100)
                throw new ArgumentException("The password can have at most 100 characters.");
        }

        private static void ValidateProfile(User user)
        {
            if (user == null) throw new ArgumentException("There is no user to save.");

            user.FullName = (user.FullName ?? string.Empty).Trim();
            if (user.FullName.Length == 0) throw new ArgumentException("Full name is required.");
            if (user.FullName.Length > 100) throw new ArgumentException("Full name can have at most 100 characters.");
            if (!Enum.IsDefined(typeof(UserRole), user.Role)) throw new ArgumentException("Choose a role.");
        }
    }

    public sealed class InventoryService
    {
        private readonly ProductRepository products = new ProductRepository();
        private readonly CategoryRepository categories = new CategoryRepository();

        public List<Product> Search(string text, bool includeInactive = false)
        {
            return products.Search(text, includeInactive);
        }

        public Product FindByCode(string code)
        {
            code = (code ?? string.Empty).Trim();
            if (code.Length == 0) return null;

            foreach (var p in products.Search(code))
            {
                if (string.Equals(p.Sku, code, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Barcode, code, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            return null;
        }

        public Product GetProduct(int productId)
        {
            return products.GetById(productId);
        }

        public List<Category> Categories()
        {
            return categories.List();
        }

        public int Save(Product product, User actor, bool changeImage)
        {
            RequireManager(actor);
            Validate(product);

            if (product.Id == 0) return products.Insert(product, actor.Id);

            products.Update(product, changeImage);
            return product.Id;
        }

        public void Deactivate(int productId, User actor)
        {
            RequireManager(actor);
            products.Deactivate(productId);
        }

        public int AdjustStock(int productId, int change, string reason, User actor)
        {
            RequireManager(actor);

            if (change == 0) throw new ArgumentException("The change cannot be 0.");

            reason = (reason ?? string.Empty).Trim();
            if (reason.Length == 0) throw new ArgumentException("Enter a reason for the change.");
            if (reason.Length > 200) throw new ArgumentException("The reason can have at most 200 characters.");

            return products.AdjustStock(productId, change, reason, actor.Id);
        }

        private static void RequireManager(User actor)
        {
            if (actor == null || (actor.Role != UserRole.Admin && actor.Role != UserRole.Manager))
                throw new UnauthorizedAccessException("Only an admin or manager can change products.");
        }

        private static void Validate(Product p)
        {
            if (p == null) throw new ArgumentException("There is no product to save.");

            p.Sku = (p.Sku ?? string.Empty).Trim();
            p.Name = (p.Name ?? string.Empty).Trim();
            p.Barcode = string.IsNullOrWhiteSpace(p.Barcode) ? null : p.Barcode.Trim();

            if (p.Sku.Length == 0) throw new ArgumentException("SKU is required.");
            if (p.Sku.Length > 30) throw new ArgumentException("SKU can have at most 30 characters.");
            if (p.Name.Length == 0) throw new ArgumentException("Product name is required.");
            if (p.Name.Length > 150) throw new ArgumentException("Product name can have at most 150 characters.");
            if (p.Barcode != null && p.Barcode.Length > 50) throw new ArgumentException("Barcode can have at most 50 characters.");
            if (p.CategoryId <= 0) throw new ArgumentException("Choose a category.");
            if (p.Price < 0) throw new ArgumentException("Selling price cannot be negative.");
            if (p.Cost < 0) throw new ArgumentException("Cost price cannot be negative.");
            if (p.ReorderLevel < 0) throw new ArgumentException("Reorder level cannot be negative.");
            if (p.Id == 0 && p.StockQty < 0) throw new ArgumentException("Stock cannot be negative.");
        }
    }

    public interface IReceiptPrinter
    {
        void Print(ReceiptData receipt);
        void Preview(ReceiptData receipt);
    }

    public sealed class OrderTotals
    {
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
    }

    public sealed class SalesService
    {
        public const decimal TaxRatePercent = 12m;

        private readonly OrderRepository orders = new OrderRepository();

        public OrderTotals Calculate(IEnumerable<CartLine> lines, decimal discount)
        {
            decimal subtotal = 0m;
            if (lines != null)
            {
                foreach (var line in lines) subtotal += line.LineTotal;
            }

            if (discount < 0m || discount > subtotal) throw new ArgumentException("Invalid discount amount.");

            decimal tax = Math.Round((subtotal - discount) * TaxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);

            return new OrderTotals
            {
                Subtotal = subtotal,
                Discount = discount,
                Tax = tax,
                Total = subtotal - discount + tax
            };
        }

        public int Checkout(User cashier, IList<CartLine> lines, int? customerId, string paymentMethod, decimal discount, decimal amountPaid)
        {
            if (cashier == null) throw new UnauthorizedAccessException("Sign in before taking an order.");
            if (lines == null || lines.Count == 0) throw new InvalidOperationException("The cart is empty.");
            if (Array.IndexOf(PaymentMethods.All, paymentMethod) < 0) throw new ArgumentException("Choose a payment method.");

            var totals = Calculate(lines, discount);
            if (amountPaid < totals.Total) throw new InvalidOperationException("The amount received is less than the total.");

            return orders.Create(new Order
            {
                UserId = cashier.Id,
                CustomerId = customerId,
                PaymentMethod = paymentMethod,
                DiscountAmount = discount,
                AmountPaid = amountPaid,
                Lines = new List<CartLine>(lines)
            }, TaxRatePercent);
        }

        public Order GetOrder(int orderId)
        {
            return orders.GetById(orderId);
        }

        private const string BusinessName = "Enterprise POS";
        private const string BusinessAddress = "123 Main Street, City";
        private const string BusinessPhone = "+1 555 0100";

        public ReceiptData BuildReceipt(Order order, string cashierName)
        {
            if (order == null) throw new ArgumentNullException("order");

            var receipt = new ReceiptData
            {
                BusinessName = BusinessName,
                Address = BusinessAddress,
                Phone = BusinessPhone,
                OrderNumber = order.OrderNumber,
                Date = order.OrderDate,
                CashierName = cashierName,
                Subtotal = order.Subtotal,
                Tax = order.TaxAmount,
                Discount = order.DiscountAmount,
                Total = order.Total,
                PaymentMethod = order.PaymentMethod,
                AmountTendered = order.AmountPaid,
                Change = order.ChangeDue
            };

            foreach (var line in order.Lines)
            {
                receipt.Lines.Add(new ReceiptLine
                {
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    Amount = line.LineTotal
                });
            }

            return receipt;
        }
    }

    public sealed class ReportingService
    {
        private readonly ReportRepository reports = new ReportRepository();

        public List<DailySales> GetDailySales(User actor, DateTime start, DateTime end)
        {
            RequireManager(actor);
            CheckRange(start, end);
            return reports.GetDailySales(start.Date, end.Date);
        }

        public List<TopProduct> GetTopProducts(User actor, DateTime start, DateTime end, int top = 10)
        {
            RequireManager(actor);
            CheckRange(start, end);
            if (top < 1 || top > 100) throw new ArgumentException("Top must be between 1 and 100.");
            return reports.GetTopProducts(start.Date, end.Date, top);
        }

        public List<CategorySales> GetSalesByCategory(User actor, DateTime start, DateTime end)
        {
            RequireManager(actor);
            CheckRange(start, end);
            return reports.GetSalesByCategory(start.Date, end.Date);
        }

        public List<LowStockItem> GetLowStock(User actor)
        {
            RequireManager(actor);
            return reports.GetLowStock();
        }

        public DashboardStats GetDashboard(User actor)
        {
            RequireManager(actor);
            return reports.GetDashboard();
        }

        private static void RequireManager(User actor)
        {
            if (actor == null || (actor.Role != UserRole.Admin && actor.Role != UserRole.Manager))
                throw new UnauthorizedAccessException("Only an admin or manager can view sales reports.");
        }

        private static void CheckRange(DateTime start, DateTime end)
        {
            if (start.Date > end.Date) throw new ArgumentException("The start date cannot be after the end date.");
        }
    }
}