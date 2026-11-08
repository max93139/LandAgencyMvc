using System.Security.Cryptography;
using System.Text;

namespace LandAgencyMvc.Models;

public static class LandAgencyHistorySeed
{
    public static void SeedHistoryData(LandAgencyContext db)
    {
        if (db.Users.Count() >= 10 && db.LandPlots.Count() >= 10)
        {
            return;
        }

        var realtor1 = db.Users.FirstOrDefault(u => u.Email == "realtor@landagency.ua");
        if (realtor1 == null)
        {
            realtor1 = new AppUser
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
                CommissionRate = 3.0,
                IsActive = true
            };
            realtor1.SetPassword("realtor123");
            db.Users.Add(realtor1);
        }

        var lawyer1 = db.Users.FirstOrDefault(u => u.Email == "lawyer@landagency.ua");
        if (lawyer1 == null)
        {
            lawyer1 = new AppUser
            {
                FullName = "Коваленко Олена Вікторівна",
                Email = "lawyer@landagency.ua",
                Phone = "+380 50 777 88 99",
                TaxId = "3051948271",
                Role = UserRole.Lawyer,
                CertificateNumber = "LW-881",
                Specialization = "Земельне право, ДЗК та ДРРП",
                SpecializationOrLicense = "Свідоцтво про право на заняття адвокатською діяльністю №LW-881",
                IsActive = true
            };
            lawyer1.SetPassword("lawyer123");
            db.Users.Add(lawyer1);
        }

        var realtor2 = db.Users.FirstOrDefault(u => u.Email == "realtor2@landagency.ua");
        if (realtor2 == null)
        {
            realtor2 = new AppUser
            {
                FullName = "Григоренко Сергій Петрович",
                Email = "realtor2@landagency.ua",
                Phone = "+380 67 444 33 22",
                WorkPhone = "+380 44 220 10 25",
                TaxId = "2891456281",
                BankDetails = "UA753052990000026000001002009",
                Role = UserRole.Realtor,
                LicenseNumber = "RL-409",
                SpecializationOrLicense = "Провідний фахівець із заміської нерухомості №RL-409",
                CommissionRate = 3.0,
                IsActive = true
            };
            realtor2.SetPassword("realtor123");
            db.Users.Add(realtor2);
        }

        var lawyer2 = db.Users.FirstOrDefault(u => u.Email == "lawyer2@landagency.ua");
        if (lawyer2 == null)
        {
            lawyer2 = new AppUser
            {
                FullName = "Кравчук Наталія Дмитрівна",
                Email = "lawyer2@landagency.ua",
                Phone = "+380 50 666 77 88",
                TaxId = "3128495018",
                Role = UserRole.Lawyer,
                CertificateNumber = "LW-895",
                Specialization = "Нотаріат, іпотечні обтяження та судовий аудит",
                SpecializationOrLicense = "Свідоцтво про право на заняття адвокатською діяльністю №LW-895",
                IsActive = true
            };
            lawyer2.SetPassword("lawyer123");
            db.Users.Add(lawyer2);
        }

        var admin = db.Users.FirstOrDefault(u => u.Email == "admin@landagency.ua");
        if (admin == null)
        {
            admin = new AppUser
            {
                FullName = "Адміністратор системи",
                Email = "admin@landagency.ua",
                Phone = "+380 50 123 45 67",
                TaxId = "3345109821",
                BankDetails = "UA123052990000026000009998881",
                Role = UserRole.Realtor,
                LicenseNumber = "ADM-001",
                SpecializationOrLicense = "Керівник департаменту аналітики та аудиту земельного банку",
                CommissionRate = 3.0,
                IsActive = true
            };
            admin.SetPassword("admin123");
            db.Users.Add(admin);
        }

        var seller1 = db.Users.FirstOrDefault(u => u.Email == "seller@landagency.ua");
        if (seller1 == null)
        {
            seller1 = new AppUser
            {
                FullName = "Шевченко Тарас Григорович",
                Email = "seller@landagency.ua",
                Phone = "+380 67 111 22 33",
                TaxId = "2845109283",
                BankDetails = "UA213223130000026007233566001",
                OwnershipDoc = "ДРРП-59102847",
                Role = UserRole.Client,
                IsActive = true
            };
            seller1.SetPassword("client123");
            db.Users.Add(seller1);
        }

