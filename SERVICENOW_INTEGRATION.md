# Integracion con ServiceNow

## Objetivo

El objetivo fue conectar la API del proyecto con ServiceNow para poder traer tickets de la tabla `incident` usando OAuth.

La integracion actual permite:

- Obtener un token OAuth desde ServiceNow.
- Consultar tickets en vivo desde `/api/now/table/incident`.
- Exponer esos tickets desde la API local.
- Preparar una sincronizacion hacia la base local cuando SQL Server/LocalDB este disponible.

## Flujo OAuth Validado

Primero se corrigio el endpoint de autenticacion.

Endpoint correcto:

```text
https://dev375453.service-now.com/oauth_token.do
```

No era:

```text
https://dev375453.service-now.com/oath_token.do
```

El body debe enviarse como `application/x-www-form-urlencoded`:

```text
grant_type=password
client_id=...
client_secret=...
username=admin
password=...
```

En Bash/Git Bash se deben usar comillas simples para valores con caracteres especiales como `!`, `;`, `|`, `)` o `^`.

Ejemplo:

```bash
curl -X POST "https://dev375453.service-now.com/oauth_token.do" \
  -H "Accept: application/json" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "grant_type=password" \
  --data-urlencode "client_id=TU_CLIENT_ID" \
  --data-urlencode 'client_secret=TU_CLIENT_SECRET' \
  --data-urlencode "username=admin" \
  --data-urlencode 'password=TU_PASSWORD'
```

## Configuracion en ServiceNow

La OAuth Application Registry tenia `Scope Restriction` en modo restringido, lo que producia este error:

```json
{
  "error": {
    "message": "User Not Authorized",
    "detail": "Access to unscoped api is not allowed"
  },
  "status": "failure"
}
```

Se cambio la configuracion para permitir llamar APIs globales/no scoped, como:

```text
/api/now/table/incident
```

Luego se pudo consultar ServiceNow correctamente con:

```bash
curl "https://dev375453.service-now.com/api/now/table/incident?sysparm_limit=5" \
  -H "Accept: application/json" \
  -H "Authorization: Bearer TU_ACCESS_TOKEN"
```

## Configuracion Local

Los secretos se guardan con `dotnet user-secrets`, no en `appsettings.json`.

Ejemplo:

```bash
dotnet user-secrets set 'ServiceNow:BaseUrl' 'https://dev375453.service-now.com'
dotnet user-secrets set 'ServiceNow:ClientId' 'TU_CLIENT_ID'
dotnet user-secrets set 'ServiceNow:ClientSecret' 'TU_CLIENT_SECRET'
dotnet user-secrets set 'ServiceNow:Username' 'admin'
dotnet user-secrets set 'ServiceNow:Password' 'TU_PASSWORD'
```

Para probar sin SQL Server/LocalDB:

```bash
dotnet user-secrets set 'Database:MigrateOnStartup' 'false'
dotnet user-secrets set 'ServiceNow:SyncEnabled' 'false'
```

## Endpoints Agregados

### Traer tickets en vivo desde ServiceNow

```text
GET /tickets/servicenow
```

Ejemplo:

```text
http://localhost:5038/tickets/servicenow?limit=100&query=ORDERBYDESCsys_updated_on
```

Este endpoint consulta ServiceNow directamente. No usa la base local.

### Sincronizar tickets a la base local

```text
POST /tickets/sync-servicenow
```

Ejemplo:

```bash
curl -X POST "http://localhost:5038/tickets/sync-servicenow?limit=100&query=ORDERBYDESCsys_updated_on"
```

Este endpoint requiere que SQL Server/LocalDB este configurado, porque guarda o actualiza registros en la tabla local `Tickets`.

## Query Para Traer Todos Los Tickets

Para traer todos los tickets visibles para el usuario OAuth, se debe evitar filtrar por `caller_id`.

Query recomendada:

```text
ORDERBYDESCsys_updated_on
```

URL local:

```text
http://localhost:5038/tickets/servicenow?limit=100&query=ORDERBYDESCsys_updated_on
```

Para tickets activos:

```text
active=true^ORDERBYDESCsys_updated_on
```

En URL, el caracter `^` puede codificarse como `%5E`:

