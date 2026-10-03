# commerce-challenge

Load a daily commerce CSV file, check it, and move invalid records to quarantine.
Angular front end, .NET 10 API, SQL Server database.
Users log in with a JWT session. Every login, logout, upload and process is saved in an audit table, with what came in and what went out.

The original statement mentions SpringBoot. This solution uses .NET 10 for the API.

## Requirements

| Tool | Version |
| --- | --- |
| .NET SDK | 10 (see `backend/global.json`) |
| Node.js | 24 LTS (see `frontend/.nvmrc`) |
| SQL Server | 2017 or later (the script uses `TRIM`, `CONCAT_WS`, `STRING_AGG`, `CREATE OR ALTER`) |
| VS Code (optional) | Extensions in `.vscode/extensions.json` |

## Run it

The steps are the same on Windows and Mac.

### 1. Database

Run these files in order, with any SQL client that understands `GO` (SSMS, Azure Data Studio, DBeaver, `sqlcmd`, VS Code SQL Server extension).
All of them can run more than once.

| Order | File | Connect to | What it does |
| --- | --- | --- | --- |
| 1 | `backend/database/01_create_database.sql` | `master` | Creates `CommerceDb` and the `commerce_app` login |
| 2 | `backend/database/02_schema.sql` | `CommerceDb` | Tables, `motivo` column, table type, validation function, stored procedures, `commerce_app` user (EXECUTE only) |
| 3 (optional) | `backend/database/seed.sql` | `CommerceDb` | Loads the base file straight into `dbo.commerce` |
| 4 (optional) | `backend/database/tests.sql` | `CommerceDb` | SQL tests, all rolled back |

Run each file as a whole script (not statement by statement) and keep auto-commit on.

On Windows, follow [docs/implementacion-windows.md](docs/implementacion-windows.md): it lists the supported versions, the order of the scripts and the connection settings.
For SQL Server Express use the server name `localhost\SQLEXPRESS` in the client and in the connection string.
The `commerce_app` login needs mixed mode authentication. With Windows authentication only,
change the connection string to `Server=...;Database=CommerceDb;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True`.

On Mac or Linux without SQL Server, start one with Docker and run the scripts in one step:

```bash
scripts/sqlserver-docker.sh --seed --test
```

`--seed` loads the base file straight into the table, and `--test` runs `backend/database/tests.sql`.

### 2. API

```bash
dotnet run --project backend/src/Commerce.Api
```

The API listens on `http://localhost:5080`. In Development, API docs are at `/scalar`.
The connection string is in `backend/src/Commerce.Api/appsettings.Development.json`.

### 3. Front end

```bash
cd frontend
npm ci
npm start
```

Open `http://localhost:4200` and log in with a development user:

| User | Password |
| --- | --- |
| `admin` | `Admin#2026` |
| `analista` | `Analista#2026` |

They are created by `02_schema.sql` and are for development only (see [SECURITY.md](SECURITY.md)).
The session lives in the tab (sessionStorage): a reload keeps it. Closing the tab or the browser ends it: the server closes the token and audits the logout 15 seconds after the page leaves, unless the page comes back.
The application can be open in only one tab of the browser: a second tab shows a message instead.

## CSV format

| Rule | Value |
| --- | --- |
| File name | `commerce_DDMMYYYY.csv` with a real date |
| Columns | `pc_processdate`, `pc_codcomred`, `pc_nomcomred`, `pc_tipdoc`, `pc_numdoc` (exact, any order) |
| Separator | `,` or `;`, detected in the header line |
| Date | `yyyy-MM-dd` or `dd/MM/yyyy` |
| Encoding | UTF-8, with or without BOM |
| Limits | 5 MB and 50,000 rows (`Upload` section of `appsettings.json`) |

The key of a record is `pc_processdate` + `pc_codcomred`. Loading a key that already exists answers 409.

The upload saves every record in the `commerce` table. The name and number of each record are checked later, when a date is processed (in SQL, by `sp_process_commerce`): the records that fail move to quarantine with their reason.

## API

