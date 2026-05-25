# Prueba Tecnica Full-Stack Jr

Aplicacion de autenticacion y CRUD de usuarios con:
- Frontend: Angular
- Backend: ASP.NET Core Web API (.NET 8)
- Base de datos: SQL Server
- Seguridad: JWT + hash de contrasenas con BCrypt

## Estructura del proyecto

```text
prueba-fullstack-jr/
|- backend/
|- backend.Tests/
|- frontend/
```

## Supuestos

- Ejecucion en entorno local de desarrollo (no produccion).
- SQL Server esta instalado/activo y accesible desde la maquina local.
- Puertos por defecto disponibles: backend `5241`, frontend `4200`.
- `ASPNETCORE_ENVIRONMENT=Development` para usar Swagger UI.

## Requisitos previos

- .NET SDK 8.0+
- SQL Server local (SQLEXPRESS, LocalDB o equivalente)
- Node.js 20+ (recomendado 22+)
- npm
- Angular CLI
- dotnet-ef

Instalar herramientas si hacen falta:

```powershell
npm install -g @angular/cli
dotnet tool install --global dotnet-ef
```

## Configuracion de secretos (backend)

El backend carga variables desde `backend/.env`.

1. Ir a backend:

```powershell
cd backend
```

2. Copiar plantilla:

```powershell
Copy-Item .env.example .env
```

3. Editar `backend/.env` con valores reales:

```env
ConnectionStrings__DefaultConnection=Server=TU_SERVIDOR_SQL\TU_INSTANCIA;Database=TU_BASE_DE_DATOS;Trusted_Connection=True;TrustServerCertificate=True;
Jwt__Key=REEMPLAZAR_POR_UNA_CLAVE_LARGA_Y_SEGURA_MINIMO_32_CARACTERES
Jwt__Issuer=FullStackJrApi
Jwt__Audience=FullStackJrClient
Jwt__ExpiresInMinutes=60
Jwt__RefreshTokenExpiresInDays=7
Security__PasswordMinLength=8
Security__MaxFailedLoginAttempts=5
Security__LockoutMinutes=15
```

Notas:
- `Jwt__Key` debe tener minimo 32 caracteres.
- `Jwt__RefreshTokenExpiresInDays` define la vigencia del refresh token (1 a 90 dias, default 7).
- `Security__PasswordMinLength` define longitud minima de contrasena (8 a 128).
- `Security__MaxFailedLoginAttempts` define intentos maximos antes de bloqueo (1 a 20).
- `Security__LockoutMinutes` define duracion del bloqueo temporal (1 a 1440).
- `backend/.env` no se sube al repo.
- `backend/.env.example` si se sube como plantilla.

## Base de datos

Conexion SQL Server:
- Se usa `ConnectionStrings__DefaultConnection` desde `backend/.env`.

Ejemplo con autenticacion de Windows:

```env
ConnectionStrings__DefaultConnection=Server=.\SQLEXPRESS;Database=FullStackJrDb;Trusted_Connection=True;TrustServerCertificate=True;
```

Ejemplo con usuario/clave SQL:

```env
ConnectionStrings__DefaultConnection=Server=localhost,1433;Database=FullStackJrDb;User Id=sa;Password=TuPassword!123;TrustServerCertificate=True;
```

Desde `backend/`:

```powershell
dotnet ef database update
```

Esto crea la base y las tablas `Users`, `RefreshTokens` y `AuthAuditLogs` usando migraciones.

## Ejecucion del backend

Desde `backend/`:

```powershell
dotnet restore
dotnet run
```

URLs:
- API: `http://localhost:5241`
- Swagger: `http://localhost:5241/swagger`

## Ejecucion del frontend

Desde `frontend/`:

```powershell
npm install
npm start
```

URL:
- `http://localhost:4200`

El frontend consume:
- `http://localhost:5241/api`

Si cambias el puerto del backend, actualiza:
- `frontend/src/app/core/api.config.ts`

