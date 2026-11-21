# Flock Weight Management & Analysis System — Phases 1 to 5

ASP.NET MVC 5.2 on .NET Framework 4.8 · C# 7.3 · Bootstrap 5.3 · jQuery · SQL Server 2016+ · ADO.NET + stored procedures

> "ASP.NET MVC 4.8" in the specification = **ASP.NET MVC 5.2.9 running on .NET Framework 4.8**, the current MVC release for that framework.

## What is in the solution

| Phase | Area | Delivered |
|---|---|---|
| 1 | Database | Normalised schema (masters, weekly transactions, weight samples, feed, nutrition, mortality, transfer, adjustment, alerts, score, audit), PK/FK/CHECK/UNIQUE constraints, indexes |
| 1 | Security & admin | Login / logout, PBKDF2-SHA256 hashing, lock-out, forced password change, permission-based menus and buttons, farm-level data restriction, Users, Roles & permission matrix, Audit Trail |
| 1 | Masters | Farm, House, Bird Type, Breed, Lot, Feed Type, Flock (with initial placement) |
| 2 | Standards | Age/week standard per bird type, breed (optional), feed type (optional), parameter, effective date; Min/Max and critical limits; week grid editor with paste-from-Excel and "fill limits ± %" |
| 2 | Movements | Additional placements, transfers (in / out / house move, with mirror entry on the other flock), authorised population adjustments — each recalculates the population chain |
| 2 | Weekly Flock Entry | One screen, five tabs: population & mortality (with reasons), body weight samples, feed lines, nutrition, live summary. Weeks must be entered in order; only the latest week can be deleted |
| 3 | Weight & uniformity | Individual bird weights per sex → average, min, max, SD, CV %, uniformity % (± range from settings), histogram, grade |
| 3 | Feed & nutrition | Feed lines by type / formula / sex with wastage → g/bird/day on bird-days; nutrients from the feed formula's declared values **or** lab analysis; Feed Formula master |
| 4 | Analysis | Flock Weight Management Analysis (sections A–F + standard-vs-actual table with previous-week comparison), Standard vs Actual for all flocks, Weight / Feed / Nutrition / Uniformity / Mortality focus screens, Trend, Flock Comparison, Exceptions |
| 4 | Score & alerts | Weighted performance score with configurable components and bands; alerts generated on save (body weight, gain, feed, nutrition, mortality, uniformity, score); Alert Center with acknowledge / close and action taken; bell counter in the top bar |
| 4 | Dashboard | Filters (farm, house, bird type, flock, lot, week, as-of date), 10 KPI cards, 8 Chart.js charts (click a point to open that week), latest-week matrix of every active flock |
| 5 | Reports | Weekly Performance, Flock Weight, Feed, Mortality, Nutrition, Uniformity, Standard vs Actual, Flock Comparison, Farm-wise, Age-wise — on screen and exported to **Excel (.xlsx), PDF and CSV** (no third-party libraries) |
| 5 | Configuration | Tolerances, score weights & bands, system settings, module switches — all from one screen |
| 6 | Production & environment | Weekly entry tab for total / hatching eggs, egg weight, lighting hours and intensity, day / night temperature; Hatch Results screen (eggs set, infertile, chicks hatched) giving fertility % and hatchability % of the egg week; standards, statuses and alerts for all of them; **Flock Production Performance Analysis** sheet (24 rows: bird type to temperature, STD vs ACT) and report with Excel / PDF / CSV |
| 5 | JSON API | Read-only `api/v1/...` endpoints for a future mobile app (see below) |

## Solution layout

```
FlockManagement.sln
Database/
  01_Schema.sql                  database + all tables, keys, indexes
  02_StoredProcedures_Phase1.sql security, audit, flock procedures
  03_SeedData.sql                roles, permissions, users, masters, parameters, settings, sample flocks
  04_Schema_Phase2to5.sql        extra columns / indexes for weekly entry and standards
  05_Procedures_Phase2_3.sql     functions, standards, movements, weekly save, population chain, uniformity
  06_Procedures_Phase4_5.sql     score & alerts, analysis, dashboard, trend, comparison, exceptions, reports
  07_SeedData_Phase2to5.sql      Phase 2-5 permissions/menus, SAMPLE standards (wk 1-66), feed formulas, 82 sample weeks
  08_Production.sql              production & environment: egg production, egg weight, fertility, hatchability,
                                 lighting, temperature (procedures, menus, SAMPLE standards, sample data)
FlockManagement/            the single ASP.NET MVC project (FlockManagement.csproj)
  Common/                    entities, constants, PasswordHasher, VarianceCalculator, export writers
  Data/                      SqlDb (ADO.NET), repositories, audit writer, SQL error translation
  Business/                  services (validation, rules, caching of permissions/settings)
  Controllers/ ViewModels/   MVC controllers and view models
  Views/ Content/ Scripts/   Razor views, Bootstrap UI, charts
  App_Start/ Infrastructure/ routing, auth, DI, HTML helpers
```

