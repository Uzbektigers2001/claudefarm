using AgentFarm.Bot.Services;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace AgentFarm.API.Controllers;

[ApiController]
[Route("webhook")]
public sealed class WebhookController : ControllerBase
{
    private readonly UpdateHandler _handler;

    public WebhookController(UpdateHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Telegram bu endpoint ga POST yuboradi.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] Update update,
        CancellationToken ct)
    {
        await _handler.HandleAsync(update, ct);
        return Ok();
    }
}
