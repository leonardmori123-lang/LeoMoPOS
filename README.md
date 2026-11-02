# EnterprisePOS

Enterprise POS & Inventory Management System. A Windows desktop point-of-sale and stock program written in C# (Windows Forms, .NET Framework 4.8) on SQL Server. It was built as a class project, following a step-by-step tutorial, on the database script supplied by the instructor.

## What it does

- **Sign-in with roles.** Admin, Manager and Cashier. Menus follow the role, and the service layer checks the role again, so a hidden menu is not the only protection.
- **Products and stock.** Search by SKU, name or barcode. Add, edit and deactivate products. Adjust stock with a reason; each change is written to the inventory log. Low-stock rows are shaded.
- **Checkout.** Scan or type a SKU or barcode, edit quantities, apply a discount, add 12% tax, pay by Cash, Card or Mobile, and see the change due. The order and the stock change are saved in one SQL transaction, so two terminals cannot sell the same last item.
- **Receipts.** Preview or print an 80 mm receipt for the last completed order.
- **Sales analytics** (Admin, Manager). Daily sales, top products, sales by category and current low stock for a date range, plus dashboard tiles on the main window.
- **User administration** (Admin). Add and edit users, activate or deactivate them, reset passwords, see the last login.
- **Error handling.** Unexpected errors are written to a log file, and the user sees a short message with a reference number. Logs are in `%LocalAppData%\EnterprisePOS\Logs`; files older than 30 days are deleted at start-up.

## Roles

| Role | Can use |
|---|---|
| Admin | Everything, including user administration |
| Manager | New Order, Products and stock, Sales Analytics |
| Cashier | New Order only |

## Requirements

- Windows 10 or 11
- Visual Studio 2026 with the ".NET desktop development" workload. The solution file uses the newer `.slnx` format, so older Visual Studio versions may not open it.
- .NET Framework 4.8 Developer Pack
- SQL Server Express (or another SQL Server instance) and SQL Server Management Studio

## Project layout

```text
EnterprisePOS.slnx
Database/                   SQL scripts, run in numeric order
EnterprisePOS.Domain/       Entities.cs: User, Product, Order, CartLine, report rows
EnterprisePOS.Data/         Db.cs, Security.cs, Repositories.cs: connection, password hashing, stored-procedure calls
EnterprisePOS.Services/     Services.cs: business rules and role checks
EnterprisePOS/              The Windows Forms app (startup project)
  Program.cs                Start-up, global error handler, log file
  LoginForm.cs, MainForm.cs
  ProductForm.cs, ProductEditorForm.cs, StockAdjustForm.cs
  CheckoutForm.cs, ReceiptPrinter.cs
  AnalyticsForm.cs
  UserAdminForm.cs, UserEditorForm.cs, ResetPasswordForm.cs
  App.config                Connection string
  App.config.example        The same file with placeholders
```

Rule of the layers: a form never runs SQL, and a repository never shows a message box. Forms call services, services call repositories, repositories call stored procedures.

## Set up the database

1. Open SQL Server Management Studio and connect to your instance. The default configuration uses `.\SQLEXPRESS`.
2. Open and run the scripts in the `Database` folder, in this order:
   1. `01_CreateDatabase.sql`: the instructor's script. Creates the `PosDB` database, tables, seed data and stored procedures.
   2. `02_Additions.sql`: category list and product search.
   3. `03_Additions.sql`: reads a saved order back for the receipt.
   4. `04_Additions.sql`: user administration procedures.

   Scripts 02 to 04 can be run more than once.
3. Check that `PosDB` appears under Databases.

## Configure the connection string

The program reads the `EnterprisePOS` connection string from `EnterprisePOS/App.config`, and after a build from `EnterprisePOS.exe.config` next to the exe.

The default uses Windows authentication on the local SQL Express instance and contains no password:

```xml
<add name="EnterprisePOS"
     connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=PosDB;Integrated Security=True;TrustServerCertificate=True;"
     providerName="System.Data.SqlClient" />
```

For another server, start from `App.config.example` and replace `YOUR_SQL_SERVER`. On a computer that only runs the built program, editing the `.exe.config` is enough and no rebuild is needed.

## Build and run

1. Open `EnterprisePOS.slnx` in Visual Studio.
2. Set `EnterprisePOS` as the startup project and press F5.

To build a copy that runs without Visual Studio:

1. Switch the configuration to **Release**, then choose Build > Clean Solution and Build > Rebuild Solution.
2. Copy everything in `EnterprisePOS\bin\Release` (the exe, `EnterprisePOS.exe.config` and all DLLs) to the other computer. Copying the exe alone does not work.
3. Edit `EnterprisePOS.exe.config` there if the database is on a different server.

## Demo accounts

The database script creates these accounts for testing:

| Username | Password | Role |
|---|---|---|
| admin | Admin@123 | Admin |
| manager | Manager@123 | Manager |
| cashier | Cashier@123 | Cashier |

These are shared demo passwords. Change them (Administration > Users > Reset Password) before the program is used with real data.

## Keeping secrets out of Git

Do not commit real database passwords, `.exe.config` files that contain them, backups (`*.bak`), logs or customer data. `.gitignore` covers build output, backups, logs and PDF files. The instructor's handouts are not part of this repository.

## License

Class project. Not licensed for redistribution.