```text
http://localhost:5038/tickets/servicenow?limit=100&query=active=true%5EORDERBYDESCsys_updated_on
```

Importante: el link de la UI de ServiceNow que tenia:

```text
caller_id=javascript:gs.getUserID()
```

filtra por tickets del usuario logueado. Para traer tickets de todos los usuarios, ese filtro no debe usarse.

## Clases Modificadas

### `Modules/ServiceNow/ServiceNowConnector.cs`

Antes era un conector simple con Basic Auth.

Se modifico para:

- Pedir token OAuth automaticamente a `/oauth_token.do`.
- Usar `Authorization: Bearer`.
- Leer configuracion desde `user-secrets` o variables de entorno.
- Llamar a `/api/now/table/incident`.
- Aceptar `sysparm_query`.
- Ordenar por defecto por `sys_updated_on` descendente.
- Mapear el estado `7` como `Closed`.

### `Modules/ServiceNow/ServiceNowModels.cs`

Se agrego el modelo:

```csharp
ServiceNowTokenResponse
```

Este modelo representa la respuesta OAuth:

```json
{
  "access_token": "...",
  "refresh_token": "...",
  "token_type": "Bearer",
  "expires_in": 1799
}
```

### `Modules/Tickets/TicketService.cs`

Antes solo trabajaba contra la base local.

Se modifico para:

- Traer tickets directamente desde ServiceNow.
- Mapear `ServiceNowIncident` a `Ticket`.
- Sincronizar tickets contra la base local.
- Actualizar tickets existentes usando `SysId`.

### `Modules/Tickets/ITicketService.cs`

Se agregaron metodos al contrato:

```csharp
GetFromServiceNowAsync(...)
SyncFromServiceNowAsync(...)
```

Esto permite que los endpoints usen el servicio para obtener o sincronizar tickets de ServiceNow.

### `Modules/Tickets/TicketEndpoints.cs`

Se agregaron endpoints:

```text
GET /tickets/servicenow
POST /tickets/sync-servicenow
```

Tambien se marcaron como `AllowAnonymous` para poder probarlos, porque la API tiene una fallback policy de Cognito que exige autenticacion por defecto.

### `Modules/Tickets/TicketModule.cs`

Se registro el conector y el worker en Dependency Injection:

```csharp
services.AddHttpClient<IServiceNowConnector, ServiceNowConnector>();
services.AddHostedService<ServiceNowSyncWorker>();
```

### `Program.cs`

Se hizo opcional la migracion automatica de Entity Framework:

```csharp
Database:MigrateOnStartup
```

Esto fue necesario porque la maquina local no tenia SQL Server LocalDB instalado y la API no podia iniciar.

### `Extensions/ExceptionExtensions.cs`

En `Development`, ahora se muestra el mensaje real del error `500`.

Esto ayudo a detectar errores como:

```text
ServiceNow OAuth returned Unauthorized
```

## Clases Creadas

### `Modules/ServiceNow/ServiceNowSyncWorker.cs`

Se creo un worker en background para sincronizar tickets automaticamente.

Cuando este habilitado, ejecuta periodicamente:

```csharp
SyncFromServiceNowAsync(...)
```

Configuracion:

```bash
dotnet user-secrets set 'ServiceNow:SyncEnabled' 'true'
dotnet user-secrets set 'ServiceNow:SyncIntervalMinutes' '5'
dotnet user-secrets set 'ServiceNow:SyncLimit' '100'
dotnet user-secrets set 'ServiceNow:SyncQuery' 'ORDERBYDESCsys_updated_on'
```

Por ahora debe quedar desactivado si no hay SQL Server/LocalDB:

```bash
dotnet user-secrets set 'ServiceNow:SyncEnabled' 'false'
```

## Estado Actual

Funciona:

```text
GET http://localhost:5038/tickets/servicenow?limit=100&query=ORDERBYDESCsys_updated_on
```

Esto trae tickets en vivo desde ServiceNow.

Pendiente:

- Instalar o configurar SQL Server/LocalDB.
- Activar `/tickets/sync-servicenow`.
- Activar `ServiceNowSyncWorker`.
- Agregar paginacion con `sysparm_offset` si se necesita descargar todos los tickets en lotes.
- Rotar secretos de ServiceNow porque fueron expuestos durante las pruebas.
