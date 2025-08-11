using Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Note>(e =>
        {
            e.ToTable("notes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
