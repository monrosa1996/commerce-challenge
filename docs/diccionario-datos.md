# Diccionario de datos

Describe cada dato del sistema: el que llega en el archivo, el que se guarda en la base de datos y el que devuelve la API.
Está verificado contra la base de datos real (`CommerceDb`). Para el diagrama y los procedimientos vea
[base-de-datos.md](base-de-datos.md).

## 1. Del archivo a la pantalla: un dato, tres nombres

| Qué es | Columna del archivo CSV | Columna en la base | Campo en la API (JSON) |
| --- | --- | --- | --- |
| Fecha de proceso | `pc_processdate` | `pc_processdate` | `processDate` |
| Código del comercio | `pc_codcomred` | `pc_codcomred` | `codComRed` |
| Nombre del comercio | `pc_nomcomred` | `pc_nomcomred` | `nomComRed` |
| Tipo de documento | `pc_tipdoc` | `pc_tipdoc` | `tipDoc` |
| Número de documento | `pc_numdoc` | `pc_numdoc` | `numDoc` |
| Archivo de origen | (nombre del archivo) | `source_file` | `sourceFile` |
| Hora de carga | (la pone el sistema) | `created_at` | `createdAt` |
| Hora de ingreso a cuarentena | (la pone el sistema) | `quarantined_at` | `quarantinedAt` |
| Motivo de cuarentena | (lo calcula el sistema) | `motivo` | `motivo` |

## 2. Archivo CSV de entrada

Nombre: `commerce_DDMMYYYY.csv`. Codificación UTF-8, separador `,` o `;`, hasta 5 MB y 50.000 filas.
Las cinco columnas son obligatorias en el encabezado, en cualquier orden, sin repetirse ni agregar otras.

| Columna | Obligatorio | Formato y longitud máxima | Ejemplo | Qué pasa si falla |
| --- | --- | --- | --- | --- |
| `pc_processdate` | Sí | `yyyy-MM-dd` o `dd/MM/yyyy`, fecha real | `2024-03-15`, `15/03/2024` | Se rechaza el archivo e indica fila y valor |
| `pc_codcomred` | Sí | Texto, 20 caracteres | `1001` | Se rechaza el archivo |
| `pc_nomcomred` | No | Texto, 150 caracteres | `Ferreteria El Puerto` | Vacío: pasa a cuarentena al procesar. Más largo: se rechaza el archivo |
| `pc_tipdoc` | No | Texto, 10 caracteres | `RUC`, `CED` | Más largo: se rechaza el archivo. No se valida el valor |
| `pc_numdoc` | No | Texto, 30 caracteres | `0912345678001` | Vacío, con letras o con caracteres especiales: pasa a cuarentena al procesar. Más largo: se rechaza el archivo |

Un campo vacío se guarda como `NULL`. La combinación `pc_processdate` + `pc_codcomred` no puede repetirse en el archivo
ni estar ya cargada.

## 3. Tabla `dbo.commerce`

Registros cargados. Tiene un registro por fecha y código de comercio. Después de procesar una fecha, quedan solo los que pasaron la regla de validación.

| # | Columna | Tipo | ¿Acepta nulo? | Descripción | Ejemplo |
| --- | --- | --- | --- | --- | --- |
| 1 | `id` | `BIGINT` autonumérico | No | Identificador interno del registro. Clave primaria | `31` |
| 2 | `source_file` | `NVARCHAR(100)` | No | Nombre del archivo del que vino | `commerce_21032024.csv` |
| 3 | `pc_processdate` | `DATE` | No | Fecha de proceso. Parte de la clave de negocio | `2024-03-21` |
| 4 | `pc_codcomred` | `NVARCHAR(20)` | No | Código del comercio. Parte de la clave de negocio | `2001` |
| 5 | `pc_nomcomred` | `NVARCHAR(150)` | Sí | Nombre del comercio, tal como llegó | `Farmacia Central` |
| 6 | `pc_tipdoc` | `NVARCHAR(10)` | Sí | Tipo de documento, tal como llegó | `RUC` |
| 7 | `pc_numdoc` | `NVARCHAR(30)` | Sí | Número de documento. Es texto y no número para poder guardar los valores con letras o símbolos y llevarlos a cuarentena | `0912345678001` |
| 8 | `created_at` | `DATETIME2(0)` | No | Hora UTC en que se cargó. Valor por defecto: la hora actual (`DF_commerce_created_at`) | `2026-10-03 04:59:12` |

