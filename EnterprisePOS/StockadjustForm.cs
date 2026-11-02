using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // Adds or removes stock for one product and records the reason.
    public sealed class StockAdjustForm : Form
    {
        private readonly User currentUser;
        private readonly int productId;
        private readonly int currentStock;

        private readonly TextBox txtChange = new TextBox();
        private readonly ComboBox cboReason = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, MaxLength = 200 };
        private readonly Label lblNewStock = new Label { AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

        public StockAdjustForm(User user, int productId, string productName, int currentStock)
        {
            currentUser = user;
            this.productId = productId;
            this.currentStock = currentStock;

            Text = "Adjust Stock";
            Width = 470;
            Height = 340;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;

            // Suggestions only; any reason can be typed.
            cboReason.Items.AddRange(new object[]
            {
                "Delivery received",
                "Stock count correction",
                "Damaged or expired",
                "Returned to supplier",
                "Customer return"
            });

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 7 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddRow(table, "Product", new Label { Text = productName, AutoSize = true, UseMnemonic = false }, 0);
            AddRow(table, "Current stock", new Label { Text = currentStock.ToString(CultureInfo.InvariantCulture), AutoSize = true }, 1);
            AddRow(table, "Change", txtChange, 2);

            var hint = new Label
            {
                Text = "Use a positive number to add stock and a negative number to remove it.",
                AutoSize = true,
                MaximumSize = new Size(290, 0),
                ForeColor = Color.DimGray
            };
            table.Controls.Add(hint, 1, 3);

            AddRow(table, "Reason", cboReason, 4);
            AddRow(table, "New stock", lblNewStock, 5);

            var apply = new Button { Text = "Apply", AutoSize = true };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            apply.Click += Apply;

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Top, AutoSize = true };
            buttons.Controls.Add(apply);
            buttons.Controls.Add(cancel);
            table.Controls.Add(buttons, 1, 6);

            AcceptButton = apply;
            CancelButton = cancel;
            Controls.Add(table);

            txtChange.TextChanged += (s, e) => UpdatePreview();
            ActiveControl = txtChange;
            UpdatePreview();
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control, int row)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
            control.Dock = DockStyle.Top;
            table.Controls.Add(control, 1, row);
        }

        // Shows what the stock will be, in red if it would go below zero
        private void UpdatePreview()
        {
            int change;
            if (int.TryParse(txtChange.Text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out change))
            {
                long newStock = (long)currentStock + change;
                lblNewStock.Text = newStock.ToString(CultureInfo.InvariantCulture);
                lblNewStock.ForeColor = newStock < 0 ? Color.Firebrick : Color.Black;
            }
            else
            {
                lblNewStock.Text = "-";
                lblNewStock.ForeColor = Color.Black;
            }
        }

        private void Apply(object sender, EventArgs e)
        {
            try
            {
                int change;
                if (!int.TryParse(txtChange.Text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out change))
                    throw new InvalidOperationException("Enter the change as a whole number, for example 10 or -3.");

                new InventoryService().AdjustStock(productId, change, cboReason.Text, currentUser);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Adjust stock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}