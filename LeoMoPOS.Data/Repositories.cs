using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using LeoMoPOS.Domain;

namespace LeoMoPOS.Data
{
    public sealed class UserRepository
    {
        public User Login(string username, string password)
        {
            if (username == null) username = string.Empty;

            User user = null;
            string salt = string.Empty;
            string hash = string.Empty;

            string sql = "SELECT UserId, Username, PasswordHash, Salt, FullName, RoleId, IsActive " +
                         "FROM Users WHERE Username = @username";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@username", username);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    salt = reader.GetString(reader.GetOrdinal("Salt"));
                    hash = reader.GetString(reader.GetOrdinal("PasswordHash"));
                    user = ReadUser(reader);
                }
            }

            if (!user.IsActive) return null;
            if (!PasswordHasher.Verify(password, salt, hash)) return null;

            TouchLogin(user.Id);
            return user;
        }

        public List<User> List()
        {
            List<User> list = new List<User>();

            string sql = "SELECT UserId, Username, FullName, RoleId, IsActive, LastLoginAt " +
                         "FROM Users ORDER BY Username";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                int lastLoginColumn = reader.GetOrdinal("LastLoginAt");

                while (reader.Read())
                {
                    User user = ReadUser(reader);
                    if (!reader.IsDBNull(lastLoginColumn))
                    {
                        user.LastLoginAt = reader.GetDateTime(lastLoginColumn);
                    }
                    list.Add(user);
                }
            }

            return list;
        }

        public int Create(User user, string password)
        {
            string salt = PasswordHasher.NewSalt();
            string hash = PasswordHasher.Hash(salt, password);

            string sql = "INSERT INTO Users (Username, PasswordHash, Salt, FullName, RoleId) " +
                         "VALUES (@username, @hash, @salt, @fullName, @roleId)";

            try
            {
                using (SQLiteConnection connection = SqliteDb.Open())
                using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", user.Username);
                    command.Parameters.AddWithValue("@hash", hash);
                    command.Parameters.AddWithValue("@salt", salt);
                    command.Parameters.AddWithValue("@fullName", user.FullName);
                    command.Parameters.AddWithValue("@roleId", (int)user.Role);
                    command.ExecuteNonQuery();

                    return (int)connection.LastInsertRowId;
                }
            }
            catch (SQLiteException ex)
            {
                if (IsDuplicate(ex)) throw new InvalidOperationException("That username is already taken.");
                throw;
            }
        }

        public void Update(User user)
        {
            int newActive = 0;
            if (user.IsActive) newActive = 1;

            try
            {
                using (SQLiteConnection connection = SqliteDb.Open())
                {
                    SqliteDb.BeginWrite(connection);
                    try
                    {
                        int oldRole = 0;
                        bool oldActive = false;

                        string findSql = "SELECT RoleId, IsActive FROM Users WHERE UserId = @userId";
                        using (SQLiteCommand find = new SQLiteCommand(findSql, connection))
                        {
                            find.Parameters.AddWithValue("@userId", user.Id);
                            using (SQLiteDataReader reader = find.ExecuteReader())
                            {
                                if (!reader.Read()) throw new InvalidOperationException("User not found.");
                                oldRole = reader.GetInt32(0);
                                oldActive = reader.GetBoolean(1);
                            }
                        }

                        int admin = (int)UserRole.Admin;
                        bool wasActiveAdmin = (oldRole == admin) && oldActive;
                        bool stopsBeingActiveAdmin = ((int)user.Role != admin) || (newActive == 0);

                        if (wasActiveAdmin && stopsBeingActiveAdmin)
                        {
                            string countSql = "SELECT COUNT(*) FROM Users WHERE UserId <> @userId AND RoleId = @admin AND IsActive = 1";
                            using (SQLiteCommand count = new SQLiteCommand(countSql, connection))
                            {
                                count.Parameters.AddWithValue("@userId", user.Id);
                                count.Parameters.AddWithValue("@admin", admin);
                                long otherAdmins = Convert.ToInt64(count.ExecuteScalar());
                                if (otherAdmins == 0) throw new InvalidOperationException("At least one active admin must remain.");
                            }
                        }

                        string updateSql = "UPDATE Users SET FullName = @fullName, RoleId = @roleId, IsActive = @isActive " +
                                           "WHERE UserId = @userId";
                        using (SQLiteCommand update = new SQLiteCommand(updateSql, connection))
                        {
                            update.Parameters.AddWithValue("@fullName", user.FullName);
                            update.Parameters.AddWithValue("@roleId", (int)user.Role);
                            update.Parameters.AddWithValue("@isActive", newActive);
                            update.Parameters.AddWithValue("@userId", user.Id);
                            update.ExecuteNonQuery();
                        }

                        SqliteDb.Commit(connection);
                    }
                    catch
                    {
                        SqliteDb.Rollback(connection);
                        throw;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                if (IsDuplicate(ex)) throw new InvalidOperationException("That username is already taken.");
                throw;
            }
        }

        public void ResetPassword(int userId, string newPassword)
        {
            string salt = PasswordHasher.NewSalt();
            string hash = PasswordHasher.Hash(salt, newPassword);

            string sql = "UPDATE Users SET PasswordHash = @hash, Salt = @salt WHERE UserId = @userId";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@hash", hash);
                command.Parameters.AddWithValue("@salt", salt);
                command.Parameters.AddWithValue("@userId", userId);

                int rows = command.ExecuteNonQuery();
                if (rows == 0) throw new InvalidOperationException("User not found.");
            }
        }

        private static User ReadUser(SQLiteDataReader reader)
        {
            User user = new User();
            user.Id = reader.GetInt32(reader.GetOrdinal("UserId"));
            user.Username = reader.GetString(reader.GetOrdinal("Username"));
            user.FullName = reader.GetString(reader.GetOrdinal("FullName"));
            user.Role = (UserRole)reader.GetInt32(reader.GetOrdinal("RoleId"));
            user.IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
            return user;
        }

        private static bool IsDuplicate(SQLiteException ex)
        {
            return ex.Message.Contains("UNIQUE constraint failed");
        }

        private static void TouchLogin(int userId)
        {
            string sql = "UPDATE Users SET LastLoginAt = datetime('now', 'localtime') WHERE UserId = @userId";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                command.ExecuteNonQuery();
            }
        }
    }

    public sealed class CategoryRepository
    {
        public List<Category> List()
        {
            List<Category> list = new List<Category>();

            string sql = "SELECT CategoryId, Name, Description FROM Categories ORDER BY Name";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    Category category = new Category();
                    category.Id = ReaderHelper.GetInt(reader, "CategoryId");
                    category.Name = ReaderHelper.GetString(reader, "Name");
                    category.Description = ReaderHelper.GetString(reader, "Description");
                    list.Add(category);
                }
            }

            return list;
        }
    }

    public sealed class ProductRepository
    {
        public List<Product> Search(string search, bool includeInactive = false)
        {
            List<Product> list = new List<Product>();

            string text = string.Empty;
            if (!string.IsNullOrWhiteSpace(search)) text = search.Trim();

            int showInactive = 0;
            if (includeInactive) showInactive = 1;

            string sql =
                "SELECT p.ProductId, p.Sku, p.Barcode, p.Name, p.CategoryId, c.Name AS CategoryName, " +
                "       p.Price, p.Cost, p.StockQty, p.ReorderLevel, p.IsActive " +
                "FROM Products p " +
                "JOIN Categories c ON c.CategoryId = p.CategoryId " +
                "WHERE (@showInactive = 1 OR p.IsActive = 1) " +
                "  AND (@search = '' " +
                "       OR p.Sku LIKE '%' || @search || '%' " +
                "       OR p.Barcode LIKE '%' || @search || '%' " +
                "       OR p.Name LIKE '%' || @search || '%') " +
                "ORDER BY p.Name COLLATE NOCASE";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@showInactive", showInactive);
                command.Parameters.AddWithValue("@search", text);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(ReadProduct(reader, false));
                    }
                }
            }

            return list;
        }

        public Product GetById(int productId)
        {
            string sql =
                "SELECT p.ProductId, p.Sku, p.Barcode, p.Name, p.CategoryId, c.Name AS CategoryName, " +
                "       p.Price, p.Cost, p.StockQty, p.ReorderLevel, p.Image, p.IsActive " +
                "FROM Products p " +
                "JOIN Categories c ON c.CategoryId = p.CategoryId " +
                "WHERE p.ProductId = @productId";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@productId", productId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadProduct(reader, true);
                }
            }
        }

        public int Insert(Product p, int userId)
        {
            string sql =
                "INSERT INTO Products (Sku, Barcode, Name, CategoryId, Price, Cost, StockQty, ReorderLevel, Image) " +
                "VALUES (@sku, @barcode, @name, @categoryId, @price, @cost, @stockQty, @reorderLevel, @image)";

            try
            {
                using (SQLiteConnection connection = SqliteDb.Open())
                {
                    SqliteDb.BeginWrite(connection);
                    try
                    {
                        using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@sku", p.Sku);
                            command.Parameters.AddWithValue("@barcode", TextOrNull(p.Barcode));
                            command.Parameters.AddWithValue("@name", p.Name);
                            command.Parameters.AddWithValue("@categoryId", p.CategoryId);
                            command.Parameters.AddWithValue("@price", p.Price);
                            command.Parameters.AddWithValue("@cost", p.Cost);
                            command.Parameters.AddWithValue("@stockQty", p.StockQty);
                            command.Parameters.AddWithValue("@reorderLevel", p.ReorderLevel);
                            command.Parameters.AddWithValue("@image", BytesOrNull(p.Image));
                            command.ExecuteNonQuery();
                        }

                        int newId = (int)connection.LastInsertRowId;

                        if (p.StockQty > 0)
                        {
                            AddLog(connection, newId, p.StockQty, "Initial stock", userId);
                        }

                        SqliteDb.Commit(connection);
                        return newId;
                    }
                    catch
                    {
                        SqliteDb.Rollback(connection);
                        throw;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                throw Friendly(ex);
            }
        }

        public void Update(Product p, bool changeImage)
        {
            int active = 0;
            if (p.IsActive) active = 1;

            int replaceImage = 0;
            if (changeImage) replaceImage = 1;

            string sql =
                "UPDATE Products " +
                "SET Sku = @sku, Barcode = @barcode, Name = @name, CategoryId = @categoryId, " +
                "    Price = @price, Cost = @cost, ReorderLevel = @reorderLevel, IsActive = @isActive, " +
                "    Image = CASE WHEN @replaceImage = 1 THEN @image ELSE Image END, " +
                "    UpdatedAt = datetime('now', 'localtime') " +
                "WHERE ProductId = @productId";

            try
            {
                using (SQLiteConnection connection = SqliteDb.Open())
                using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@sku", p.Sku);
                    command.Parameters.AddWithValue("@barcode", TextOrNull(p.Barcode));
                    command.Parameters.AddWithValue("@name", p.Name);
                    command.Parameters.AddWithValue("@categoryId", p.CategoryId);
                    command.Parameters.AddWithValue("@price", p.Price);
                    command.Parameters.AddWithValue("@cost", p.Cost);
                    command.Parameters.AddWithValue("@reorderLevel", p.ReorderLevel);
                    command.Parameters.AddWithValue("@isActive", active);
                    command.Parameters.AddWithValue("@replaceImage", replaceImage);
                    command.Parameters.AddWithValue("@image", BytesOrNull(p.Image));
                    command.Parameters.AddWithValue("@productId", p.Id);

                    int rows = command.ExecuteNonQuery();
                    if (rows == 0) throw new InvalidOperationException("Product not found.");
                }
            }
            catch (SQLiteException ex)
            {
                throw Friendly(ex);
            }
        }

        public void Deactivate(int productId)
        {
            string sql = "UPDATE Products SET IsActive = 0, UpdatedAt = datetime('now', 'localtime') WHERE ProductId = @productId";

            try
            {
                using (SQLiteConnection connection = SqliteDb.Open())
                using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@productId", productId);

                    int rows = command.ExecuteNonQuery();
                    if (rows == 0) throw new InvalidOperationException("Product not found.");
                }
            }
            catch (SQLiteException ex)
            {
                throw Friendly(ex);
            }
        }

        public int AdjustStock(int productId, int change, string reason, int userId)
        {
            string findSql = "SELECT StockQty FROM Products WHERE ProductId = @productId";
            string updateSql = "UPDATE Products SET StockQty = @newStock, UpdatedAt = datetime('now', 'localtime') WHERE ProductId = @productId";

            try
            {
                using (SQLiteConnection connection = SqliteDb.Open())
                {
                    SqliteDb.BeginWrite(connection);
                    try
                    {
                        int current = 0;
                        using (SQLiteCommand find = new SQLiteCommand(findSql, connection))
                        {
                            find.Parameters.AddWithValue("@productId", productId);
                            object result = find.ExecuteScalar();
                            if (result == null) throw new InvalidOperationException("Product not found.");
                            current = Convert.ToInt32(result);
                        }

                        int newStock = current + change;
                        if (newStock < 0) throw new InvalidOperationException("Adjustment would make stock negative.");

                        using (SQLiteCommand update = new SQLiteCommand(updateSql, connection))
                        {
                            update.Parameters.AddWithValue("@newStock", newStock);
                            update.Parameters.AddWithValue("@productId", productId);
                            update.ExecuteNonQuery();
                        }

                        AddLog(connection, productId, change, reason, userId);

                        SqliteDb.Commit(connection);
                        return newStock;
                    }
                    catch
                    {
                        SqliteDb.Rollback(connection);
                        throw;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                throw Friendly(ex);
            }
        }

        internal static void AddLog(SQLiteConnection connection, int productId, int change, string reason, int userId)
        {
            string sql = "INSERT INTO InventoryLog (ProductId, ChangeQty, Reason, UserId) VALUES (@productId, @change, @reason, @userId)";

            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@productId", productId);
                command.Parameters.AddWithValue("@change", change);
                command.Parameters.AddWithValue("@reason", reason);
                command.Parameters.AddWithValue("@userId", userId);
                command.ExecuteNonQuery();
            }
        }

        private static Product ReadProduct(SQLiteDataReader reader, bool withImage)
        {
            Product product = new Product();
            product.Id = ReaderHelper.GetInt(reader, "ProductId");
            product.Sku = ReaderHelper.GetString(reader, "Sku");
            product.Barcode = ReaderHelper.GetString(reader, "Barcode");
            product.Name = ReaderHelper.GetString(reader, "Name");
            product.CategoryId = ReaderHelper.GetInt(reader, "CategoryId");
            product.CategoryName = ReaderHelper.GetString(reader, "CategoryName");
            product.Price = ReaderHelper.GetDecimal(reader, "Price");
            product.Cost = ReaderHelper.GetDecimal(reader, "Cost");
            product.StockQty = ReaderHelper.GetInt(reader, "StockQty");
            product.ReorderLevel = ReaderHelper.GetInt(reader, "ReorderLevel");
            product.IsActive = ReaderHelper.GetBool(reader, "IsActive");

            if (withImage)
            {
                product.Image = ReaderHelper.GetBytes(reader, "Image");
            }

            return product;
        }

        private static object TextOrNull(string text)
        {
            if (text == null) return DBNull.Value;
            return text;
        }

        private static object BytesOrNull(byte[] bytes)
        {
            if (bytes == null) return DBNull.Value;
            return bytes;
        }

        private static Exception Friendly(SQLiteException ex)
        {
            string message = ex.Message;

            if (message.Contains("UNIQUE constraint failed"))
            {
                if (message.IndexOf("Sku", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new InvalidOperationException("That SKU is already used by another product.");
                if (message.IndexOf("Barcode", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new InvalidOperationException("That barcode is already used by another product.");
                return new InvalidOperationException("A product with the same value already exists.");
            }

            return ex;
        }
    }

    public sealed class OrderRepository
    {
        private sealed class OrderItem
        {
            public int ProductId { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public int Stock { get; set; }
            public int Quantity { get; set; }
        }

        public int Create(Order order, decimal taxRatePercent)
        {
            SortedDictionary<int, int> wanted = new SortedDictionary<int, int>();
            foreach (CartLine line in order.Lines)
            {
                if (wanted.ContainsKey(line.ProductId))
                {
                    wanted[line.ProductId] = wanted[line.ProductId] + line.Quantity;
                }
                else
                {
                    wanted.Add(line.ProductId, line.Quantity);
                }
            }

            if (wanted.Count == 0) throw new InvalidOperationException("The order has no items.");

            foreach (KeyValuePair<int, int> pair in wanted)
            {
                if (pair.Value <= 0) throw new InvalidOperationException("Invalid item quantity.");
            }

            using (SQLiteConnection connection = SqliteDb.Open())
            {
                SqliteDb.BeginWrite(connection);
                try
                {
                    List<OrderItem> items = LoadItems(connection, wanted);

                    foreach (OrderItem item in items)
                    {
                        if (item.Stock < item.Quantity)
                        {
                            throw new InvalidOperationException(
                                "Insufficient stock for " + item.Name + " (available: " + item.Stock + ").");
                        }
                    }

                    decimal subtotal = 0m;
                    foreach (OrderItem item in items)
                    {
                        subtotal = subtotal + (item.Price * item.Quantity);
                    }

                    decimal discount = order.DiscountAmount;
                    if (discount < 0m || discount > subtotal) throw new InvalidOperationException("Invalid discount amount.");

                    decimal tax = Math.Round((subtotal - discount) * taxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
                    decimal total = subtotal - discount + tax;

                    if (order.AmountPaid < total) throw new InvalidOperationException("Amount paid is less than the total.");

                    DateTime now = DateTime.Now;
                    int orderId = InsertOrder(connection, order, now, subtotal, tax, discount, total);

                    string digits = "000000" + orderId.ToString(CultureInfo.InvariantCulture);
                    string orderNumber = "ORD-" + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + digits.Substring(digits.Length - 6);

                    SetOrderNumber(connection, orderId, orderNumber);
                    InsertDetails(connection, orderId, items);
                    TakeStock(connection, items, orderNumber, order.UserId);

                    SqliteDb.Commit(connection);
                    return orderId;
                }
                catch
                {
                    SqliteDb.Rollback(connection);
                    throw;
                }
            }
        }

        public Order GetById(int orderId)
        {
            Order order = null;

            string headerSql =
                "SELECT OrderId, OrderNumber, CustomerId, UserId, OrderDate, Subtotal, TaxAmount, DiscountAmount, " +
                "       Total, PaymentMethod, AmountPaid, ChangeDue, Status " +
                "FROM Orders WHERE OrderId = @orderId";

            string linesSql =
                "SELECT d.ProductId, p.Sku, p.Name AS ProductName, d.Quantity, d.UnitPrice, d.LineTotal " +
                "FROM OrderDetails d " +
                "JOIN Products p ON p.ProductId = d.ProductId " +
                "WHERE d.OrderId = @orderId " +
                "ORDER BY d.OrderDetailId";

            using (SQLiteConnection connection = SqliteDb.Open())
            {
                using (SQLiteCommand command = new SQLiteCommand(headerSql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) return null;

                        order = new Order();
                        order.Id = ReaderHelper.GetInt(reader, "OrderId");
                        order.OrderNumber = ReaderHelper.GetString(reader, "OrderNumber");
                        order.CustomerId = ReaderHelper.GetNullableInt(reader, "CustomerId");
                        order.UserId = ReaderHelper.GetInt(reader, "UserId");
                        order.OrderDate = ReaderHelper.GetDateTime(reader, "OrderDate");
                        order.Subtotal = ReaderHelper.GetDecimal(reader, "Subtotal");
                        order.TaxAmount = ReaderHelper.GetDecimal(reader, "TaxAmount");
                        order.DiscountAmount = ReaderHelper.GetDecimal(reader, "DiscountAmount");
                        order.Total = ReaderHelper.GetDecimal(reader, "Total");
                        order.PaymentMethod = ReaderHelper.GetString(reader, "PaymentMethod");
                        order.AmountPaid = ReaderHelper.GetDecimal(reader, "AmountPaid");
                        order.ChangeDue = ReaderHelper.GetDecimal(reader, "ChangeDue");
                        order.Status = ReaderHelper.GetString(reader, "Status");
                    }
                }

                using (SQLiteCommand command = new SQLiteCommand(linesSql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CartLine line = new CartLine();
                            line.ProductId = ReaderHelper.GetInt(reader, "ProductId");
                            line.Sku = ReaderHelper.GetString(reader, "Sku");
                            line.ProductName = ReaderHelper.GetString(reader, "ProductName");
                            line.Quantity = ReaderHelper.GetInt(reader, "Quantity");
                            line.UnitPrice = ReaderHelper.GetDecimal(reader, "UnitPrice");
                            order.Lines.Add(line);
                        }
                    }
                }
            }

            return order;
        }

        private static List<OrderItem> LoadItems(SQLiteConnection connection, SortedDictionary<int, int> wanted)
        {
            List<OrderItem> items = new List<OrderItem>();

            string sql = "SELECT Name, Price, StockQty, IsActive FROM Products WHERE ProductId = @productId";

            foreach (KeyValuePair<int, int> pair in wanted)
            {
                using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@productId", pair.Key);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) throw new InvalidOperationException("One or more products are unavailable.");
                        if (!ReaderHelper.GetBool(reader, "IsActive")) throw new InvalidOperationException("One or more products are unavailable.");

                        OrderItem item = new OrderItem();
                        item.ProductId = pair.Key;
                        item.Name = ReaderHelper.GetString(reader, "Name");
                        item.Price = ReaderHelper.GetDecimal(reader, "Price");
                        item.Stock = ReaderHelper.GetInt(reader, "StockQty");
                        item.Quantity = pair.Value;
                        items.Add(item);
                    }
                }
            }

            return items;
        }

        private static int InsertOrder(SQLiteConnection connection, Order order, DateTime now, decimal subtotal, decimal tax, decimal discount, decimal total)
        {
            string sql =
                "INSERT INTO Orders (OrderNumber, CustomerId, UserId, OrderDate, Subtotal, TaxAmount, DiscountAmount, Total, " +
                "                    PaymentMethod, AmountPaid, ChangeDue, Status) " +
                "VALUES (@orderNumber, @customerId, @userId, @orderDate, @subtotal, @tax, @discount, @total, " +
                "        @paymentMethod, @amountPaid, @changeDue, 'Completed')";

            string temporaryNumber = Guid.NewGuid().ToString("N").Substring(0, 30);

            object customerId = DBNull.Value;
            if (order.CustomerId.HasValue) customerId = order.CustomerId.Value;

            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@orderNumber", temporaryNumber);
                command.Parameters.AddWithValue("@customerId", customerId);
                command.Parameters.AddWithValue("@userId", order.UserId);
                command.Parameters.AddWithValue("@orderDate", now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("@subtotal", subtotal);
                command.Parameters.AddWithValue("@tax", tax);
                command.Parameters.AddWithValue("@discount", discount);
                command.Parameters.AddWithValue("@total", total);
                command.Parameters.AddWithValue("@paymentMethod", order.PaymentMethod);
                command.Parameters.AddWithValue("@amountPaid", order.AmountPaid);
                command.Parameters.AddWithValue("@changeDue", order.AmountPaid - total);
                command.ExecuteNonQuery();
            }

            return (int)connection.LastInsertRowId;
        }

        private static void SetOrderNumber(SQLiteConnection connection, int orderId, string orderNumber)
        {
            string sql = "UPDATE Orders SET OrderNumber = @orderNumber WHERE OrderId = @orderId";

            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@orderNumber", orderNumber);
                command.Parameters.AddWithValue("@orderId", orderId);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertDetails(SQLiteConnection connection, int orderId, List<OrderItem> items)
        {
            string sql =
                "INSERT INTO OrderDetails (OrderId, ProductId, Quantity, UnitPrice, LineTotal) " +
                "VALUES (@orderId, @productId, @quantity, @unitPrice, @lineTotal)";

            foreach (OrderItem item in items)
            {
                using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);
                    command.Parameters.AddWithValue("@productId", item.ProductId);
                    command.Parameters.AddWithValue("@quantity", item.Quantity);
                    command.Parameters.AddWithValue("@unitPrice", item.Price);
                    command.Parameters.AddWithValue("@lineTotal", item.Price * item.Quantity);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void TakeStock(SQLiteConnection connection, List<OrderItem> items, string orderNumber, int userId)
        {
            string sql =
                "UPDATE Products SET StockQty = StockQty - @quantity, UpdatedAt = datetime('now', 'localtime') " +
                "WHERE ProductId = @productId";

            foreach (OrderItem item in items)
            {
                using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@quantity", item.Quantity);
                    command.Parameters.AddWithValue("@productId", item.ProductId);
                    command.ExecuteNonQuery();
                }

                ProductRepository.AddLog(connection, item.ProductId, -item.Quantity, "Sale " + orderNumber, userId);
            }
        }
    }

    public sealed class ReportRepository
    {
        public List<DailySales> GetDailySales(DateTime start, DateTime end)
        {
            List<DailySales> list = new List<DailySales>();

            string sql =
                "SELECT date(OrderDate) AS SaleDate, COUNT(*) AS OrderCount, SUM(Total) AS Revenue " +
                "FROM Orders " +
                "WHERE Status = 'Completed' AND OrderDate >= @startText AND OrderDate < @endText " +
                "GROUP BY date(OrderDate) " +
                "ORDER BY SaleDate";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = RangeCommand(connection, sql, start, end))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    DailySales day = new DailySales();
                    day.SaleDate = DateTime.ParseExact(ReaderHelper.GetString(reader, "SaleDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    day.OrderCount = ReaderHelper.GetInt(reader, "OrderCount");
                    day.Revenue = ReaderHelper.GetMoney(reader, "Revenue");
                    list.Add(day);
                }
            }

            return list;
        }

        public List<TopProduct> GetTopProducts(DateTime start, DateTime end, int top)
        {
            List<TopProduct> list = new List<TopProduct>();

            string sql =
                "SELECT p.Name AS ProductName, SUM(d.Quantity) AS QtySold, SUM(d.LineTotal) AS Revenue " +
                "FROM OrderDetails d " +
                "JOIN Orders o ON o.OrderId = d.OrderId " +
                "JOIN Products p ON p.ProductId = d.ProductId " +
                "WHERE o.Status = 'Completed' AND o.OrderDate >= @startText AND o.OrderDate < @endText " +
                "GROUP BY p.Name " +
                "ORDER BY SUM(d.LineTotal) DESC, p.Name " +
                "LIMIT @top";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = RangeCommand(connection, sql, start, end))
            {
                command.Parameters.AddWithValue("@top", top);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        TopProduct product = new TopProduct();
                        product.ProductName = ReaderHelper.GetString(reader, "ProductName");
                        product.QtySold = ReaderHelper.GetInt(reader, "QtySold");
                        product.Revenue = ReaderHelper.GetMoney(reader, "Revenue");
                        list.Add(product);
                    }
                }
            }

            return list;
        }

        public List<CategorySales> GetSalesByCategory(DateTime start, DateTime end)
        {
            List<CategorySales> list = new List<CategorySales>();

            string sql =
                "SELECT c.Name AS CategoryName, SUM(d.LineTotal) AS Revenue " +
                "FROM OrderDetails d " +
                "JOIN Orders o ON o.OrderId = d.OrderId " +
                "JOIN Products p ON p.ProductId = d.ProductId " +
                "JOIN Categories c ON c.CategoryId = p.CategoryId " +
                "WHERE o.Status = 'Completed' AND o.OrderDate >= @startText AND o.OrderDate < @endText " +
                "GROUP BY c.Name " +
                "ORDER BY SUM(d.LineTotal) DESC, c.Name";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = RangeCommand(connection, sql, start, end))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    CategorySales category = new CategorySales();
                    category.CategoryName = ReaderHelper.GetString(reader, "CategoryName");
                    category.Revenue = ReaderHelper.GetMoney(reader, "Revenue");
                    list.Add(category);
                }
            }

            return list;
        }

        public List<LowStockItem> GetLowStock()
        {
            List<LowStockItem> list = new List<LowStockItem>();

            string sql =
                "SELECT ProductId, Sku, Name, StockQty, ReorderLevel " +
                "FROM Products " +
                "WHERE IsActive = 1 AND StockQty <= ReorderLevel " +
                "ORDER BY StockQty, Name COLLATE NOCASE";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    LowStockItem item = new LowStockItem();
                    item.ProductId = ReaderHelper.GetInt(reader, "ProductId");
                    item.Sku = ReaderHelper.GetString(reader, "Sku");
                    item.Name = ReaderHelper.GetString(reader, "Name");
                    item.StockQty = ReaderHelper.GetInt(reader, "StockQty");
                    item.ReorderLevel = ReaderHelper.GetInt(reader, "ReorderLevel");
                    list.Add(item);
                }
            }

            return list;
        }

        public DashboardStats GetDashboard()
        {
            string sql =
                "SELECT " +
                "  (SELECT COALESCE(SUM(Total), 0) FROM Orders WHERE Status = 'Completed' AND OrderDate >= @todayText) AS TodaySales, " +
                "  (SELECT COUNT(*) FROM Orders WHERE Status = 'Completed' AND OrderDate >= @todayText) AS TodayOrders, " +
                "  (SELECT COUNT(*) FROM Products WHERE IsActive = 1 AND StockQty <= ReorderLevel) AS LowStockCount, " +
                "  (SELECT COUNT(*) FROM Products WHERE IsActive = 1) AS ActiveProducts";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@todayText", DateText(DateTime.Today));

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    DashboardStats stats = new DashboardStats();
                    if (!reader.Read()) return stats;

                    stats.TodaySales = ReaderHelper.GetMoney(reader, "TodaySales");
                    stats.TodayOrders = ReaderHelper.GetInt(reader, "TodayOrders");
                    stats.LowStockCount = ReaderHelper.GetInt(reader, "LowStockCount");
                    stats.ActiveProducts = ReaderHelper.GetInt(reader, "ActiveProducts");
                    return stats;
                }
            }
        }

        private static SQLiteCommand RangeCommand(SQLiteConnection connection, string sql, DateTime start, DateTime end)
        {
            SQLiteCommand command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@startText", DateText(start.Date));
            command.Parameters.AddWithValue("@endText", DateText(end.Date.AddDays(1)));
            return command;
        }

        private static string DateText(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }

    internal static class ReaderHelper
    {
        public static int GetInt(SQLiteDataReader reader, string column)
        {
            return Convert.ToInt32(reader[column]);
        }

        public static int? GetNullableInt(SQLiteDataReader reader, string column)
        {
            int position = reader.GetOrdinal(column);
            if (reader.IsDBNull(position)) return null;
            return Convert.ToInt32(reader[position]);
        }

        public static decimal GetDecimal(SQLiteDataReader reader, string column)
        {
            return Convert.ToDecimal(reader[column]);
        }

        public static decimal GetMoney(SQLiteDataReader reader, string column)
        {
            decimal value = Convert.ToDecimal(reader[column]);
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        public static string GetString(SQLiteDataReader reader, string column)
        {
            int position = reader.GetOrdinal(column);
            if (reader.IsDBNull(position)) return null;
            return reader.GetString(position);
        }

        public static bool GetBool(SQLiteDataReader reader, string column)
        {
            return Convert.ToBoolean(reader[column]);
        }

        public static DateTime GetDateTime(SQLiteDataReader reader, string column)
        {
            int position = reader.GetOrdinal(column);
            return reader.GetDateTime(position);
        }

        public static byte[] GetBytes(SQLiteDataReader reader, string column)
        {
            int position = reader.GetOrdinal(column);
            if (reader.IsDBNull(position)) return null;
            return (byte[])reader[position];
        }
    }
}