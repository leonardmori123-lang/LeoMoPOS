using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // New order screen: build the cart, pick the payment, save the order.
    public sealed class CheckoutForm : Form
    {
        private readonly User cashier;
        private readonly SalesService sales = new SalesService();
        private readonly BindingList<CartLine> cart = new BindingList<CartLine>();
        private readonly Dictionary<int, int> stockByProduct = new Dictionary<int, int>();

        private readonly TextBox txtCode = new TextBox { Width = 220 };
        private readonly NumericUpDown numQty = new NumericUpDown { Minimum = 1, Maximum = 999, Value = 1, Width = 60 };
        private readonly TextBox txtDiscount = new TextBox { Text = "0.00", Width = 110, TextAlign = HorizontalAlignment.Right, Anchor = AnchorStyles.Right };
        private readonly ComboBox cboPayment = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Anchor = AnchorStyles.Right };
        private readonly TextBox txtTendered = new TextBox { Width = 110, TextAlign = HorizontalAlignment.Right, Anchor = AnchorStyles.Right };
        private readonly Label lblSubtotal = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label lblTax = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        private readonly Label lblTotal = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 14, FontStyle.Bold)
        };
        private readonly Label lblChange = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        };
        private readonly Button btnCheckout = new Button
        {
            Text = "Checkout",
            Dock = DockStyle.Bottom,
            Height = 64,
            Font = new Font("Segoe UI", 14, FontStyle.Bold)
        };
        private readonly DataGridView grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        // Both stay gray until a sale is saved. They always refer to the last completed order.
        private readonly Button btnPreview = new Button { Text = "Preview receipt", Dock = DockStyle.Fill, Enabled = false };
        private readonly Button btnPrint = new Button { Text = "Print receipt", Dock = DockStyle.Fill, Enabled = false };
        private ReceiptData lastReceipt;

        public CheckoutForm(User user)
        {
            cashier = user;

            Text = "New Order | Cashier: " + user.FullName;
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;

            BuildColumns();
            grid.DataSource = cart;

            cboPayment.Items.AddRange(PaymentMethods.All);
            cboPayment.SelectedIndex = 0;

            // Top bar: barcode or SKU, quantity, Add
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
            var add = new Button { Text = "Add", AutoSize = true };
            add.Click += (s, e) => AddLine();
            txtCode.KeyDown += (s, e) =>
            {
                // A barcode scanner types the code and then presses Enter.
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AddLine();
                }
            };
            top.Controls.Add(new Label { Text = "SKU / barcode", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            top.Controls.Add(txtCode);
            top.Controls.Add(new Label { Text = "Qty", AutoSize = true, Margin = new Padding(14, 8, 6, 0) });
            top.Controls.Add(numQty);
            top.Controls.Add(add);

            // Bottom: cart buttons and Checkout on the left, totals and payment on the right
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 280, Padding = new Padding(8) };

            Panel left = new Panel { Dock = DockStyle.Left, Width = 260 };
            FlowLayoutPanel cartButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44 };
            Button remove = new Button { Text = "Remove line", AutoSize = true };
            Button clear = new Button { Text = "Clear cart", AutoSize = true };
            remove.Click += RemoveButton_Click;
            clear.Click += ClearCartButton_Click;
            btnCheckout.Click += (s, e) => Checkout();
            cartButtons.Controls.Add(remove);
            cartButtons.Controls.Add(clear);

            TableLayoutPanel receiptButtons = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 44, ColumnCount = 2, RowCount = 1 };
            receiptButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            receiptButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            receiptButtons.Controls.Add(btnPreview, 0, 0);
            receiptButtons.Controls.Add(btnPrint, 1, 0);
            btnPreview.Click += (s, e) => ShowReceipt(false);
            btnPrint.Click += (s, e) => ShowReceipt(true);

            // The last control added docks first: Checkout takes the very bottom, the receipt buttons sit above it.
            left.Controls.Add(receiptButtons);
            left.Controls.Add(btnCheckout);
            left.Controls.Add(cartButtons);

            TableLayoutPanel amounts = new TableLayoutPanel { Dock = DockStyle.Right, Width = 340, ColumnCount = 2, RowCount = 7 };
            amounts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            amounts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 7; i++)
            {
                amounts.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7f));
            }
            string taxCaption = "Tax (" + SalesService.TaxRatePercent.ToString("0.##", CultureInfo.InvariantCulture) + "%)";
            AddAmountRow(amounts, "Subtotal", lblSubtotal, 0);
            AddAmountRow(amounts, "Discount", txtDiscount, 1);
            AddAmountRow(amounts, taxCaption, lblTax, 2);
            AddAmountRow(amounts, "Total", lblTotal, 3);
            AddAmountRow(amounts, "Payment", cboPayment, 4);
            AddAmountRow(amounts, "Cash received", txtTendered, 5);
            AddAmountRow(amounts, "Change due", lblChange, 6);

            footer.Controls.Add(amounts);
            footer.Controls.Add(left);

            // Fill control first, then the bottom bar, then the top bar.
            Controls.Add(grid);
            Controls.Add(footer);
            Controls.Add(top);

            grid.CellEndEdit += Grid_CellEndEdit;
            grid.DataError += Grid_DataError;
            txtDiscount.TextChanged += TxtDiscount_TextChanged;
            txtTendered.TextChanged += TxtTendered_TextChanged;
            cboPayment.SelectedIndexChanged += CboPayment_SelectedIndexChanged;

            // Do not throw away a cart by accident
            FormClosing += CheckoutForm_FormClosing;

            ActiveControl = txtCode;
            PaymentChanged();
        }

        private void BuildColumns()
        {
            DataGridViewCellStyle money = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight };

            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SKU", DataPropertyName = "Sku", ReadOnly = true, FillWeight = 20 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product", DataPropertyName = "ProductName", ReadOnly = true, FillWeight = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty (click to edit)", DataPropertyName = "Quantity", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unit price", DataPropertyName = "UnitPrice", ReadOnly = true, FillWeight = 15, DefaultCellStyle = money });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Line total", DataPropertyName = "LineTotal", ReadOnly = true, FillWeight = 15, DefaultCellStyle = money });
        }

        private static void AddAmountRow(TableLayoutPanel table, string caption, Control value, int row)
        {
            table.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, row);
            table.Controls.Add(value, 1, row);
        }

        // Blank counts as 0. Returns false for text that is not a number or is negative.
        private static bool TryParseMoney(string text, out decimal value)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                value = 0m;
                return true;
            }
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value >= 0m;
        }

        // Looks the code up, then adds a line, or adds to the quantity when the product is already in the cart.
        private void AddLine()
        {
            var code = txtCode.Text.Trim();
            if (code.Length == 0) return;

            try
            {
                var product = new InventoryService().FindByCode(code);
                if (product == null)
                {
                    MessageBox.Show("Product not found: " + code, "New Order");
                    txtCode.SelectAll();
                    return;
                }

                int qty = (int)numQty.Value;
                CartLine existing = null;
                foreach (CartLine line in cart)
                {
                    if (line.ProductId == product.Id)
                    {
                        existing = line;
                        break;
                    }
                }

                int inCart;
                if (existing == null)
                {
                    inCart = 0;
                }
                else
                {
                    inCart = existing.Quantity;
                }

                if (inCart + qty > product.StockQty)
                {
                    string alreadyText;
                    if (inCart > 0)
                    {
                        alreadyText = " (" + inCart + " already in the cart).";
                    }
                    else
                    {
                        alreadyText = ".";
                    }
                    MessageBox.Show(
                        "Only " + product.StockQty + " of \"" + product.Name + "\" in stock" + alreadyText,
                        "New Order");
                    txtCode.SelectAll();
                    return;
                }

                stockByProduct[product.Id] = product.StockQty;

                if (existing != null)
                {
                    existing.Quantity += qty;
                    cart.ResetItem(cart.IndexOf(existing));
                }
                else
                {
                    cart.Add(new CartLine
                    {
                        ProductId = product.Id,
                        Sku = product.Sku,
                        ProductName = product.Name,
                        Quantity = qty,
                        UnitPrice = product.Price
                    });
                }

                txtCode.Clear();
                numQty.Value = 1;
                RefreshTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "New Order", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Ready for the next scan
                txtCode.Focus();
            }
        }

        // After the cashier types a new quantity in the grid
        private void QuantityEdited(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= cart.Count)
            {
                return;
            }

            CartLine line = cart[rowIndex];
            int stock;
            if (!stockByProduct.TryGetValue(line.ProductId, out stock))
            {
                stock = int.MaxValue;
            }

            if (line.Quantity < 1)
            {
                line.Quantity = 1;
            }
            else if (line.Quantity > stock)
            {
                MessageBox.Show("Only " + stock + " of \"" + line.ProductName + "\" in stock.", "New Order");
                line.Quantity = stock;
            }

            cart.ResetItem(rowIndex);
            RefreshTotals();
        }

        private void RemoveSelected()
        {
            if (grid.CurrentRow == null)
            {
                return;
            }

            cart.RemoveAt(grid.CurrentRow.Index);
            RefreshTotals();
            txtCode.Focus();
        }

        private void ClearCart()
        {
            if (cart.Count == 0)
            {
                return;
            }

            DialogResult answer = MessageBox.Show("Remove every line from the cart?", "New Order", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            cart.Clear();
            RefreshTotals();
            txtCode.Focus();
        }

        // Cash needs the amount received typed in. Card and Mobile pay exactly the total.
        private void PaymentChanged()
        {
            bool cash = cboPayment.Text == PaymentMethods.Cash;
            txtTendered.Enabled = cash;
            if (!cash)
            {
                txtTendered.Clear();
            }
            RefreshTotals();
        }

        // The discount box turns pink when the value cannot be used; the totals then ignore it.
        private void RefreshTotals()
        {
            decimal discount;
            bool discountOk = TryParseMoney(txtDiscount.Text, out discount);

            OrderTotals totals = null;
            if (discountOk)
            {
                try
                {
                    totals = sales.Calculate(cart, discount);
                }
                catch (ArgumentException)
                {
                    discountOk = false;
                }
            }

            if (!discountOk)
            {
                totals = sales.Calculate(cart, 0m);
            }

            if (discountOk)
            {
                txtDiscount.BackColor = SystemColors.Window;
            }
            else
            {
                txtDiscount.BackColor = Color.MistyRose;
            }

            lblSubtotal.Text = totals.Subtotal.ToString("N2");
            lblTax.Text = totals.Tax.ToString("N2");
            lblTotal.Text = totals.Total.ToString("N2");

            decimal received;
            if (cboPayment.Text != PaymentMethods.Cash)
            {
                lblChange.Text = "0.00";
            }
            else if (TryParseMoney(txtTendered.Text, out received) && received >= totals.Total)
            {
                lblChange.Text = (received - totals.Total).ToString("N2");
            }
            else
            {
                lblChange.Text = "-";
            }
        }

        private void Checkout()
        {
            btnCheckout.Enabled = false; // one click, one order
            try
            {
                if (cart.Count == 0) throw new InvalidOperationException("The cart is empty.");

                decimal discount;
                if (!TryParseMoney(txtDiscount.Text, out discount))
                {
                    throw new InvalidOperationException("The discount must be a number, 0 or more.");
                }

                OrderTotals totals = sales.Calculate(cart, discount);

                string method = cboPayment.Text;
                decimal paid = totals.Total;
                if (method == PaymentMethods.Cash)
                {
                    if (!TryParseMoney(txtTendered.Text, out paid) || paid < totals.Total)
                    {
                        throw new InvalidOperationException("Enter the cash received. It must cover the total of " + totals.Total.ToString("N2") + ".");
                    }
                }

                int orderId = sales.Checkout(cashier, cart.ToList(), null, method, discount, paid);

                // From here the order is saved. Reading it back only fills in the confirmation and the receipt,
                // so a problem there must never look like a failed checkout.
                lastReceipt = null;
                btnPreview.Enabled = false;
                btnPrint.Enabled = false;

                string summary;
                try
                {
                    Order order = sales.GetOrder(orderId);
                    lastReceipt = sales.BuildReceipt(order, cashier.FullName);
                    btnPreview.Enabled = true;
                    btnPrint.Enabled = true;

                    summary = "Order " + order.OrderNumber + " saved." + Environment.NewLine + Environment.NewLine +
                              "Total: " + order.Total.ToString("N2") + Environment.NewLine +
                              "Paid (" + order.PaymentMethod + "): " + order.AmountPaid.ToString("N2") + Environment.NewLine +
                              "Change due: " + order.ChangeDue.ToString("N2") + Environment.NewLine + Environment.NewLine +
                              "The receipt is ready to preview or print.";
                }
                catch
                {
                    lastReceipt = null;
                    summary = "Order " + orderId + " saved.";
                }

                MessageBox.Show(summary, "Checkout", MessageBoxButtons.OK, MessageBoxIcon.Information);
                StartNewOrder();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Checkout failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCheckout.Enabled = true;
                txtCode.Focus();
            }
        }

        // Preview or print the receipt of the last completed order. This only reads, it never creates an order,
        // and a printer problem is reported here without touching the sale that is already saved.
        private void ShowReceipt(bool print)
        {
            if (lastReceipt == null)
            {
                MessageBox.Show("Complete a checkout first.", "Receipt");
                return;
            }

            try
            {
                IReceiptPrinter printer = new ReceiptPrinter();
                if (print)
                {
                    printer.Print(lastReceipt);
                }
                else
                {
                    printer.Preview(lastReceipt);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("The receipt could not be shown: " + ex.Message + Environment.NewLine +
                                "The order is already saved.", "Receipt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StartNewOrder()
        {
            cart.Clear();
            stockByProduct.Clear();
            txtDiscount.Text = "0.00";
            txtTendered.Clear();
            cboPayment.SelectedIndex = 0;
            RefreshTotals();
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            RemoveSelected();
        }

        private void ClearCartButton_Click(object sender, EventArgs e)
        {
            ClearCart();
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            QuantityEdited(e.RowIndex);
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            MessageBox.Show("Quantity must be a whole number.", "New Order");
        }

        private void TxtDiscount_TextChanged(object sender, EventArgs e)
        {
            RefreshTotals();
        }

        private void TxtTendered_TextChanged(object sender, EventArgs e)
        {
            RefreshTotals();
        }

        private void CboPayment_SelectedIndexChanged(object sender, EventArgs e)
        {
            PaymentChanged();
        }

        private void CheckoutForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (cart.Count > 0 &&
                MessageBox.Show("The cart still has items. Close and discard it?", "New Order",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
            }
        }
    }
}