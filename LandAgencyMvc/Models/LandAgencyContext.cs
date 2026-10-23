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

        if (!LandPlots.Any())
        {
            SeedInitialDomainData();
        }
    }

    private void SeedInitialDomainData()
    {
        var realtor = Users.FirstOrDefault(u => u.Role == UserRole.Realtor);
        var lawyer = Users.FirstOrDefault(u => u.Role == UserRole.Lawyer);

        var seller = Users.FirstOrDefault(u => u.Email == "seller@landagency.ua");
        if (seller == null)
        {
            seller = new AppUser
            {
                FullName = "Шевченко Тарас Григорович",
                Email = "seller@landagency.ua",
                Phone = "+380 67 111 22 33",
                TaxId = "2845109283",
                BankDetails = "UA213223130000026007233566001",
                OwnershipDoc = "ДРРП-59102847",
                Role = UserRole.Client
            };
            seller.SetPassword("client123");
            Users.Add(seller);
        }

        var buyer = Users.FirstOrDefault(u => u.Email == "buyer@landagency.ua");
        if (buyer == null)
        {
            buyer = new AppUser
            {
                FullName = "Мельник Оксана Іванівна",
                Email = "buyer@landagency.ua",
                Phone = "+380 50 444 55 66",
                TaxId = "3194827165",
                BankDetails = "UA453052990000026201234567890",
                MaxBudget = 40000,
                TargetPurpose = "02.01 Для будівництва житлового будинку",
                Role = UserRole.Client
            };
            buyer.SetPassword("client123");
            Users.Add(buyer);
        }

        SaveChanges();

        var plot1 = new LandPlot
        {
            CadastralNumber = "3221884000:03:001:0120",
            Title = "Ділянка біля соснового лісу в с. Лісники",
            Location = "Київська область, Обухівський район, с. Лісники, вул. Лісова, 14",
            AreaHectares = 0.15,
            Purpose = "02.01 Для будівництва житлового будинку",
            PriceUsd = 28500m,
            OwnershipDocNumber = "ДРРП-59102847",
            SellerBankIban = seller.BankDetails,
            Description = "Рівна фасаднa ділянка 15 соток, асфальтований під'їзд, газ та електрика 15 кВт уздовж межі, межові знаки винесені в натуру.",
            Status = PlotStatus.VerifiedInCatalog,
            SellerId = seller.Id,
            AssignedRealtorId = realtor?.Id,
            CreatedAt = DateTime.Now.AddDays(-5)
        };

        var plot2 = new LandPlot
        {
            CadastralNumber = "4623683300:01:002:0045",
            Title = "Панорамна ділянка під котедж у с. Сокільники",
            Location = "Львівська область, Львівський район, с. Сокільники, вул. Сонячна, 8",
            AreaHectares = 0.20,
            Purpose = "02.01 Для будівництва житлового будинку",
            PriceUsd = 34000m,
            OwnershipDocNumber = "ДРРП-60291834",
            SellerBankIban = seller.BankDetails,
            Description = "Ділянка правильної прямокутної форми у котеджному масиві, поруч міські комунікації та зручний виїзд на кільцеву дорогу.",
            Status = PlotStatus.VerifiedInCatalog,
            SellerId = seller.Id,
            AssignedRealtorId = realtor?.Id,
            CreatedAt = DateTime.Now.AddDays(-4)
        };

        var plot3 = new LandPlot
        {
            CadastralNumber = "3222486201:01:015:0084",
            Title = "Фермерський масив чорнозему в Бучанському районі",
            Location = "Київська область, Бучанський район, с. Петропавлівська Борщагівка",
            AreaHectares = 1.20,
            Purpose = "01.03 Для ведення особистого селянського господарства",
            PriceUsd = 42000m,
            OwnershipDocNumber = "ДА-ЯЯ-410293",
            SellerBankIban = seller.BankDetails,
            Description = "Родюча земельна ділянка сільськогосподарського призначення, якісний чорнозем, рівнинний рельєф, зручний асфальтований під'їзд для великогабаритної агротехніки.",
            Status = PlotStatus.PendingLegalCheck,
            SellerId = seller.Id,
            AssignedRealtorId = realtor?.Id,
            CreatedAt = DateTime.Now.AddDays(-1)
        };

        var plot4 = new LandPlot
        {
            CadastralNumber = "3221884000:03:001:0000",
            Title = "Ділянка під житлову забудову в с. Гатне",
            Location = "Київська область, Фастівський район, с. Гатне, вул. Покровська, 3",
            AreaHectares = 0.12,
            Purpose = "02.01 Для будівництва житлового будинку",
            PriceUsd = 5400m,
            OwnershipDocNumber = "ДРРП-10029384",
            SellerBankIban = seller.BankDetails,
            Description = "Терміновий продаж ділянки правильної прямокутної форми у новому масиві забудови. Газ та електромережа проходять уздовж вулиці.",
            Status = PlotStatus.PendingLegalCheck,
            SellerId = seller.Id,
            AssignedRealtorId = realtor?.Id,
            CreatedAt = DateTime.Now
        };

        LandPlots.AddRange(plot1, plot2, plot3, plot4);
        SaveChanges();

        var doc1Bytes = System.Text.Encoding.UTF8.GetBytes($"Витяг з ДРРП №{plot1.OwnershipDocNumber} Кадастровий номер {plot1.CadastralNumber}");
        var doc1 = new PlotDocument
        {
            LandPlotId = plot1.Id,
            Title = $"Витяг з ДРРП ({plot1.OwnershipDocNumber})",
            DocType = "Витяг з ДРРП про право власності",
            DocumentNumber = plot1.OwnershipDocNumber,
            IssueDate = DateTime.Today.AddMonths(-6),
            IsVerified = true
        };
        doc1.AttachFile(string.Empty, doc1Bytes, "application/pdf");
        doc1.ApplyKepSignature(seller.FullName, seller.TaxId, doc1Bytes);

        var doc2Bytes = System.Text.Encoding.UTF8.GetBytes($"Витяг з ДРРП №{plot2.OwnershipDocNumber} Кадастровий номер {plot2.CadastralNumber}");
        var doc2 = new PlotDocument
        {
            LandPlotId = plot2.Id,
            Title = $"Витяг з ДРРП ({plot2.OwnershipDocNumber})",
            DocType = "Витяг з ДРРП про право власності",
            DocumentNumber = plot2.OwnershipDocNumber,
            IssueDate = DateTime.Today.AddMonths(-4),
            IsVerified = true
        };
        doc2.AttachFile(string.Empty, doc2Bytes, "application/pdf");
        doc2.ApplyKepSignature(seller.FullName, seller.TaxId, doc2Bytes);

        var doc3Bytes = System.Text.Encoding.UTF8.GetBytes($"Державний акт №{plot3.OwnershipDocNumber} Кадастровий номер {plot3.CadastralNumber}");
        var doc3 = new PlotDocument
        {
            LandPlotId = plot3.Id,
            Title = $"Державний акт ({plot3.OwnershipDocNumber})",
            DocType = "Державний акт на право власності на землю",
            DocumentNumber = plot3.OwnershipDocNumber,
            IssueDate = DateTime.Today.AddYears(-2),
            IsVerified = false
        };
        doc3.AttachFile(string.Empty, doc3Bytes, "application/pdf");
        doc3.ApplyKepSignature(seller.FullName, seller.TaxId, doc3Bytes);

        PlotDocuments.AddRange(doc1, doc2, doc3);

        var conclusion1 = new LegalConclusion
        {
            ConclusionNumber = $"ЮВ-{DateTime.Now:yyyy}-001",
            LandPlotId = plot1.Id,
            LawyerId = lawyer?.Id,
            NoArrestsOrMortgages = true,
            NoServitudes = true,
            BoundariesMatchDzk = true,
            SellerMatchesDrp = true,
            NoSanctions = true,
            RiskLevel = "Низький (Чисто)",
            DzkExtractNumber = $"ВТ-ДЗК-{DateTime.Now:yyyy}-0001",
            CreatedAt = DateTime.Now.AddDays(-4)
        };
        conclusion1.Approve("За даними ДЗК та ДРРП ділянка у с. Лісники зареєстрована належним чином. Координати поворотних точок меж узгоджені, обтяжень не виявлено.", "Низький (Чисто)");

        var conclusion2 = new LegalConclusion
        {
            ConclusionNumber = $"ЮВ-{DateTime.Now:yyyy}-002",
            LandPlotId = plot2.Id,
            LawyerId = lawyer?.Id,
            NoArrestsOrMortgages = true,
            NoServitudes = true,
            BoundariesMatchDzk = true,
            SellerMatchesDrp = true,
            NoSanctions = true,
            RiskLevel = "Низький (Чисто)",
            DzkExtractNumber = $"ВТ-ДЗК-{DateTime.Now:yyyy}-0002",
            CreatedAt = DateTime.Now.AddDays(-3)
        };
        conclusion2.Approve("Перевірку у ДЗК та ДРРП пройдено успішно. Судових арештів, іпотек та земельних сервітутів не зареєстровано.", "Низький (Чисто)");

        LegalConclusions.AddRange(conclusion1, conclusion2);

        var selectionReq = new SelectionRequest
        {
            BuyerId = buyer.Id,
            DesiredDistrict = "Обухівський район",
            MinAreaHa = 0.12,
            MaxBudgetUsd = 32000m,
            Purpose = "02.01 Житлова забудова",
            IsProcessedByRealtor = true,
            ProcessedByRealtorId = realtor?.Id,
            MatchedPlotIds = plot1.Id.ToString(),
            RealtorNotes = "Підібрано перевірену земельну ділянку в Обухівському районі (с. Лісники), що повністю відповідає критеріям площі та бюджету.",
            CreatedAt = DateTime.Now.AddDays(-2)
        };
        SelectionRequests.Add(selectionReq);

        var viewing1 = new ViewingRequest
        {
            LandPlotId = plot1.Id,
            BuyerId = buyer.Id,
            RealtorId = realtor?.Id,
            PreferredDate = DateTime.Today.AddDays(1),
            TimeSlot = "11:30",
            Comment = "Огляд межових знаків та трансформаторної підстанції біля ділянки.",
            Status = ViewingStatus.Scheduled
        };
        ViewingRequests.Add(viewing1);

        var deal1 = new SaleDeal
        {
            LandPlotId = plot2.Id,
            BuyerId = buyer.Id,
            RealtorId = realtor?.Id,
            LawyerId = lawyer?.Id,
            NotaryOffice = "Перша Львівська державна нотаріальна контора",
            NotaryDate = DateTime.Today.AddDays(3).AddHours(14),
            LawyerApprovalNotes = "Текст договору відповідає вимогам ст. 132 Земельного кодексу України. Дозволено передачу пакету документів нотаріусу."
        };
        deal1.PrepareContractDraft($"ДГ-{DateTime.Now:yyyy}-101", plot2.PriceUsd, 3.0);
        deal1.Status = DealStatus.ApprovedByLawyerForNotary;

        SaleDeals.Add(deal1);
        SaveChanges();

        var payments = deal1.CreateDefaultPayments(
            seller.BankDetails,
            isPaid: false,
            senderIban: buyer.BankDetails,
            agencyIban: realtor?.BankDetails);

        if (payments.Count > 1)
        {
            payments[1].SubmitPayment("TRX-PB24-908172", buyer.BankDetails);
        }

        Payments.AddRange(payments);
        SaveChanges();
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