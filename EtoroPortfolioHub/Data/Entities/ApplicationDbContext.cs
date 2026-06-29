using EtoroPortfolioHub.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EtoroPortfolioHub.Data;

public sealed class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<PortfolioTargetEntity> PortfolioTargets => Set<PortfolioTargetEntity>();
    public DbSet<DividendEventEntity> DividendEvents => Set<DividendEventEntity>();
    public DbSet<EtoroConnectionEntity> EtoroConnections => Set<EtoroConnectionEntity>();

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
                .HasPrecision(18, 2);

            entity.Property(x => x.CreatedUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedUtc)
                .IsRequired();

            entity.HasIndex(x => new { x.UserId, x.InstrumentId })
                .IsUnique();
        });

        modelBuilder.Entity<DividendEventEntity>(entity =>
        {
            entity.ToTable("DividendEvents");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Symbol)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.CompanyName)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.Sector)
                .HasMaxLength(200);

            entity.Property(x => x.AnnualDividend)
                .HasPrecision(18, 6);

            entity.Property(x => x.PeriodicDividend)
                .HasPrecision(18, 6);

            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedUtc)
                .IsRequired();

            entity.HasIndex(x => new { x.UserId, x.Symbol, x.ExDividendDate, x.PaymentDate })
                .IsUnique();
        });

        modelBuilder.Entity<EtoroConnectionEntity>(entity =>
        {
            entity.ToTable("EtoroConnections");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Environment)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.PermissionMode)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.EncryptedUserKey)
                .IsRequired();

            entity.Property(x => x.LastValidationMessage)
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedUtc)
                .IsRequired();

            entity.HasIndex(x => x.UserId)
                .IsUnique();
        });
    }
}