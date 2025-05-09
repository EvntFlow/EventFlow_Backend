using EventFlow.Data.Db;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<Account>(options)
{
    public DbSet<Attendee> Attendees { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<EventCategory> EventCategories { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Organizer> Organizers { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<SavedEvent> SavedEvents { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketOption> TicketOptions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Attendee>(b =>
        {
            b.HasOne(e => e.Account).WithOne();
        });

        builder.Entity<Category>(b =>
        {
            // No-op
        });

        builder.Entity<Event>(b =>
        {
            b.HasOne(e => e.Organizer).WithMany();
        });

        builder.Entity<EventCategory>(b =>
        {
            b.HasOne(e => e.Event).WithMany();
            b.HasOne(e => e.Category).WithMany();
        });

        builder.Entity<Notification>(b =>
        {
            b.HasOne(e => e.Account).WithMany();
        });

        builder.Entity<Organizer>(b =>
        {
            b.HasOne(e => e.Account).WithOne();
        });

        builder.Entity<PaymentMethod>(b =>
        {
            b.HasOne(e => e.Account).WithMany();
            b.HasDiscriminator(e => e.Type)
                .HasValue<CardPaymentMethod>(nameof(CardPaymentMethod));
        });

        builder.Entity<SavedEvent>(b =>
        {
            b.HasOne(e => e.Attendee).WithMany();
            b.HasOne(e => e.Event).WithMany();
        });

        builder.Entity<Ticket>(b =>
        {
            b.HasOne(e => e.TicketOption).WithMany()
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Attendee).WithMany()
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TicketOption>(b =>
        {
            b.HasOne(e => e.Event).WithMany();
        });
    }
}
