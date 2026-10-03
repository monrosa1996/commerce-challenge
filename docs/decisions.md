# Decisions

Short record of the choices made. Each one has the context, the decision and the cost.

## 1. Three layers in one repository

- Context: one small API and one front end, reviewed together.
- Decision: `Commerce.Api` (controllers, errors, startup), `Commerce.Application` (file rules, services),
  `Commerce.Infrastructure` (Dapper). One git repository for back end and front end.
- Cost: more projects than a single one, but each file has one clear job and the file rules are easy to test.

## 2. Dapper and stored procedures

- Context: the statement asks for `sp_create_commerce` and works with a table-shaped load.
- Decision: Dapper calls stored procedures with typed parameters. No dynamic SQL, and every query lists its columns.
- Cost: SQL lives in `backend/database/`, not in C#. In return the API user only needs EXECUTE.

## 3. Table-valued parameter for the load

- Context: a file can have up to 50,000 rows.
- Decision: send all rows in one `dbo.CommerceTableType` parameter. One round trip, one transaction.
- Cost: for millions of rows, `SqlBulkCopy` into a staging table would be better.

## 4. Business key and unique index

- Context: the same file can be sent twice, or two users can send it at the same time.
- Decision: key is `pc_processdate` + `pc_codcomred`, protected by `UX_commerce_key`.
  `sp_create_commerce` checks first and answers 409 with up to 20 keys. If two requests pass the check
  together, the unique index stops the second one and the API also answers 409.
- Cost: no partial load. Reconciliation (next phase) uses the same key.

## 4b. Why 409 and not an update

- Decision: a repeated key is not overwritten. The user gets a clear error.
- Cost: to fix a loaded record the user needs a new process. Reconciliation removes this limit later.

## 5. Validate the file before saving anything

- Context: the statement asks for 400 on an empty file; other format errors cost a database call if checked late.
- Decision: the API checks 12 rules in memory, in a fixed order (empty, name, size, text, separator, header,
  rows, row length, date, code, key). Format errors reject the whole file because date and code make the key.
  Up to 50 errors are reported.
- Cost: none for the database. The file is read in memory and never written to disk.

## 6. Business rules in SQL, applied when a date is processed

- Context: the statement asks for a stored procedure that checks `pc_nomcomred` and `pc_numdoc` when a date is
  processed, and that moves the records that fail from `commerce` to `commerce_quarantine`.
- Decision: the upload (`sp_create_commerce`) saves every record in `commerce`. Processing a date is a second step:
  one inline function, `dbo.fn_commerce_validation`, returns the motivo, and `sp_process_commerce` moves the
  invalid rows with a single `DELETE ... OUTPUT ... INTO` statement, so the move is atomic.
  The data is stored as received (`NVARCHAR`), so records with letters can reach quarantine.
- Cost: the rule is in SQL, not in C#. The function keeps it in one place.
  Wrong records stay in `commerce` until someone processes their date.

## 7. Front end checks too, but the API decides

- Decision: Angular repeats the file rules in `shared/csv-validation.ts` (a pure function) to answer at once and
  show a preview. The API checks everything again.
- Cost: the rules exist twice. Tests cover both.

## 8. Double click and cancel

- Decision: the button is disabled while a request runs, and the action uses `exhaustMap`. Cancel closes the
  subscription and the browser aborts the request. The API passes the request `CancellationToken` to Dapper.
- Cost: if the load finished before the abort, it stays saved. The screen says so. The 409 on a second send
  protects against duplicates.

## 9. Connection string in the repository

- Decision: `appsettings.Development.json` has the dev connection string so the project starts without extra steps.
  The `commerce_app` login can only execute procedures on a local database.
- Cost: not for production. See SECURITY.md.

## 10. Errors

- Decision: expected errors (400, 409, 413) are mapped in an MVC exception filter, so CORS headers stay on the
  response and the browser can read the message. Anything else goes to a global `IExceptionHandler` that logs
  and answers a generic 500.

## 11. Local HTTP in Development

- Decision: HTTPS redirection and HSTS run only outside Development. In Development the API uses plain http
  on port 5080, so the front end does not need a dev certificate.

## 12. The records list shows what is in the commerce table

- Context: users need to see the loaded information of a day before processing it.
- Decision: the Registros screen lists `commerce` with an optional date filter and has the button that processes the
  selected date. `sp_list_commerce` is a plain query. After a date is processed, the records that failed are gone
  from the list because they moved to quarantine.
- Cost: before processing, the list includes records that will go to quarantine.

## 13. Screen state lives in services

- Context: users change tab and expect to find the date, the table and the chosen file as they left them, and a
  button to empty everything.