Layering: **Views → Controllers → Services (Business) → Repositories (Data) → SQL Server**. Controllers never touch SQL; services never touch HTTP (they receive `IUserContext`). Everything is in one project; the Common, Data and Business folders keep their own namespaces, so they can be split back into class libraries later if needed.

## Installation

### 1. Prerequisites
- Windows Server 2016+ / Windows 10+ with **IIS 10**, *ASP.NET 4.8* and *.NET Extensibility 4.8* features enabled
- **.NET Framework 4.8** runtime (developer pack for building)
- **SQL Server 2016 or later** (Express is fine). The scripts use `CREATE OR ALTER`, `FOR JSON` and `STRING_SPLIT` (database compatibility level 130+)
- Visual Studio 2019/2022 with the *ASP.NET and web development* workload

### 2. Create the database
Run the scripts in order in SSMS (or `sqlcmd`):

```
sqlcmd -S .\SQLEXPRESS -E -i Database\01_Schema.sql
sqlcmd -S .\SQLEXPRESS -E -i Database\02_StoredProcedures_Phase1.sql
sqlcmd -S .\SQLEXPRESS -E -i Database\03_SeedData.sql
sqlcmd -S .\SQLEXPRESS -E -i Database\04_Schema_Phase2to5.sql
sqlcmd -S .\SQLEXPRESS -E -i Database\05_Procedures_Phase2_3.sql
sqlcmd -S .\SQLEXPRESS -E -i Database\06_Procedures_Phase4_5.sql
sqlcmd -S .\SQLEXPRESS -E -i Database\07_SeedData_Phase2to5.sql
sqlcmd -S .\SQLEXPRESS -E -i Database\08_Production.sql
```

Scripts 01, 03, 04 and 07 refuse to run twice (no data is overwritten). Procedure scripts 02, 05 and 06 can be re-run at any time.
If you already installed Phase 1, run only 04 to 08.
If you already installed Phases 1-5, re-run **04, 05 and 06** (they are safe to re-run and add the production columns,
calculations, alerts and report), then run **08**.

> **The standards in 07 are SAMPLE values** for testing the screens. Replace them with your breed company's management-guide figures (Standards → Edit Standards Grid, or bulk insert into `tblStandard`) before using the system for real decisions. The 82 sample weeks on flocks F001–F003 deliberately contain problems (low uniformity, underweight, over-feeding, nutrient deviations, high mortality) so that every alert and report has something to show; delete those flocks before go-live.

### 3. Database login for IIS
With Windows authentication (recommended), give the application-pool identity access:

```sql
USE [master];
CREATE LOGIN [IIS APPPOOL\FlockManagement] FROM WINDOWS;
USE FlockManagementDB;
CREATE USER [IIS APPPOOL\FlockManagement] FOR LOGIN [IIS APPPOOL\FlockManagement];
ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\FlockManagement];
ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\FlockManagement];
GRANT EXECUTE TO [IIS APPPOOL\FlockManagement];
```

(For SQL authentication create a SQL login instead and use the commented connection string in `Web.config`.)

### 4. Build
1. Open `FlockManagement.sln` in Visual Studio.
2. Right-click the solution → **Restore NuGet Packages** (packages: Microsoft.AspNet.Mvc 5.2.9, Razor 3.2.9, WebPages 3.2.9, Microsoft.Web.Infrastructure 2.0.0, Newtonsoft.Json 13.0.3, Microsoft.CodeDom.Providers.DotNetCompilerPlatform 2.0.1).
3. Edit the `FlockDb` connection string in `FlockManagement/Web.config`.
4. Build (Ctrl+Shift+B) and run with IIS Express (F5) to test.

### 5. Publish to IIS
1. Right-click **FlockManagement** → **Publish** → *Folder* → Release.
2. In IIS Manager create an application pool **FlockManagement** (.NET CLR v4.0, Integrated pipeline).
3. Create a site or application pointing to the publish folder, using that pool.
4. Grant the pool identity **Modify** permission on `App_Data\Logs` (error logs).
5. Bind HTTPS, then set `requireSSL="true"` on `<forms>` and `<httpCookies>` in `Web.config`.
6. For more than one web server, add a fixed `<machineKey>` (IIS Manager → Machine Key → Generate Keys) so login cookies work on every server.

