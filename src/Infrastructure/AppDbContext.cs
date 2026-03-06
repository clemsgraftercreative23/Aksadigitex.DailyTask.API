
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

        // Configure UTC datetime converter for all DateTime properties
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
            }
        }

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
