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
        private const string FormTitle = "Adjust Stock";
        private const string ErrorDialogTitle = "Adjust stock";

        private readonly User currentUser;
        private readonly int productId;
        private readonly int currentStock;

        private TextBox changeTextBox;
        private ComboBox reasonComboBox;
        private Label newStockLabel;
        private Button applyButton;
        private Button cancelButton;

        // Creates the stock adjust form for a product.
        public StockAdjustForm(User user, int productId, string productName, int currentStock)
        {
            this.currentUser = user;
            this.productId = productId;
            this.currentStock = currentStock;

            InitializeFormProperties();
            InitializeControls();
            PopulateReasonSuggestions();
            BuildLayout(productName);
            AttachEvents();

            ActiveControl = changeTextBox;
            UpdatePreview();
        }

        // Sets the basic window properties.
        private void InitializeFormProperties()
        {
            Text = FormTitle;
            Width = 470;
            Height = 340;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
        }

        // Creates the controls used on this form.
        private void InitializeControls()
        {
            changeTextBox = new TextBox();

            reasonComboBox = new ComboBox();
            reasonComboBox.DropDownStyle = ComboBoxStyle.DropDown;
            reasonComboBox.MaxLength = 200;

            newStockLabel = new Label();
            newStockLabel.AutoSize = true;
            newStockLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);

            applyButton = new Button();
            applyButton.Text = "Apply";
            applyButton.AutoSize = true;

            cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.AutoSize = true;
            cancelButton.DialogResult = DialogResult.Cancel;
        }

        // Suggestions only; any reason can be typed.
        private void PopulateReasonSuggestions()
        {
            reasonComboBox.Items.AddRange(new object[]
            {
                "Delivery received",
                "Stock count correction",
                "Damaged or expired",
                "Returned to supplier",
                "Customer return"
            });
        }

        // Builds the table and button layout.
        private void BuildLayout(string productName)
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(18);
            table.ColumnCount = 2;
            table.RowCount = 7;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Label productLabel = new Label();
            productLabel.Text = productName;
            productLabel.AutoSize = true;
            productLabel.UseMnemonic = false;
            AddRow(table, "Product", productLabel, 0);

            Label currentStockValueLabel = new Label();
            currentStockValueLabel.Text = currentStock.ToString(CultureInfo.InvariantCulture);
            currentStockValueLabel.AutoSize = true;
            AddRow(table, "Current stock", currentStockValueLabel, 1);

            AddRow(table, "Change", changeTextBox, 2);

            Label hintLabel = new Label();
            hintLabel.Text = "Use a positive number to add stock and a negative number to remove it.";
            hintLabel.AutoSize = true;
            hintLabel.MaximumSize = new Size(290, 0);
            hintLabel.ForeColor = Color.DimGray;
            table.Controls.Add(hintLabel, 1, 3);

            AddRow(table, "Reason", reasonComboBox, 4);
            AddRow(table, "New stock", newStockLabel, 5);

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel();
            buttonPanel.FlowDirection = FlowDirection.RightToLeft;
            buttonPanel.Dock = DockStyle.Top;
            buttonPanel.AutoSize = true;
            buttonPanel.Controls.Add(applyButton);
            buttonPanel.Controls.Add(cancelButton);
            table.Controls.Add(buttonPanel, 1, 6);

            AcceptButton = applyButton;
            CancelButton = cancelButton;
            Controls.Add(table);
        }

        // Hooks up all event handlers in one place.
        private void AttachEvents()
        {
            applyButton.Click += ApplyButton_Click;
            changeTextBox.TextChanged += ChangeTextBox_TextChanged;
        }

        // Handles the click on the apply button to validate and save changes.
        private void ApplyButton_Click(object sender, EventArgs e)
        {
            try
            {
                int change;
                if (!int.TryParse(changeTextBox.Text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out change))
                {
                    throw new InvalidOperationException("Enter the change as a whole number, for example 10 or -3.");
                }

                InventoryService inventoryService = new InventoryService();
                inventoryService.AdjustStock(productId, change, reasonComboBox.Text, currentUser);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ErrorDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Updates the stock preview whenever the text changes.
        private void ChangeTextBox_TextChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        // Shows what the stock will be, in red if it would go below zero
        private void UpdatePreview()
        {
            int change;
            if (int.TryParse(changeTextBox.Text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out change))
            {
                long newStock = (long)currentStock + change;
                newStockLabel.Text = newStock.ToString(CultureInfo.InvariantCulture);
                if (newStock < 0)
                {
                    newStockLabel.ForeColor = Color.Firebrick;
                }
                else
                {
                    newStockLabel.ForeColor = Color.Black;
                }
            }
            else
            {
                newStockLabel.Text = "-";
                newStockLabel.ForeColor = Color.Black;
            }
        }

        // Adds a labeled control row to the table layout.
        private static void AddRow(TableLayoutPanel table, string labelText, Control control, int row)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            table.Controls.Add(label, 0, row);

            control.Dock = DockStyle.Top;
            table.Controls.Add(control, 1, row);
        }
    }
}