- Decision: each screen has a state service (`upload.state.ts`, `records.state.ts`, `quarantine.state.ts`) that lives
  while the app is open. The components only show it. Searches need a date, and changing the date clears the table
  and cancels the search in progress, so the table always belongs to the date shown. The Limpiar button empties the
  screen and the service. A link from Carga opens Registros with the date of the file already searched.
- Cost: the data is kept only in memory: reloading the page (F5) empties it.

## 14. Login with a JWT that lives in the tab

- Context: the application needs users, and what they do must be traceable. The user also asked that a session does not
  stay open when the tab or the browser is closed.
- Decision: `POST /api/auth/login` checks the user and gives a JWT (HS256, 30 minutes). The front end keeps it in
  memory and in sessionStorage (never localStorage): a reload keeps the session and the browser deletes it when the tab closes. Every endpoint needs the
  token by default (a fallback authorization policy); only login, `/health` and the API docs are open.
- Alternative not taken: a cookie session (`HttpOnly`). It cannot be read by the page code, but it needs CSRF protection.
- Cost: a script injected in the page could read sessionStorage (same exposure as memory while the page is open). The token
  lasts 30 minutes and a logout closes it.

## 15. Passwords with PBKDF2 from the framework

- Decision: PBKDF2-SHA256, 600,000 iterations, a random salt, and a constant-time comparison. It uses
  `Rfc2898DeriveBytes` from .NET, so there is no new package for it. The iterations are saved inside each hash, so they can
  grow later without breaking old users. A wrong user and a wrong password get the same message and take the same time.
- Cost: each login takes about 100 ms of CPU on purpose.

## 16. The audit is written by the service layer and never stops the user

- Context: the audit must show what came in and what went out, also when the action fails.
- Decision: `CommerceService` and `AuthService` write the audit entry (not the controller), with the same status code and
  message the user received. Payloads are JSON in two columns. Passwords, tokens and the content of the file are never
  saved: the file is identified by name, size and SHA-256. A failure to write the audit is logged and does not stop the
  action, because the action already happened (the repository never throws).
- Cost: in the rare case the audit write fails, one action has no audit row. A stricter policy would reject the action.

## 17. What a page can and cannot do when it closes

- Context: "do not allow closing the tab or the browser without a logout".
- Decision: a web page cannot block the closing. The application shows the browser confirmation while there is a session
  (`beforeunload`) and, when the page leaves (`pagehide`), announces it to the server with `keepalive`
  (`POST /api/auth/closing`). A reload and a close fire the same event, so the server waits 15 seconds: a page that comes
  back calls `/api/auth/resume` and keeps its session; otherwise a background worker revokes the token and audits the
  `Logout`. If the announcement does not arrive, the token expires by itself.
- Cost: the logout of a closed tab is audited up to ~20 seconds late (15 s grace plus the worker tick of 5 s), and the
  pending closes are in memory (a restart of the API forgets them). Some browsers show the confirmation only after the
  user clicked in the page.

## 18. Closed tokens are kept in memory

- Decision: a logout adds the token id (`jti`) to an in-memory list that the token validation checks. Entries that already
  expired are removed. There is no extra table: the database has only users and audit for this feature.
- Cost: a restart empties the list (tokens live 30 minutes at most), and several API instances would need to share it.

## 19. One tab at a time, with the browser lock

- Context: the user asked that the system cannot be open in two tabs of the browser.
- Decision: the app takes a Web Lock when it starts and keeps it while the tab lives. A second tab finds it taken and shows a
  message with a Reintentar button; it makes no calls to the API. The app waits for this answer before it starts.
- Alternative not taken: a session per user checked in the server. It also covers other computers, but it needs the server to
  remember sessions, and the user asked only for tabs.
- Cost: it is not a security control. Another browser, a private window or another computer is not covered.

## Planned next phase (designed, not built)

| Feature | Design |
| --- | --- |
| Reconciliation | `sp_compare_commerce` joins by key and returns New, Modified, Identical. Screen with tabs "New and modified" (checked) and "Repeated" (unchecked). `MERGE` only the selected keys. |
| Upload control and retry | Table `commerce_upload` with states (Received, Processing, PendingReconciliation, Applied, Failed, Canceled, Discarded), attempts and last error. Temporary file `{id}.csv` on disk. |
| Idempotency in the API | `Idempotency-Key` header, unique in the control table. Same key and same hash returns the first result. |
| Quarantine resolution | Columns `resuelto`, `justificacion`, `resolved_at`. Edit, justify and move the record back to `commerce` with the same SQL rule. |
| Period filter | Week, month or year on both lists, plus resolved yes/no. A `MaxRows` cap instead of paging. |
| Revert a load | Version history of inserted and updated rows. |
