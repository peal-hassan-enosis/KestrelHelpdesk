namespace KestrelHelpdesk.Models
{
    public enum TicketStatus { Open, InProgress, Resolved }
    public enum TicketPriority { Low, Medium, High, Critical }

    public class Ticket
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TicketStatus Status { get; set; }
        public TicketPriority Priority { get; set; }
    }
}