| Objeto | Tipo | Columnas | Para qué |
| --- | --- | --- | --- |
| `PK_commerce` | Clave primaria | `id` | Identificar cada fila |
| `UX_commerce_key` | Índice único | `pc_processdate`, `pc_codcomred` | Impedir claves repetidas, aun si dos cargas llegan a la vez |

## 4. Tabla `dbo.commerce_quarantine`

Registros apartados por no cumplir la regla de validación. Copia los datos de negocio de `commerce` y agrega el motivo.

| # | Columna | Tipo | ¿Acepta nulo? | Descripción | Ejemplo |
| --- | --- | --- | --- | --- | --- |
| 1 | `id` | `BIGINT` autonumérico | No | Identificador interno. Clave primaria | `6` |
| 2 | `source_file` | `NVARCHAR(100)` | No | Archivo de origen | `commerce_15032024.csv` |
| 3 | `pc_processdate` | `DATE` | No | Fecha de proceso | `2024-03-15` |
| 4 | `pc_codcomred` | `NVARCHAR(20)` | No | Código del comercio | `1010` |
| 5 | `pc_nomcomred` | `NVARCHAR(150)` | Sí | Nombre del comercio | (vacío) |
| 6 | `pc_tipdoc` | `NVARCHAR(10)` | Sí | Tipo de documento | `RUC` |
| 7 | `pc_numdoc` | `NVARCHAR(30)` | Sí | Número de documento | `12A-45` |
| 8 | `quarantined_at` | `DATETIME2(0)` | No | Hora UTC en que pasó a cuarentena. Por defecto: la hora actual (`DF_quarantine_quarantined_at`) | `2026-10-03 04:58:40` |
| 9 | `motivo` | `NVARCHAR(500)` | No | Por qué se apartó. Si hay varios, se unen con `; `. Se agregó con `ALTER TABLE` (`DF_quarantine_motivo`, por defecto vacío) | Ver el catálogo |

La columna `motivo` aparece al final porque se agregó después de crear la tabla, como pide el enunciado.

| Objeto | Tipo | Columnas |
| --- | --- | --- |
| `PK_commerce_quarantine` | Clave primaria | `id` |
| `IX_quarantine_processdate` | Índice | `pc_processdate` |

La cuarentena no tiene índice único: un mismo registro puede apartarse más de una vez si se vuelve a cargar y procesar.

## 5. Catálogo de motivos de cuarentena

Los genera `dbo.fn_commerce_validation(nombre, numero)`. Un registro sin motivo es correcto.

| Código interno | Condición | Texto guardado en `motivo` |
| --- | --- | --- |
| M1 | El nombre está vacío o solo tiene espacios | El nombre del comercio (nomcomred) se encuentra vacío |
| M2 | El número está vacío o solo tiene espacios | El número (numdoc) se encuentra vacío |
| M3 | El número contiene alguna letra (A a Z) | El número (numdoc) contiene letras |
| M4 | El número contiene algo distinto de dígitos y letras (guion, espacio, símbolo) | El número (numdoc) contiene caracteres especiales |

Se pueden sumar. Ejemplo: nombre vacío y número `12A-45` dan
`El nombre del comercio (nomcomred) se encuentra vacío; El número (numdoc) contiene letras; El número (numdoc) contiene caracteres especiales`.

Los códigos M1 a M4 son solo para esta guía: en la base de datos se guarda el texto.

## 6. Tipo de tabla `dbo.CommerceTableType`

Se usa para enviar todas las filas del archivo de una sola vez a `sp_create_commerce`.

| Columna | Tipo | ¿Acepta nulo? |
| --- | --- | --- |
| `pc_processdate` | `DATE` | No |
| `pc_codcomred` | `NVARCHAR(20)` | No |
| `pc_nomcomred` | `NVARCHAR(150)` | Sí |
| `pc_tipdoc` | `NVARCHAR(10)` | Sí |
| `pc_numdoc` | `NVARCHAR(30)` | Sí |

