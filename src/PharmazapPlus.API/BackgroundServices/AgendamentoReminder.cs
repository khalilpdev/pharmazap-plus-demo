namespace PharmazapPlus.API.BackgroundServices;

public class AgendamentoReminder
{
    public int AgendamentoId { get; set; }
    public DateTime ReminderTime { get; set; }
    public string Action { get; set; } = null!;
}
