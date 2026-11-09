using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
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
            Button edit = new Button { Text = "Edit", AutoSize = true };
            Button adjust = new Button { Text = "Adjust Stock", AutoSize = true };
            Button deactivate = new Button { Text = "Deactivate", AutoSize = true };

            search.Click += (s, e) => LoadRows();
            add.Click += AddButton_Click;
            edit.Click += EditButton_Click;
            adjust.Click += AdjustButton_Click;
            deactivate.Click += DeactivateButton_Click;
            chkInactive.CheckedChanged += ChkInactive_CheckedChanged;
            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    LoadRows();
                }
            };
            grid.CellDoubleClick += Grid_CellDoubleClick;

            commands.Controls.Add(new Label { Text = "SKU / name / barcode", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            commands.Controls.Add(txtSearch);
            commands.Controls.Add(search);
            commands.Controls.Add(chkInactive);
            commands.Controls.Add(add);
            commands.Controls.Add(edit);
            commands.Controls.Add(adjust);
            commands.Controls.Add(deactivate);

            grid.DataBindingComplete += Grid_DataBindingComplete;

            Controls.Add(grid);
            Controls.Add(lblSummary);
            Controls.Add(commands);

            Load += (s, e) => LoadRows();
        }

        private void LoadRows()
        {
            try
            {
                List<Product> products = new InventoryService().Search(txtSearch.Text, chkInactive.Checked);

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

                int low = 0;
                foreach (Product p in products)
                {
                    if (p.IsLowStock)
                    {
                        low++;
                    }
                }

                lblSummary.Text = products.Count + " products, " + low + " low on stock (shaded red)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            AddProduct();
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void AdjustButton_Click(object sender, EventArgs e)
        {
            AdjustSelected();
        }

        private void DeactivateButton_Click(object sender, EventArgs e)
        {
            DeactivateSelected();
        }

        private void ChkInactive_CheckedChanged(object sender, EventArgs e)
        {
            LoadRows();
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                EditSelected();
            }
        }

        private void Grid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            StyleGrid();
        }

        private void AddProduct()
        {
            using (var editor = new ProductEditorForm(currentUser))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    LoadRows();
                }
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
            using (ProductEditorForm editor = new ProductEditorForm(currentUser, id))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    LoadRows();
                }
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

            using (StockAdjustForm form = new StockAdjustForm(currentUser, id, productName, stock))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadRows();
                }
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

            DialogResult answer = MessageBox.Show(
                "Deactivate \"" + productName + "\"?\n\nIt will no longer appear at checkout. Past sales stay in the reports. You can turn it back on later with Edit.",
                "Deactivate product", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }

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

        private void StyleGrid()
        {
            if (!grid.Columns.Contains("Price"))
            {
                return;
            }

            grid.Columns["Price"].DefaultCellStyle.Format = "N2";
            grid.Columns["Price"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns["Id"].FillWeight = 35;

            foreach (DataGridViewRow row in grid.Rows)
            {
                int stock = Convert.ToInt32(row.Cells["StockQty"].Value);
                int reorder = Convert.ToInt32(row.Cells["ReorderLevel"].Value);
                bool active = (bool)row.Cells["IsActive"].Value;

                if (stock <= reorder)
                {
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.White;
                }

                if (active)
                {
                    row.DefaultCellStyle.ForeColor = Color.Black;
                }
                else
                {
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                }
            }
        }
    }
}