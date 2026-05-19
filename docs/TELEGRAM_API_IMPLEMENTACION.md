# Implementacion de Telegram Bot API

Este documento describe como conectar AgentAI con Telegram para enviar notificaciones y recibir consultas de usuarios por numero de ticket.

## 1. Objetivo

Permitir que AgentAI envie y reciba mensajes por Telegram usando un bot propio.

Flujo esperado:

```text
ServiceNow
  -> AgentAI detecta o procesa un ticket
  -> AgentAI arma el mensaje
  -> Telegram Bot API envia la notificacion
```

Tambien permite:

```text
Usuario escribe INC0010024 en Telegram
  -> Telegram llama al webhook de AgentAI
  -> AgentAI consulta ServiceNow
  -> AgentAI responde el estado del ticket por Telegram
```

## 2. Requisitos

- Una cuenta de Telegram.
- Permiso para crear un bot con BotFather.
- Acceso al servidor donde corre AgentAI.
- La API AgentAI levantada y funcionando.

Documentacion oficial:

- Telegram Bot API: https://core.telegram.org/bots/api
- Introduccion a bots de Telegram: https://core.telegram.org/bots

## 3. Crear el bot

1. Abrir Telegram.
2. Buscar el bot oficial `@BotFather`.
3. Enviar:

```text
/newbot
```

4. Elegir un nombre visible para el bot.
5. Elegir un username para el bot. Debe terminar en `bot`, por ejemplo:

```text
agentai_soporte_bot
```

6. BotFather devuelve un token similar a:

```text
123456789:AAExampleTokenExampleToken
```

Ese valor se usara como:

```text
Telegram:BotToken
```

## 4. Obtener el chat_id

Telegram no permite que un bot inicie una conversacion privada con un usuario si ese usuario nunca hablo con el bot.

Para obtener el `chat_id`:

1. Abrir el bot creado.
2. Presionar `Start`.
3. Enviar cualquier mensaje, por ejemplo:

```text
hola
```

4. Consultar updates del bot:

```powershell
curl.exe "https://api.telegram.org/botTU_BOT_TOKEN/getUpdates"
```

5. Buscar en la respuesta:

```json
"chat": {
  "id": 123456789
}
```

Ese valor se usara como:

```text
Telegram:DefaultChatId
```

## 5. Configurar AgentAI

Guardar la configuracion con `dotnet user-secrets`:

```powershell
dotnet user-secrets set "Telegram:BotToken" "TU_BOT_TOKEN"
dotnet user-secrets set "Telegram:DefaultChatId" "TU_CHAT_ID"
```

Verificar:

```powershell
dotnet user-secrets list --project AgentAI.csproj
```

Debe aparecer:

```text
Telegram:BotToken = ...
Telegram:DefaultChatId = ...
```

## 6. Probar Telegram directo

Antes de probar AgentAI, validar que Telegram envia mensajes:

```powershell
curl.exe -X POST "https://api.telegram.org/botTU_BOT_TOKEN/sendMessage" `
  -H "Content-Type: application/json" `
  -d "{\"chat_id\":\"TU_CHAT_ID\",\"text\":\"Prueba desde AgentAI\"}"
```

Si funciona, el mensaje llega al chat configurado.

## 7. Probar desde AgentAI

Levantar la API:

```powershell
dotnet run --project AgentAI.csproj
```

Probar el endpoint de notificacion:

```powershell
curl.exe -X POST "http://localhost:5038/notifications/test" `
  -H "Content-Type: application/json" `
  -d "{\"recipientEmail\":\"usuario@cliente.com\",\"message\":\"Mensaje de prueba por Telegram\"}"
```

Respuesta esperada:

```json
{
  "sent": true,
  "provider": "Telegram",
  "message": "Notification sent."
}
```

## 8. Webhook entrante de Telegram

AgentAI expone este endpoint para recibir mensajes desde Telegram:

```text
POST /telegram/webhook
```

