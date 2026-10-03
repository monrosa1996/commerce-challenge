# Security

## Development credentials

These values are for development only. Change them or remove them outside a development machine.

| What | User | Password |
| --- | --- | --- |
| Application users (created by `backend/database/02_schema.sql`) | `admin` | `Admin#2026` |
| | `analista` | `Analista#2026` |
| Database login used by the API (`01_create_database.sql`) | `commerce_app` | `Commerce_App#2026` |
| Database administrator of the Docker container (`scripts/sqlserver-docker.sh`) | `sa` | `Commerce_Sa#2026` |
| JWT signing key (`appsettings.Development.json`) | | `dev-only-signing-key-change-it-0123456789abcdef` |

## Login and session

- Passwords are never stored. The table `app_user` holds a PBKDF2-SHA256 hash with a random salt and 600,000 iterations.
- A wrong user and a wrong password get the same message, and the check takes the same time in both cases.
- The API gives a JWT (HS256) that expires in 30 minutes (`Jwt:ExpiryMinutes`). Every endpoint needs it, except login and `/health`.
- The signing key has no default. Outside Development the API does not start without `Jwt:SigningKey` (32 characters or more).
  Give it with an environment variable (`Jwt__SigningKey`) or user-secrets, never in the repository.
- A logout closes the token on the server (it stops working before it expires). The list of closed tokens is in memory:
  a restart empties it, which is acceptable because tokens expire in minutes.
- The front end keeps the token in memory and in sessionStorage (never localStorage). sessionStorage belongs to the tab: a
  reload keeps the session and the browser deletes it when the tab or the browser closes. A page cannot stop the user from
  closing the tab, so it asks the browser for a confirmation and, when the page leaves, tells the server (`/api/auth/closing`).
  A reload and a close look the same at that moment, so the server waits 15 seconds: if the page comes back
  (`/api/auth/resume`) the session goes on; if not, it closes the token and audits the `Logout`.

The rule "only one tab" is a convenience of the screen (Web Locks), not a security control: it does not stop another
browser, a private window or another computer.

## Audit

The table `audit_log` saves, for every login, failed login, logout, file upload and process: the user, the time, the address,
the status code, and the request and response. It never saves passwords, tokens or the content of an uploaded file
(the file is identified by name, size and SHA-256).

## Other controls

- The API uses only stored procedures with typed parameters. There is no dynamic SQL, and the file content travels as data.
  The sample `injection-attempt` shows SQL and HTML text stored as plain text. See `docs/arquitectura.md`.
- The database login `commerce_app` can only execute procedures.
- Limits: file size, row count and number of reported errors are configurable. The log never contains row data.
- CORS allows only the origin of the front end (`Cors:AllowedOrigins`).
- In production, keep the connection string out of the repository (user-secrets, Key Vault or similar), use HTTPS, and use
  `Trusted_Connection` where Windows authentication is available.
- To report a problem, open an issue without sensitive data.

Before a release run `dotnet list backend/Commerce.sln package --vulnerable --include-transitive`
and `npm audit --omit=dev` inside `frontend`.
