using KestrelHelpdesk.Components.Validators;
using System.ComponentModel.DataAnnotations;

namespace KestrelHelpdesk.Models
{
    public enum TicketStatus { Open, InProgress, Resolved }
    public enum TicketPriority { Low, Medium, High, Critical }
    public enum TicketCategory { Hardware, Software, Network, Access }

    [CriticalPriorityDescription]
    public class Ticket
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 100 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Priority is required.")]
        public TicketPriority Priority { get; set; }

        [Required(ErrorMessage = "Category is required.")]
        public TicketCategory? Category { get; set; }

        public TicketStatus Status { get; set; } 
        public List<Comment> Comments { get; set; } = new();
    }
}