        var seller2 = db.Users.FirstOrDefault(u => u.Email == "seller2@landagency.ua");
        if (seller2 == null)
        {
            seller2 = new AppUser
            {
                FullName = "Кравченко Андрій Миколайович",
                Email = "seller2@landagency.ua",
                Phone = "+380 67 222 33 44",
                TaxId = "2910485729",
                BankDetails = "UA353223130000026007233566002",
                OwnershipDoc = "ДРРП-60291834",
                Role = UserRole.Client,
                IsActive = true
            };
            seller2.SetPassword("client123");
            db.Users.Add(seller2);
        }

        var seller3 = db.Users.FirstOrDefault(u => u.Email == "seller3@landagency.ua");
        if (seller3 == null)
        {
            seller3 = new AppUser
            {
                FullName = "Поліщук Роман Володимирович",
                Email = "seller3@landagency.ua",
                Phone = "+380 67 333 44 55",
                TaxId = "3019284756",
                BankDetails = "UA483223130000026007233566003",
                OwnershipDoc = "ДА-ЯЯ-410293",
                Role = UserRole.Client,
                IsActive = true
            };
            seller3.SetPassword("client123");
            db.Users.Add(seller3);
        }

        var seller4 = db.Users.FirstOrDefault(u => u.Email == "seller4@landagency.ua");
        if (seller4 == null)
        {
            seller4 = new AppUser
            {
                FullName = "Дмитренко Галина Василівна",
                Email = "seller4@landagency.ua",
                Phone = "+380 67 444 55 66",
                TaxId = "2719485023",
                BankDetails = "UA593223130000026007233566004",
                OwnershipDoc = "ДРРП-71029481",
                Role = UserRole.Client,
                IsActive = true
            };
            seller4.SetPassword("client123");
            db.Users.Add(seller4);
        }

        var buyer1 = db.Users.FirstOrDefault(u => u.Email == "buyer@landagency.ua");
        if (buyer1 == null)
        {
            buyer1 = new AppUser
            {
                FullName = "Мельник Оксана Іванівна",
                Email = "buyer@landagency.ua",
                Phone = "+380 50 444 55 66",
                TaxId = "3194827165",
                BankDetails = "UA453052990000026201234567890",
                MaxBudget = 40000,
                TargetPurpose = "02.01 Для будівництва житлового будинку",
                Role = UserRole.Client,
                IsActive = true
            };
            buyer1.SetPassword("client123");
            db.Users.Add(buyer1);
        }

        var buyer2 = db.Users.FirstOrDefault(u => u.Email == "buyer2@landagency.ua");
        if (buyer2 == null)
        {
            buyer2 = new AppUser
            {
                FullName = "Сидоренко Максим Олегович",
                Email = "buyer2@landagency.ua",
                Phone = "+380 50 222 33 44",
                TaxId = "3219485012",
                BankDetails = "UA563052990000026201234567891",
                MaxBudget = 75000,
                TargetPurpose = "03.07 Для будівництва курортних готелів",
                Role = UserRole.Client,
                IsActive = true
            };
            buyer2.SetPassword("client123");
            db.Users.Add(buyer2);
        }

        var buyer3 = db.Users.FirstOrDefault(u => u.Email == "buyer3@landagency.ua");
        if (buyer3 == null)
        {
            buyer3 = new AppUser
            {
                FullName = "Ковальчук Ярослав Сергійович",
                Email = "buyer3@landagency.ua",
                Phone = "+380 50 333 44 55",
                TaxId = "3091827461",
                BankDetails = "UA673052990000026201234567892",
                MaxBudget = 90000,
                TargetPurpose = "11.02 Для розміщення складів",
                Role = UserRole.Client,
                IsActive = true
            };
            buyer3.SetPassword("client123");
            db.Users.Add(buyer3);
        }

