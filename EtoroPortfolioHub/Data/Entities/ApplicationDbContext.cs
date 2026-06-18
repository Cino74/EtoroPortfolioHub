using EtoroPortfolioHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EtoroPortfolioHub.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<PortfolioTargetEntity> PortfolioTargets => Set<PortfolioTargetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PortfolioTargetEntity>(entity =>
        {
            entity.ToTable("PortfolioTargets");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Symbol)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.InstrumentName)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.TargetPercentage)
                .IsRequired();

            entity.Property(x => x.CreatedUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedUtc)
                .IsRequired();

            // Un solo target per utente e strumento
            entity.HasIndex(x => new { x.UserId, x.InstrumentId })
                .IsUnique();
        });
    }
}