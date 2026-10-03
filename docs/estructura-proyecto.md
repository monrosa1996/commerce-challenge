# Estructura del proyecto

Un solo repositorio con el back end, el front end, la base de datos y la documentación.

```text
commerce-challenge/
├── README.md                      Cómo levantar y probar (resumen)
├── SECURITY.md                    Contraseñas de desarrollo y qué cambiar en producción
├── CHANGELOG.md                   Cambios por versión
├── .editorconfig                  Formato común en cualquier editor
├── .gitattributes                 Fin de línea LF: Windows y Mac no marcan todo como modificado
├── .gitignore                     bin, obj, node_modules, .angular, dist...
├── .vscode/
│   └── extensions.json            Extensiones recomendadas
│
├── backend/
│   ├── Commerce.sln               Solución con los cuatro proyectos
│   ├── global.json                Versión fija del SDK de .NET
│   ├── Directory.Build.props      Reglas comunes: nulos, advertencias como error, auditoría de paquetes
│   ├── Directory.Packages.props   Versiones exactas de los paquetes NuGet
│   ├── database/
│   │   ├── 01_create_database.sql Base y login (se ejecuta en master)
│   │   ├── 02_schema.sql          Tablas, función, procedimientos y usuario
│   │   ├── seed.sql               Datos de ejemplo (opcional)
│   │   ├── tests.sql              Pruebas de la base de datos (opcional)
│   │   └── samples/               Archivos CSV de ejemplo, uno por escenario
│   │       ├── all-correct/           Todo correcto: no deja nada en cuarentena
│   │       ├── valid-comma/           Archivo base: 5 correctos y 5 con errores de negocio
│   │       ├── valid-semicolon/       Mismo contenido con ";" y fechas dd/MM/yyyy
│   │       ├── empty/                 Archivo vacío
│   │       ├── missing-column/        Falta una columna
│   │       ├── invalid-date/          Fecha imposible en la fila 7
│   │       ├── duplicate-key/         Clave repetida en las filas 3 y 8
│   │       └── injection-attempt/     Textos con SQL y HTML malicioso
│   ├── http/
│   │   └── commerce.http          Solicitudes de prueba (extensión REST Client de VS Code)
│   ├── src/
│   │   ├── Commerce.Api/              Entrada HTTP
│   │   │   ├── Auth/                      Opciones del token, emisor de tokens JWT y datos del usuario en la solicitud
│   │   │   ├── Controllers/               CommerceController y AuthController (login y logout)
│   │   │   ├── Middleware/                Filtro de errores y manejador global
│   │   │   ├── Properties/                launchSettings (puerto 5080)
│   │   │   ├── Program.cs                 Arranque, autenticación JWT, CORS, límites, salud
│   │   │   └── appsettings*.json          Configuración y cadena de conexión de desarrollo
│   │   ├── Commerce.Application/      Reglas de negocio (sin SQL)
│   │   │   ├── Abstractions/              ICommerceRepository
│   │   │   ├── Audit/                     Auditoría: acciones, entradas y AuditService
│   │   │   ├── Auth/                      Inicio y cierre de sesión, hash de contraseñas, tokens cerrados
│   │   │   ├── Errors/                    Excepciones de validación, conflicto y sesión
│   │   │   ├── Files/                     Lector y revisión del CSV, nombre, fechas, límites
│   │   │   ├── Models/                    Registros y respuestas
│   │   │   └── Services/                  CommerceService
│   │   └── Commerce.Infrastructure/   Acceso a datos (Dapper)
│   │       ├── CommerceRepository.cs      Comercios
│   │       ├── UserRepository.cs          Usuarios (login)
│   │       ├── AuditRepository.cs         Auditoría
│   │       └── SqlConnectionFactory.cs
│   └── tests/
│       └── Commerce.Tests/            Pruebas xUnit: archivo, servicios, sesión, auditoría y API completa
│
├── frontend/
│   ├── package.json               Dependencias y comandos
│   ├── angular.json               Configuración de Angular
│   ├── .nvmrc                     Versión fija de Node
│   └── src/
│       ├── environments/              URL del API y límites de carga
│       └── app/
│           ├── core/                      Cliente del API, modelos, manejo de errores
│           │   └── auth/                      Sesión (memoria y sessionStorage), interceptor del token, guardias y avisos al salir
│           ├── shared/                    Revisión local del CSV (función pura), formato de fechas y sus pruebas
│           └── features/
│               ├── login/                 Pantalla Iniciar sesión
│               ├── upload/                Pantalla Carga y su estado (upload.state.ts)
│               ├── records/               Pantalla Registros y su estado (records.state.ts)
│               └── quarantine/            Pantalla Cuarentena y su estado (quarantine.state.ts)
│
├── scripts/
│   └── sqlserver-docker.sh        Levanta SQL Server en Docker y ejecuta los scripts (Mac y Linux)
│
└── docs/
    ├── README.md                  Índice de documentos
    ├── analisis.md · arquitectura.md · base-de-datos.md · diccionario-datos.md · decisions.md
    ├── api.md · pruebas.md · mejoras.md · implementacion-windows.md
    ├── estructura-proyecto.md     Este documento
    ├── img/                       Gráficos (SVG)
    └── postman/                   Colección y entorno de Postman
```

## Dónde buscar cada cosa

| Quiero cambiar... | Voy a... |
| --- | --- |
| Una regla del archivo (columnas, fechas, límites) | `backend/src/Commerce.Application/Files/CsvFileValidator.cs` y `frontend/src/app/shared/csv-validation.ts` |
| La regla de cuarentena | `fn_commerce_validation` en `backend/database/02_schema.sql` |
| Un mensaje al usuario | El mismo validador, o `ApiExceptionFilter.cs`, o `error.interceptor.ts` |
| Los límites de tamaño o filas | `appsettings.json` (sección `Upload`) y `environment.ts` |
| La cadena de conexión | `backend/src/Commerce.Api/appsettings.Development.json` |
| Una pantalla | `frontend/src/app/features/<pantalla>/` |
| Cómo se inicia sesión y se firma el token | `backend/src/Commerce.Application/Auth/AuthService.cs` y `backend/src/Commerce.Api/Auth/JwtTokenIssuer.cs` |
| Qué se audita y cómo | `backend/src/Commerce.Application/Audit/` y `CommerceService.cs` |
| La duración de la sesión o la clave del token | `appsettings.json` y `appsettings.Development.json` (sección `Jwt`) |
| La sesión del navegador y el aviso al salir | `frontend/src/app/core/auth/` |