        var buyer4 = db.Users.FirstOrDefault(u => u.Email == "buyer4@landagency.ua");
        if (buyer4 == null)
        {
            buyer4 = new AppUser
            {
                FullName = "Василенко Юлія Борисівна",
                Email = "buyer4@landagency.ua",
                Phone = "+380 50 888 99 00",
                TaxId = "2948172039",
                BankDetails = "UA783052990000026201234567893",
                MaxBudget = 55000,
                TargetPurpose = "02.01 Для будівництва житлового будинку",
                Role = UserRole.Client,
                IsActive = true
            };
            buyer4.SetPassword("client123");
            db.Users.Add(buyer4);
        }

        var buyer5 = db.Users.FirstOrDefault(u => u.Email == "buyer5@landagency.ua");
        if (buyer5 == null)
        {
            buyer5 = new AppUser
            {
                FullName = "Ткаченко Дмитро Ігорович",
                Email = "buyer5@landagency.ua",
                Phone = "+380 50 999 11 22",
                TaxId = "3281940284",
                BankDetails = "UA893052990000026201234567894",
                MaxBudget = 100000,
                TargetPurpose = "11.01 Для розміщення промислових об'єктів",
                Role = UserRole.Client,
                IsActive = true
            };
            buyer5.SetPassword("client123");
            db.Users.Add(buyer5);
        }

        db.SaveChanges();

        var existingCadastrals = db.LandPlots.Select(p => p.CadastralNumber).ToHashSet();

