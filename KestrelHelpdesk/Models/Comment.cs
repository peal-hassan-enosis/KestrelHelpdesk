namespace KestrelHelpdesk.Models;

public class Comment
{
    public int Id { get; set; }
    public int TicketId { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Comment text is required.")]
    public string Text { get; set; } = string.Empty;

    public string Author { get; set; } = "Current User";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}