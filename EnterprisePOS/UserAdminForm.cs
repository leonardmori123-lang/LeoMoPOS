using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnterprisePOS.Domain;
using EnterprisePOS.Services;

namespace EnterprisePOS
{
    // Admin only: the list of users with add and edit.
    public sealed class UserAdminForm : Form
    {
        private readonly User currentUser;
        private readonly UserService service = new UserService();
        private List<User> loaded = new List<User>();

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
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        public UserAdminForm(User user)
        {
            currentUser = user;

            Text = "User & Role Management";
            Width = 850;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;

            var commands = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
            var add = new Button { Text = "Add User", AutoSize = true };
            var edit = new Button { Text = "Edit", AutoSize = true };
            var reset = new Button { Text = "Reset Password", AutoSize = true };
            add.Click += (s, e) => AddUser();
            edit.Click += (s, e) => EditSelected();
            reset.Click += (s, e) => ResetSelected();
            grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) EditSelected();
            };
            commands.Controls.Add(add);
            commands.Controls.Add(edit);
            commands.Controls.Add(reset);

            BuildColumns();
            grid.DataBindingComplete += (s, e) => StyleRows();

            // Fill control first, then the bottom bar, then the top bar.
            Controls.Add(grid);
            Controls.Add(lblSummary);
            Controls.Add(commands);

            Load += (s, e) => LoadRows();
        }

        private void BuildColumns()
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", HeaderText = "Username", DataPropertyName = "Username", FillWeight = 25 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FullName", HeaderText = "Full name", DataPropertyName = "FullName", FillWeight = 35 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "Role", DataPropertyName = "Role", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsActive", HeaderText = "Active", DataPropertyName = "IsActive", FillWeight = 10 });

            var lastLogin = new DataGridViewTextBoxColumn { Name = "LastLoginAt", HeaderText = "Last login", DataPropertyName = "LastLoginAt", FillWeight = 25 };
            lastLogin.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
            lastLogin.DefaultCellStyle.NullValue = "never";
            grid.Columns.Add(lastLogin);
        }

        private void LoadRows()
        {
            try
            {
                loaded = service.List(currentUser);

                grid.DataSource = loaded.Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.FullName,
                    Role = u.Role.ToString(),
                    u.IsActive,
                    u.LastLoginAt
                }).ToList();

                int inactive = loaded.Count(u => !u.IsActive);
                lblSummary.Text = loaded.Count + " users, " + inactive + " inactive (shown in grey)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Users", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddUser()
        {
            using (var editor = new UserEditorForm(currentUser))
            {
                if (editor.ShowDialog(this) == DialogResult.OK) LoadRows();
            }
        }

        private void EditSelected()
        {
            if (grid.CurrentRow == null)
            {
                MessageBox.Show("Select a user first.", "Users");
                return;
            }

            int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
            var selected = loaded.FirstOrDefault(u => u.Id == id);
            if (selected == null) return;

            using (var editor = new UserEditorForm(currentUser, selected))
            {
                if (editor.ShowDialog(this) == DialogResult.OK) LoadRows();
            }
        }

        private void ResetSelected()
        {
            if (grid.CurrentRow == null)
            {
                MessageBox.Show("Select a user first.", "Users");
                return;
            }

            int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
            var selected = loaded.FirstOrDefault(u => u.Id == id);
            if (selected == null) return;

            using (var form = new ResetPasswordForm(currentUser, selected))
            {
                form.ShowDialog(this);
            }
        }

        // Grey text for inactive users
        private void StyleRows()
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                bool active = (bool)row.Cells["IsActive"].Value;
                row.DefaultCellStyle.ForeColor = active ? Color.Black : Color.Gray;
            }
        }
    }
}