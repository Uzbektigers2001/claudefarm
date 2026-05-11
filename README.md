# ClaudeFarm 🤖🚜

Telegram bot orqali Claude AI agentlarini boshqarish platformasi.

## Nima qiladi?

Foydalanuvchi Telegram botga vazifa yozadi — Developer, QA va Reviewer agentlari parallel ishlaydi va natijani Telegram guruhiga yuboradi.

```
Siz: /task Login funksiyasini yoz

🤖 [Developer] Kod tayyor:
   ```csharp
   public async Task<IActionResult> Login(...)
   ```

🤖 [QA] 3 ta test yozdim, 1 xato topdim...

🤖 [Reviewer] LGTM, faqat exception handling yo'q
```

## Arxitektura

```
Telegram Bot
    ↓
.NET 8 Backend (Orchestrator)
    ↓
┌─────────────┬──────────┬────────────┐
│  Developer  │    QA    │  Reviewer  │
│   Agent     │  Agent   │   Agent    │
└─────────────┴──────────┴────────────┘
    ↓
Telegram (har agent o'z xabarini yuboradi)
```

## Texnologiyalar

- **.NET 8** — Backend
- **Telegram.Bot** — Bot integratsiyasi
- **Anthropic Claude API** — AI agentlar
- **Webhook** — Real-time xabarlar

## Loyiha tuzilmasi

```
src/
├── AgentFarm.Core/      # Modellar va interfeylar
├── AgentFarm.Agents/    # Agent pipeline
├── AgentFarm.Bot/       # Telegram bot service
└── AgentFarm.API/       # Web API + Webhook
```

## Sozlash

### 1. Telegram Bot yarating

1. [@BotFather](https://t.me/BotFather) ga "/newbot" yozing
2. Bot nomini va username kiriting
3. Token oling (masalan: `123456789:ABCdefGHIjklMNOpqrsTUVwxyz`)

### 2. Anthropic API Key oling

1. [console.anthropic.com](https://console.anthropic.com) ga kiring
2. "API Keys" bo'limidan yangi key yarating
3. Key ni nusxalab oling (masalan: `sk-ant-api03-...`)

### 3. Local development uchun konfiguratsiya

`src/AgentFarm.API/appsettings.Development.json` faylini tahrirlang:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "TelegramBot": {
    "Token": "SIZNING_BOT_TOKEN",
    "WebhookUrl": ""
  },
  "Anthropic": {
    "ApiKey": "SIZNING_API_KEY",
    "Model": "claude-sonnet-4-20250514",
    "MaxTokens": 4096
  }
}
```

**Diqqat:** `WebhookUrl` ni bo'sh qoldiring — local development da polling ishlaydi.

### 4. Ishga tushirish

```bash
cd src/AgentFarm.API
dotnet run
```

## Mualliflar

Uzbektigers2001