Telegram solo puede llamar a URLs publicas HTTPS. Para probar localmente se puede usar una herramienta como ngrok.

Ejemplo:

```powershell
ngrok http 5038
```

Si ngrok devuelve:

```text
https://abc123.ngrok-free.app
```

Configurar el webhook:

```powershell
curl.exe "https://api.telegram.org/botTU_BOT_TOKEN/setWebhook?url=https://abc123.ngrok-free.app/telegram/webhook"
```

Verificar:

```powershell
curl.exe "https://api.telegram.org/botTU_BOT_TOKEN/getWebhookInfo"
```

Para desactivar el webhook:

```powershell
curl.exe "https://api.telegram.org/botTU_BOT_TOKEN/deleteWebhook"
```

## 9. Consultar ticket por Telegram

Con el webhook activo, el usuario puede escribir:

```text
INC0010024
```

Tambien puede incluir el numero dentro de una frase y no hace falta respetar mayusculas:

```text
hola como va vengo a ver el ticket inc10024
```

El prefijo `INC` si es obligatorio. Si el usuario omite ceros a la izquierda, AgentAI prueba la variante normalizada contra ServiceNow; por ejemplo, `inc10024` puede resolver `INC0010024`.

AgentAI:

1. recibe el mensaje;
2. detecta el numero de ticket;
3. consulta ServiceNow por `number=INC0010024`;
4. responde al mismo chat con informacion del caso.

Respuesta esperada:

```text
Ticket INC0010024
Estado: In Progress
Prioridad: Medium
Asunto: No puedo iniciar sesion
Ultima actualizacion: 2026-05-16 14:30 UTC
```

Si el mensaje no contiene un numero de ticket, AgentAI responde:

```text
No pude identificar un numero de ticket. Enviame algo con INC, por ejemplo INC0010024 o inc10024.
```

## 10. Integracion con tickets por polling

Con `Notifications:PollingEnabled=true`, cuando AgentAI detecta un ticket nuevo desde ServiceNow, el polling actualiza el ticket en ServiceNow:

- cambia el estado a `In Progress`;
- agrega un comentario visible para el usuario;
- agrega una work note interna.

El polling no envia mensajes salientes por Telegram. Telegram se usa para responder consultas entrantes: el usuario escribe un mensaje que incluya el numero de ticket y AgentAI responde solo a ese chat.

Ejemplo:

```text
hola como va vengo a ver el ticket INC0010024
```

## 11. Mapeo por usuario

Los envios proactivos por Telegram deben evitarse para no mandar informacion de un ticket a contactos incorrectos. Si mas adelante se necesitara habilitar notificaciones salientes, primero habria que mapear explicitamente:

```text
email del solicitante en ServiceNow -> chat_id de Telegram
```

Ejemplo:

```text
Telegram:RecipientChatIds:usuario@cliente.com = 123456789
```

Esto permitiria enviar cada notificacion al chat correcto, pero no forma parte del flujo actual.

## 12. Consideraciones importantes

- El bot no puede escribirle a un usuario que nunca inicio conversacion con el bot.
- Para recibir mensajes, el webhook debe estar configurado con una URL publica HTTPS.
- Para grupos, hay que agregar el bot al grupo y obtener el `chat_id` del grupo.
- El token del bot debe guardarse como secreto, no en `appsettings.json`.
- Si el token se expone, se debe regenerar desde BotFather.
- La consulta por numero de ticket usa ServiceNow como fuente de verdad.

## 13. Checklist de puesta en marcha

- Bot creado en BotFather.
- Usuario o grupo inicio conversacion con el bot.
- `chat_id` obtenido.
- `Telegram:BotToken` configurado.
- `Telegram:DefaultChatId` configurado.
- Prueba directa con `sendMessage` exitosa.
- Prueba desde AgentAI exitosa.
- URL publica HTTPS disponible para webhook.
- Webhook configurado con `setWebhook`.
- Usuario envia numero de ticket y recibe informacion del caso.
