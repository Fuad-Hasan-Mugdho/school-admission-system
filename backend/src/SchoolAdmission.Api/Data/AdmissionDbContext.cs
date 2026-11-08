using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Data;

public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(e =>
        {
            e.ToTable("classes");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").HasMaxLength(64);
            e.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(c => c.TotalSeats).HasColumnName("total_seats");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.ToTable(t => t.HasCheckConstraint("ck_classes_total_seats_positive", "total_seats > 0"));
        });

        modelBuilder.Entity<Hold>(e =>
        {
            e.ToTable("holds");
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasColumnName("id").HasMaxLength(64);
            e.Property(h => h.ClassId).HasColumnName("class_id").HasMaxLength(64);
            e.Property(h => h.StudentId).HasColumnName("student_id").HasMaxLength(64);
            e.Property(h => h.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
            e.Property(h => h.CreatedAt).HasColumnName("created_at");
            e.Property(h => h.ExpiresAt).HasColumnName("expires_at");
            e.Property(h => h.ConfirmedAt).HasColumnName("confirmed_at");
            e.Property(h => h.CancelledAt).HasColumnName("cancelled_at");

            e.HasOne(h => h.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(h => new { h.ClassId, h.Status }).HasDatabaseName("ix_holds_class_status");
            e.HasIndex(h => h.StudentId).HasDatabaseName("ix_holds_student");

            // Defence in depth for "one active application per student per class".
            // The service also checks this under the class row lock; the partial unique
            // index guarantees it even if a future code path forgets to.
            e.HasIndex(h => new { h.ClassId, h.StudentId })
                .IsUnique()
                .HasDatabaseName("ux_holds_active_student_class")
                .HasFilter("status IN ('HELD', 'CONFIRMED')");
        });
    }
}
