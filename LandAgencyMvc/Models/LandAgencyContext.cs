using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Models;

public class LandAgencyContext : DbContext
{
    public DbSet<AppUser> Users { get; set; } = null!;
    public DbSet<LandPlot> LandPlots { get; set; } = null!;
    public DbSet<PlotDocument> PlotDocuments { get; set; } = null!;
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

        modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<LandPlot>().HasIndex(p => p.CadastralNumber).IsUnique();
        modelBuilder.Entity<LegalConclusion>().HasIndex(c => c.ConclusionNumber).IsUnique();
        modelBuilder.Entity<SaleDeal>().HasIndex(d => d.DraftContractNumber).IsUnique();
        modelBuilder.Entity<Payment>().HasIndex(p => p.InvoiceNumber).IsUnique();

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