## Optimizaciones frontend (Lighthouse)

- Lazy loading de rutas/componentes con `loadComponent` para reducir el bundle inicial.
- `ChangeDetectionStrategy.OnPush` en componentes clave para evitar renders innecesarios.
- `trackBy` en listados para minimizar recreacion de nodos en cambios de estado.
- Ajustes base de `index.html` para Lighthouse:
  - `lang="es"`, `meta description`, `theme-color`
  - carga no bloqueante de Font Awesome (`preconnect` + `preload` + `noscript`).

## Credenciales de prueba

Objetivo de prueba:
- `admin@demo.com / Admin123!`
- `user@demo.com / User123!`

En `Development`, el backend ejecuta un seeder al iniciar y garantiza que ambas cuentas existan (si faltan, las crea; si existen, las corrige al estado demo esperado).

## Endpoints principales

Autenticacion:
- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`

Auditoria:
- `GET /api/audit/auth` (admin, con filtros, paginacion y ordenamiento)

Usuarios:
- `GET /api/users?search=&page=&size=&sortBy=&sortDir=` (admin)
- `GET /api/users/{id}` (admin o dueno)
- `POST /api/users` (admin)
- `PUT /api/users/{id}` (admin o dueno con restricciones)
- `DELETE /api/users/{id}` (admin)
- `POST /api/users/{id}/avatar` (admin o dueno, multipart/form-data)
- `DELETE /api/users/{id}/avatar` (admin o dueno)

## Patron de servicios (`AppServiceResult`)

La capa de aplicacion (`AuthAppService`, `UserAppService`) usa un contrato uniforme:
- `AppServiceResult<T>` para exponer resultado de negocio.
- `IsSuccess=true` con `Data`.
- `IsSuccess=false` con `Error` (`Type`, `Code`, `Message`).

Tipos de error estandar:
- `BadRequest`
- `Unauthorized`
- `Forbidden`
- `NotFound`
- `Conflict`

Los controladores traducen este resultado a HTTP:
- `BadRequest` -> `400`
- `Unauthorized` -> `401`
- `Forbidden` -> `403`
- `NotFound` -> `404`
- `Conflict` -> `409`

## Avatar de usuario

- Almacenamiento local: `backend/uploads/avatars/`.
- Archivos permitidos: `image/jpeg`, `image/png`, `image/webp`.
- Tamano maximo: `2 MB` por imagen.
- En frontend, la carga de avatar se hace desde la vista `Mi Perfil`.
- El backend devuelve `avatarUrl` en los DTOs de usuario para renderizar la imagen.

## Pruebas de API

### Swagger

1. Abrir `http://localhost:5241/swagger`.
2. Ejecutar login o register.
3. Copiar `accessToken`.
4. Autorizar en Swagger con `Bearer <token>`.
5. Probar endpoints de `/api/users`.

### Archivo HTTP

Tambien puedes usar:
- `backend/backend.http`

### Postman

Tambien puedes importar la coleccion versionada:
- `backend/docs/postman/fullstack-jr-api.postman_collection.json`

## Pruebas automatizadas

### Frontend

