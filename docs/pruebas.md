# Pruebas

## Estrategia

Se prueba más donde más falla: la entrada de datos. Hay cuatro niveles, de lo más rápido a lo más completo.

| Nivel | Qué cubre | Dónde | Cantidad |
| --- | --- | --- | --- |
| Unitarias del back end | Nombre, fechas, lector del CSV, servicios, contraseñas, inicio y cierre de sesión, auditoría | `backend/tests/Commerce.Tests` | 112 (con las de API) |
| Integración de la API | La API completa con la base de datos simulada: sesión (token válido, vencido, falso y cerrado), login, logout, carga, proceso y auditoría | `ApiEndpointTests` | 21 (incluidas arriba) |
| Base de datos | Carga, proceso, función de validación, conflicto, listado, usuarios y auditoría | `backend/database/tests.sql` | 7 |
| Unitarias del front end | Revisión local del CSV y estado de las pantallas (fecha, tabla, limpiar, doble clic, enlaces), sesión, interceptor, guardias y avisos al salir | `frontend/src/app/**/*.spec.ts` | 83 |
| Manuales / de extremo a extremo | Escenarios con archivos reales y la API en marcha | `samples/`, Postman, `commerce.http`, pantallas | ver abajo |

Estado al cierre de esta versión: 112 pruebas del back end, 7 de SQL y 83 del front end, todas en verde. La compilación
no genera advertencias.

## Cómo ejecutarlas

```bash
# Back end (desde la raíz del repositorio)
dotnet test backend/Commerce.sln

# Front end
cd frontend
npm run test:ci
```

Pruebas de la base de datos: ejecute `backend/database/tests.sql` como script completo en su cliente SQL, sobre
`CommerceDb` (después de los scripts 01 y 02). Termina con `All database tests passed.` y no deja datos.
En Mac con Docker también se puede usar `scripts/sqlserver-docker.sh --test`.

## Escenarios manuales

**Antes de todo, inicie sesión** (`admin` / `Admin#2026`). Sin sesión, el sistema lleva a la pantalla de inicio de sesión.

Archivos en `backend/database/samples/`. Úselos desde la pantalla Carga, Postman o `commerce.http`.
Orden sugerido sobre una base vacía.

| # | Archivo | Resultado esperado |
| --- | --- | --- |
| 1 | `valid-comma/commerce_15032024.csv` | 201, 10 cargados |
| 2 | Procesar `2024-03-15` | 200, 5 a cuarentena. La fila 1010 trae tres motivos |
| 3 | `valid-semicolon/commerce_16032024.csv` | 201, 10 cargados (`;` y `dd/MM/yyyy`) |
| 4 | `all-correct/commerce_21032024.csv` | 201, 10 cargados. Al procesar `2024-03-21`: 0 a cuarentena |
| 5 | Subir otra vez el escenario 1 | 409, "Hay registros que ya existen en el sistema" |
| 6 | `empty/commerce_17032024.csv` | 400, "El archivo está vacío" |
| 7 | `missing-column/commerce_18032024.csv` | 400, falta `pc_numdoc` |
| 8 | `invalid-date/commerce_19032024.csv` | 400, fila 7 |
| 9 | `duplicate-key/commerce_20032024.csv` | 400, filas 3 y 8 |
| 10 | `injection-attempt/commerce_22032024.csv` | 201, 5 cargados. Los textos peligrosos se guardan como texto. Al procesar `2024-03-22`: 1 a cuarentena |

Pruebas de sesión y auditoría:

| Acción | Resultado esperado |
| --- | --- |
| Abrir `http://localhost:4200` sin sesión | Lleva a la pantalla Iniciar sesión. Las otras direcciones también |
| Entrar con una contraseña incorrecta | "Usuario o contraseña incorrectos." Con un usuario que no existe, el mismo mensaje |
| Entrar con `admin` / `Admin#2026` | Entra a Carga. Arriba aparece "Administrador" y el botón **Cerrar sesión** |
| Con la sesión abierta, intentar cerrar la pestaña | El navegador pide confirmar (si no aparece, haga clic dentro de la página y repita) |
| Con la sesión abierta, recargar la página (F5) | La sesión continúa y no pide iniciar sesión. No se reenvía ningún archivo |
| Con la sesión abierta, cerrar la pestaña, abrir el sistema de nuevo y consultar `audit_log` pasados 20 segundos | Aparece `Logout` con motivo `pagehide`; al reabrir hay que iniciar sesión |
| Pulsar **Cerrar sesión** | Vuelve a Iniciar sesión. Al volver atrás con el navegador no se ve información |
| Cerrar sesión en una pestaña y usar un token anterior (Postman o `.http`) | 401 "Su sesión ha expirado o no es válida. Inicie sesión de nuevo." |
| Abrir una segunda pestaña (o ventana) con `http://localhost:4200` mientras la primera está abierta | Aviso "La aplicación ya está abierta" con el botón **Reintentar**. No pide iniciar sesión |
| Cerrar la primera pestaña y pulsar **Reintentar** en la segunda | Entra y muestra el inicio de sesión |
| Dejar la sesión abierta más de 30 minutos y usarla | La pantalla vuelve a Iniciar sesión con el aviso "Su sesión terminó" |

Después de probar, revise la auditoría con un usuario administrador de la base de datos:

```sql
SELECT id, occurred_at, username, action, status_code, request_payload, response_payload
FROM dbo.audit_log ORDER BY id;
```

Debe verse, en orden: `LoginFailed` (si hubo intentos fallidos), `Login`, `Upload` (201 y el 409 del reenvío), `Process` y `Logout`,
cada una con lo que entró y lo que salió. Ninguna fila trae contraseñas, tokens ni el contenido del archivo.

Pruebas de la pantalla Registros (después de cargar el escenario 1):

| Acción | Resultado esperado |
| --- | --- |
| Abrir Registros | No trae nada: pide elegir una fecha |
| Elegir `15/03/2024` y pulsar **Buscar** | Muestra los 10 registros cargados |
| Cambiar la fecha a `16/03/2024` | La tabla se vacía de inmediato (no queda la información de la fecha anterior) |
| Volver a `15/03/2024` y pulsar **Procesar fecha** | "5 registros enviados a cuarentena", enlace a la cuarentena de esa fecha, y la lista queda con 5 registros |
| Ir a Cuarentena y volver a Registros | La fecha y la tabla siguen ahí |
| Pulsar **Limpiar** | Se borran la fecha, la tabla y los mensajes. Al cambiar de pestaña y volver, siguen vacíos |

Pruebas de la pantalla Cuarentena:

| Acción | Resultado esperado |
| --- | --- |
| Abrir Cuarentena | No trae nada: pide elegir una fecha |
| Elegir `15/03/2024` y pulsar **Buscar** | 5 registros, cada uno con su motivo |
| Cambiar la fecha | La tabla se vacía de inmediato |
| Entrar desde el enlace de Registros | Llega con la fecha cargada y ya buscada |
| Cambiar de pestaña y volver | La fecha y la tabla siguen ahí |
| Pulsar **Limpiar** | Se borran la fecha y la tabla |

Pruebas de la pantalla Carga:

| Acción | Resultado esperado |
| --- | --- |
| Elegir un archivo `.txt` | El selector solo ofrece `.csv`. Si se fuerza, indica el nombre inválido |
| Elegir `invalid-date` | Muestra el error de la fila 7 y la fila resaltada. El botón Enviar queda deshabilitado |
| Enviar `valid-comma` | "Se cargaron 10 registros" con el enlace **Ver registros del 15/03/2024**. Al pulsarlo, Registros llega con la fecha cargada y ya buscada |
| Cambiar de pestaña y volver | El archivo seleccionado, la vista previa y el mensaje siguen ahí |
| Elegir otro archivo | Se borran el mensaje y el enlace del archivo anterior |
| Pulsar **Limpiar** | Se borran el archivo, la vista previa y el mensaje. No funciona mientras se está enviando |
| Doble clic en Enviar | Una sola petición en la pestaña Network |
| Cancelar durante la carga | Mensaje de carga cancelada y sin errores en la consola |