## 7. Parámetros y resultados de los procedimientos

| Procedimiento | Parámetro | Tipo | Obligatorio | Resultado |
| --- | --- | --- | --- | --- |
| `sp_create_commerce` | `@SourceFile` | `NVARCHAR(100)` | Sí | `InsertedCount` (cantidad guardada). Error 50001 si hay claves ya cargadas |
| | `@Rows` | `dbo.CommerceTableType` (solo lectura) | Sí | |
| `sp_process_commerce` | `@ProcessDate` | `DATE` | Sí | `QuarantinedCount` (cantidad apartada) |
| `sp_list_commerce` | `@ProcessDate` | `DATE` | No (por defecto, todas) | `Id`, `SourceFile`, `ProcessDate`, `CodComRed`, `NomComRed`, `TipDoc`, `NumDoc`, `CreatedAt` |
| `sp_list_commerce_quarantine` | `@ProcessDate` | `DATE` | No (por defecto, todas) | `Id`, `SourceFile`, `ProcessDate`, `CodComRed`, `NomComRed`, `TipDoc`, `NumDoc`, `Motivo`, `QuarantinedAt` |
| `sp_auth_get_user` | `@Username` | `NVARCHAR(50)` | Sí | `Id`, `Username`, `DisplayName`, `PasswordHash`, `IsActive`. Ninguna fila si el usuario no existe |
| `sp_audit_add` | `@UserId` | `INT` | No (puede ser nulo) | Nada. Agrega una fila a `audit_log` |
| | `@Username`, `@Action`, `@StatusCode` | `NVARCHAR(50)`, `NVARCHAR(20)`, `INT` | Sí | |
| | `@IpAddress`, `@RequestPayload`, `@ResponsePayload` | `NVARCHAR(45)`, `NVARCHAR(MAX)`, `NVARCHAR(MAX)` | No | |

Las dos consultas devuelven como máximo 5000 filas.

## 8. Tabla `dbo.app_user`

Personas que pueden iniciar sesión. No hay pantalla para administrarlas: las crea el script `02_schema.sql`.

| # | Columna | Tipo | ¿Acepta nulo? | Descripción | Ejemplo |
| --- | --- | --- | --- | --- | --- |
| 1 | `id` | `INT` autonumérico | No | Identificador interno. Clave primaria | `1` |
| 2 | `username` | `NVARCHAR(50)` | No | Nombre de usuario. Único, sin distinguir mayúsculas (`admin` y `ADMIN` son el mismo) | `admin` |
| 3 | `display_name` | `NVARCHAR(100)` | No | Nombre que se muestra en la pantalla | `Administrador` |
| 4 | `password_hash` | `NVARCHAR(200)` | No | Hash de la contraseña, formato `PBKDF2-SHA256$iteraciones$sal$hash`. La contraseña no se guarda | `PBKDF2-SHA256$600000$...` |
| 5 | `is_active` | `BIT` | No | Si el usuario puede entrar. Por defecto `1` (`DF_app_user_is_active`) | `1` |
| 6 | `created_at` | `DATETIME2(0)` | No | Hora UTC de creación (`DF_app_user_created_at`) | `2026-10-03 06:30:00` |

| Objeto | Tipo | Columnas |
| --- | --- | --- |
| `PK_app_user` | Clave primaria | `id` |
| `UX_app_user_username` | Índice único | `username` |

## 9. Tabla `dbo.audit_log`

Una fila por acción de un usuario. Solo se agregan filas: la aplicación nunca modifica ni borra.

