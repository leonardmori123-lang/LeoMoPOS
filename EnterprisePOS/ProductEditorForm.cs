using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // Add or edit one product. productId 0 means a new product.
    public sealed class ProductEditorForm : Form
    {
        private const int MaxImageBytes = 1024 * 1024; // 1 MB

        private readonly User currentUser;
        private readonly int productId;

        private readonly TextBox sku = new TextBox { MaxLength = 30 };
        private readonly TextBox barcode = new TextBox { MaxLength = 50 };
        private readonly TextBox name = new TextBox { MaxLength = 150 };
        private readonly ComboBox category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox cost = new TextBox();
        private readonly TextBox price = new TextBox();
        private readonly TextBox stock = new TextBox();
        private readonly TextBox reorder = new TextBox();
        private readonly TextBox imageName = new TextBox { ReadOnly = true };
        private readonly CheckBox active = new CheckBox { Text = "Active", Checked = true, AutoSize = true };

        private byte[] newImage;
        private bool imageChanged;

        public ProductEditorForm(User user, int productId = 0)
        {
            currentUser = user;
            this.productId = productId;

            Text = productId == 0 ? "Add Product" : "Edit Product";
            Width = 520;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 3, RowCount = 11 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            AddRow(table, "SKU", sku, 0);
            AddRow(table, "Barcode", barcode, 1);
            AddRow(table, "Product name", name, 2);
            AddRow(table, "Category", category, 3);
            AddRow(table, "Cost price", cost, 4);
            AddRow(table, "Selling price", price, 5);

            // Stock is only typed in for a new product. Later changes go through an inventory adjustment.
            stock.ReadOnly = productId != 0;
            AddRow(table, productId == 0 ? "Stock" : "Stock (view only)", stock, 6);

            AddRow(table, "Reorder level", reorder, 7);
            AddRow(table, "Image", imageName, 8);

            var browse = new Button { Text = "Browse" };
            browse.Click += BrowseImage;
            table.Controls.Add(browse, 2, 8);

            if (productId != 0)
            {
                table.Controls.Add(active, 1, 9);
            }
                
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var save = new Button { Text = "Save" };
            save.Click += Save;
            table.Controls.Add(cancel, 1, 10);
            table.Controls.Add(save, 2, 10);

            AcceptButton = save;
            CancelButton = cancel;
            Controls.Add(table);

            Load += EditorLoad;
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control, int row)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
            control.Dock = DockStyle.Top;
            table.Controls.Add(control, 1, row);
        }

        // Data is loaded here, not in the constructor.
        private void EditorLoad(object sender, EventArgs e)
        {
            try
            {
                InventoryService service = new InventoryService();

                category.DataSource = service.Categories();
                category.DisplayMember = "Name";
                category.ValueMember = "Id";

                if (productId == 0)
                {
                    stock.Text = "0";
                    reorder.Text = "5";
                    imageName.Text = "(none)";
                    return;
                }

                Product existing = service.GetProduct(productId);
                if (existing == null)
                {
                    throw new InvalidOperationException("Product not found.");
                }

                PopulateExistingProduct(existing);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Product", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void BrowseImage(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    FileInfo info = new FileInfo(dialog.FileName);
                    if (info.Length > MaxImageBytes)
                    {
                        throw new InvalidOperationException("The image must be 1 MB or smaller.");
                    }

                    byte[] bytes = File.ReadAllBytes(dialog.FileName);
                    using (MemoryStream stream = new MemoryStream(bytes))
                    using (Image.FromStream(stream))
                    {
                        // Opening it proves the file really is a picture.
                    }

                    newImage = bytes;
                    imageChanged = true;
                    imageName.Text = info.Name;
                }
                catch (ArgumentException)
                {
                    MessageBox.Show("That file is not a valid image.", "Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                decimal costValue;
                decimal priceValue;
                int stockValue = 0;
                int reorderValue;

                if (!decimal.TryParse(cost.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out costValue))
                {
                    throw new InvalidOperationException("Cost price must be a number, for example 12.50.");
                }
                if (!decimal.TryParse(price.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out priceValue))
                {
                    throw new InvalidOperationException("Selling price must be a number, for example 15.00.");
                }
                if (productId == 0 && !int.TryParse(stock.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out stockValue))
                {
                    throw new InvalidOperationException("Stock must be a whole number.");
                }
                if (!int.TryParse(reorder.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out reorderValue))
                {
                    throw new InvalidOperationException("Reorder level must be a whole number.");
                }
                if (category.SelectedValue == null)
                {
                    throw new InvalidOperationException("Choose a category.");
                }

                Product product = new Product
                {
                    Id = productId,
                    Sku = sku.Text,
                    Barcode = barcode.Text,
                    Name = name.Text,
                    CategoryId = (int)category.SelectedValue,
                    Cost = costValue,
                    Price = priceValue,
                    StockQty = stockValue,
                    ReorderLevel = reorderValue,
                    IsActive = productId == 0 || active.Checked,
                    Image = newImage
                };

                new InventoryService().Save(product, currentUser, imageChanged);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Product validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Helper to fill the input fields when editing an existing product.
        private void PopulateExistingProduct(Product existing)
        {
            sku.Text = existing.Sku;
            barcode.Text = existing.Barcode;
            name.Text = existing.Name;
            category.SelectedValue = existing.CategoryId;
            cost.Text = existing.Cost.ToString("0.00", CultureInfo.CurrentCulture);
            price.Text = existing.Price.ToString("0.00", CultureInfo.CurrentCulture);
            stock.Text = existing.StockQty.ToString(CultureInfo.InvariantCulture);
            reorder.Text = existing.ReorderLevel.ToString(CultureInfo.InvariantCulture);
            if (existing.Image == null)
            {
                imageName.Text = "(none)";
            }
            else
            {
                imageName.Text = "(current image kept)";
            }
            active.Checked = existing.IsActive;
        }
    }
}