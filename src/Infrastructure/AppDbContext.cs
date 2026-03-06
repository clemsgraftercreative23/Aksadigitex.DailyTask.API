
using Microsoft.EntityFrameworkCore;
using Domain;

namespace Infrastructure;

public class AppDbContext : DbContext {
    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt){}
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();
    public DbSet<DailyReportAttachment> DailyReportAttachments => Set<DailyReportAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasOne(u => u.RoleRef)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .HasPrincipalKey(r => r.Id)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure DailyReportAttachment -> DailyReport relationship
        modelBuilder.Entity<DailyReportAttachment>()
            .HasOne(a => a.DailyReport)
            .WithMany(r => r.Attachments)
            .HasForeignKey(a => a.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
