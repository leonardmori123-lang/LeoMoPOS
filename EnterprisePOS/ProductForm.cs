using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // Product list with search, add, edit, stock adjustment and deactivate.
    public sealed class ProductForm : Form
    {
        private readonly User currentUser;
        private readonly TextBox txtSearch = new TextBox { Width = 230 };
        private readonly CheckBox chkInactive = new CheckBox
        {
            Text = "Show inactive",
            AutoSize = true,
            Margin = new Padding(14, 6, 0, 0)
        };
        private readonly Label lblSummary = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        private readonly DataGridView grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        public ProductForm(User user)
        {
            currentUser = user;

            Text = "Inventory Management";
            Width = 1100;
            Height = 650;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;

            var commands = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
            var search = new Button { Text = "Search", AutoSize = true };
            var add = new Button { Text = "Add Product", AutoSize = true, Margin = new Padding(30, 3, 3, 3) };
            var edit = new Button { Text = "Edit", AutoSize = true };
            var adjust = new Button { Text = "Adjust Stock", AutoSize = true };
            var deactivate = new Button { Text = "Deactivate", AutoSize = true };

            search.Click += (s, e) => LoadRows();
            add.Click += (s, e) => AddProduct();
            edit.Click += (s, e) => EditSelected();
            adjust.Click += (s, e) => AdjustSelected();
            deactivate.Click += (s, e) => DeactivateSelected();
            chkInactive.CheckedChanged += (s, e) => LoadRows();
            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    LoadRows();
                }
            };
            grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) EditSelected();
            };

            commands.Controls.Add(new Label { Text = "SKU / name / barcode", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            commands.Controls.Add(txtSearch);
            commands.Controls.Add(search);
            commands.Controls.Add(chkInactive);
            commands.Controls.Add(add);
            commands.Controls.Add(edit);
            commands.Controls.Add(adjust);
            commands.Controls.Add(deactivate);

            grid.DataBindingComplete += (s, e) => StyleGrid();

            // Fill control first, then the bottom bar, then the top bar.
            Controls.Add(grid);
            Controls.Add(lblSummary);
            Controls.Add(commands);

            Load += (s, e) => LoadRows();
        }

        private void LoadRows()
        {
            try
            {
                var products = new InventoryService().Search(txtSearch.Text, chkInactive.Checked);

                grid.DataSource = products.Select(p => new
                {
                    p.Id,
                    p.Sku,
                    p.Barcode,
                    p.Name,
                    Category = p.CategoryName,
                    p.Price,
                    p.StockQty,
                    p.ReorderLevel,
                    p.IsActive
                }).ToList();

                int low = products.Count(p => p.IsLowStock);
                lblSummary.Text = products.Count + " products, " + low + " low on stock (shaded red)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddProduct()
        {
            using (var editor = new ProductEditorForm(currentUser))
            {
                if (editor.ShowDialog(this) == DialogResult.OK) LoadRows();
            }
        }

        private void EditSelected()
        {
            if (grid.CurrentRow == null)
            {
                MessageBox.Show("Select a product first.", "Inventory");
                return;
            }

            int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
            using (var editor = new ProductEditorForm(currentUser, id))
            {
                if (editor.ShowDialog(this) == DialogResult.OK) LoadRows();
            }
        }

        private void AdjustSelected()
        {
            if (grid.CurrentRow == null)
            {
                MessageBox.Show("Select a product first.", "Inventory");
                return;
            }

            int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
            string productName = Convert.ToString(grid.CurrentRow.Cells["Name"].Value);
            int stock = Convert.ToInt32(grid.CurrentRow.Cells["StockQty"].Value);

            using (var form = new StockAdjustForm(currentUser, id, productName, stock))
            {
                if (form.ShowDialog(this) == DialogResult.OK) LoadRows();
            }
        }

        private void DeactivateSelected()
        {
            if (grid.CurrentRow == null)
            {
                MessageBox.Show("Select a product first.", "Inventory");
                return;
            }

            int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
            string productName = Convert.ToString(grid.CurrentRow.Cells["Name"].Value);
            bool isActive = (bool)grid.CurrentRow.Cells["IsActive"].Value;

            if (!isActive)
            {
                MessageBox.Show("This product is already inactive. Use Edit to turn it back on.", "Inventory");
                return;
            }

            var answer = MessageBox.Show(
                "Deactivate \"" + productName + "\"?\n\nIt will no longer appear at checkout. Past sales stay in the reports. You can turn it back on later with Edit.",
                "Deactivate product", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            try
            {
                new InventoryService().Deactivate(id, currentUser);
                LoadRows();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Number format, narrow Id column, red shading for low stock, grey text for inactive products
        private void StyleGrid()
        {
            if (!grid.Columns.Contains("Price")) return;

            grid.Columns["Price"].DefaultCellStyle.Format = "N2";
            grid.Columns["Price"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns["Id"].FillWeight = 35;

            foreach (DataGridViewRow row in grid.Rows)
            {
                int stock = Convert.ToInt32(row.Cells["StockQty"].Value);
                int reorder = Convert.ToInt32(row.Cells["ReorderLevel"].Value);
                bool active = (bool)row.Cells["IsActive"].Value;

                row.DefaultCellStyle.BackColor = stock <= reorder ? Color.MistyRose : Color.White;
                row.DefaultCellStyle.ForeColor = active ? Color.Black : Color.Gray;
            }
        }
    }
}