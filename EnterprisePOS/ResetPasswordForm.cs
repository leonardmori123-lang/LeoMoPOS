using System;
using System.Drawing;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // Admin sets a new password for one user. The old password is not asked for.
    public sealed class ResetPasswordForm : Form
    {
        private readonly User currentUser;
        private readonly User target;

        private readonly TextBox password = new TextBox { MaxLength = 100, UseSystemPasswordChar = true };
        private readonly TextBox confirm = new TextBox { MaxLength = 100, UseSystemPasswordChar = true };

        public ResetPasswordForm(User actor, User targetUser)
        {
            currentUser = actor;
            target = targetUser;

            Text = "Reset Password";
            Width = 460;
            Height = 290;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 3, RowCount = 5 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            var who = new Label
            {
                Text = "New password for " + target.Username + " (" + target.FullName + ")",
                UseMnemonic = false,
                AutoSize = true,
                MaximumSize = new Size(390, 0),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(3, 3, 3, 12)
            };
            table.Controls.Add(who, 0, 0);
            table.SetColumnSpan(who, 3);

            AddRow(table, "New password", password, 1);
            AddRow(table, "Confirm password", confirm, 2);

            var hint = new Label { Text = "At least 8 characters.", AutoSize = true, ForeColor = Color.DimGray };
            table.Controls.Add(hint, 1, 3);

            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var save = new Button { Text = "Save" };
            save.Click += Save;
            table.Controls.Add(cancel, 1, 4);
            table.Controls.Add(save, 2, 4);

            AcceptButton = save;
            CancelButton = cancel;
            Controls.Add(table);
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control, int row)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
            control.Dock = DockStyle.Top;
            table.Controls.Add(control, 1, row);
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                if (password.Text != confirm.Text)
                    throw new InvalidOperationException("The two passwords do not match.");

                new UserService().ResetPassword(currentUser, target.Id, password.Text);

                MessageBox.Show("Password changed for " + target.Username + ".", "Reset Password",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Reset Password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}