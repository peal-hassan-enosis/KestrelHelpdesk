namespace KestrelHelpdesk.Data;

using Microsoft.EntityFrameworkCore;
using KestrelHelpdesk.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
}