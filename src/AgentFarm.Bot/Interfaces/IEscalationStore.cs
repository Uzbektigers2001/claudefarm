using AgentFarm.Core.Models;

namespace AgentFarm.Bot.Interfaces;

public interface IEscalationStore
{
    // Foydalanuvchi javob tokenlari
    public const string SkipToken = "\x00skip";
    public const string StopToken = "\x00stop";

    void               AddEscalation      (EscalationSession session);
    EscalationSession? GetEscalation      (long chatId);
    void               RemoveEscalation   (long chatId);
    bool               HasPendingEscalation(long chatId);
}
