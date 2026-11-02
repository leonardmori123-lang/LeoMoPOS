using System;
using System.Drawing;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // Add a user (existing is null) or edit one. In edit mode the username stays fixed and the
    // password is left alone.
    public sealed class UserEditorForm : Form
    {
        private readonly User currentUser;
        private readonly User existing;

        private readonly TextBox username = new TextBox { MaxLength = 50 };
        private readonly TextBox fullName = new TextBox { MaxLength = 100 };
        private readonly TextBox password = new TextBox { MaxLength = 100, UseSystemPasswordChar = true };
        private readonly TextBox confirm = new TextBox { MaxLength = 100, UseSystemPasswordChar = true };
        private readonly ComboBox role = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox active = new CheckBox { Text = "Active", Checked = true, AutoSize = true };

        public UserEditorForm(User actor, User existingUser = null)
        {
            currentUser = actor;
            existing = existingUser;
            bool isNew = existing == null;
            bool editingSelf = !isNew && existing.Id == currentUser.Id;

            Text = isNew ? "Add User" : "Edit User";
            Width = 480;
            Height = isNew ? 360 : 330;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;

            role.DataSource = Enum.GetValues(typeof(UserRole));

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 3 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            int row = 0;
            AddRow(table, "Username", username, row++);
            AddRow(table, "Full name", fullName, row++);

            if (isNew)
            {
                AddRow(table, "Password", password, row++);
                AddRow(table, "Confirm password", confirm, row++);
            }

            AddRow(table, "Role", role, row++);

            if (!isNew)
            {
                table.Controls.Add(active, 1, row++);
            }

            if (editingSelf)
            {
                var note = new Label
                {
                    Text = "This is your own account, so the role and active setting are locked.",
                    AutoSize = true,
                    MaximumSize = new Size(300, 0),
                    ForeColor = Color.DimGray
                };
                table.Controls.Add(note, 1, row++);
            }

            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var save = new Button { Text = "Save" };
            save.Click += Save;
            table.Controls.Add(cancel, 1, row);
            table.Controls.Add(save, 2, row);
            table.RowCount = row + 1;

            AcceptButton = save;
            CancelButton = cancel;
            Controls.Add(table);

            if (isNew)
            {
                role.SelectedItem = UserRole.Cashier;
            }
            else
            {
                username.Text = existing.Username;
                username.ReadOnly = true;
                fullName.Text = existing.FullName;
                role.SelectedItem = existing.Role;
                active.Checked = existing.IsActive;

                if (editingSelf)
                {
                    role.Enabled = false;
                    active.Enabled = false;
                }
            }
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
                if (existing == null && password.Text != confirm.Text)
                    throw new InvalidOperationException("The two passwords do not match.");

                var user = new User
                {
                    Id = existing == null ? 0 : existing.Id,
                    Username = username.Text,
                    FullName = fullName.Text,
                    Role = (UserRole)role.SelectedItem,
                    IsActive = existing == null || active.Checked
                };

                var service = new UserService();
                if (existing == null) service.Create(currentUser, user, password.Text);
                else service.Update(currentUser, user);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "User validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}