| # | Columna | Tipo | ¿Acepta nulo? | Descripción | Ejemplo |
| --- | --- | --- | --- | --- | --- |
| 1 | `id` | `BIGINT` autonumérico | No | Identificador. Clave primaria | `9` |
| 2 | `occurred_at` | `DATETIME2(3)` | No | Hora UTC de la acción (`DF_audit_log_occurred_at`) | `2026-10-03 06:42:11.512` |
| 3 | `user_id` | `INT` | Sí | Usuario (clave foránea a `app_user`). Es nulo si el nombre escrito no existe | `1` |
| 4 | `username` | `NVARCHAR(50)` | No | Nombre de usuario tal como lo escribió quien actuó (cortado a 50) | `admin` |
| 5 | `action` | `NVARCHAR(20)` | No | Qué hizo. Solo admite los valores del catálogo (`CK_audit_log_action`) | `Upload` |
| 6 | `status_code` | `INT` | No | Código de respuesta que recibió el usuario | `201` |
| 7 | `ip_address` | `NVARCHAR(45)` | Sí | Dirección desde donde llamó | `::1` |
| 8 | `request_payload` | `NVARCHAR(MAX)` | Sí | Lo que entró, en JSON | `{"fileName":"commerce_15032024.csv",...}` |
| 9 | `response_payload` | `NVARCHAR(MAX)` | Sí | Lo que salió, en JSON | `{"insertedCount":10,...}` |

| Objeto | Tipo | Columnas |
| --- | --- | --- |
| `PK_audit_log` | Clave primaria | `id` |
| `FK_audit_log_user` | Clave foránea | `user_id` → `app_user.id` |
| `CK_audit_log_action` | Restricción | `action` solo `Login`, `LoginFailed`, `Logout`, `Upload`, `Process` |
| `IX_audit_log_occurred_at` | Índice | `occurred_at` |
| `IX_audit_log_user` | Índice | `user_id`, `occurred_at` |

## 10. Catálogo de acciones auditadas

| Acción | Significado | `status_code` típico | Ejemplo de `request_payload` | Ejemplo de `response_payload` |
| --- | --- | --- | --- | --- |
| `Login` | Ingreso al sistema | 200 | `{"username":"admin"}` | `{"success":true,"expiresAt":"2026-10-03T07:48:28+00:00"}` |
| `LoginFailed` | Ingreso rechazado | 401 (400 si faltaron datos) | `{"username":"admin"}` | `{"success":false,"reason":"wrong_password"}` |
| `Logout` | Salida del sistema | 200 | `{"reason":"manual"}` o `{"reason":"pagehide"}` (pestaña cerrada) | `{"loggedOut":true}` |
| `Upload` | Carga de archivo | 201, 400, 409, 413 o 500 | `{"fileName":"commerce_15032024.csv","sizeBytes":495,"sha256":"..."}` | `{"sourceFile":"commerce_15032024.csv","insertedCount":10,"warnings":[]}` |
| `Process` | Procesado de una fecha | 200, 400 o 500 | `{"processDate":"2024-03-15"}` | `{"processDate":"2024-03-15","quarantinedCount":5}` |

Motivos internos de `LoginFailed` (solo en la auditoría, el usuario siempre ve el mismo mensaje):

| Motivo | Significado |
| --- | --- |
| `unknown_user` | El usuario no existe |
| `wrong_password` | La contraseña no coincide |
| `inactive_user` | El usuario existe y la contraseña es correcta, pero está inactivo |
| `missing_data` | Faltó el usuario o la contraseña |

Lo que **nunca** se guarda: contraseñas, tokens y el contenido del archivo cargado.

## 11. Token de sesión (JWT)

| Dato del token | Contenido |
| --- | --- |
| `sub` | Número del usuario (`app_user.id`) |
| `unique_name` | Nombre de usuario |
| `jti` | Identificador propio del token. Sirve para cerrarlo al salir |
| `iss` / `aud` | Emisor `commerce-api` y destino `commerce-web` |
| `iat`, `nbf`, `exp` | Emitido, válido desde y vence (30 minutos) |
| Firma | HS256 con la clave `Jwt:SigningKey` |

## 12. Campos de la API de sesión

**Solicitud de ingreso (`POST /api/auth/login`)**

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `username` | texto | Nombre de usuario (se ignoran los espacios de los extremos) |
| `password` | texto | Contraseña. Nunca se guarda ni se devuelve |

**Respuesta de ingreso**

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `accessToken` | texto | Token JWT. Se envía como `Authorization: Bearer <token>` |
| `expiresAt` | fecha y hora UTC | Cuándo vence |
| `username` | texto | Nombre de usuario |
| `displayName` | texto | Nombre para mostrar |

**Solicitud de salida (`POST /api/auth/logout`)**: `reason` (opcional): `manual` o `pagehide`. Cualquier otro valor se guarda como `manual`. Responde 204 sin cuerpo.