        var plotsData = new List<(string Cad, string Title, string Loc, double Area, string Purpose, decimal Price, string Doc, AppUser Seller, AppUser Realtor, PlotStatus Status, DateTime Created)>
        {
            ("3221884000:03:001:0120", "Ділянка біля соснового лісу в с. Лісники", "Київська область, Обухівський район, с. Лісники, вул. Лісова, 14", 0.15, "02.01 Для будівництва житлового будинку", 28500m, "ДРРП-59102847", seller1, realtor1, PlotStatus.Sold, new DateTime(2025, 7, 10)),
            ("4623683300:01:002:0045", "Панорамна ділянка під котедж у с. Сокільники", "Львівська область, Львівський район, с. Сокільники, вул. Сонячна, 8", 0.20, "02.01 Для будівництва житлового будинку", 34000m, "ДРРП-60291834", seller2, realtor2, PlotStatus.Sold, new DateTime(2025, 8, 5)),
            ("3222486201:01:015:0084", "Фермерський масив чорнозему в Бучанському районі", "Київська область, Бучанський район, с. Петропавлівська Борщагівка", 1.20, "01.03 Для ведення особистого селянського господарства", 42000m, "ДА-ЯЯ-410293", seller3, realtor1, PlotStatus.Sold, new DateTime(2025, 9, 12)),
            ("3221884000:03:001:0000", "Ділянка під житлову забудову в с. Гатне", "Київська область, Фастівський район, с. Гатне, вул. Покровська, 3", 0.12, "02.01 Для будівництва житлового будинку", 15400m, "ДРРП-10029384", seller1, realtor2, PlotStatus.PendingLegalCheck, new DateTime(2025, 10, 1)),
            ("5122785800:02:001:0312", "Фасадна рекреаційна ділянка біля моря в смт Затока", "Одеська область, Білгород-Дністровський район, смт Затока, вул. Приморська, 42", 0.25, "03.07 Для будівництва курортних готелів", 65000m, "ДРРП-71029481", seller4, realtor1, PlotStatus.Sold, new DateTime(2025, 7, 20)),
            ("2611092000:01:004:0189", "Гірська садиба з краєвидом на Чорногору", "Івано-Франківська область, Яремчанський район, с. Татарів, вул. Шевченка, 21", 0.18, "02.01 Для будівництва житлового будинку", 48000m, "ДРРП-72819401", seller2, realtor2, PlotStatus.Sold, new DateTime(2025, 8, 15)),
            ("2110100000:04:002:0521", "Земельний масив під логістичний склад", "Закарпатська область, м. Ужгород, вул. Об'їзна, 109", 0.80, "11.02 Для розміщення складів", 82000m, "ДРРП-73910284", seller3, realtor1, PlotStatus.VerifiedInCatalog, new DateTime(2025, 9, 20)),
            ("3223186801:01:003:0245", "Рівна ділянка під таунхауси в с. Козин", "Київська область, Обухівський район, смт Козин, вул. Старокиївська, 15", 0.30, "02.01 Для будівництва житлового будинку", 75000m, "ДРРП-74820193", seller1, realtor1, PlotStatus.Sold, new DateTime(2025, 10, 5)),
            ("4625882900:02:001:0410", "Ділянка в курортному містечку Східниця", "Львівська область, Дрогобицький район, смт Східниця, вул. Зарічна, 7", 0.16, "03.08 Для санаторіїв", 39000m, "ДРРП-75910283", seller2, realtor2, PlotStatus.Sold, new DateTime(2025, 11, 10)),
            ("5324087700:01:008:0156", "Рілля високої якості біля річки Ворскла", "Полтавська область, Полтавський район, с. Нижні Млини", 2.50, "01.01 Для товарного с/г виробництва", 31500m, "ДА-ЯЯ-551029", seller3, realtor1, PlotStatus.Sold, new DateTime(2026, 1, 15)),
            ("1221486200:03:002:0078", "Промислова ділянка з залізничною колією", "Дніпропетровська область, м. Дніпро, вул. Заводська, 88", 1.50, "11.01 Для промислових об'єктів", 95000m, "ДРРП-76810294", seller4, realtor2, PlotStatus.Sold, new DateTime(2026, 3, 10)),
            ("3220882600:02:005:0114", "Затишна ділянка під садівництво в с. Вишеньки", "Київська область, Бориспільський район, с. Вишеньки, вул. Лугова, 3", 0.10, "01.05 Індивідуальне садівництво", 18000m, "ДРРП-77820194", seller1, realtor1, PlotStatus.Sold, new DateTime(2026, 4, 12)),
            ("3222482001:01:006:0290", "Кутова ділянка біля озера в с. Крюківщина", "Київська область, Бучанський район, с. Крюківщина, вул. Озерна, 19", 0.14, "02.01 Для будівництва житлового будинку", 26000m, "ДРРП-78910284", seller2, realtor2, PlotStatus.Sold, new DateTime(2026, 5, 20)),
            ("4622784000:01:001:0388", "Ділянка під приватний будинок у Брюховичах", "Львівська область, Львівський район, смт Брюховичі, вул. Лісна, 5", 0.12, "02.01 Для будівництва житлового будинку", 38500m, "ДРРП-79018274", seller3, realtor1, PlotStatus.Sold, new DateTime(2026, 6, 18)),
            ("5123782200:02:003:0167", "Ділянка під котеджну забудову в с. Фонтанка", "Одеська область, Одеський район, с. Фонтанка, вул. Морська, 11", 0.15, "02.01 Для будівництва житлового будинку", 32000m, "ДРРП-80129481", seller4, realtor2, PlotStatus.Sold, new DateTime(2026, 7, 22)),
            ("2622481001:01:002:0095", "Рекреаційна ділянка поруч із Буковелем", "Івано-Франківська область, Надвірнянський район, с. Поляниця, урочище Вишня", 0.22, "03.07 Для будівництва курортних готелів", 92000m, "ДРРП-81204918", seller2, realtor1, PlotStatus.Sold, new DateTime(2026, 8, 25))
        };

        var plots = new List<LandPlot>();

        foreach (var p in plotsData)
        {
            var existing = db.LandPlots.FirstOrDefault(x => x.CadastralNumber == p.Cad);
            if (existing == null)
            {
                existing = new LandPlot
                {
                    CadastralNumber = p.Cad,
                    Title = p.Title,
                    Location = p.Loc,
                    AreaHectares = p.Area,
                    Purpose = p.Purpose,
                    PriceUsd = p.Price,
                    OwnershipDocNumber = p.Doc,
                    SellerBankIban = p.Seller.BankDetails,
                    Description = $"{p.Title}. Правильна геометрія меж, комунікації поруч, перевірено в ДЗК/ДРРП.",
                    Status = p.Status,
                    SellerId = p.Seller.Id,
                    AssignedRealtorId = p.Realtor.Id,
                    CreatedAt = p.Created
                };
                db.LandPlots.Add(existing);
                db.SaveChanges();
            }
            plots.Add(existing);
        }

