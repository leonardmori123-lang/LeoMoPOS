# LeoMoPOS

Enterprise POS & Inventory Management System. A Windows desktop point-of-sale and stock program written in C# (Windows Forms, .NET Framework 4.8). It was built as a class project, following a step-by-step tutorial, on the database design supplied by the instructor. The program carries its own SQLite database inside the exe, so it runs on a Windows PC with nothing else installed.

## Run the program (no setup)

1. Open the **Releases** page of this repository and download `LeoMoPOS.exe`.
2. Double-click it. Nothing else has to be installed. Windows 10 and 11 already include .NET Framework 4.8.
3. Sign in with a demo account from the table below, for example `admin` / `Admin@123`.

Windows SmartScreen may warn about the exe, since the program is not code-signed. Choose **More info**, then **Run anyway**.

The first start creates the database file `%LocalAppData%\LeoMoPOS\mori.db` and fills it with sample data: 3 users, 5 categories and 13 products. To start over with fresh sample data, close the program and delete `mori.db`. The next start builds it again.

## What it does

- **Sign-in with roles.** Admin, Manager and Cashier. Menus follow the role, and the service layer checks the role again, so a hidden menu is not the only protection.
- **Products and stock.** Search by SKU, name or barcode. Add, edit and deactivate products. Adjust stock with a reason; each change is written to the inventory log. Low-stock rows are shaded.
- **Checkout.** Scan or type a SKU or barcode, edit quantities, apply a discount, add 12% tax, pay by Cash, Card or Mobile, and see the change due. The order and the stock change are saved in one database transaction, so two copies of the program cannot sell the same last item.
- **Receipts.** Preview or print an 80 mm receipt for the last completed order.
- **Sales analytics** (Admin, Manager). Daily sales, top products, sales by category and current low stock for a date range, plus dashboard tiles on the main window.
- **User administration** (Admin). Add and edit users, activate or deactivate them, reset passwords, see the last login.
- **Error handling.** Unexpected errors are written to a log file, and the user sees a short message with a reference number. Logs are in `%LocalAppData%\LeoMoPOS\Logs`; files older than 30 days are deleted at start-up.

## Roles

| Role | Can use |
|---|---|
| Admin | Everything, including user administration |
| Manager | New Order, Products and stock, Sales Analytics |
| Cashier | New Order only |

## Demo accounts

The sample data creates these accounts for testing:

| Username | Password | Role |
|---|---|---|
| admin | Admin@123 | Admin |
| manager | Manager@123 | Manager |
| cashier | Cashier@123 | Cashier |

These are shared demo passwords. Change them (Administration > Users > Reset Password) before the program is used with real data.

## The built-in database

- The database is one SQLite file, `%LocalAppData%\LeoMoPOS\mori.db`. It is created on the first start from `LeoMoPOS.Data/PosDatabase.sql`, which is embedded in the program.
- `PosDatabase.sql` is the instructor's PosDB tables and sample data translated to SQLite. The stored procedures of the original became SQL statements in `Repositories.cs`, and their error messages are unchanged.
- Every write runs in a `BEGIN IMMEDIATE` transaction. If a second copy of the program writes at the same moment, it waits up to 10 seconds and then sees the updated stock.
- SQLite needs a native file, `SQLite.Interop.dll`. The x86 and x64 versions are embedded in the program (`LeoMoPOS.Data/Native`) and unpacked to `%LocalAppData%\LeoMoPOS\native` on the first start.
- The data stays on the PC that runs the program. Copies on other PCs have their own databases.
- The `Database` folder holds the instructor's original SQL Server scripts, kept as a reference. The database name in them is changed to `mori`, following the class naming rule. The program does not use them.

## Requirements

To run the exe:

- Windows 10 or 11

To build from source:

- Visual Studio 2026 with the ".NET desktop development" workload. The solution file uses the newer `.slnx` format, so older Visual Studio versions may not open it.
- .NET Framework 4.8 Developer Pack
- Internet access on the first build, so Visual Studio can restore the NuGet packages (`System.Data.SQLite.Core`, `Costura.Fody`)

After a fresh clone, the first build restores the NuGet packages, but Costura.Fody does not merge the DLLs in that same session. Build once, close and reopen the solution, then rebuild. `LeoMoPOS.exe` in `bin\Release` should then be about 2 MB. An exe of about 59 KB means the merge did not happen.

## Project layout

```text
LeoMoPOS.slnx
Database/                   The instructor's original SQL Server scripts (reference only)
LeoMoPOS.Domain/       Entities.cs: User, Product, Order, CartLine, report rows
LeoMoPOS.Data/         The built-in database and the repositories
  SqliteDb.cs               Opens mori.db, creates it on the first start, write transactions
  PosDatabase.sql           Tables and sample data (embedded in the program)
  NativeLibraries.cs        Unpacks and loads SQLite's native file from inside the exe
  Native/x86, Native/x64    SQLite.Interop.dll, SQLite's own native file (embedded)
  Repositories.cs           SQL for users, products, orders and reports
  Security.cs               Password hashing
LeoMoPOS.Services/     Services.cs: business rules and role checks
LeoMoPOS/              The Windows Forms app (startup project)
  Program.cs                Start-up, global error handler, log file
  LoginForm.cs, MainForm.cs
  ProductForm.cs, ProductEditorForm.cs, StockadjustForm.cs
  CheckoutForm.cs, ReceiptPrinter.cs
  AnalyticsForm.cs
  UserAdminForm.cs, UserEditorForm.cs, ResetPasswordForm.cs
  FodyWeavers.xml           Costura.Fody settings (merges the DLLs into the exe)
  App.config                Runtime version only
```

Rule of the layers: a form never runs SQL, and a repository never shows a message box. Forms call services, services call repositories, repositories run SQL with parameters against the built-in database.

## Build from source

1. Open `LeoMoPOS.slnx` in Visual Studio.
2. Set `LeoMoPOS` as the startup project and press F5.

To build the single exe:

1. Switch the configuration to **Release**, then choose Build > Rebuild Solution.
2. Take `LeoMoPOS\bin\Release\LeoMoPOS.exe`. Costura.Fody merges the DLLs into it and SQLite's native files travel inside it, so this one file is all that has to be copied to another PC.

## Repository notes

- This repository is private. The instructor's handouts (PDF files) are not part of it.
- `.gitignore` covers build output, `packages/`, backups, logs and PDF files.
- The two `SQLite.Interop.dll` files under `LeoMoPOS.Data/Native` are SQLite's own binaries. They are committed on purpose, since the program embeds them.

## License

Class project. Not licensed for redistribution.
