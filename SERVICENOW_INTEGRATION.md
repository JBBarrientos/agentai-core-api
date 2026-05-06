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

# Preparacion para Microsoft Teams

## Objetivo

Se agrego una estructura inicial para que la API pueda notificar por Teams cuando la IA tome, resuelva o escale un ticket.

Por ahora el envio real a Teams queda desactivado/fake, porque todavia falta el consentimiento de administrador en Microsoft Graph. Esto permite probar el flujo sin depender de permisos externos.

## App de Azure

La app creada en Microsoft Entra ID es:

```text
AgentAI Teams Notifications
```

Permiso agregado:

```text
Microsoft Graph / Application permission / User.Read.All
```

Estado actual esperado hasta que lo apruebe un admin:

```text
No concedido para Asociacion ORT Argentina
```

Este permiso es necesario para que la API pueda buscar usuarios por email sin que un usuario tenga una sesion iniciada.

## Configuracion local

Guardar los datos de Microsoft Graph en `user-secrets`:

```bash
dotnet user-secrets set 'MicrosoftGraph:TenantId' 'TU_TENANT_ID'
dotnet user-secrets set 'MicrosoftGraph:ClientId' 'TU_CLIENT_ID'
dotnet user-secrets set 'MicrosoftGraph:ClientSecret' 'TU_CLIENT_SECRET'
```

Mientras el permiso no este aprobado, dejar apagada la resolucion real de usuarios:

```bash
dotnet user-secrets set 'MicrosoftGraph:ResolveUsersEnabled' 'false'
```

Cuando el admin apruebe `User.Read.All`, activar:

```bash
dotnet user-secrets set 'MicrosoftGraph:ResolveUsersEnabled' 'true'
```

Para variables de entorno, .NET usa doble guion bajo:

```text
MicrosoftGraph__TenantId=...
MicrosoftGraph__ClientId=...
MicrosoftGraph__ClientSecret=...
MicrosoftGraph__ResolveUsersEnabled=true
```

Tambien se soportan variables estilo:

```text
MICROSOFT_GRAPH_TENANT_ID=...
MICROSOFT_GRAPH_CLIENT_ID=...
MICROSOFT_GRAPH_CLIENT_SECRET=...
```

## Clases creadas para Teams

### `Modules/Teams/MicrosoftGraphClient.cs`

Cliente HTTP para Microsoft Graph.

Hace:

- OAuth client credentials contra Microsoft Entra ID.
- Usa scope `https://graph.microsoft.com/.default`.
- Busca usuarios por email con:

```text
GET https://graph.microsoft.com/v1.0/users/{email}?$select=id,displayName,mail,userPrincipalName
```

Esto queda listo para cuando `User.Read.All` este aprobado.

### `Modules/Teams/ITeamsNotificationSender.cs`

Contrato para enviar notificaciones.

Sirve para que el resto del sistema no dependa de si el envio es fake, Teams real, email u otro canal.

### `Modules/Teams/FakeTeamsNotificationSender.cs`

Implementacion temporal.

No envia a Teams real. Escribe en logs algo como:

```text
[Teams fake] To=usuario@dominio.com Ticket=INC0010008 Subject=Ticket en revision Body=...
```

Esto permite probar endpoints y flujo de negocio sin permisos de Graph.

### `Modules/Teams/TeamsNotificationService.cs`

Orquesta las notificaciones:

- Busca el ticket en ServiceNow por `sys_id`.
- Toma `CreatedByEmail` como destinatario.
- Opcionalmente resuelve el usuario en Graph si `MicrosoftGraph:ResolveUsersEnabled=true`.
- Arma mensajes para:
  - ticket en revision
  - ticket resuelto
  - ticket escalado a soporte
- Envia usando `ITeamsNotificationSender`.

### `Modules/Teams/TeamsEndpoints.cs`

Expone endpoints de prueba.

### `Modules/Teams/TeamsModule.cs`

Registra los servicios de Teams en Dependency Injection.

## Endpoints agregados

### Probar notificacion fake

```text
POST /teams/notifications/test
```

Body:

```json
{
  "recipientEmail": "usuario@dominio.com",
  "message": "Mensaje de prueba"
}
```

### Avisar que el ticket esta en revision

```text
POST /teams/notifications/tickets/{sysId}/review-started
```

