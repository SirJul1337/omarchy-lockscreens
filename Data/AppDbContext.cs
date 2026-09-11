using Microsoft.EntityFrameworkCore;
using OmarchyLockscreens.Models;

namespace OmarchyLockscreens.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Design> Designs => Set<Design>();
    public DbSet<DesignVersion> DesignVersions => Set<DesignVersion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Design>().HasIndex(d => d.PublicId).IsUnique();
        b.Entity<Design>()
            .HasOne(d => d.LiveVersion)
            .WithMany()
            .HasForeignKey(d => d.LiveVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Design>()
            .HasMany(d => d.Versions)
            .WithOne(v => v.Design)
            .HasForeignKey(v => v.DesignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
