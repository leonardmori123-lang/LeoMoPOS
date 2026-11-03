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
        private const string FormTitle = "Reset Password";

        private readonly User currentUser;
        private readonly User targetUser;

        private TextBox passwordTextBox;
        private TextBox confirmTextBox;
        private Button cancelButton;
        private Button saveButton;

        // Creates the reset password form for the given user.
        public ResetPasswordForm(User actor, User targetUser)
        {
            this.currentUser = actor;
            this.targetUser = targetUser;

            InitializeFormProperties();
            InitializeInputControls();
            BuildLayout();
        }

        // Sets the basic window properties.
        private void InitializeFormProperties()
        {
            Text = FormTitle;
            Width = 460;
            Height = 290;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
        }

        // Creates the password text boxes.
        private void InitializeInputControls()
        {
            passwordTextBox = new TextBox();
            passwordTextBox.MaxLength = 100;
            passwordTextBox.UseSystemPasswordChar = true;

            confirmTextBox = new TextBox();
            confirmTextBox.MaxLength = 100;
            confirmTextBox.UseSystemPasswordChar = true;
        }

        // Builds the table layout and adds all child controls.
        private void BuildLayout()
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(18);
            table.ColumnCount = 3;
            table.RowCount = 5;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            Label headerLabel = new Label();
            headerLabel.Text = "New password for " + targetUser.Username + " (" + targetUser.FullName + ")";
            headerLabel.UseMnemonic = false;
            headerLabel.AutoSize = true;
            headerLabel.MaximumSize = new Size(390, 0);
            headerLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            headerLabel.Margin = new Padding(3, 3, 3, 12);
            table.Controls.Add(headerLabel, 0, 0);
            table.SetColumnSpan(headerLabel, 3);

            AddRow(table, "New password", passwordTextBox, 1);
            AddRow(table, "Confirm password", confirmTextBox, 2);

            Label hintLabel = new Label();
            hintLabel.Text = "At least 8 characters.";
            hintLabel.AutoSize = true;
            hintLabel.ForeColor = Color.DimGray;
            table.Controls.Add(hintLabel, 1, 3);

            cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Click += SaveButton_Click;

            table.Controls.Add(cancelButton, 1, 4);
            table.Controls.Add(saveButton, 2, 4);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            Controls.Add(table);
        }

        // Validates input and saves the new password.
        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (passwordTextBox.Text != confirmTextBox.Text)
                {
                    throw new InvalidOperationException("The two passwords do not match.");
                }

                UserService userService = new UserService();
                userService.ResetPassword(currentUser, targetUser.Id, passwordTextBox.Text);

                MessageBox.Show(
                    "Password changed for " + targetUser.Username + ".",
                    FormTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, FormTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
