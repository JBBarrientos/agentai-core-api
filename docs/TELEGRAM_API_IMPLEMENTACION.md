# Implementacion de Telegram Bot API

Este documento describe como conectar AgentAI con Telegram para enviar notificaciones al usuario cuando se detecta o actualiza un ticket.

## 1. Objetivo

Permitir que AgentAI envie mensajes por Telegram usando un bot propio.

Flujo esperado:

```text
ServiceNow
  -> AgentAI detecta o procesa un ticket
  -> AgentAI arma el mensaje
  -> Telegram Bot API envia la notificacion
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

## 8. Integracion con tickets

Con `Notifications:PollingEnabled=true`, cuando AgentAI detecte un ticket nuevo desde ServiceNow, el flujo de notificacion usara Telegram.

La primera implementacion envia todo a:

```text
Telegram:DefaultChatId
```

## 9. Mapeo por usuario

En una etapa posterior se puede mapear:

```text
email del solicitante en ServiceNow -> chat_id de Telegram
```

Ejemplo:

```text
Telegram:RecipientChatIds:usuario@cliente.com = 123456789
```

Esto permite enviar cada notificacion al chat correcto.

## 10. Consideraciones importantes

- El bot no puede escribirle a un usuario que nunca inicio conversacion con el bot.
- Para grupos, hay que agregar el bot al grupo y obtener el `chat_id` del grupo.
- El token del bot debe guardarse como secreto, no en `appsettings.json`.
- Si el token se expone, se debe regenerar desde BotFather.
- Esta integracion solo envia mensajes salientes.

## 11. Checklist de puesta en marcha

- Bot creado en BotFather.
- Usuario o grupo inicio conversacion con el bot.
- `chat_id` obtenido.
- `Telegram:BotToken` configurado.
- `Telegram:DefaultChatId` configurado.
- Prueba directa con `sendMessage` exitosa.
- Prueba desde AgentAI exitosa.
