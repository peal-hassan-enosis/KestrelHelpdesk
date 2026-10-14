namespace KestrelHelpdesk.Core.Models;

using System.ComponentModel.DataAnnotations;

public enum TicketStatus { Open, InProgress, Resolved }
public enum TicketPriority { Low, Medium, High, Critical }
public enum TicketCategory { Hardware, Software, Network, Access }

public class Ticket
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(100, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    public string Description { get; set; } = string.Empty;

    // Priority is non-nullable, so the form always submits an explicit enum value.
    public TicketPriority Priority { get; set; }

    // The domain model stores only a valid category. The form uses a nullable selection
    // so a user must choose a category instead of silently accepting the enum's default.
    public TicketCategory Category { get; set; } = TicketCategory.Hardware;

    public TicketStatus Status { get; set; }

    public List<Comment> Comments { get; set; } = new();

    public List<TicketAttachment> Attachments { get; set; } = new();

    // Identity user id of the creator.
    public string CreatedBy { get; set; } = string.Empty;

    // Identity user id of the assigned agent, or null when unassigned.
    public string? AssignedToUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
