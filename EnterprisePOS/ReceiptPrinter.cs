using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // Draws the receipt for an 80 mm roll. Preview shows it on screen, Print sends it to a printer.
    public sealed class ReceiptPrinter : IReceiptPrinter
    {
        private readonly PrintDocument document = new PrintDocument();
        private readonly Font normal = new Font("Consolas", 8.5f);
        private readonly Font bold = new Font("Consolas", 8.5f, FontStyle.Bold);
        private ReceiptData current;

        public ReceiptPrinter()
        {
            document.DocumentName = "Enterprise POS Receipt";
            // PaperSize is in hundredths of an inch: 315 is about 80 mm wide.
            document.DefaultPageSettings.PaperSize = new PaperSize("Receipt", 315, 900);
            document.DefaultPageSettings.Margins = new Margins(12, 12, 12, 12);
            document.PrintPage += PrintPage;
        }

        public void Print(ReceiptData receipt)
        {
            if (receipt == null) throw new ArgumentNullException("receipt");
            current = receipt;

            using (var dialog = new PrintDialog { Document = document, AllowSomePages = false })
            {
                if (dialog.ShowDialog() == DialogResult.OK) document.Print();
            }
        }

        public void Preview(ReceiptData receipt)
        {
            if (receipt == null) throw new ArgumentNullException("receipt");
            current = receipt;

            using (var preview = new PrintPreviewDialog { Document = document, Width = 900, Height = 700 })
            {
                preview.ShowDialog();
            }
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            var x = e.MarginBounds.Left;
            var y = e.MarginBounds.Top;
            var width = e.MarginBounds.Width;
            var g = e.Graphics;
            string rule = new string('-', 40);

            DrawCentered(g, current.BusinessName, bold, ref y, x, width);
            DrawCentered(g, current.Address, normal, ref y, x, width);
            DrawCentered(g, current.Phone, normal, ref y, x, width);
            DrawText(g, rule, normal, ref y, x);
            DrawText(g, "Order: " + current.OrderNumber, normal, ref y, x);
            DrawText(g, "Date : " + current.Date.ToString("yyyy-MM-dd HH:mm"), normal, ref y, x);
            DrawText(g, "Cashier: " + current.CashierName, normal, ref y, x);
            DrawText(g, rule, normal, ref y, x);
            DrawText(g, string.Format("{0,-23} {1,3} {2,10}", "Product", "Qty", "Amount"), bold, ref y, x);

            foreach (var line in current.Lines)
            {
                // A long name is cut so it never runs into the amount.
                var name = line.ProductName ?? string.Empty;
                if (name.Length > 23) name = name.Substring(0, 23);
                DrawText(g, string.Format("{0,-23} {1,3} {2,10:C2}", name, line.Quantity, line.Amount), normal, ref y, x);
            }

            DrawText(g, rule, normal, ref y, x);
            DrawRight(g, "Subtotal: " + current.Subtotal.ToString("C2"), normal, ref y, x, width);
            DrawRight(g, "Tax: " + current.Tax.ToString("C2"), normal, ref y, x, width);
            DrawRight(g, "Discount: " + current.Discount.ToString("C2"), normal, ref y, x, width);
            DrawRight(g, "TOTAL: " + current.Total.ToString("C2"), bold, ref y, x, width);
            DrawText(g, rule, normal, ref y, x);
            DrawText(g, "Payment: " + current.PaymentMethod, normal, ref y, x);
            DrawRight(g, "Tendered: " + current.AmountTendered.ToString("C2"), normal, ref y, x, width);
            DrawRight(g, "Change: " + current.Change.ToString("C2"), normal, ref y, x, width);
            y += 12;
            DrawCentered(g, "Thank you for your purchase!", bold, ref y, x, width);

            e.HasMorePages = false;
        }

        private static void DrawText(Graphics g, string text, Font font, ref int y, int x)
        {
            g.DrawString(text, font, Brushes.Black, x, y);
            y += font.Height + 2;
        }   

        private static void DrawCentered(Graphics g, string text, Font font, ref int y, int x, int width)
        {
            text = text ?? string.Empty;
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, Brushes.Black, x + (width - size.Width) / 2, y);
            y += font.Height + 2;
        }

        private static void DrawRight(Graphics g, string text, Font font, ref int y, int x, int width)
        {
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, Brushes.Black, x + width - size.Width, y);
            y += font.Height + 2;
        }
    }
}