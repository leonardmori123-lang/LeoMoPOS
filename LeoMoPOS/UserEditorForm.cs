using System;
using System.Drawing;
using System.Windows.Forms;
using LeoMoPOS.Domain;
using LeoMoPOS.Services;

namespace LeoMoPOS
{
    public sealed class UserEditorForm : Form
    {
        private const string AddUserTitle = "Add User";
        private const string EditUserTitle = "Edit User";
        private const string ValidationDialogTitle = "User validation";

        private readonly User currentUser;
        private readonly User existingUser;

        private TextBox usernameTextBox;
        private TextBox fullNameTextBox;
        private TextBox passwordTextBox;
        private TextBox confirmTextBox;
        private ComboBox roleComboBox;
        private CheckBox activeCheckBox;
        private Button cancelButton;
        private Button saveButton;

        public UserEditorForm(User actor, User existingUser = null)
        {
            this.currentUser = actor;
            this.existingUser = existingUser;

            bool isNew = (this.existingUser == null);
            bool editingSelf = (!isNew && this.existingUser.Id == this.currentUser.Id);

            InitializeFormProperties(isNew);
            InitializeControls();
            BuildLayout(isNew, editingSelf);
            PopulateInitialValues(isNew, editingSelf);
            AttachEvents();
        }

        private void InitializeFormProperties(bool isNew)
        {
            if (isNew)
            {
                Text = AddUserTitle;
                Height = 360;
            }
            else
            {
                Text = EditUserTitle;
                Height = 330;
            }

            Width = 480;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
        }

        private void InitializeControls()
        {
            usernameTextBox = new TextBox();
            usernameTextBox.MaxLength = 50;

            fullNameTextBox = new TextBox();
            fullNameTextBox.MaxLength = 100;

            passwordTextBox = new TextBox();
            passwordTextBox.MaxLength = 100;
            passwordTextBox.UseSystemPasswordChar = true;

            confirmTextBox = new TextBox();
            confirmTextBox.MaxLength = 100;
            confirmTextBox.UseSystemPasswordChar = true;

            roleComboBox = new ComboBox();
            roleComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            roleComboBox.DataSource = Enum.GetValues(typeof(UserRole));

            activeCheckBox = new CheckBox();
            activeCheckBox.Text = "Active";
            activeCheckBox.Checked = true;
            activeCheckBox.AutoSize = true;

            cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            saveButton = new Button();
            saveButton.Text = "Save";
        }

        private void BuildLayout(bool isNew, bool editingSelf)
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(18);
            table.ColumnCount = 3;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            int row = 0;
            AddRow(table, "Username", usernameTextBox, row);
            row++;

            AddRow(table, "Full name", fullNameTextBox, row);
            row++;

            if (isNew)
            {
                AddRow(table, "Password", passwordTextBox, row);
                row++;

                AddRow(table, "Confirm password", confirmTextBox, row);
                row++;
            }

            AddRow(table, "Role", roleComboBox, row);
            row++;

            if (!isNew)
            {
                table.Controls.Add(activeCheckBox, 1, row);
                row++;
            }

            if (editingSelf)
            {
                Label noteLabel = new Label();
                noteLabel.Text = "This is your own account, so the role and active setting are locked.";
                noteLabel.AutoSize = true;
                noteLabel.MaximumSize = new Size(300, 0);
                noteLabel.ForeColor = Color.DimGray;
                table.Controls.Add(noteLabel, 1, row);
                row++;
            }

            table.Controls.Add(cancelButton, 1, row);
            table.Controls.Add(saveButton, 2, row);
            table.RowCount = row + 1;

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            Controls.Add(table);
        }

        private void PopulateInitialValues(bool isNew, bool editingSelf)
        {
            if (isNew)
            {
                roleComboBox.SelectedItem = UserRole.Cashier;
            }
            else
            {
                usernameTextBox.Text = existingUser.Username;
                usernameTextBox.ReadOnly = true;
                fullNameTextBox.Text = existingUser.FullName;
                roleComboBox.SelectedItem = existingUser.Role;
                activeCheckBox.Checked = existingUser.IsActive;

                if (editingSelf)
                {
                    roleComboBox.Enabled = false;
                    activeCheckBox.Enabled = false;
                }
            }
        }

        private void AttachEvents()
        {
            saveButton.Click += SaveButton_Click;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (existingUser == null && passwordTextBox.Text != confirmTextBox.Text)
                {
                    throw new InvalidOperationException("The two passwords do not match.");
                }

                User user = new User();
                if (existingUser == null)
                {
                    user.Id = 0;
                    user.IsActive = true;
                }
                else
                {
                    user.Id = existingUser.Id;
                    user.IsActive = activeCheckBox.Checked;
                }

                user.Username = usernameTextBox.Text;
                user.FullName = fullNameTextBox.Text;
                user.Role = (UserRole)roleComboBox.SelectedItem;

                UserService service = new UserService();
                if (existingUser == null)
                {
                    service.Create(currentUser, user, passwordTextBox.Text);
                }
                else
                {
                    service.Update(currentUser, user);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ValidationDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

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
