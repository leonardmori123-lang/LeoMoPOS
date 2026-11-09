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
        private const string DefaultDash = "-";

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
                admin.DropDownItems.Add("Check database connection", null, CheckConnectionMenuItem_Click);
                admin.DropDownItems.Add("Open log folder", null, OpenLogFolderMenuItem_Click);
                admin.DropDownItems.Add("Simulate error (test)", null, SimulateErrorMenuItem_Click);
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
            if (canManage)
            {
                body.Controls.Add(tiles, 0, 1);
            }
            body.Controls.Add(shortcuts, 0, 2);

            Controls.Add(body);
            Controls.Add(menu);
            MainMenuStrip = menu;

            Load += MainForm_Load;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            RefreshDashboard();
        }

        private void CheckConnectionMenuItem_Click(object sender, EventArgs e)
        {
            CheckConnection();
        }

        private void OpenLogFolderMenuItem_Click(object sender, EventArgs e)
        {
            OpenLogFolder();
        }

        private void SimulateErrorMenuItem_Click(object sender, EventArgs e)
        {
            throw new InvalidOperationException("Test error from the Administration menu.");
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
                MessageBox.Show(SqliteDb.Describe(), "Database connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                string reference = AppLog.Error("Database check failed", ex);
                MessageBox.Show(
                    ex.Message + Environment.NewLine + Environment.NewLine + "Reference: " + reference,
                    "Database connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            if (!canManage)
            {
                return;
            }

            try
            {
                DashboardStats stats = reports.GetDashboard(currentUser);
                lblSales.Text = stats.TodaySales.ToString("N2");
                lblOrders.Text = stats.TodayOrders.ToString();
                lblLowStock.Text = stats.LowStockCount.ToString();
                if (stats.LowStockCount > 0)
                {
                    lblLowStock.ForeColor = Color.Firebrick;
                }
                else
                {
                    lblLowStock.ForeColor = Color.DarkSlateBlue;
                }
                lblProducts.Text = stats.ActiveProducts.ToString();
            }
            catch
            {
                lblSales.Text = DefaultDash;
                lblOrders.Text = DefaultDash;
                lblLowStock.Text = DefaultDash;
                lblProducts.Text = DefaultDash;
            }
        }

        private static Label NewTileValue()
        {
            return new Label
            {
                Text = DefaultDash,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };
        }

        private static Panel MakeTile(string caption, Label value)
        {
            Panel tile = new Panel
            {
                Width = 210,
                Height = 90,
                Margin = new Padding(10),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Label label = new Label
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