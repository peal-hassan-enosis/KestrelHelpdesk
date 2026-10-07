namespace KestrelHelpdesk.Components.Validators;
using System.ComponentModel.DataAnnotations;
using KestrelHelpdesk.Models;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class CriticalPriorityDescriptionAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is Ticket ticket)
        {
            if (ticket.Priority == TicketPriority.Critical)
            {
                if (string.IsNullOrWhiteSpace(ticket.Description) || ticket.Description.Trim().Length < 50)
                {
                    return new ValidationResult(
                        "Critical priority tickets require a description of at least 50 characters.",
                        new[] { nameof(Ticket.Description) }
                    );
                }
            }
        }
        return ValidationResult.Success;
    }
}