| Method and route | Body | Answers |
| --- | --- | --- |
| `POST /api/auth/login` | `{ "username", "password" }` | 200 `{ accessToken, expiresAt, username, displayName }`, 400, 401 |
| `POST /api/auth/logout` | `{ "reason": "manual" }` (optional) | 204. The token stops working |
| `POST /api/commerce/files` | multipart, field `file` | 201 `{ sourceFile, insertedCount, warnings }`, 400, 409, 413 |
| `POST /api/commerce/process` | `{ "processDate": "2024-03-15" }` | 200 `{ processDate, quarantinedCount }`, 400 |
| `GET /api/commerce?processDate=` | optional date | 200 records in the commerce table (after a date is processed, only the correct ones remain) |
| `GET /api/commerce/quarantine?processDate=` | optional date | 200 list of quarantined records with `motivo` |
| `GET /health` | | 200 |

Every endpoint except login and `/health` needs the header `Authorization: Bearer <accessToken>`. Without a valid session the API answers 401.

Errors use ProblemDetails. Row errors come in `errors: [{ row, column, message }]`.

## Test it

Screens: **Login**, **Carga** (upload with preview), **Registros** (records in the commerce table, filter by date, process a date) and **Cuarentena** (quarantined records with their reason, filter by date). Registros and Cuarentena need a date and the Buscar button; changing the date clears the table. Each screen keeps its data when you change tab and has a Limpiar button. After an upload, Carga links to Registros with the date of the file.

Unit and integration tests:

```bash
dotnet test backend/Commerce.sln
cd frontend && npm run test:ci
```

Log in first (the screens and the requests ask for it). Manual tests with the files in `backend/database/samples/`. Use `backend/http/commerce.http` (VS Code REST Client) or the Upload screen.

| # | File | Expected |
| --- | --- | --- |
| 1 | `valid-comma/commerce_15032024.csv` | 201, 10 inserted. Then process 2024-03-15: 5 go to quarantine, row 1010 has 3 reasons |
| 2 | `valid-semicolon/commerce_16032024.csv` | 201, 10 inserted (`;` and `dd/MM/yyyy`) |
| 3 | `empty/commerce_17032024.csv` | 400 "El archivo está vacío" |
| 4 | `missing-column/commerce_18032024.csv` | 400, column `pc_numdoc` is missing |
| 5 | `invalid-date/commerce_19032024.csv` | 400, row 7 |
| 6 | `duplicate-key/commerce_20032024.csv` | 400, rows 3 and 8 |
| 7 | Upload scenario 1 again | 409 "Hay registros que ya existen en el sistema" |
| 8 | `all-correct/commerce_21032024.csv` | 201, 10 inserted. Process 2024-03-21: 0 go to quarantine |
| 9 | `injection-attempt/commerce_22032024.csv` | 201, 5 inserted as plain text. Process 2024-03-22: 1 goes to quarantine. No table is changed |

JSON cases:

| Request | Expected |
| --- | --- |
| process `{ "processDate": "2024-03-15" }` | 200, `quarantinedCount` 5 |
| process, same body again | 200, `quarantinedCount` 0 |
| process `{ "processDate": "15/03/2024" }` | 400, wrong format |
| process `{}` | 400, required |
| `GET /api/commerce?processDate=2024-03-15` | 200, 10 records before processing, 5 after |
| `GET /api/commerce/quarantine?processDate=2024-03-15` | 200, 5 records with `motivo` |

Quarantine reasons (in Spanish, from `dbo.fn_commerce_validation`): empty name, empty number, number with letters, number with special characters. A record can have several, joined with `; `.

## Documents

The full set is in [docs/](docs/README.md) (in Spanish). Main ones:

- [Windows setup guide](docs/implementacion-windows.md)
- [Architecture](docs/arquitectura.md), [database](docs/base-de-datos.md), [analysis](docs/analisis.md), [decisions](docs/decisions.md)
- [API contract](docs/api.md) and [Postman collection](docs/postman/)
- [Folder structure](docs/estructura-proyecto.md), [tests](docs/pruebas.md)
- [Improvements](docs/mejoras.md), [SECURITY.md](SECURITY.md), [CHANGELOG.md](CHANGELOG.md)

## Next steps

See [docs/mejoras.md](docs/mejoras.md). The first ones: login and secrets, reconciliation of existing keys, quarantine resolution, CI.
