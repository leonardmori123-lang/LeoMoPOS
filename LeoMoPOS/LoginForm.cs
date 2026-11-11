using System;
using System.Drawing;
using System.Windows.Forms;
using LeoMoPOS.Services;

namespace LeoMoPOS
{
    public sealed class LoginForm : Form
    {
        private readonly TextBox txtUsername = new TextBox();
        private readonly TextBox txtPassword = new TextBox();
        private readonly Button btnLogin = new Button();
        private readonly Label lblStatus = new Label();

        public LoginForm()
        {
            Text = "Enterprise POS - Login";
            Width = 430;
            Height = 280;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(32),
                ColumnCount = 2,
                RowCount = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            txtPassword.PasswordChar = '●';
            btnLogin.Text = "Sign in";
            btnLogin.AutoSize = true;
            btnLogin.Click += LoginClicked;
            AcceptButton = btnLogin;

            AddRow(layout, "Username", txtUsername, 0);
            AddRow(layout, "Password", txtPassword, 1);
            layout.Controls.Add(btnLogin, 1, 2);

            lblStatus.ForeColor = Color.Firebrick;
            lblStatus.AutoSize = true;
            layout.Controls.Add(lblStatus, 0, 3);
            layout.SetColumnSpan(lblStatus, 2);

            Controls.Add(layout);
            ActiveControl = txtUsername;
        }

        private static void AddRow(TableLayoutPanel layout, string caption, Control control, int row)
        {
            layout.Controls.Add(new Label { Text = caption, AutoSize = true }, 0, row);
            control.Dock = DockStyle.Top;
            layout.Controls.Add(control, 1, row);
        }

        private void LoginClicked(object sender, EventArgs e)
        {
            var username = txtUsername.Text.Trim();
            if (username.Length == 0 || txtPassword.Text.Length == 0)
            {
                lblStatus.Text = "Enter your username and password.";
                return;
            }

            try
            {
                var user = new AuthService().Login(username, txtPassword.Text);
                if (user == null)
                {
                    lblStatus.Text = "Invalid credentials or inactive user.";
                    txtPassword.Clear();
                    txtPassword.Focus();
                    return;
                }

                lblStatus.Text = string.Empty;
                Hide();
                using (var main = new MainForm(user))
                {
                    main.ShowDialog(this);
                }
                txtPassword.Clear();
                Show();
                txtPassword.Focus();
            }
            catch (Exception ex)
            {
                AppLog.Error("Sign-in failed", ex);
                lblStatus.Text = "Unable to connect. Contact the administrator.";

                if (!Visible)
                {
                    Show();
                }
            }
        }
    }
}