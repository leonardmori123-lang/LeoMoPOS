using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // Sales reports from the teacher's report procedures, one tab per report.
    public sealed class AnalyticsForm : Form
    {
        private readonly User user;
        private readonly ReportingService reports = new ReportingService();

        private readonly DateTimePicker dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Value = DateTime.Today.AddDays(-30) };
        private readonly DateTimePicker dtTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Value = DateTime.Today };
        private readonly Label lblSummary = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        private readonly DataGridView gridDaily = NewGrid();
        private readonly DataGridView gridTop = NewGrid();
        private readonly DataGridView gridCategory = NewGrid();
        private readonly DataGridView gridLow = NewGrid();

        public AnalyticsForm(User currentUser)
        {
            user = currentUser;

            Text = "Sales Analytics";
            Width = 900;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
            var refresh = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(14, 0, 0, 0) };
            refresh.Click += (s, e) => LoadReports();
            top.Controls.Add(new Label { Text = "From", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            top.Controls.Add(dtFrom);
            top.Controls.Add(new Label { Text = "To", AutoSize = true, Margin = new Padding(14, 8, 6, 0) });
            top.Controls.Add(dtTo);
            top.Controls.Add(refresh);

            BuildColumns();

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(MakePage("Daily sales", gridDaily));
            tabs.TabPages.Add(MakePage("Top products", gridTop));
            tabs.TabPages.Add(MakePage("Sales by category", gridCategory));
            tabs.TabPages.Add(MakePage("Low stock (now)", gridLow));

            // Fill control first, then the bottom bar, then the top bar.
            Controls.Add(tabs);
            Controls.Add(lblSummary);
            Controls.Add(top);

            // Database calls wait for the Load event, not the constructor.
            Load += (s, e) => LoadReports();
        }

        private static DataGridView NewGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
        }

        private static TabPage MakePage(string title, DataGridView grid)
        {
            TabPage page = new TabPage(title);
            page.Controls.Add(grid);
            return page;
        }

        // Format and alignment: money and counts line up on the right.
        private static void AddColumn(DataGridView grid, string header, string property, float weight, string format = null, bool right = false)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = property,
                FillWeight = weight,
                ReadOnly = true
            };
            if (format != null)
            {
                column.DefaultCellStyle.Format = format;
            }
            if (right)
            {
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            grid.Columns.Add(column);
        }

        private void BuildColumns()
        {
            AddColumn(gridDaily, "Date", "SaleDate", 40, "yyyy-MM-dd");
            AddColumn(gridDaily, "Orders", "OrderCount", 25, null, true);
            AddColumn(gridDaily, "Revenue", "Revenue", 35, "N2", true);

            AddColumn(gridTop, "Product", "ProductName", 50);
            AddColumn(gridTop, "Qty sold", "QtySold", 20, null, true);
            AddColumn(gridTop, "Revenue", "Revenue", 30, "N2", true);

            AddColumn(gridCategory, "Category", "CategoryName", 60);
            AddColumn(gridCategory, "Revenue", "Revenue", 40, "N2", true);

            AddColumn(gridLow, "SKU", "Sku", 20);
            AddColumn(gridLow, "Product", "Name", 45);
            AddColumn(gridLow, "In stock", "StockQty", 17, null, true);
            AddColumn(gridLow, "Reorder level", "ReorderLevel", 18, null, true);
        }

        private void LoadReports()
        {
            try
            {
                DateTime start = dtFrom.Value.Date;
                DateTime end = dtTo.Value.Date;

                List<DailySales> daily = reports.GetDailySales(user, start, end);
                gridDaily.DataSource = daily;
                gridTop.DataSource = reports.GetTopProducts(user, start, end, 10);
                gridCategory.DataSource = reports.GetSalesByCategory(user, start, end);
                gridLow.DataSource = reports.GetLowStock(user);

                if (daily.Count == 0)
                {
                    lblSummary.Text = "No completed orders in this period.";
                }
                else
                {
                    int totalOrders = 0;
                    decimal totalRevenue = 0m;
                    foreach (DailySales row in daily)
                    {
                        totalOrders += row.OrderCount;
                        totalRevenue += row.Revenue;
                    }

                    lblSummary.Text = "Orders: " + totalOrders + "      Revenue: " + totalRevenue.ToString("N2");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Sales Analytics", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}