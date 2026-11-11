using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LeoMoPOS.Domain;
using LeoMoPOS.Services;

namespace LeoMoPOS
{
    public sealed class UserAdminForm : Form
    {
        private const string FormTitle = "User & Role Management";
        private const string DialogTitle = "Users";

        private readonly User currentUser;
        private readonly UserService userService = new UserService();
        private List<User> loadedUsers = new List<User>();

        private Label summaryLabel;
        private DataGridView userGrid;
        private FlowLayoutPanel commandPanel;
        private Button addButton;
        private Button editButton;
        private Button resetPasswordButton;

        public UserAdminForm(User user)
        {
            this.currentUser = user;

            InitializeFormProperties();
            InitializeControls();
            BuildColumns();
            BuildLayout();
            AttachEvents();
        }

        private void InitializeFormProperties()
        {
            Text = FormTitle;
            Width = 850;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
        }

        private void InitializeControls()
        {
            summaryLabel = new Label();
            summaryLabel.Dock = DockStyle.Bottom;
            summaryLabel.Height = 28;
            summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
            summaryLabel.Padding = new Padding(8, 0, 0, 0);

            userGrid = new DataGridView();
            userGrid.Dock = DockStyle.Fill;
            userGrid.BackgroundColor = Color.White;
            userGrid.ReadOnly = true;
            userGrid.AllowUserToAddRows = false;
            userGrid.AllowUserToDeleteRows = false;
            userGrid.AutoGenerateColumns = false;
            userGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            userGrid.MultiSelect = false;
            userGrid.RowHeadersVisible = false;
            userGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            commandPanel = new FlowLayoutPanel();
            commandPanel.Dock = DockStyle.Top;
            commandPanel.Height = 48;
            commandPanel.Padding = new Padding(8);

            addButton = new Button();
            addButton.Text = "Add User";
            addButton.AutoSize = true;

            editButton = new Button();
            editButton.Text = "Edit";
            editButton.AutoSize = true;

            resetPasswordButton = new Button();
            resetPasswordButton.Text = "Reset Password";
            resetPasswordButton.AutoSize = true;
        }

        private void BuildColumns()
        {
            DataGridViewTextBoxColumn idColumn = new DataGridViewTextBoxColumn();
            idColumn.Name = "Id";
            idColumn.DataPropertyName = "Id";
            idColumn.Visible = false;
            userGrid.Columns.Add(idColumn);

            DataGridViewTextBoxColumn usernameColumn = new DataGridViewTextBoxColumn();
            usernameColumn.Name = "Username";
            usernameColumn.HeaderText = "Username";
            usernameColumn.DataPropertyName = "Username";
            usernameColumn.FillWeight = 25;
            userGrid.Columns.Add(usernameColumn);

            DataGridViewTextBoxColumn fullNameColumn = new DataGridViewTextBoxColumn();
            fullNameColumn.Name = "FullName";
            fullNameColumn.HeaderText = "Full name";
            fullNameColumn.DataPropertyName = "FullName";
            fullNameColumn.FillWeight = 35;
            userGrid.Columns.Add(fullNameColumn);

            DataGridViewTextBoxColumn roleColumn = new DataGridViewTextBoxColumn();
            roleColumn.Name = "Role";
            roleColumn.HeaderText = "Role";
            roleColumn.DataPropertyName = "Role";
            roleColumn.FillWeight = 15;
            userGrid.Columns.Add(roleColumn);

            DataGridViewCheckBoxColumn activeColumn = new DataGridViewCheckBoxColumn();
            activeColumn.Name = "IsActive";
            activeColumn.HeaderText = "Active";
            activeColumn.DataPropertyName = "IsActive";
            activeColumn.FillWeight = 10;
            userGrid.Columns.Add(activeColumn);

            DataGridViewTextBoxColumn lastLoginColumn = new DataGridViewTextBoxColumn();
            lastLoginColumn.Name = "LastLoginAt";
            lastLoginColumn.HeaderText = "Last login";
            lastLoginColumn.DataPropertyName = "LastLoginAt";
            lastLoginColumn.FillWeight = 25;
            lastLoginColumn.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
            lastLoginColumn.DefaultCellStyle.NullValue = "never";
            userGrid.Columns.Add(lastLoginColumn);
        }

