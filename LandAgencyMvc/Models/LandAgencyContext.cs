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
            .HasOne(v => v.Realtor)
            .WithMany()
            .HasForeignKey(v => v.RealtorId)
            .OnDelete(DeleteBehavior.SetNull);

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
    }

    public void InitializeDatabase()
    {
        EnsureSchemaUpToDate();
        Database.EnsureCreated();

        bool changed = false;

        if (!Users.Any(u => u.Role == UserRole.Realtor))
        {
            var realtor = new AppUser
            {
                FullName = "Бондар Ігор Олександрович",
                Email = "realtor@landagency.ua",
                Phone = "+380 67 555 44 33",
                WorkPhone = "+380 44 220 10 20",
                TaxId = "2984512390",
                BankDetails = "UA903052990000026000001002003",
                Role = UserRole.Realtor,
                LicenseNumber = "RL-402",
                SpecializationOrLicense = "Сертифікований рієлтор АСНУ №RL-402",
                CommissionRate = 3.0
            };
            realtor.SetPassword("realtor123");
            Users.Add(realtor);
            changed = true;
        }

        if (!Users.Any(u => u.Role == UserRole.Lawyer))
        {
            var lawyer = new AppUser
            {
                FullName = "Коваленко Олена Вікторівна",
                Email = "lawyer@landagency.ua",
                Phone = "+380 50 777 88 99",
                TaxId = "3051948271",
                Role = UserRole.Lawyer,
                CertificateNumber = "LW-881",
                Specialization = "Земельне право, ДЗК та ДРРП",
                SpecializationOrLicense = "Свідоцтво про право на заняття адвокатською діяльністю №LW-881"
            };
            lawyer.SetPassword("lawyer123");
            Users.Add(lawyer);
            changed = true;
        }

        // Автоматичне безпечне перехешування незахешованих паролів існуючих користувачів
        var legacyUsers = Users.Where(u => !string.IsNullOrEmpty(u.Password) && !u.Password.StartsWith("AQAAAA")).ToList();
        foreach (var legacyUser in legacyUsers)
        {
            legacyUser.SetPassword(legacyUser.Password);
            changed = true;
        }

        if (changed)
        {
            SaveChanges();
        }
    }

    private void EnsureSchemaUpToDate()
    {
        try
        {
            _ = PlotDocuments.Select(d => d.FileHash).FirstOrDefault();
            _ = Payments.Select(p => p.InvoiceNumber).FirstOrDefault();
            _ = LandPlots.Select(p => p.SellerBankIban).FirstOrDefault();
            _ = Users.Select(u => u.OwnershipDoc).FirstOrDefault();
        }
        catch
        {
            Database.EnsureDeleted();
        }
    }
}