# API

Contrato del servicio. Los mensajes de esta guía son los que el sistema devuelve al usuario: están escritos para el
negocio, sin términos técnicos.

- Dirección en desarrollo: `http://localhost:5080`
- Documentación interactiva (solo en desarrollo): `http://localhost:5080/scalar`
- Colección de Postman: [postman/](postman/) · Solicitudes para VS Code: [`backend/http/commerce.http`](../backend/http/commerce.http)

## Resumen

| Operación | Método y ruta | Resultado correcto |
| --- | --- | --- |
| Iniciar sesión | `POST /api/auth/login` | 200 |
| Cerrar sesión | `POST /api/auth/logout` | 204 |
| La página se va (cerrar o recargar) | `POST /api/auth/closing` | 204 |
| La página volvió (recargó) | `POST /api/auth/resume` | 204 |
| Cargar un archivo | `POST /api/commerce/files` | 201 |
| Procesar una fecha | `POST /api/commerce/process` | 200 |
| Consultar los registros cargados | `GET /api/commerce` | 200 |
| Consultar registros en cuarentena | `GET /api/commerce/quarantine` | 200 |
| Comprobar que el servicio responde | `GET /health` | 200 |

## Sesión

Todas las operaciones, salvo iniciar sesión y `/health`, necesitan el token que entrega el inicio de sesión, en la
cabecera `Authorization: Bearer <token>`. El token vence a los 30 minutos y deja de servir al cerrar sesión.
Cada ingreso, salida, carga y procesado queda en la tabla de auditoría.

Usuarios de desarrollo: `admin` / `Admin#2026` y `analista` / `Analista#2026` (ver [SECURITY.md](../SECURITY.md)).

### Iniciar sesión

`POST /api/auth/login`

```json
{ "username": "admin", "password": "Admin#2026" }
```

