using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KestrelHelpdesk.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TicketAttachment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(a => a.StoredFileName).HasMaxLength(255).IsRequired();
            entity.Property(a => a.ContentType).HasMaxLength(255).IsRequired();
            entity.Property(a => a.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(a => a.UploadedByUserId).HasMaxLength(450).IsRequired();

            entity.HasOne(a => a.Ticket)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => a.TicketId);
        });
    }
}