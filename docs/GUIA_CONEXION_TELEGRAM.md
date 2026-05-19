# Guia para conectar Telegram con AgentAI API

Esta guia explica como conectar un bot de Telegram con la API de AgentAI para:

- probar envio de mensajes desde la API hacia Telegram;
- recibir mensajes entrantes desde Telegram por webhook;
- responder consultas por numero de ticket de ServiceNow.

Flujo actual esperado:

```text
ServiceNow ticket nuevo
  -> AgentAI polling lo marca como In Progress en ServiceNow
  -> no envia Telegram automaticamente

Usuario escribe un mensaje con INC0010001
  -> Telegram llama al webhook publico
  -> AgentAI consulta ServiceNow
  -> AgentAI responde solo a ese chat
```

## 1. Crear el bot en Telegram

1. Abrir Telegram.
2. Buscar el bot oficial `@BotFather`.
3. Enviar:

```text
/newbot
```

4. Elegir un nombre visible para el bot, por ejemplo:

```text
AgentAI Soporte
```

5. Elegir un username. Debe terminar en `bot`, por ejemplo:

```text
agentai_soporte_bot
```

6. BotFather devuelve un token similar a:

```text
123456789:AAExampleTokenExampleToken
```

Ese valor se usa como:

```text
Telegram:BotToken
```

Importante: este token es un secreto. No debe subirse al repositorio ni guardarse en `appsettings.json`.

## 2. Obtener el chat_id

Telegram no permite que un bot le escriba a un usuario si ese usuario nunca inicio una conversacion con el bot.

Para obtener el `chat_id`:

1. Abrir el bot creado en Telegram.
2. Presionar `Start`.
3. Enviar cualquier mensaje, por ejemplo:

```text
hola
```

4. Consultar los updates del bot:

```powershell
curl.exe "https://api.telegram.org/botTU_BOT_TOKEN/getUpdates"
```

5. Buscar en la respuesta el bloque `chat`:

```json
"chat": {
  "id": 123456789
}
```

Ese numero se usa como:

```text
Telegram:DefaultChatId
```

## 3. Configurar secretos en la API

Desde la carpeta del proyecto:

```powershell
dotnet user-secrets set "Telegram:BotToken" "TU_BOT_TOKEN"
dotnet user-secrets set "Telegram:DefaultChatId" "TU_CHAT_ID"
```

Verificar que quedaron guardados:

```powershell
dotnet user-secrets list --project AgentAI.csproj
```

Debe aparecer algo parecido a:

```text
Telegram:BotToken = ...
Telegram:DefaultChatId = ...
```

## 4. Probar Telegram directo

Antes de probar desde AgentAI, conviene validar que Telegram envia mensajes:

```powershell
curl.exe -X POST "https://api.telegram.org/botTU_BOT_TOKEN/sendMessage" `
  -H "Content-Type: application/json" `
  -d "{\"chat_id\":\"TU_CHAT_ID\",\"text\":\"Prueba directa desde Telegram Bot API\"}"
```

Si funciona, el mensaje llega al chat configurado.

## 5. Levantar AgentAI API

Desde la carpeta del proyecto:

```powershell
dotnet run --project AgentAI.csproj
```

Por defecto, la API local queda disponible en:

```text
http://localhost:5038
```

## 6. Probar envio desde AgentAI

Este test valida solo el envio saliente desde AgentAI hacia Telegram. No valida el webhook entrante.

Usar el endpoint de prueba:

```powershell
curl.exe -X POST "http://localhost:5038/notifications/test" `
  -H "Content-Type: application/json" `
  -d "{\"recipientEmail\":\"usuario@dominio.com\",\"message\":\"Mensaje de prueba desde AgentAI\"}"
```

Respuesta esperada:

```json
{
  "sent": true,
  "provider": "Telegram",
  "message": "Notification sent.",
  "recipientEmail": "usuario@dominio.com"
}
```

Si `sent` aparece en `false`, revisar el mensaje devuelto. Los errores mas comunes son:

- `Telegram:BotToken is not configured.`
- `Telegram chat id is not configured for the recipient.`
- Token invalido.
- El usuario o grupo nunca inicio conversacion con el bot.

## 7. Exponer la API local con ngrok

Telegram no puede llamar directamente a:

```text
http://localhost:5038
```

`localhost` solo existe dentro de tu computadora. Para Telegram, que corre en servidores externos, esa direccion no apunta a tu maquina. Por eso hace falta una URL publica HTTPS.

En desarrollo local se usa ngrok como tunel:

```text
Telegram servers
  -> https://URL_PUBLICA_NGROK
  -> ngrok abierto en tu PC
  -> http://localhost:5038
  -> AgentAI API
```

Mientras la API corra localmente, ngrok tiene que estar abierto. Si cerras ngrok, la URL publica queda offline y Telegram no puede entregar mensajes al webhook.

Levantar ngrok apuntando al puerto de la API:

```powershell
ngrok http 5038
```

Si `ngrok` no esta en el PATH, usar la ruta completa:

```powershell
& "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Ngrok.Ngrok_Microsoft.Winget.Source_8wekyb3d8bbwe\ngrok.exe" http 5038
```

Ngrok debe mostrar algo como:

