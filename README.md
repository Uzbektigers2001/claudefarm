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

```json
{
  "TelegramBot": { "Token": "YOUR_BOT_TOKEN" },
  "Anthropic": { "ApiKey": "YOUR_API_KEY" }
}
```

## Mualliflar

Uzbektigers2001
