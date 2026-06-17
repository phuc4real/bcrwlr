using Bcrwlr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bcrwlr.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).IsRequired();
            e.Property(a => a.SourceUrl).IsRequired();
            e.HasIndex(a => a.SavedAt);
        });
    }
}