Ejemplo:

```bash
curl -X POST "http://localhost:5038/teams/notifications/tickets/07b6916793e8071061d8fb97dd03d63f/review-started"
```

### Avisar que el ticket fue resuelto

```text
POST /teams/notifications/tickets/{sysId}/resolved?summary=Texto
```

### Avisar que el ticket fue escalado

```text
POST /teams/notifications/tickets/{sysId}/escalated?reason=Texto
```

## Que falta para Teams real

El permiso `User.Read.All` solo permite buscar usuarios. Para mandar mensajes reales por Teams falta definir la estrategia final:

- Bot de Teams con instalacion en usuarios y mensajes proactivos.
- O envio via Microsoft Graph con permisos adicionales y modelo de chat permitido por el tenant.

La estructura actual deja preparado el flujo para cambiar solo la implementacion de `ITeamsNotificationSender` cuando este definida la estrategia real.

## Polling de tickets nuevos

Se agrego un worker:

```text
Modules/Teams/TeamsTicketPollingWorker.cs
```

Este worker consulta ServiceNow periodicamente y dispara la notificacion de "ticket en revision" cuando detecta un ticket nuevo.

Por defecto esta apagado:

```json
"Teams": {
  "PollingEnabled": false
}
```

Para probarlo localmente:

```bash
dotnet user-secrets set 'Teams:PollingEnabled' 'true'
dotnet user-secrets set 'Teams:PollingIntervalSeconds' '30'
dotnet user-secrets set 'Teams:PollingLimit' '20'
dotnet user-secrets set 'Teams:PollingStartupLookbackMinutes' '2'
dotnet user-secrets set 'Teams:NotifyExistingOnStartup' 'false'
dotnet user-secrets remove 'Teams:PollingQuery'
```

Con `Teams:PollingQuery` vacio, el worker arma una query incremental usando `sys_created_on` desde el ultimo polling. Esto evita revisar todos los tickets en cada arranque o intervalo.

Query generada internamente:

```text
incident_state=1^sys_created_on>=yyyy-MM-dd HH:mm:ss^ORDERBYsys_created_on
```

Con `NotifyExistingOnStartup=false`, el primer polling solo marca como conocidos los tickets encontrados dentro de la ventana inicial. A partir del segundo polling, si aparece un ticket nuevo, lo notifica.

`PollingStartupLookbackMinutes` agrega una ventana chica al inicio para no perder tickets creados justo mientras la API esta levantando.

El estado ya no queda solo en memoria. Se guarda en:

```text
App_Data/teams-ticket-polling-state.json
```

Ese archivo queda ignorado por git.

El estado persistido incluye:

- `LastProcessedOpenedAtUtc`: fecha del ultimo ticket procesado.
- `LastProcessedTicketSysId`: `sys_id` del ultimo ticket procesado.
- `LastProcessedTicketNumber`: numero visible del ultimo ticket procesado.
- `ProcessedTicketSysIds`: tickets ya notificados.

Esto permite que, si la API se apaga, al volver a iniciar consulte desde el ultimo ticket procesado y no dependa solamente de la ventana inicial.

Importante: ServiceNow `sys_id` no es incremental ni ordenable, por eso la query no puede ser literalmente "desde este sys_id en adelante". La query usa `sys_created_on`/`openedAt` como cursor y `ProcessedTicketSysIds` para evitar duplicados. En la practica esto cubre el caso buscado:

```text
API apagada
  -> se crean tickets nuevos
API encendida
  -> lee LastProcessedOpenedAtUtc
  -> consulta tickets creados desde esa fecha
  -> ignora los sys_id ya procesados
  -> notifica los que faltan
```

Para produccion, lo ideal sigue siendo mover esta implementacion a una tabla SQL o a un storage persistente administrado. La API ya usa una interfaz:

```text
ITeamsPollingStateStore
```

Por eso se puede reemplazar el archivo por una tabla sin cambiar el worker.

Flujo actual:

```text
ServiceNow ticket nuevo
  -> polling lo detecta
  -> TeamsNotificationService arma el mensaje
  -> FakeTeamsNotificationSender lo escribe en logs
```

Cuando Teams real este listo, se reemplaza `FakeTeamsNotificationSender` por una implementacion real sin cambiar el worker.
