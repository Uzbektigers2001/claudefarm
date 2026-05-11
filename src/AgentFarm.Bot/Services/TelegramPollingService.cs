using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AgentFarm.Bot.Services;

/// <summary>
/// Local development uchun — polling orqali xabarlarni oladi.
/// Production da (webhook bor bo'lsa) ishlamaydi.
/// </summary>
public sealed class TelegramPollingService : BackgroundService
{
    private readonly ITelegramBotClient              _botClient;
    private readonly UpdateHandler                   _updateHandler;
    private readonly ILogger<TelegramPollingService> _logger;

    public TelegramPollingService(
        ITelegramBotClient              botClient,
        UpdateHandler                   updateHandler,
        ILogger<TelegramPollingService> logger)
    {
        _botClient     = botClient;
        _updateHandler = updateHandler;
        _logger        = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram polling boshlandi (local development rejimi)");

        try
        {
            // Eski webhookni o'chirish va pending updatelarni tozalash
            await _botClient.DeleteWebhook(dropPendingUpdates: true, cancellationToken: stoppingToken);

            var offset = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var updates = await _botClient.GetUpdates(
                        offset: offset,
                        timeout: 30,
                        allowedUpdates: [UpdateType.Message],
                        cancellationToken: stoppingToken
                    );

                    foreach (var update in updates)
                    {
                        offset = update.Id + 1;

                        // Har bir updateni background da qayta ishlaymiz
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _updateHandler.HandleAsync(update, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Update qayta ishlashda xato. UpdateId={UpdateId}", update.Id);
                            }
                        }, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GetUpdates xatosi, 5 soniyadan keyin qayta urinaman");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Polling to'xtatildi");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Polling fatal xato");
        }
    }
}