**Avisos de salida de la página**: `POST /api/auth/closing` (la página se va) y `POST /api/auth/resume` (volvió tras recargar). No llevan cuerpo, responden 204 y no se auditan. Si no hay `resume` en 15 segundos, el servidor cierra la sesión y guarda `Logout` con `reason` `pagehide`.

## 13. Campos de la API

**Respuesta de carga (`POST /api/commerce/files`)**

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `sourceFile` | texto | Nombre del archivo cargado |
| `insertedCount` | entero | Registros guardados |
| `warnings` | lista de texto | Avisos que no impidieron la carga (por ejemplo, formatos de fecha mezclados) |

**Solicitud y respuesta de proceso (`POST /api/commerce/process`)**

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `processDate` | texto `yyyy-MM-dd` | Fecha a procesar |
| `quarantinedCount` | entero | Registros enviados a cuarentena (0 si no había ninguno) |

**Registro (`GET /api/commerce`)**: `id`, `sourceFile`, `processDate`, `codComRed`, `nomComRed`, `tipDoc`, `numDoc`, `createdAt`.
**Registro en cuarentena (`GET /api/commerce/quarantine`)**: los anteriores, sin `createdAt`, más `motivo` y `quarantinedAt`.

**Error (cualquier operación)**

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `title` | texto | Resumen del problema |
| `status` | entero | Código HTTP (400, 409, 413, 500) |
| `detail` | texto | Explicación para el usuario |
| `errors` | lista | Opcional. Cada elemento trae `row` (fila de datos, o `null`), `column` (o `null`) y `message` |

## 14. Valores y límites de configuración

| Parámetro | Valor | Dónde se cambia |
| --- | --- | --- |
| Tamaño máximo del archivo | 5 MB | `Upload:MaxFileSizeMb` en `appsettings.json` y `maxFileSizeBytes` en `environment.ts` |
| Filas máximas | 50.000 | `Upload:MaxRows` y `maxRows` en `environment.ts` |
| Errores informados por archivo | 50 | Fijo en el código |
| Filas máximas por consulta | 5000 | Fijo en los procedimientos de consulta |
| Formatos de fecha del archivo | `yyyy-MM-dd`, `dd/MM/yyyy` | Fijo en el código |
| Formato de fecha en la API | `yyyy-MM-dd` | Fijo en el código |
| Zona horaria de las marcas de tiempo | UTC | Valor por defecto de las columnas |
| Duración de la sesión | 30 minutos | `Jwt:ExpiryMinutes` en `appsettings.json` |
| Emisor y destino del token | `commerce-api` y `commerce-web` | `Jwt:Issuer` y `Jwt:Audience` |
| Clave de firma del token | 32 caracteres o más, sin valor por defecto | `Jwt:SigningKey` (en desarrollo, `appsettings.Development.json`; fuera de desarrollo, variable de entorno `Jwt__SigningKey`) |
| Iteraciones del hash de contraseña | 600.000 | Fijo en el código y guardado dentro de cada hash |
| Orígenes permitidos (CORS) | `http://localhost:4200` | `Cors:AllowedOrigins` |

## 15. Glosario

| Término | Significado |
| --- | --- |
| Clave de negocio | Fecha de proceso + código del comercio. Identifica un registro sin importar su `id` |
| Procesar una fecha | Revisar los registros de esa fecha y apartar a cuarentena los que no cumplen la regla |
| Cuarentena | Tabla donde quedan los registros con error, con su motivo, para revisarlos |
| Registro correcto | Registro cuyo nombre y número cumplen la regla de validación |
| Todo o nada | Si algo falla, no se guarda ninguna fila del archivo |
| Sesión | Período en que un usuario puede usar la aplicación, entre su ingreso y su salida o el vencimiento del token |
| JWT | Token firmado que prueba quién es el usuario. Se envía en cada solicitud |
| Auditoría | Registro de lo que hizo cada usuario, con lo que entró y lo que salió |
| Payload | Contenido de una solicitud (request) o de una respuesta (response), guardado como JSON |
| Hash de contraseña | Resultado irreversible de una contraseña. Permite comprobarla sin guardarla |