**Respuesta correcta (200):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-10-03T07:48:28+00:00",
  "username": "admin",
  "displayName": "Administrador"
}
```

| Situación | Código | Mensaje | Qué hacer |
| --- | --- | --- | --- |
| Faltó el usuario o la contraseña | 400 | Ingrese usuario y contraseña. | Complete los dos campos |
| Usuario o contraseña incorrectos, o usuario inactivo | 401 | Usuario o contraseña incorrectos. | Revise los datos. El mensaje es el mismo si el usuario no existe |

### Cerrar sesión

`POST /api/auth/logout` (con el token). Cuerpo opcional: `{ "reason": "manual" }`. Responde `204` sin cuerpo.

`POST /api/auth/closing` y `POST /api/auth/resume` (con el token, sin cuerpo). Los usa la propia aplicación: `closing` avisa que la página se va; si no llega `resume` en 15 segundos, el servidor cierra la sesión y registra la salida. Responden `204`.
Desde ese momento el token deja de servir, aunque no haya vencido.

### Cuando no hay sesión

Cualquier otra operación responde `401`:

| Situación | Mensaje | Qué hacer |
| --- | --- | --- |
| No se envió el token | Debe iniciar sesión para continuar. | Inicie sesión |
| Token vencido, cerrado o inválido | Su sesión ha expirado o no es válida. Inicie sesión de nuevo. | Inicie sesión otra vez |

## Convenciones

| Tema | Regla |
| --- | --- |
| Formato | JSON con nombres en `camelCase` |
| Fechas de negocio | `yyyy-MM-dd` (por ejemplo `2024-03-15`) |
| Marcas de tiempo | UTC, formato ISO 8601 |
| Seguridad | Todo exige sesión (cabecera `Authorization`), salvo `POST /api/auth/login` y `GET /health` |
| Errores | Siempre con el mismo formato: `title`, `status`, `detail` y, si aplica, `errors` con la fila y la columna |
| Tipo de contenido de errores | `application/problem+json` (estándar RFC 9457) |
| Listas | Hasta 5000 registros, los más recientes primero |

Formato de un error:

```json
{
  "title": "Archivo inválido",
  "status": 400,
  "detail": "El archivo tiene filas con errores de formato.",
  "errors": [
    { "row": 7, "column": "pc_processdate", "message": "Fecha inválida: '31/02/2024'" }
  ]
}
```

`row` es el número de fila de datos (la primera fila después del encabezado es la 1).

---

## 1. Cargar un archivo

`POST /api/commerce/files`

Guarda todos los registros del archivo en la tabla de registros. El nombre y el número de cada registro se revisan después,
al procesar la fecha.

**Solicitud:** formulario `multipart/form-data` con un campo `file` que contiene el CSV.

```bash
curl -X POST http://localhost:5080/api/commerce/files \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@commerce_15032024.csv;type=text/csv"
```

**Reglas del archivo:** nombre `commerce_DDMMYYYY.csv`; columnas `pc_processdate`, `pc_codcomred`, `pc_nomcomred`,
`pc_tipdoc`, `pc_numdoc` en cualquier orden; separador `,` o `;`; fechas `yyyy-MM-dd` o `dd/MM/yyyy`; UTF-8;
hasta 5 MB y 50.000 filas.

**Respuesta correcta (201):**

```json
{
  "sourceFile": "commerce_15032024.csv",
  "insertedCount": 10,
  "warnings": []
}
```

Si el archivo mezcla los dos formatos de fecha, se carga igual y `warnings` trae:
`El archivo mezcla fechas con formato yyyy-MM-dd y dd/MM/yyyy. Se cargó igual.`

**Otras respuestas:**

| Situación | Código | Mensaje | Qué hacer |
| --- | --- | --- | --- |
| No se envió archivo o está vacío | 400 | El archivo está vacío | Seleccione un archivo con datos |
| Nombre incorrecto | 400 | El nombre del archivo debe tener el formato commerce_DDMMYYYY.csv | Renombre el archivo |
| Fecha del nombre imposible | 400 | El nombre del archivo contiene una fecha inválida | Corrija la fecha del nombre |
| Archivo demasiado grande | 413 | El archivo supera el máximo de 5 MB | Divida el archivo |
| No es un archivo de texto | 400 | El archivo no es un CSV de texto | Guarde el archivo como CSV UTF-8 |
| Sin separador | 400 | No se pudo detectar el separador (',' o ';') en el encabezado | Use coma o punto y coma |
| Columnas incorrectas | 400 | El encabezado no tiene la estructura esperada. Faltan columnas: pc_numdoc. | Agregue o quite las columnas indicadas |
| Solo encabezado | 400 | El archivo no contiene registros | Agregue las filas de datos |
| Demasiadas filas | 400 | El archivo supera el máximo de 50000 filas | Divida el archivo |
| Comillas mal cerradas | 400 | El archivo CSV está mal formado. Revise las comillas. | Revise las comillas del archivo |
| Problemas en filas | 400 | El archivo tiene filas con errores de formato. | Revise `errors`, que indica fila y columna |
| Clave ya cargada | 409 | Hay registros que ya existen en el sistema (fecha\|código): 2024-03-15\|1001, ... | Esos registros ya fueron cargados |
| Sin sesión o sesión vencida | 401 | Debe iniciar sesión para continuar. / Su sesión ha expirado o no es válida. Inicie sesión de nuevo. | Inicie sesión |
| Falla inesperada | 500 | Ocurrió un error inesperado. Intente de nuevo. | Reintente. Si persiste, avise a soporte |

Mensajes que aparecen dentro de `errors` (por fila):

| Mensaje | Significado |
| --- | --- |
| La fila tiene 4 campos y se esperaban 5 | Falta o sobra un dato en esa fila |
| Fecha inválida: '31/02/2024' | La fecha no existe o no tiene un formato permitido |
| El código del comercio está vacío | El código es obligatorio |
| El valor supera los N caracteres | El dato es más largo de lo permitido |
| Clave repetida 2024-03-20\|1003 en las filas 3, 8 | La misma fecha y código aparecen más de una vez |

La carga es todo o nada: si hay un error, no se guarda ninguna fila.

---

## 2. Procesar una fecha

`POST /api/commerce/process`

Revisa el nombre y el número de cada registro de la fecha y envía a cuarentena los que no cumplen. La revisión la hace la
base de datos con un procedimiento almacenado.

**Solicitud:**

```json
{ "processDate": "2024-03-15" }
```

**Respuesta correcta (200):**

```json
{ "processDate": "2024-03-15", "quarantinedCount": 5 }
```

`quarantinedCount` es la cantidad enviada a cuarentena. Si ya se procesó o no hay registros con error, es `0`.
Procesar la misma fecha dos veces es seguro.

| Situación | Código | Mensaje | Qué hacer |
| --- | --- | --- | --- |
| Falta la fecha | 400 | La fecha de proceso es obligatoria. | Indique una fecha |
| Formato o fecha incorrectos | 400 | La fecha de proceso debe tener el formato yyyy-MM-dd y ser una fecha real. | Use, por ejemplo, 2024-03-15 |
| Falla inesperada | 500 | Ocurrió un error inesperado. Intente de nuevo. | Reintente |

Motivos que se registran en la cuarentena (se pueden sumar, separados por `;`):

| Motivo |
| --- |
| El nombre del comercio (nomcomred) se encuentra vacío |
| El número (numdoc) se encuentra vacío |
| El número (numdoc) contiene letras |
| El número (numdoc) contiene caracteres especiales |

---

## 3. Consultar los registros cargados

`GET /api/commerce?processDate=2024-03-15`

`processDate` es opcional. Sin ella se listan todas las fechas. Muestra lo que hay en la tabla de registros: todo lo
cargado antes de procesar la fecha y solo los registros correctos después de procesarla.

**Respuesta correcta (200):**

```json
[
  {
    "id": 31,
    "sourceFile": "commerce_21032024.csv",
    "processDate": "2024-03-21",
    "codComRed": "2001",
    "nomComRed": "Ferreteria El Puerto",
    "tipDoc": "RUC",
    "numDoc": "0912345678001",
    "createdAt": "2026-10-03T04:59:12Z"
  }
]
```

Si no hay registros, la respuesta es una lista vacía `[]`.

| Situación | Código | Mensaje |
| --- | --- | --- |
| Fecha con formato incorrecto | 400 | La fecha de proceso debe tener el formato yyyy-MM-dd y ser una fecha real. |

---

## 4. Consultar registros en cuarentena

`GET /api/commerce/quarantine?processDate=2024-03-15`

Mismo filtro opcional. Son los registros que se apartaron al procesar una fecha. Cada uno trae el motivo.

**Respuesta correcta (200):**

```json
[
  {
    "id": 6,
    "sourceFile": "commerce_15032024.csv",
    "processDate": "2024-03-15",
    "codComRed": "1010",
    "nomComRed": null,
    "tipDoc": "RUC",
    "numDoc": "12A-45",
    "motivo": "El nombre del comercio (nomcomred) se encuentra vacío; El número (numdoc) contiene letras; El número (numdoc) contiene caracteres especiales",
    "quarantinedAt": "2026-10-03T04:58:40Z"
  }
]
```

Un dato faltante llega como `null`.

---

## 5. Estado del servicio

`GET /health` responde `200` con el texto `Healthy` cuando el servicio está en marcha. No necesita sesión.

---

## Buenas prácticas aplicadas

| Práctica | Cómo se aplica |
| --- | --- |
| Recursos con sustantivos y verbos HTTP con su significado | `GET` consulta, `POST` crea o ejecuta una acción. Sin verbos en la ruta, salvo la acción `process` |
| Códigos de estado precisos | 201 al crear, 400 solicitud inválida, 409 conflicto, 413 tamaño excedido, 500 falla propia |
| Errores uniformes y entendibles | Un solo formato (RFC 9457) y mensajes en lenguaje de negocio. Nunca trazas ni detalles internos |
| Validar en el borde | El archivo se revisa completo antes de tocar la base de datos |
| Operaciones seguras de repetir | Procesar la misma fecha dos veces no duplica nada. La carga repetida se rechaza con 409 |
| Todo o nada | La carga y el proceso son atómicos |
| Entrada acotada | Límites de tamaño, filas, longitud por columna y cantidad de errores informados |
| Seguridad por defecto | Parámetros tipados, usuario de base de datos con solo EXECUTE, CORS limitado al front, cabecera `nosniff`, HTTPS y HSTS fuera de desarrollo |
| Sin datos sensibles en respuestas ni en el registro de eventos | El log guarda rutas y errores, nunca el contenido del archivo |
| Documentación viva | OpenAPI y Scalar en desarrollo, colección de Postman y archivo `.http` |
| Autenticación con token en la cabecera, no en la dirección | `Authorization: Bearer`. Un token en la URL quedaría en los registros |
| Tokens de corta duración, cerrados en el servidor | 30 minutos y revocación al cerrar sesión |
| Mensajes de ingreso sin pistas | El mismo texto si el usuario no existe o la contraseña falla |
| Auditoría de lo que importa | Ingreso, salida, carga y procesado, con entrada y salida, sin contraseñas ni contenido del archivo |
| Estado del servicio | `/health` |

Pendientes recomendados (ver [mejoras.md](mejoras.md)): versionado de la ruta (`/api/v1`), paginación, `Idempotency-Key`
en la carga, bloqueo por intentos fallidos de ingreso, roles y renovación del token.

## Respuestas del framework

Dos casos los resuelve el framework y no tienen mensaje de negocio, porque no ocurren desde la pantalla: enviar la
carga sin formato de formulario (415) y enviar un cuerpo vacío o con JSON mal escrito al procesar (400).