```text
Forwarding  https://generous-flashy-oxygen.ngrok-free.dev -> http://localhost:5038
```

La URL real es la que empieza con `https://`. Cada vez que se reinicia ngrok en plan gratis puede cambiar.

## 8. Configurar webhook de Telegram

El webhook entrante de AgentAI es:

```text
/telegram/webhook
```

Entonces, si ngrok muestra:

```text
https://generous-flashy-oxygen.ngrok-free.dev
```

el webhook completo es:

```text
https://generous-flashy-oxygen.ngrok-free.dev/telegram/webhook
```

Configurar Telegram:

```powershell
curl.exe -X POST "https://api.telegram.org/botTU_BOT_TOKEN/setWebhook" `
  -d "url=https://URL_REAL_NGROK/telegram/webhook" `
  -d "drop_pending_updates=true"
```

Verificar:

```powershell
curl.exe "https://api.telegram.org/botTU_BOT_TOKEN/getWebhookInfo"
```

Respuesta esperada:

```json
{
  "ok": true,
  "result": {
    "url": "https://URL_REAL_NGROK/telegram/webhook",
    "pending_update_count": 0
  }
}
```

Si aparece un error como:

```text
ERR_NGROK_3200
The endpoint ... is offline
```

significa que ngrok esta cerrado o que el webhook apunta a una URL vieja. Hay que volver a levantar ngrok, copiar la URL actual y ejecutar `setWebhook` de nuevo.

## 9. Consultar ticket por Telegram

Con la API levantada, ngrok abierto y el webhook configurado, el usuario puede escribirle al bot:

```text
INC0010001
```

Tambien puede mandar una frase:

```text
hola como va vengo a ver el ticket inc10001
```

Reglas del numero:

- el prefijo `INC` es obligatorio;
- no importa si viene en mayusculas o minusculas;
- puede venir dentro de una frase;
- puede omitir ceros a la izquierda, por ejemplo `inc10001` puede resolver `INC0010001`.

La API responde solo al chat que envio el mensaje.

Para diagnosticar, mirar la consola de `dotnet run`. Cuando Telegram llega al webhook debe aparecer:

```text
Telegram webhook received message
```

Si esa linea no aparece, Telegram no esta llegando a la API. Revisar ngrok y `getWebhookInfo`.

## 10. Usar chat_id por usuario

La primera configuracion envia todo a:

```text
Telegram:DefaultChatId
```

Si despues se quiere enviar a chats distintos segun el email del solicitante, se puede configurar:

```powershell
dotnet user-secrets set "Telegram:RecipientChatIds:usuario@dominio.com" "CHAT_ID_DEL_USUARIO"
```

Cuando AgentAI reciba un ticket con `CreatedByEmail = usuario@dominio.com`, primero buscara:

```text
Telegram:RecipientChatIds:usuario@dominio.com
```

Si no existe, usara:

```text
Telegram:DefaultChatId
```

## 11. Probar endpoints de notificacion manual

Estos endpoints buscan el ticket en ServiceNow, actualizan su estado o comentarios, y luego mandan una notificacion por Telegram. Son pruebas manuales de envio saliente, no forman parte del flujo automatico actual.

### Ticket en revision

```powershell
curl.exe -X POST "http://localhost:5038/notifications/tickets/SYS_ID_DEL_TICKET/review-started"
```

### Ticket resuelto

```powershell
curl.exe -X POST "http://localhost:5038/notifications/tickets/SYS_ID_DEL_TICKET/resolved?summary=El caso fue resuelto"
```

### Ticket escalado

```powershell
curl.exe -X POST "http://localhost:5038/notifications/tickets/SYS_ID_DEL_TICKET/escalated?reason=Requiere soporte humano"
```

## 12. Activar polling automatico

El polling automatico esta apagado por defecto:

```json
"Notifications": {
  "PollingEnabled": false
}
```

Para activarlo localmente:

```powershell
dotnet user-secrets set "Notifications:PollingEnabled" "true"
dotnet user-secrets set "Notifications:PollingIntervalSeconds" "30"
dotnet user-secrets set "Notifications:PollingLimit" "20"
dotnet user-secrets set "Notifications:PollingStartupLookbackMinutes" "2"
dotnet user-secrets set "Notifications:NotifyExistingOnStartup" "false"
dotnet user-secrets remove "Notifications:PollingQuery"
```

Con esto, AgentAI consulta ServiceNow periodicamente. Cuando detecta tickets nuevos, los marca como `In Progress` y agrega comentario/work note en ServiceNow. No manda Telegram automaticamente.

## 13. Checklist

- Bot creado con BotFather.
- Token guardado en `Telegram:BotToken`.
- Usuario o grupo inicio conversacion con el bot.
- `chat_id` obtenido con `getUpdates`.
- `chat_id` guardado en `Telegram:DefaultChatId`.
- Prueba directa contra Telegram Bot API exitosa.
- API levantada con `dotnet run`.
- `POST /notifications/test` devuelve `sent: true`.
- ngrok levantado contra `5038`.
- webhook configurado con la URL actual de ngrok.
- `getWebhookInfo` muestra la URL actual.
- Al mandar `INC...` en Telegram, la consola muestra `Telegram webhook received message`.
- Opcional: polling automatico activado con `Notifications:PollingEnabled=true`.
