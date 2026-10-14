using System.ComponentModel.DataAnnotations;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Web.Components.Helper;

/// <summary>
/// Captures editable form state separately from the persisted Ticket entity.
/// Category remains nullable here so the UI can represent an unselected option;
/// validation requires a choice before the entity is updated.
/// </summary>
public sealed class TicketFormModel
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(100, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    public TicketCategory? Category { get; set; }

    public TicketPriority Priority { get; set; }

    public TicketStatus Status { get; set; }
}
