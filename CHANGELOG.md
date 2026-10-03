# Changelog

## 2.2.0

- A reload keeps the session: the token is kept in the sessionStorage of the tab, which the browser deletes when the tab closes.
- When the page leaves it calls `POST /api/auth/closing`; if it does not come back (`POST /api/auth/resume`) in 15 seconds, a background worker revokes the token and audits the `Logout` (reason `pagehide`).
- Tests: 112 back end, 7 SQL, 83 front end.

## 2.1.0

- The application can be open in only one tab of the browser. A second tab shows "La aplicación ya está abierta" with a Reintentar button. It uses the Web Locks API, so the lock is released by the browser when the first tab closes.
- It covers the tabs and windows of the same browser profile. It is a convenience of the screen, not a security control.
- Tests: 78 front end.

## 2.0.0

- Login with JWT: `POST /api/auth/login` and `POST /api/auth/logout`. Every other endpoint needs the token (except `/health`).
- New tables `app_user` (users, password as PBKDF2 hash) and `audit_log` (login, failed login, logout, upload, process, with request and response payloads).
- New procedures `sp_auth_get_user` and `sp_audit_add`. Two development users are created by `02_schema.sql`.
- Passwords are checked in constant time; a wrong user and a wrong password get the same message.
- A logout closes the token on the server. The front end keeps the session only in memory, asks for confirmation when the user leaves with an open session, and closes the session when the page leaves.
- New Login screen, user name and **Cerrar sesión** button, route guards and an interceptor that sends the token and handles 401.
- The signing key has no default: outside Development it must be given by configuration.
- Tests: 107 back end, 7 SQL, 73 front end.

## 1.2.0

- The Carga screen shows, after a successful upload, a link to Registros with the date of the file already chosen (one link per date).
- The three screens keep their data when the user changes tab: the chosen file and preview, the date, the table and the messages.
- Registros and Cuarentena no longer load everything at once: they need a date and the **Buscar** button. The "Ver todos" button was removed.
- Changing the date clears the table, so it never shows data of another date.
- New **Limpiar** button on each screen: it empties the screen and the data kept in memory.
- Tests for the state of the three screens (50 front end tests).

## 1.1.0

- New screen **Registros**: lists the records in the commerce table with a date filter and lets the user process a date.
- New query `GET /api/commerce` with an optional date filter.
- The 409 message is clearer: "Hay registros que ya existen en el sistema (fecha|código)".
- New samples: `all-correct` and `injection-attempt`.
- Documentation in `docs/`: analysis, architecture, database, API, tests, Windows setup, improvements, Postman collection.

## 1.0.0

- Upload of `commerce_DDMMYYYY.csv` with local checks, preview, progress and cancel.
- API with 12 file checks, load through `sp_create_commerce`, 409 when keys already exist.
- Process by date: invalid records move to `commerce_quarantine` with a `motivo`.
- Quarantine list with date filter.
- Database script, seed, SQL tests and CSV scenarios.
