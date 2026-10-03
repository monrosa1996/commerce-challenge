# Implementación en Windows

Guía para un perfil técnico. Se asume que ya tiene SQL Server, un cliente SQL y las herramientas de desarrollo.
Aquí está lo que importa para que **compile y ejecute**: versiones, orden de los scripts y configuración.

## 1. Versiones

| Componente | Requerido | Verificado | Qué pasa si no se cumple |
| --- | --- | --- | --- |
| Sistema operativo | Windows 10 u 11 (requisito de .NET 10) | — | — |
| .NET SDK | **10.0.100 o superior** (`backend/global.json`, `rollForward: latestFeature`) | 10.0.401 | Con SDK 9 o anterior no compila: `global.json` y `net10.0` exigen el SDK 10 |
| ASP.NET Core runtime | 10.0.x (viene con el SDK) | 10.0.12 | No arranca la API |
| Node.js | **`^20.19` o `^22.12` o `>=24`** (lo exige Angular CLI 21). Recomendado 24 LTS (`frontend/.nvmrc`) | 25.7.0 | Con una versión fuera de ese rango, `ng build` se niega a ejecutarse |
| npm | 10 o superior | 11.10.1 | — |
| Angular / TypeScript | 21.2.x / 5.9.x, fijados por `package-lock.json` | 21.2.25 / 5.9.3 | Usar `npm ci`, no `npm install`. Angular 21 no admite TypeScript 6 |
| SQL Server | **2017 (14.x) o superior** | 2022 (16.0.4295, CU27) | Ver la sección 2 |
| Editor | Opcional: Visual Studio con soporte de .NET 10, o VS Code con C# Dev Kit | VS Code | La solución es `backend/Commerce.sln` |

Paquetes NuGet (versiones exactas en `backend/Directory.Packages.props`): Dapper 2.1.89, Microsoft.Data.SqlClient 6.1.7,
CsvHelper 33.1.0, Scalar.AspNetCore 2.17.13, Microsoft.AspNetCore.OpenApi 10.0.12. Pruebas: xunit 2.9.3,
Microsoft.NET.Test.Sdk 17.14.1, Microsoft.AspNetCore.Mvc.Testing 10.0.12. La restauración necesita acceso a nuget.org y npmjs.com.

Puertos usados: API `5080`, pantallas `4200`, SQL Server `1433`.

## 2. Compatibilidad con SQL Server

Los scripts son compatibles con **SQL Server 2017 (14.x) y superiores**, hasta 2022 (16.x), en cualquier edición
(Developer, Express, Standard o Enterprise). Verificado en 2022. No sirven en Azure SQL Database, porque crean la base y el
login. La base debe usar una intercalación que no distinga mayúsculas (la predeterminada, `SQL_Latin1_General_CP1_CI_AS`).

### Qué exige la conexión

La API usa Microsoft.Data.SqlClient 6.1.7, que **cifra por defecto** (`Encrypt=True`).

| Requisito | Detalle |
| --- | --- |
| TLS 1.2 en el servidor | En SQL Server 2017 sin parches recientes puede faltar |
| Certificado | La cadena de conexión incluida ya acepta certificados autofirmados (`TrustServerCertificate=True`) |
| Autenticación | La API usa el login `commerce_app`: requiere **modo de autenticación mixto** |
| Red | La cadena incluida apunta a `localhost,1433`: TCP/IP habilitado en ese puerto |
| Permisos | El script 01 necesita un inicio de sesión con permiso de crear bases y logins (`sysadmin`) |

## 3. Obtener el código

Descargue el repositorio como ZIP desde la página del repositorio y descomprímalo, o clónelo si ya usa Git.
Los comandos siguientes se ejecutan desde la carpeta raíz (`commerce-challenge`).

## 4. Base de datos

Ejecute los scripts de `backend\database` **en este orden**, con cualquier cliente que entienda `GO` como separador de
lotes (SSMS, Azure Data Studio, DBeaver, `sqlcmd`, extensión mssql de VS Code). Ejecute el archivo completo como
*script*, no sentencia por sentencia. Cada script se puede repetir sin error.

| Orden | Script | Base de contexto | Qué hace | ¿Obligatorio? |
| --- | --- | --- | --- | --- |
| 1 | `01_create_database.sql` | `master` | Crea `CommerceDb` y el login `commerce_app` | Sí |
| 2 | `02_schema.sql` | `CommerceDb` | Crea tablas, tipo de tabla, función, procedimientos y permisos | Sí |
| 3 | `seed.sql` | `CommerceDb` | Carga 10 registros de ejemplo | No |
| 4 | `tests.sql` | `CommerceDb` | Pruebas de la base. Termina con `All database tests passed.` y no deja datos | No |

- Cada script ya incluye su `USE`. Si su cliente no respeta el cambio de base dentro del script, conéctese a la base
  indicada en la columna "Base de contexto".
- El cliente debe estar en modo de confirmación automática: `CREATE DATABASE` no puede ir dentro de una transacción.
- No ejecute `seed.sql` si va a probar subiendo `commerce_15032024.csv`: ya trae esos registros y la carga daría 409.
- Las contraseñas de `sa` y de `commerce_app` son de desarrollo. La de `commerce_app` está en el script 01.

## 5. Ejecutar

**Clave del token JWT.** En desarrollo ya viene en `appsettings.Development.json`. Fuera de desarrollo la API no arranca sin
`Jwt:SigningKey` (32 caracteres o más): defínala con la variable de entorno `Jwt__SigningKey`, nunca en el repositorio.

**Usuarios de la aplicación** (los crea `02_schema.sql`): `admin` / `Admin#2026` y `analista` / `Analista#2026`.

API (primera terminal):

```powershell
dotnet run --project backend\src\Commerce.Api
```

Debe mostrar `Now listening on: http://localhost:5080`. Compruebe `http://localhost:5080/health` (responde `Healthy`).
La documentación interactiva está en `http://localhost:5080/scalar`.

Pantallas (segunda terminal):

```powershell
cd frontend
npm ci
npm start
```

Abra `http://localhost:4200`.

## 6. Verificación

```powershell
dotnet build backend\Commerce.sln        # 0 advertencias, 0 errores
dotnet test backend\Commerce.sln         # 112 pruebas en verde
cd frontend; npm run test:ci; npm run build   # 83 pruebas en verde y compilación de producción
```

Recorrido funcional:

1. Abra `http://localhost:4200`: lleva a **Iniciar sesión**. Entre con `admin` / `Admin#2026`.
2. Pantalla **Carga**: `backend\database\samples\valid-comma\commerce_15032024.csv` → "Se cargaron 10 registros" y el enlace "Ver registros del 15/03/2024".
3. Pantalla **Registros**: fecha `15/03/2024` → **Buscar** → 10 registros.
4. **Procesar fecha** → "5 registros enviados a cuarentena". La lista queda con 5 registros.
5. Pantalla **Cuarentena**: fecha `15/03/2024` → **Buscar** → 5 registros, cada uno con su motivo.
6. **Cerrar sesión**. En la base de datos, la consulta de `dbo.audit_log` (ver docs/base-de-datos.md) muestra el ingreso, la carga, el procesado y la salida.
