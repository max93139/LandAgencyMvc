using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Models;

public class LandAgencyContext : DbContext
{
    public DbSet<AppUser> Users { get; set; } = null!;
    public DbSet<LandPlot> LandPlots { get; set; } = null!;
    public DbSet<PlotDocument> PlotDocuments { get; set; } = null!;
    public DbSet<PlotMedia> PlotMedia { get; set; } = null!;
    public DbSet<SelectionRequest> SelectionRequests { get; set; } = null!;
    public DbSet<ViewingRequest> ViewingRequests { get; set; } = null!;
    public DbSet<LegalConclusion> LegalConclusions { get; set; } = null!;
    public DbSet<SaleDeal> SaleDeals { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    public LandAgencyContext(DbContextOptions<LandAgencyContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LandPlot>().Property(p => p.Version).IsConcurrencyToken();
        modelBuilder.Entity<SaleDeal>().Property(d => d.Version).IsConcurrencyToken();

        modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<LandPlot>().HasIndex(p => p.CadastralNumber).IsUnique();
        modelBuilder.Entity<LegalConclusion>().HasIndex(c => c.ConclusionNumber).IsUnique();
        modelBuilder.Entity<SaleDeal>().HasIndex(d => d.DraftContractNumber).IsUnique();
        modelBuilder.Entity<Payment>().HasIndex(p => p.InvoiceNumber).IsUnique();

        modelBuilder.Entity<SaleDeal>()
            .HasIndex(d => d.LandPlotId)
            .IsUnique()
            .HasDatabaseName("UX_SaleDeals_LandPlotId_Active")
            .HasFilter($"\"Status\" <> {(int)DealStatus.Cancelled}");

        modelBuilder.Entity<AppUser>().ToTable(t => t.HasCheckConstraint(
            "CK_Users_Role", "\"Role\" BETWEEN 0 AND 3"));

        modelBuilder.Entity<LandPlot>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_LandPlots_Area", "\"AreaHectares\" > 0");
            t.HasCheckConstraint("CK_LandPlots_Price", "CAST(\"PriceUsd\" AS REAL) > 0");
            t.HasCheckConstraint("CK_LandPlots_Latitude", "\"Latitude\" IS NULL OR \"Latitude\" BETWEEN 44 AND 53");
            t.HasCheckConstraint("CK_LandPlots_Longitude", "\"Longitude\" IS NULL OR \"Longitude\" BETWEEN 22 AND 41");
        });

        modelBuilder.Entity<SaleDeal>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_SaleDeals_Price", "CAST(\"FinalPriceUsd\" AS REAL) > 0");
            t.HasCheckConstraint("CK_SaleDeals_Commission", "CAST(\"AgencyCommissionUsd\" AS REAL) >= 0");
        });

        modelBuilder.Entity<Payment>().ToTable(t => t.HasCheckConstraint(
            "CK_Payments_Amount", "CAST(\"AmountUsd\" AS REAL) > 0"));

        modelBuilder.Entity<LandPlot>()
            .HasOne(p => p.Seller)
            .WithMany(u => u.OwnedPlots)
            .HasForeignKey(p => p.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LandPlot>()
            .HasOne(p => p.AssignedRealtor)
            .WithMany()
            .HasForeignKey(p => p.AssignedRealtorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ViewingRequest>()
            .HasOne(v => v.Buyer)
            .WithMany(u => u.ViewingRequests)
            .HasForeignKey(v => v.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ViewingRequest>()
            .HasOne(v => v.LandPlot)
            .WithMany(p => p.ViewingRequests)
            .HasForeignKey(v => v.LandPlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ViewingRequest>()
            .HasOne(v => v.Realtor)
            .WithMany()
            .HasForeignKey(v => v.RealtorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SaleDeal>()
            .HasOne(d => d.LandPlot)
            .WithMany()
            .HasForeignKey(d => d.LandPlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SaleDeal>()
            .HasOne(d => d.Buyer)
            .WithMany()
            .HasForeignKey(d => d.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SaleDeal>()
            .HasOne(d => d.Realtor)
            .WithMany()
            .HasForeignKey(d => d.RealtorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SaleDeal>()
            .HasOne(d => d.Lawyer)
            .WithMany()
            .HasForeignKey(d => d.LawyerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.SaleDeal)
            .WithMany(d => d.Payments)
            .HasForeignKey(p => p.SaleDealId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlotDocument>()
            .HasOne(d => d.LandPlot)
            .WithMany(p => p.Documents)
            .HasForeignKey(d => d.LandPlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlotMedia>()
            .HasOne(m => m.LandPlot)
            .WithMany(p => p.Media)
            .HasForeignKey(m => m.LandPlotId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LegalConclusion>()
            .HasOne(c => c.LandPlot)
            .WithOne(p => p.LegalConclusion)
            .HasForeignKey<LegalConclusion>(c => c.LandPlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SelectionRequest>()
            .HasOne(s => s.Buyer)
            .WithMany()
            .HasForeignKey(s => s.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Запам'ятовує версію, з якою користувач відкрив форму редагування. Якщо рядок змінили в проміжку,
    /// збереження завершиться DbUpdateConcurrencyException, а не перезапише чужі зміни.
    /// </summary>
    public void UseSubmittedVersion<T>(T entity, Guid submittedVersion) where T : class, IVersioned
    {
        if (submittedVersion != Guid.Empty)
        {
            Entry(entity).Property(e => e.Version).OriginalValue = submittedVersion;
        }
    }

    /// <summary>
    /// Оновлює маркер версії кожного змінюваного рядка. Оригінальне значення лишається тим, що було прочитано,
    /// тому UPDATE ... WHERE Version = прочитане не торкнеться рядка, який уже змінив хтось інший.
    /// </summary>
    private void StampVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IVersioned>().Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.Version = Guid.NewGuid();
        }
    }

    public void InitializeDatabase()
    {
        Database.EnsureCreated();

        if (!Users.Any())
        {
            var seedPath = Path.Combine(AppContext.BaseDirectory, "landagency_seed.sql");
            if (!File.Exists(seedPath))
            {
                seedPath = Path.Combine(Directory.GetCurrentDirectory(), "landagency_seed.sql");
            }
            if (!File.Exists(seedPath))
            {
                seedPath = Path.Combine(Directory.GetCurrentDirectory(), "LandAgencyMvc", "landagency_seed.sql");
            }

            if (File.Exists(seedPath))
            {
                var sql = File.ReadAllText(seedPath);
                using var connection = Database.GetDbConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}