        foreach (var plot in plots)
        {
            if (!db.PlotDocuments.Any(d => d.LandPlotId == plot.Id))
            {
                var docBytes = Encoding.UTF8.GetBytes($"Витяг ДРРП {plot.OwnershipDocNumber} для ділянки {plot.CadastralNumber}");
                var doc = new PlotDocument
                {
                    LandPlotId = plot.Id,
                    Title = $"Витяг з ДРРП про право власності ({plot.OwnershipDocNumber})",
                    DocType = "Витяг з ДРРП",
                    DocumentNumber = plot.OwnershipDocNumber,
                    IssueDate = plot.CreatedAt.AddMonths(-3),
                    IsVerified = true
                };
                doc.AttachFile(string.Empty, docBytes, "application/pdf");
                db.PlotDocuments.Add(doc);
            }

            if (!db.LegalConclusions.Any(c => c.LandPlotId == plot.Id))
            {
                var lawyer = plot.Id % 2 == 0 ? lawyer2 : lawyer1;
                bool hasRisk = plot.CadastralNumber.EndsWith(":0000");
                var conclusion = new LegalConclusion
                {
                    ConclusionNumber = $"ЮВ-{plot.CreatedAt:yyyy}-{plot.Id:D4}",
                    LandPlotId = plot.Id,
                    LawyerId = lawyer.Id,
                    NoArrestsOrMortgages = !hasRisk,
                    NoServitudes = !hasRisk,
                    BoundariesMatchDzk = true,
                    SellerMatchesDrp = true,
                    NoSanctions = true,
                    RiskLevel = hasRisk ? "Високий (Судовий арешт)" : "Низький (Чисто)",
                    DzkExtractNumber = $"ВТ-ДЗК-{plot.CreatedAt:yyyy}-{plot.Id:D4}",
                    CreatedAt = plot.CreatedAt.AddDays(2)
                };

                if (hasRisk)
                {
                    conclusion.Reject("Виявлено обтяження у ДРРП: судова заборона відчуження. Відмовлено у внесенні до каталогу.");
                }
                else
                {
                    conclusion.Approve("Повну перевірку у ДЗК та ДРРП пройдено успішно. Обтяжень, арештів та сервітутів не виявлено.", "Низький (Чисто)");
                }
                db.LegalConclusions.Add(conclusion);
            }
        }
        db.SaveChanges();

        if (db.ViewingRequests.Count() < 10)
        {
            var buyers = new[] { buyer1, buyer2, buyer3, buyer4, buyer5 };
            for (int i = 0; i < plots.Count; i++)
            {
                var targetPlot = plots[i];
                var targetBuyer = buyers[i % buyers.Length];
                var targetRealtor = i % 2 == 0 ? realtor1 : realtor2;
                var viewDate = targetPlot.CreatedAt.AddDays(7);

                if (!db.ViewingRequests.Any(v => v.LandPlotId == targetPlot.Id && v.BuyerId == targetBuyer.Id))
                {
                    db.ViewingRequests.Add(new ViewingRequest
                    {
                        LandPlotId = targetPlot.Id,
                        BuyerId = targetBuyer.Id,
                        RealtorId = targetRealtor.Id,
                        PreferredDate = viewDate,
                        TimeSlot = $"{10 + (i % 6)}:00",
                        Comment = $"Виїзний огляд меж ділянки {targetPlot.CadastralNumber} на місцевості разом із геодезистом.",
                        Status = i < 12 ? ViewingStatus.Completed : ViewingStatus.Scheduled,
                        CreatedAt = viewDate.AddDays(-2)
                    });
                }
            }
            db.SaveChanges();
        }

