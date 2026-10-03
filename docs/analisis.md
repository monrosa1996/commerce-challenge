# Análisis

## Problema

Cada día llega un archivo CSV con los comercios y sus documentos. Hay que cargarlo, detectar los registros con datos
incorrectos y apartarlos para revisarlos, sin perder ni duplicar información.

## Alcance

| Incluido | Fuera de alcance (ver [mejoras.md](mejoras.md)) |
| --- | --- |
| Cargar el archivo `commerce_DDMMYYYY.csv` con vista previa y revisión antes de enviar | Conciliar claves ya cargadas (hoy se rechazan) |
| Revisar la estructura del archivo y rechazarlo con un mensaje claro | Corregir registros desde la pantalla de cuarentena |
| Guardar todos los registros del archivo en `commerce` en una sola operación | Historial de cargas y reintentos |
| Procesar por fecha: revisar el nombre y el número, mover a cuarentena los registros con error y registrar el motivo | Roles y permisos por usuario |
| Consultar los registros cargados y los de cuarentena, con filtro por fecha | Paginación |
| Evitar doble clic y permitir cancelar la carga | Despliegue automático (CI/CD) |
| Inicio de sesión con usuario y contraseña (JWT) | Pantalla para consultar la auditoría (hoy se consulta en la tabla) |
| Auditoría de ingreso, salida, carga y procesado, con lo que entró y lo que salió | Alta de usuarios y cambio de contraseña desde la aplicación |
| Aviso y cierre de sesión al cerrar la pestaña o el navegador; recargar mantiene la sesión | Renovar la sesión sin volver a pedir contraseña y bloquear tras intentos fallidos |

## Actores

- **Usuario de negocio:** inicia sesión, sube archivos, procesa fechas y revisa la cuarentena.
- **Auditor / soporte:** consulta en la tabla de auditoría quién hizo qué, cuándo, y con qué datos.
- **Evaluador / equipo técnico:** levanta el sistema, ejecuta las pruebas y revisa el código.

## Reglas de negocio

| # | Regla |
| --- | --- |
| R1 | El archivo se llama `commerce_DDMMYYYY.csv` y la fecha del nombre debe existir |
| R2 | Tiene exactamente cinco columnas: `pc_processdate`, `pc_codcomred`, `pc_nomcomred`, `pc_tipdoc`, `pc_numdoc` |
| R3 | La clave de un registro es la fecha de proceso más el código del comercio. No se repite ni dentro del archivo ni en el sistema |
| R4 | Un error de formato (fecha, código, columnas) rechaza todo el archivo. No se carga nada a medias |
| R5 | La carga guarda todos los registros en la tabla `commerce`. El nombre y el número del comercio se revisan después, al **procesar** una fecha, con un procedimiento de SQL |
| R6 | El nombre no puede estar vacío. El número no puede estar vacío, ni tener letras, ni caracteres especiales |
| R7 | Un registro puede tener varios motivos de error. Se guardan juntos |
| R8 | Los datos se guardan tal como llegan, para que los registros con letras puedan llegar a la cuarentena |
| R9 | Al procesar, los registros que no cumplen salen de `commerce` y pasan a `commerce_quarantine` con su motivo. Procesar la misma fecha otra vez no cambia nada |
| R10 | Para usar cualquier función hay que iniciar sesión con usuario y contraseña. La sesión usa un token JWT que vence a los 30 minutos |
| R11 | Cada ingreso, ingreso rechazado, salida, carga de archivo y procesado de fecha queda en la tabla de auditoría: usuario, hora, dirección, resultado, lo que entró (request) y lo que salió (response). Nunca se guardan contraseñas, tokens ni el contenido del archivo |
| R12 | Un ingreso rechazado da siempre el mismo mensaje, exista o no el usuario |
| R13 | La sesión termina al cerrar sesión y al cerrar la pestaña o el navegador (el servidor la cierra y audita si la página no vuelve en 15 segundos). Recargar la página no la cierra. Con una sesión abierta, el navegador pide confirmación antes de salir |
| R14 | Un fallo al guardar la auditoría no impide la operación del usuario: se anota en el registro del servidor |
| R15 | La aplicación solo se puede usar en una pestaña del navegador a la vez. Una segunda pestaña o ventana del mismo navegador muestra el aviso "La aplicación ya está abierta" y no hace ninguna llamada al API |

## Requisitos no funcionales

| Tema | Meta |
| --- | --- |
| Tamaño | Hasta 5 MB y 50.000 filas por archivo (configurable) |
| Integridad | La carga es todo o nada. Un índice único protege la clave aunque dos usuarios suban lo mismo a la vez |
| Seguridad | Sesión con JWT firmado y de corta duración. Contraseñas con hash PBKDF2. La aplicación solo ejecuta procedimientos y el contenido del archivo nunca se arma como texto SQL |
| Trazabilidad | Toda acción relevante queda en la tabla de auditoría, con el dato de entrada y el de salida |
| Mensajes | Claros y en español, sin términos técnicos |
| Portabilidad | Mismo código en Windows y Mac. Versiones fijadas (`global.json`, `.nvmrc`, versiones exactas de paquetes) |
| Calidad | Compilación sin advertencias. Pruebas automáticas de archivo, servicio, API y base de datos |

## Supuestos

1. El enunciado no incluye un archivo de ejemplo, así que el formato se definió aquí (ver README).
2. Una fecha de proceso puede tener varias cargas, pero cada clave solo una vez.
3. La interfaz y los mensajes están en español. El código y los nombres de la base de datos fijados por el enunciado se respetan.
4. El motor es SQL Server 2017 o superior.
5. Los usuarios los crea un script de base de datos. No hay pantalla para administrarlos.

## Riesgos y cómo se tratan

| Riesgo | Tratamiento |
| --- | --- |
| Archivos con formato distinto al esperado | Revisión en pantalla y en la API con mensajes por fila |
| Doble carga del mismo archivo | Se rechaza con 409. El índice único cubre cargas simultáneas |
| Datos maliciosos dentro del archivo | Parámetros tipados y procedimientos. Ver la sección de seguridad en [arquitectura.md](arquitectura.md) |
| Archivos muy grandes | Límites de tamaño y de filas, y tope de errores informados |
| Contraseñas del repositorio | Son de desarrollo. Ver [SECURITY.md](../SECURITY.md) |
| Adivinar contraseñas probando muchas | Mismo mensaje para usuario y contraseña, mismo tiempo de respuesta y cada intento queda en la auditoría. Pendiente: bloqueo por intentos (ver [mejoras.md](mejoras.md)) |
| Robo del token | Vive en `sessionStorage` (propio de la pestaña, se borra al cerrarla), vence a los 30 minutos y se cierra en el servidor al salir |
