using Microsoft.EntityFrameworkCore;
using AVEquipmentManager.Shared.Models;

namespace AVEquipmentManager.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Equipment> Equipment { get; set; } = null!;
    public DbSet<Ticket> Tickets { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Staff> Staff { get; set; } = null!;
    public DbSet<Disposal> Disposals { get; set; } = null!;
    public DbSet<Acquisition> Acquisitions { get; set; } = null!;
    public DbSet<LifecycleLog> LifecycleLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SerialNumber).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RoomName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).IsRequired().HasMaxLength(300);
            entity.Property(t => t.Description).HasMaxLength(2000);
            entity.Property(t => t.ReportedBy).HasMaxLength(200);
            entity.Property(t => t.AssignedTo).HasMaxLength(200);
            entity.Property(t => t.Resolution).HasMaxLength(2000);
            entity.Property(t => t.ExternalTicketId).HasMaxLength(100);
            entity.HasOne(t => t.Equipment)
                  .WithMany()
                  .HasForeignKey(t => t.EquipmentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(200);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Role).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<Staff>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.EmployeeId).IsUnique();
            entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
            entity.Property(s => s.EmployeeId).IsRequired().HasMaxLength(50);
            entity.Property(s => s.RoomNumber).IsRequired().HasMaxLength(200);
            entity.Property(s => s.Department).HasMaxLength(150);
            entity.Property(s => s.Email).HasMaxLength(200);
            entity.Property(s => s.Phone).HasMaxLength(50);
        });

        modelBuilder.Entity<Disposal>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Reason).IsRequired().HasMaxLength(500);
            entity.Property(d => d.RequestedBy).HasMaxLength(200);
            entity.Property(d => d.ApprovedBy).HasMaxLength(200);
            entity.Property(d => d.DisposalNotes).HasMaxLength(1000);
            entity.Property(d => d.RowVersion).IsRowVersion();
            entity.HasOne(d => d.Equipment)
                  .WithMany()
                  .HasForeignKey(d => d.EquipmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Replacement (spare) equipment relationship — nullable, set when the
            // Supervisor opts to promote a Reserved unit during Mark Disposed.
            // Restrict so a referenced spare cannot be hard-deleted later.
            entity.HasOne(d => d.ReplacementEquipment)
                  .WithMany()
                  .HasForeignKey(d => d.ReplacementEquipmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            // V3 fix — unique filtered index: at most one Pending (0) or Approved (1) disposal per equipment.
            // Catches the TOCTOU race in DisposalsController.Create.
            entity.HasIndex(d => new { d.EquipmentId, d.Status })
                  .HasFilter("\"Status\" IN (0, 1)")
                  .IsUnique()
                  .HasDatabaseName("IX_Disposals_OpenPerEquipment");
        });

        modelBuilder.Entity<Acquisition>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.ItemName).IsRequired().HasMaxLength(200);
            entity.Property(a => a.Vendor).HasMaxLength(200);
            entity.Property(a => a.PurchaseOrderNumber).HasMaxLength(100);
            entity.Property(a => a.IntendedRoom).HasMaxLength(200);
            entity.Property(a => a.RequestedBy).HasMaxLength(200);
            entity.Property(a => a.Notes).HasMaxLength(1000);
            entity.Property(a => a.RowVersion).IsRowVersion();
            entity.HasOne(a => a.DeployedEquipment)
                  .WithMany()
                  .HasForeignKey(a => a.DeployedEquipmentId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(t => t.RowVersion).IsRowVersion();
        });

        // Append-only audit table written by the lifecycle services
        // inside the same transaction as every state change.
        modelBuilder.Entity<LifecycleLog>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.EntityType, l.EntityId, l.TransitionedAtUtc })
                  .HasDatabaseName("IX_LifecycleLogs_EntityHistory");
        });
    }
}