        if (db.SelectionRequests.Count() < 10)
        {
            var selData = new[]
            {
                (buyer1, "Обухівський район", 0.12, 35000m, "02.01 Для будівництва житлового будинку", realtor1, "Підібрано ділянки у Лісниках та Козині"),
                (buyer2, "Одеський район", 0.15, 70000m, "03.07 Для будівництва курортних готелів", realtor2, "Підібрано рекреаційні ділянки біля моря"),
                (buyer3, "м. Ужгород", 0.50, 90000m, "11.02 Для розміщення складів", realtor1, "Підібрано промислові ділянки під хаб"),
                (buyer4, "Львівський район", 0.10, 45000m, "02.01 Для будівництва житлового будинку", realtor2, "Підібрано ділянки у Сокільниках та Брюховичах"),
                (buyer5, "м. Дніпро", 1.00, 110000m, "11.01 Для промислових об'єктів", realtor1, "Підібрано ділянки з залізничною гілкою"),
                (buyer1, "Бучанський район", 0.10, 30000m, "02.01 Для будівництва житлового будинку", realtor2, "Підібрано об'єкти у Крюківщині"),
                (buyer2, "Яремчанський район", 0.15, 60000m, "02.01 Для будівництва житлового будинку", realtor1, "Підібрано панорамну ділянку в Татарові"),
                (buyer3, "Дрогобицький район", 0.12, 45000m, "03.08 Для санаторіїв", realtor2, "Підібрано курортну ділянку у Східниці"),
                (buyer4, "Полтавський район", 2.00, 35000m, "01.01 Для товарного с/г виробництва", realtor1, "Підібрано родючі чорноземи біля річки"),
                (buyer5, "Надвірнянський район", 0.20, 100000m, "03.07 Для будівництва курортних готелів", realtor2, "Підібрано ділянку в урочищі Вишня біля Буковелю")
            };

            foreach (var s in selData)
            {
                if (!db.SelectionRequests.Any(r => r.BuyerId == s.Item1.Id && r.DesiredDistrict == s.Item2))
                {
                    db.SelectionRequests.Add(new SelectionRequest
                    {
                        BuyerId = s.Item1.Id,
                        DesiredDistrict = s.Item2,
                        MinAreaHa = s.Item3,
                        MaxBudgetUsd = s.Item4,
                        Purpose = s.Item5,
                        IsProcessedByRealtor = true,
                        ProcessedByRealtorId = s.Item6.Id,
                        MatchedPlotsCount = 2,
                        MatchedPlotIds = "1,2",
                        RealtorNotes = s.Item7,
                        CreatedAt = DateTime.Now.AddDays(-15)
                    });
                }
            }
            db.SaveChanges();
        }

