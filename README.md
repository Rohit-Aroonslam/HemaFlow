# HemaFlow — Haematology Laboratory Management System

A web application for a haematology laboratory. Patients register, keep a medical profile, control which doctors may see their results, and view, track and download their results as PDF reports. Doctors request tests, lab technicians capture and verify results, and lab managers run the test catalogue, stock and suppliers. Built with **ASP.NET Core MVC**.

Developed as part of **ONP400** at Nelson Mandela University (Group GRP-04-13).

## Background

Laboratories need a reliable way to move a test from request, to sample, to verified result, while keeping patient data private. HemaFlow replaces manual processes with one web system where all five portals read and write the same shared SQL Server database, so a patient is the same person on every screen. Patients decide which doctors may see their results through a consent system.

## Subsystems

| Subsystem | Description |
|---|---|
| **Admin** | Maintains master medical data (conditions, allergies, medication), user accounts and exports |
| **Patient** | Registration, medical profile, doctor consent, and viewing, tracking and downloading results as PDF |
| **Doctor** | Requests tests for patients, views consented patient data, and receives results |
| **Lab Technician** | Receives or rejects samples, captures and verifies results, and uses consumables from stock |
| **Lab Manager** | Manages the test catalogue, stock, suppliers, orders, staff and reports |

## Actors / Users
- Administrator
- Super Administrator
- Patient
- Doctor
- Lab Manager
- Lab Technician

## Key Features
- **Role-based authorization** so each actor only reaches their own portal
- **Two-factor authentication** with an authenticator app (hand-written TOTP, RFC 6238) and backup codes
- **Patient consent control** over which doctors may see their results
- **Unique sample barcodes** and consumable deduction from stock when tests are run
- **PDF reports** for patients, admins, doctors and the lab, produced with QuestPDF
- **Result tracking charts** showing each result over time against normal ranges
- **Email notifications** for password resets, temporary passwords and doctor access, sent through Brevo SMTP
- **South African address dropdowns** (Province, Town, Suburb) built from GeoNames postal data

## Tech Stack
- **Backend:** ASP.NET Core MVC (.NET 8, C#)
- **Data access:** Entity Framework Core 8 (database-first)
- **Frontend:** Razor views, Bootstrap 5, Bootstrap Icons, JavaScript, Chart.js, QRCode.js
- **Database:** SQL Server 2022
- **Reports:** QuestPDF
- **Email:** Brevo SMTP

## Demo Logins

Use these accounts to sign in to each portal of the system.

| Role | Email | Password |
|---|---|---|
| Lab Manager | m.vonbuchenroder@hemaflow.co.za | `LabMan@2026` |
| Doctor | s.ndlovu@hemaflow.co.za | `$.Ndl0vu` |
| Lab Technician | b.beya@hemaflow.co.za | `LabTech#2026` |
| Patient | zayd.ibrahim89@gmail.com | `Patient@2026` |
| Admin | s.naidoo@hemaflow.co.za | `Admin@2026` |
| Super Admin | superadmin@hemaflow.co.za | `SuperAdmin@2026!` |

## Setup
1. Clone the repo
2. Add your own connection string and SMTP credentials through User Secrets (or a local settings file that is not committed). See `appsettings.json` for the expected structure:
   ```
   dotnet user-secrets set "ConnectionStrings:HemaFlowDb" "<connection string>"
   dotnet user-secrets set "Smtp:Username" "<smtp username>"
   dotnet user-secrets set "Smtp:Password" "<smtp key>"
   ```
3. Create the database and run the SQL scripts, then optionally run `seed_preload_data_gaps.sql` for sample data
4. `dotnet run`

## Credits
- Prof Nicky Mostert - Project coordinator, Nelson Mandela University
- Amity Brown - Admin & Patient Subsystems
- Rohit Aroonslam - Doctor Subsystem
- Marcellos Von Buchenroder - Lab Manager Subsystem
- Bantu-Bethu Beya - Lab Technician Subsystem