### 6. First login

| User | Password | Role |
|---|---|---|
| admin | Admin@123 | Administrator |
| manager | Demo@123 | Farm Manager |
| supervisor | Demo@123 | Farm Supervisor (restricted to Hosur farm) |
| nutritionist | Demo@123 | Nutritionist |
| vet | Demo@123 | Veterinarian |
| operator | Demo@123 | Data Entry Operator (restricted to Hosur farm) |
| viewer | Demo@123 | Management / Viewer |

Demo users must change their password at first login. **Change the admin password immediately and deactivate demo users you do not need.**

## Configuration (nothing hard-coded)

| What | Where |
|---|---|
| Who can open which screen / button | Roles & Permissions screen (`tblPermissions`, `tblRolePermissions`) |
| Sidebar menu text, icon, order | `tblPermissions` (`IsMenu = 1` rows) |
| Status labels & colours | `tblStatusConfig` + CSS classes in `Content/site.css` |
| Warning / critical tolerance % | `tblToleranceConfig` (per parameter, global default row) |
| Units (g, %, kcal/kg …) | `tblUnit`, linked from `tblParameter` |
| Uniformity range & thresholds, lock-out count, password length | `tblSystemSetting` |
| Performance-score weightage & bands | `tblScoreWeightage`, `tblScoreBand` |
| Optional breeder modules | `tblModule.IsEnabled` |

## How the numbers are calculated

| Value | Formula |
|---|---|
| Closing birds (per sex) | opening + received − mortality − culling − transferred out; opening = previous week's closing |
| Weekly mortality % | (female + male mortality) ÷ opening birds × 100 |
| Body weight | average of the main sex's sample weights (manual weight only when no samples) |
| Weight gain | this week's body weight − last week's |
| Uniformity % | birds within ± `UNIFORMITY_RANGE_PCT` % of the average ÷ birds weighed × 100 |
| CV % | sample standard deviation ÷ average × 100 |
| Feed intake (g/bird/day) | (feed kg − wastage kg) × 1000 ÷ ((opening + closing) ÷ 2 × 7) |
| Nutrient actual | lab value when the week's source is *Lab*, otherwise the selected feed formula's declared value |
| Egg production (hen-day) % | total eggs x 100 / ((opening + closing females) / 2 x 7) |
| Hatching eggs % | hatching eggs / total eggs x 100 |
| Fertility % | (eggs set - infertile) / eggs set x 100, over all settings of eggs laid in that week |
| Hatchability % | chicks hatched / eggs set x 100, over the settings of that week that have hatched |
| Status | outside critical limit → Critical; else inside Min–Max → Within, outside → Below / Above; with no Min/Max the tolerance % decides |
| Component score | 100 inside standard, falling linearly to 0 at the zero-score deviation (unfavourable side only) |
| Performance score | weighted average of the component scores that have data |

## JSON API (for the mobile app)

Requires a logged-in user with the `API_ACCESS` permission (forms cookie). All responses are JSON and respect the user's farm restriction.

| GET | Returns |
|---|---|
| `/api/v1/flocks?farmId=&status=Active&page=1&pageSize=50` | flock list (paged) |
| `/api/v1/flocks/{id}` | one flock |
| `/api/v1/flocks/{id}/trend` | week-by-week standard vs actual for every parameter |
| `/api/v1/weekly/{id}` | one weekly entry with its analysis |
| `/api/v1/dashboard?farmId=&birdTypeId=` | KPIs + latest-week snapshot of every active flock |
| `/api/v1/exceptions?farmId=&criticalOnly=false` | out-of-standard parameters |
| `/api/v1/alerts?status=Open&page=1` | alerts (paged) |

## Security notes
- All SQL is parameterised; list filters use escaped `LIKE` patterns.
- Every POST is protected by anti-forgery tokens; cookies are HttpOnly + SameSite=Lax.
- Business-rule errors raised in stored procedures (`THROW 50000–50999`) are shown to the user; other database errors are logged to `App_Data\Logs` and a generic message is shown.
- Every insert / update / status change on masters, users, roles, flocks, standards, weekly entries, movements, alerts and configuration is written to `tblAuditLog` with user, IP, old and new values (JSON), in the same transaction as the change.
- Users with a farm restriction only see and edit data of their farm.