        if (db.SaleDeals.Count() < 10)
        {
            var dealsSchedule = new List<(LandPlot Plot, AppUser Buyer, AppUser Realtor, AppUser Lawyer, DateTime NotaryDt, string NotaryOffice, string NotaryRegNum)>
            {
                (plots[4], buyer2, realtor1, lawyer1, new DateTime(2025, 8, 18, 14, 0, 0), "Білгород-Дністровська районна державна нотаріальна контора", "№ 2-1845/2025"),
                (plots[5], buyer2, realtor2, lawyer2, new DateTime(2025, 9, 24, 11, 30, 0), "Яремчанська державна нотаріальна контора", "№ 1-9402/2025"),
                (plots[7], buyer4, realtor1, lawyer1, new DateTime(2025, 11, 12, 15, 0, 0), "Обухівська районна державна нотаріальна контора", "№ 4-5012/2025"),
                (plots[8], buyer3, realtor2, lawyer2, new DateTime(2025, 12, 19, 12, 0, 0), "Дрогобицька державна нотаріальна контора", "№ 3-1948/2025"),
                (plots[9], buyer5, realtor1, lawyer1, new DateTime(2026, 2, 14, 10, 30, 0), "Полтавська міська нотаріальна контора", "№ 1-0482/2026"),
                (plots[10], buyer5, realtor2, lawyer2, new DateTime(2026, 4, 20, 16, 0, 0), "Перша Дніпровська державна нотаріальна контора", "№ 5-8910/2026"),
                (plots[11], buyer1, realtor1, lawyer1, new DateTime(2026, 5, 18, 11, 0, 0), "Бориспільська районна державна нотаріальна контора", "№ 2-4820/2026"),
                (plots[12], buyer4, realtor2, lawyer2, new DateTime(2026, 6, 25, 14, 30, 0), "Києво-Святошинська державна нотаріальна контора", "№ 6-1029/2026"),
                (plots[13], buyer1, realtor1, lawyer1, new DateTime(2026, 7, 16, 12, 0, 0), "Шевченківська державна нотаріальна контора м. Львова", "№ 3-7819/2026"),
                (plots[14], buyer2, realtor2, lawyer2, new DateTime(2026, 8, 22, 15, 30, 0), "Одеська міська державна нотаріальна контора", "№ 4-9102/2026"),
                (plots[15], buyer4, realtor1, lawyer1, new DateTime(2026, 9, 15, 11, 0, 0), "Надвірнянська державна нотаріальна контора", "№ 1-8204/2026"),
                (plots[1], buyer1, realtor2, lawyer2, new DateTime(2026, 10, 15, 14, 0, 0), "Перша Львівська державна нотаріальна контора", "№ 2-9018/2026"),
                (plots[0], buyer3, realtor1, lawyer1, new DateTime(2026, 10, 28, 16, 30, 0), "Обухівська районна державна нотаріальна контора", "№ 4-9481/2026"),
                (plots[2], buyer5, realtor1, lawyer2, new DateTime(2026, 11, 3, 10, 0, 0), "Києво-Святошинська державна нотаріальна контора", "№ 6-3910/2026")
            };

            for (int i = 0; i < dealsSchedule.Count; i++)
            {
                var item = dealsSchedule[i];
                var contractNum = $"ДГ-{item.NotaryDt:yyyy}-{100 + i + 1}";

                var deal = db.SaleDeals.FirstOrDefault(d => d.DraftContractNumber == contractNum);
                if (deal == null)
                {
                    deal = new SaleDeal
                    {
                        LandPlotId = item.Plot.Id,
                        BuyerId = item.Buyer.Id,
                        RealtorId = item.Realtor.Id,
                        LawyerId = item.Lawyer.Id,
                        FinalPriceUsd = item.Plot.PriceUsd,
                        AgencyCommissionUsd = Math.Round(item.Plot.PriceUsd * 0.03m, 2),
                        NotaryOffice = item.NotaryOffice,
                        NotaryDate = item.NotaryDt,
                        CreatedAt = item.NotaryDt.AddDays(-10),
                        DraftContractNumber = contractNum,
                        NotarialRegistryNumber = item.NotaryRegNum,
                        LawyerApprovalNotes = "Договір відчуження земельної ділянки повністю відповідає статтям 131, 132 Земельного кодексу України. Розрахунки проведено безготівково.",
                        Status = DealStatus.CompletedAtNotary
                    };
                    db.SaleDeals.Add(deal);
                    db.SaveChanges();

                    var payments = deal.CreateDefaultPayments(
                        item.Plot.Seller?.BankDetails ?? item.Plot.SellerBankIban,
                        isPaid: true,
                        senderIban: item.Buyer.BankDetails,
                        agencyIban: item.Realtor.BankDetails);

                    foreach (var p in payments)
                    {
                        p.PaymentDate = item.NotaryDt;
                        p.SubmittedAt = item.NotaryDt;
                        p.VerifiedAt = item.NotaryDt;
                        p.VerifiedByUserId = item.Realtor.Id;
                        p.PaymentReference = $"TRX-IBAN-{item.NotaryDt:yyyyMMdd}-{deal.Id:D3}";
                        p.VerificationNotes = "Зараховано та підтверджено банківською випискою IBAN.";
                    }

                    db.Payments.AddRange(payments);
                    db.SaveChanges();
                }
            }
        }
    }
}