        private void BuildLayout()
        {
            commandPanel.Controls.Add(addButton);
            commandPanel.Controls.Add(editButton);
            commandPanel.Controls.Add(resetPasswordButton);

            Controls.Add(userGrid);
            Controls.Add(summaryLabel);
            Controls.Add(commandPanel);
        }

        private void AttachEvents()
        {
            addButton.Click += AddButton_Click;
            editButton.Click += EditButton_Click;
            resetPasswordButton.Click += ResetPasswordButton_Click;
            userGrid.CellDoubleClick += UserGrid_CellDoubleClick;
            userGrid.DataBindingComplete += UserGrid_DataBindingComplete;
            Load += UserAdminForm_Load;
        }

        private void UserAdminForm_Load(object sender, EventArgs e)
        {
            LoadRows();
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            AddUser();
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void ResetPasswordButton_Click(object sender, EventArgs e)
        {
            ResetSelected();
        }

        private void UserGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                EditSelected();
            }
        }

        private void UserGrid_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            StyleRows();
        }

        private void LoadRows()
        {
            try
            {
                loadedUsers = userService.List(currentUser);

                List<UserRowViewModel> rowList = new List<UserRowViewModel>();
                int inactiveCount = 0;

                foreach (User user in loadedUsers)
                {
                    UserRowViewModel viewModel = new UserRowViewModel();
                    viewModel.Id = user.Id;
                    viewModel.Username = user.Username;
                    viewModel.FullName = user.FullName;
                    viewModel.Role = user.Role.ToString();
                    viewModel.IsActive = user.IsActive;
                    viewModel.LastLoginAt = user.LastLoginAt;
                    rowList.Add(viewModel);

                    if (!user.IsActive)
                    {
                        inactiveCount++;
                    }
                }

                userGrid.DataSource = rowList;
                summaryLabel.Text = loadedUsers.Count + " users, " + inactiveCount + " inactive (shown in grey)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddUser()
        {
            using (UserEditorForm editor = new UserEditorForm(currentUser))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    LoadRows();
                }
            }
        }

        private void EditSelected()
        {
            if (userGrid.CurrentRow == null)
            {
                MessageBox.Show("Select a user first.", DialogTitle);
                return;
            }

            int id = Convert.ToInt32(userGrid.CurrentRow.Cells["Id"].Value);
            User selected = FindLoadedUserById(id);
            if (selected == null)
            {
                return;
            }

            using (UserEditorForm editor = new UserEditorForm(currentUser, selected))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    LoadRows();
                }
            }
        }

        private void ResetSelected()
        {
            if (userGrid.CurrentRow == null)
            {
                MessageBox.Show("Select a user first.", DialogTitle);
                return;
            }

            int id = Convert.ToInt32(userGrid.CurrentRow.Cells["Id"].Value);
            User selected = FindLoadedUserById(id);
            if (selected == null)
            {
                return;
            }

            using (ResetPasswordForm form = new ResetPasswordForm(currentUser, selected))
            {
                form.ShowDialog(this);
            }
        }

        private User FindLoadedUserById(int userId)
        {
            foreach (User user in loadedUsers)
            {
                if (user.Id == userId)
                {
                    return user;
                }
            }
            return null;
        }

        private void StyleRows()
        {
            foreach (DataGridViewRow row in userGrid.Rows)
            {
                bool active = (bool)row.Cells["IsActive"].Value;
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

        private sealed class UserRowViewModel
        {
            public int Id { get; set; }
            public string Username { get; set; }
            public string FullName { get; set; }
            public string Role { get; set; }
            public bool IsActive { get; set; }
            public DateTime? LastLoginAt { get; set; }
        }
    }
}
