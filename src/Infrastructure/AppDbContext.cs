
using Microsoft.EntityFrameworkCore;
using Domain;

namespace Infrastructure;

public class AppDbContext : DbContext {
    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt){}
    public DbSet<User> Users => Set<User>();
}
