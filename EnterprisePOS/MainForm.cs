using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EnterprisePOS.Data;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    public sealed class MainForm : Form
    {
        private readonly User currentUser;
        private readonly bool canManage;
        private readonly ReportingService reports = new ReportingService();

        private readonly Label lblSales = NewTileValue();
        private readonly Label lblOrders = NewTileValue();
        private readonly Label lblLowStock = NewTileValue();
        private readonly Label lblProducts = NewTileValue();

        public MainForm(User user)
        {
            currentUser = user;
            Text = "Enterprise POS | " + user.FullName + " (" + user.Role + ")";
            WindowState = FormWindowState.Maximized;
            AutoScaleMode = AutoScaleMode.Font;

            canManage = user.Role == UserRole.Admin || user.Role == UserRole.Manager;

            var menu = new MenuStrip();

            if (canManage)
            {
                var inventory = new ToolStripMenuItem("Inventory");
                inventory.DropDownItems.Add("Products", null, (s, e) => ShowChild(new ProductForm(currentUser)));
                menu.Items.Add(inventory);
            }

            var sales = new ToolStripMenuItem("Sales");
            sales.DropDownItems.Add("New Order", null, (s, e) => ShowChild(new CheckoutForm(currentUser)));
            if (canManage)
            {
                sales.DropDownItems.Add("Sales Analytics", null, (s, e) => ShowChild(new AnalyticsForm(currentUser)));
            }
            menu.Items.Add(sales);

            if (user.Role == UserRole.Admin)
            {
                var admin = new ToolStripMenuItem("Administration");
                admin.DropDownItems.Add("Users", null, (s, e) => ShowChild(new UserAdminForm(currentUser)));
                admin.DropDownItems.Add(new ToolStripSeparator());
                admin.DropDownItems.Add("Check database connection", null, (s, e) => CheckConnection());
                admin.DropDownItems.Add("Check built-in database (SQLite)", null, (s, e) => CheckBuiltInDatabase());
                admin.DropDownItems.Add("Open log folder", null, (s, e) => OpenLogFolder());
                admin.DropDownItems.Add("Simulate error (test)", null, (s, e) =>
                {
                    throw new InvalidOperationException("Test error from the Administration menu.");
                });
                menu.Items.Add(admin);
            }

            var title = new Label
            {
                Text = "Enterprise POS & Inventory Management",
                UseMnemonic = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };

            var tiles = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Anchor = AnchorStyles.None
            };
            tiles.Controls.Add(MakeTile("Today's sales", lblSales));
            tiles.Controls.Add(MakeTile("Today's orders", lblOrders));
            tiles.Controls.Add(MakeTile("Low stock items", lblLowStock));
            tiles.Controls.Add(MakeTile("Active products", lblProducts));

            var shortcuts = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Anchor = AnchorStyles.None
            };
            shortcuts.Controls.Add(MakeShortcut("New Order", (s, e) => ShowChild(new CheckoutForm(currentUser))));
            if (canManage)
            {
                shortcuts.Controls.Add(MakeShortcut("Inventory", (s, e) => ShowChild(new ProductForm(currentUser))));
                shortcuts.Controls.Add(MakeShortcut("Analytics", (s, e) => ShowChild(new AnalyticsForm(currentUser))));
            }

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(40), ColumnCount = 1, RowCount = 3 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            body.Controls.Add(title, 0, 0);
            if (canManage) body.Controls.Add(tiles, 0, 1);
            body.Controls.Add(shortcuts, 0, 2);

            Controls.Add(body);
            Controls.Add(menu);
            MainMenuStrip = menu;

            Load += (s, e) => RefreshDashboard();
        }

        private void ShowChild(Form form)
        {
            using (form)
            {
                form.ShowDialog(this);
            }
            RefreshDashboard();
        }

        private static void CheckConnection()
        {
            try
            {
                using (var connection = Db.Open())
                {
                    MessageBox.Show(
                        "Connected to database \"" + connection.Database + "\" on " + connection.DataSource + ".",
                        "Database connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Database connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CheckBuiltInDatabase()
        {
            try
            {
                MessageBox.Show(SqliteDb.Describe(), "Built-in database", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                string reference = AppLog.Error("Built-in database check failed", ex);
                MessageBox.Show(ex.Message + Environment.NewLine + Environment.NewLine + "Reference: " + reference,
                    "Built-in database", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(AppLog.Folder);
                Process.Start("explorer.exe", "\"" + AppLog.Folder + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Log folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshDashboard()
        {
            if (!canManage) return;

            try
            {
                var stats = reports.GetDashboard(currentUser);
                lblSales.Text = stats.TodaySales.ToString("N2");
                lblOrders.Text = stats.TodayOrders.ToString();
                lblLowStock.Text = stats.LowStockCount.ToString();
                lblLowStock.ForeColor = stats.LowStockCount > 0 ? Color.Firebrick : Color.DarkSlateBlue;
                lblProducts.Text = stats.ActiveProducts.ToString();
            }
            catch
            {
                // The numbers are a convenience. If they cannot load, show dashes and let the rest work.
                lblSales.Text = "-";
                lblOrders.Text = "-";
                lblLowStock.Text = "-";
                lblProducts.Text = "-";
            }
        }

        private static Label NewTileValue()
        {
            return new Label
            {
                Text = "-",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };
        }

        private static Panel MakeTile(string caption, Label value)
        {
            var tile = new Panel
            {
                Width = 210,
                Height = 90,
                Margin = new Padding(10),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            var label = new Label
            {
                Text = caption,
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DimGray
            };
            value.Dock = DockStyle.Fill;

            tile.Controls.Add(value);
            tile.Controls.Add(label);
            return tile;
        }

        private static Button MakeShortcut(string text, EventHandler click)
        {
            var button = new Button { Text = text, Width = 150, Height = 48, Margin = new Padding(10) };
            button.Click += click;
            return button;
        }
    }
}