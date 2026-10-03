# Mejoras propuestas

Lo que se diseñó y no se construyó en esta versión, más ideas de evolución.

## Funcionales

| # | Mejora | Valor para el negocio | Diseño resumido |
| --- | --- | --- | --- |
| 1 | **Conciliación de claves existentes** | Hoy un archivo con una clave ya cargada se rechaza completo. Permitiría corregir y recargar | `sp_compare_commerce` une por clave y clasifica en Nuevo, Modificado, Idéntico. Pantalla con pestañas "Nuevos y modificados" (marcados) y "Repetidos" (desmarcados). `MERGE` solo de lo seleccionado |
| 2 | **Resolución de la cuarentena** | Hoy los registros apartados solo se consultan. Permitiría corregirlos y devolverlos | Columnas `resuelto`, `justificacion`, `resolved_at`. Editar nombre y número, exigir justificación, validar con la misma función y pasar a `commerce` |
| 3 | **Historial de cargas y reintento** | Saber qué se cargó, cuándo y qué falló. Reintentar sin volver a subir | Tabla `commerce_upload` con estados (Recibida, En proceso, Pendiente de conciliar, Aplicada, Fallida, Cancelada, Descartada), intentos y último error. Archivo temporal en disco |
| 4 | **Filtro por periodo y por estado** | Consultar la última semana, mes o año, y ver resueltos o pendientes | Parámetro `period` (7, 30 o 365 días) en ambas listas. Filtro "resuelto sí/no" en cuarentena |
| 5 | **Paginación y búsqueda** | Hoy se muestran hasta 5000 filas | Paginación por clave en el servidor, búsqueda por código y nombre, indicador `truncated` |
| 6 | **Exportar a Excel o CSV** | Entregar la cuarentena a otra área | Descarga con protección contra fórmulas (anteponer `'` a celdas que empiecen con `=`, `+`, `-`, `@`) |
| 7 | **Revertir una carga** | Deshacer una carga equivocada | Historial de versiones de los registros insertados y actualizados |
| 8 | **Proceso programado** | Procesar cada día sin intervención | Tarea diaria que procesa la fecha anterior y avisa el resultado |
| 9 | **Más formatos de archivo** | Aceptar CSV guardados desde Excel en Windows | Detectar Windows-1252 y convertir, avisando al usuario |

## Seguridad

| # | Mejora | Detalle |
| --- | --- | --- |
| 10 | **Roles y permisos** | Hoy todo usuario con sesión puede cargar, procesar y consultar. Roles: carga, consulta y administración, dentro del token |
| 11 | **Secretos fuera del repositorio** | `dotnet user-secrets` en desarrollo y Key Vault en producción. `Trusted_Connection` donde exista autenticación de Windows |
| 12 | **Control de uso y bloqueo por intentos fallidos** | Límite de peticiones por usuario y bloqueo temporal tras varios ingresos rechazados (hoy cada intento queda en la auditoría, pero no se bloquea) |
| 13 | **Pantalla de auditoría** | Consultar la tabla `audit_log` desde la aplicación, con filtros por usuario, acción y fecha, y exportar |
| 14 | **Idempotencia en la carga** | Cabecera `Idempotency-Key` única por carga. La misma clave devuelve el primer resultado; la misma clave con otro archivo, 409 |
| 15 | **Análisis de dependencias en cada cambio** | `dotnet list package --vulnerable` y `npm audit --omit=dev` automáticos |
| 16 | **Renovar la sesión sin pedir contraseña** | Token corto más token de renovación, para no cerrar la sesión a los 30 minutos si el usuario sigue trabajando |
| 17 | **Alta de usuarios y cambio de contraseña** | Hoy los usuarios los crea un script. Pantalla de administración, cambio y recuperación de contraseña, y política de contraseñas |
| 18 | **Sesión en cookie `HttpOnly`** | Alternativa al token en `sessionStorage`: la cookie no la puede leer el código de la página. Exige proteger contra CSRF |
| 19 | **Cierre de sesión que no depende de una sola instancia** | La lista de tokens cerrados y los cierres pendientes están en memoria. Con varias instancias del API habría que compartirla (base de datos o caché) |
| 20 | **Aviso de cierre de sesión por inactividad** | Cerrar la sesión tras unos minutos sin uso y avisar antes |
| 21 | **Una sola sesión por usuario, controlada en el servidor** | Hoy una sola pestaña lo controla la pantalla (mismo navegador). Para impedir dos sesiones del mismo usuario en equipos distintos, el servidor debe recordar la sesión activa de cada usuario y cerrar la anterior al ingresar de nuevo |

## Calidad y operación

| # | Mejora | Detalle |
| --- | --- | --- |
| 22 | **Integración continua** | Compilar, probar y auditar en cada cambio (GitHub Actions) |
| 23 | **Migraciones de base de datos** | Pasar los scripts a una herramienta de versionado (DbUp, Flyway) con historial |
| 24 | **Pruebas de integración con SQL Server real** | API contra un contenedor de pruebas (Testcontainers) |
| 25 | **Pruebas de pantallas y extremo a extremo** | Componentes de Angular y flujos con Playwright |
| 26 | **Observabilidad** | Registro estructurado, identificador de correlación, métricas y `/health` que verifique la base |
| 27 | **Versionado de la API** | Ruta `/api/v1`, OpenAPI publicado y Postman ejecutado en el pipeline |
| 28 | **Docker para desarrollo** | Un archivo de composición con la base y el API, para levantar todo con un comando |
| 29 | **Traducción y accesibilidad** | Textos en archivos de idioma y revisión de accesibilidad de las tablas y formularios |

## Rendimiento

| # | Mejora | Cuándo hacerla |
| --- | --- | --- |
| 30 | **Carga masiva con `SqlBulkCopy` a una tabla de paso** | Cuando los archivos pasen de unas decenas de miles de filas. Hoy se envía todo en un parámetro de tabla, en una sola operación |
| 31 | **Lectura del archivo por partes** | Si se sube el límite de 5 MB. Hoy el archivo se lee completo en memoria |
| 32 | **Índices adicionales** | Si las consultas por fecha y código crecen: índices con columnas incluidas sobre `commerce` y `commerce_quarantine` |