Desde `frontend/`:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless
```

Estado actual esperado:
- el total puede variar segun la suite y la version del frontend.

### Backend

Desde la raiz del repo:

```powershell
dotnet test backend.Tests\backend.Tests.csproj
```

Estado actual esperado:
- el total de pruebas puede variar segun la suite vigente.

## Estado de verificacion final

Verificacion realizada el **21 de mayo de 2026** (America/Bogota).

Comandos ejecutados y resultado:

1. Backend build

```powershell
dotnet build backend\backend.csproj
```

Resultado: compilacion correcta, 0 errores.

2. Backend tests

```powershell
dotnet test backend.Tests\backend.Tests.csproj
```

Resultado: el total de pruebas superadas depende de la version actual de la suite.

3. Frontend build

```powershell
cd frontend
npm run build
```

Resultado: build exitoso, salida en `frontend/dist/frontend`.

4. Frontend tests

```powershell
cd frontend
npm test -- --watch=false --browsers=ChromeHeadless
```

Resultado: el total de pruebas superadas depende de la version actual de la suite.

## Reglas de autorizacion implementadas

- `admin`: CRUD completo de usuarios.
- `user`: solo puede ver/editar su propio perfil.

## Sesion y tokens

- `accessToken`: JWT de corta duracion (`Jwt__ExpiresInMinutes`, por defecto 60).
- `refreshToken`: token opaco persistido como hash en DB (`Jwt__RefreshTokenExpiresInDays`, por defecto 7).
- El frontend intenta renovar el `accessToken` automaticamente cuando recibe `401`.
- Si el `refreshToken` ya no es valido, se cierra sesion y se redirige a login.

## Endurecimiento de seguridad

- Politica de contrasenas en backend para register/create/update:
  - minimo configurable
  - al menos mayuscula, minuscula, numero y simbolo
- Bloqueo temporal por intentos fallidos de login:
  - contador por usuario (`FailedLoginAttempts`)
  - bloqueo hasta fecha/hora (`LockoutEndAt`)
  - umbral y duracion configurables por `.env`
- Rate limiting basico:
  - limite global por IP
  - limite mas estricto para `/api/auth/*`
- CSRF:
  - para esta API JWT por header `Authorization` (sin cookies de sesion) no aplica proteccion CSRF clasica.
  - si en el futuro agregas endpoints MVC/Razor con cookies, ahi si debes habilitar antiforgery token.

## Auditoria de autenticacion

Se implemento auditoria de eventos de autenticacion en la tabla `AuthAuditLogs`.

Eventos registrados:
- `login_success`
- `login_failed`
- `refresh_success`
- `refresh_failed`
- `logout_success`
- `logout_failed`

Campos principales:
- `EventType`, `IsSuccess`, `UserId`, `Email`
- `FailureReason` (cuando aplica)
- `IpAddress`, `UserAgent`
- `CreatedAt` (UTC)

Consulta por API (solo admin):
- `GET /api/audit/auth?page=1&size=20`
- Filtros opcionales:
  - `eventType` (`login_success`, `login_failed`, `refresh_success`, `refresh_failed`, `logout_success`, `logout_failed`)
  - `isSuccess` (`true` / `false`)
  - `userId` (GUID)
  - `email`
  - `fromUtc` y `toUtc` (ISO-8601 UTC, ejemplo `2026-05-22T00:00:00Z`)
  - `sortBy` (`createdAt`, `eventType`, `isSuccess`, `email`, `userId`)
  - `sortDir` (`asc` o `desc`)

Consulta de usuarios (solo admin):
- `GET /api/users?search=&page=&size=&sortBy=&sortDir=`
- `sortBy` soportado: `createdAt`, `email`, `name`, `role`, `isActive`
- `sortDir`: `asc` o `desc`

## Solucion de problemas rapida

- Error `JWT Key is missing or insecure`:
  - Revisar `Jwt__Key` en `backend/.env`.
- Error de conexion SQL:
  - Revisar `ConnectionStrings__DefaultConnection` y que SQL Server este activo.
- Error CORS:
  - Verificar frontend en `http://localhost:4200` y backend en `http://localhost:5241`.
- Error `401`:
  - Access token invalido/expirado. El frontend intenta refresh automaticamente.
  - Si el refresh falla, volver a iniciar sesion.
- Error `403`:
  - Accion restringida por rol o por propiedad del recurso.

## Limitaciones conocidas

- CORS configurado para `http://localhost:4200` en desarrollo.
- No hay `docker-compose` para levantar backend + frontend + SQL Server en un solo comando.

## Proximos pasos

1. Parametrizar origenes CORS por entorno (`dev`, `staging`, `prod`).
2. Agregar `docker-compose` para facilitar onboarding y pruebas locales.
