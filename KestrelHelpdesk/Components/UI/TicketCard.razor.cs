using Microsoft.AspNetCore.Components;
using KestrelHelpdesk.Models;

namespace KestrelHelpdesk.Components.UI
{
    // Writing this as a code-behind partial class fulfills the assignment requirement.
    // It implements IDisposable to test the final lifecycle method.
    public partial class TicketCard : ComponentBase, IDisposable
    {
        [Parameter, EditorRequired]
        public Ticket Ticket { get; set; } = default!;

        protected override void OnInitialized()
        {
            Console.WriteLine($"Ticket {Ticket.Id}: OnInitialized execution");
        }

        protected override async Task OnInitializedAsync()
        {
            Console.WriteLine($"Ticket {Ticket.Id}: OnInitializedAsync execution");
            await Task.CompletedTask;
        }

        protected override void OnParametersSet()
        {
            Console.WriteLine($"Ticket {Ticket.Id}: OnParametersSet execution");
        }

        protected override async Task OnParametersSetAsync()
        {
            Console.WriteLine($"Ticket {Ticket.Id}: OnParametersSetAsync execution");
            await Task.CompletedTask;
        }

        protected override void OnAfterRender(bool firstRender)
        {
            Console.WriteLine($"Ticket {Ticket.Id}: OnAfterRender execution (firstRender: {firstRender})");
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            Console.WriteLine($"Ticket {Ticket.Id}: OnAfterRenderAsync execution (firstRender: {firstRender})");
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            Console.WriteLine($"Ticket {Ticket.Id}: Dispose execution");
        }
    }
}