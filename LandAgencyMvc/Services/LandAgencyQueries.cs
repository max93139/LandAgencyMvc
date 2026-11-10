using System.Linq.Expressions;
using LandAgencyMvc.Models;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Services;

public class LandAgencyQueries : ILandAgencyQueries
{
    private readonly LandAgencyContext _db;
    private const int MaxRows = 300;

    public LandAgencyQueries(LandAgencyContext db)
    {
        _db = db;
    }

    public async Task<QueryResult<PlotRow>> FilterPlotsAsync(PlotFilter filter)
    {
        var query = _db.LandPlots.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Region))
        {
            var region = filter.Region.Trim();
            var regionCap = char.ToUpper(region[0]) + (region.Length > 1 ? region.Substring(1) : "");
            var regionLower = char.ToLower(region[0]) + (region.Length > 1 ? region.Substring(1) : "");
            query = query.Where(p => p.Location.Contains(region) || p.Location.Contains(regionCap) || p.Location.Contains(regionLower));
        }

        if (!string.IsNullOrWhiteSpace(filter.Purpose))
        {
            var purpose = filter.Purpose.Trim();
            query = query.Where(p => p.Purpose.Contains(purpose));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(p => p.Status == filter.Status.Value);
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(p => p.PriceUsd >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(p => p.PriceUsd <= filter.MaxPrice.Value);
        }

        if (filter.MinArea.HasValue)
        {
            query = query.Where(p => p.AreaHectares >= filter.MinArea.Value);
        }

        if (filter.MaxArea.HasValue)
        {
            query = query.Where(p => p.AreaHectares <= filter.MaxArea.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Participant))
        {
            var text = filter.Participant.Trim();
            var textCap = char.ToUpper(text[0]) + (text.Length > 1 ? text.Substring(1) : "");
            var textLower = char.ToLower(text[0]) + (text.Length > 1 ? text.Substring(1) : "");
            query = query.Where(p => (p.Seller != null && (p.Seller.FullName.Contains(text) || p.Seller.FullName.Contains(textCap) || p.Seller.FullName.Contains(textLower) || p.Seller.Email.Contains(text) || p.Seller.Phone.Contains(text)))
                                  || (p.AssignedRealtor != null && (p.AssignedRealtor.FullName.Contains(text) || p.AssignedRealtor.FullName.Contains(textCap) || p.AssignedRealtor.FullName.Contains(textLower) || p.AssignedRealtor.Email.Contains(text))));
        }

        if (filter.From.HasValue)
        {
            var fromDt = filter.From.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.CreatedAt >= fromDt);
        }

        if (filter.To.HasValue)
        {
            var toDt = filter.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.CreatedAt < toDt);
        }

        var desc = filter.Descending;
        query = filter.Sort switch
        {
            PlotSort.Price => desc ? query.OrderByDescending(p => (double)p.PriceUsd).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => (double)p.PriceUsd).ThenBy(p => p.CreatedAt),
            PlotSort.PricePerSotka => desc ? query.OrderByDescending(p => (double)p.PriceUsd / (p.AreaHectares * 100.0)).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => (double)p.PriceUsd / (p.AreaHectares * 100.0)).ThenBy(p => p.CreatedAt),
            PlotSort.Area => desc ? query.OrderByDescending(p => p.AreaHectares).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.AreaHectares).ThenBy(p => p.CreatedAt),
            PlotSort.Title => desc ? query.OrderByDescending(p => p.Title).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.Title).ThenBy(p => p.CreatedAt),
            PlotSort.Location => desc ? query.OrderByDescending(p => p.Location).ThenByDescending(p => p.CreatedAt) : query.OrderBy(p => p.Location).ThenBy(p => p.CreatedAt),
            _ => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };

        var projected = query
            .Take(MaxRows)
            .Select(p => new PlotRow(
                p.Id,
                p.CadastralNumber,
                p.Title,
                p.Location,
                p.AreaHectares,
                Math.Round(p.AreaHectares * 100.0, 2),
                p.Purpose,
                p.PriceUsd,
                p.AreaHectares > 0 ? Math.Round(p.PriceUsd / (decimal)(p.AreaHectares * 100.0), 0) : 0m,
                p.Status,
                p.Seller != null ? p.Seller.FullName : "Не призначено",
                p.Seller != null ? p.Seller.Phone : "",
                p.AssignedRealtor != null ? p.AssignedRealtor.FullName : "Не призначено",
                p.Documents.Count,
                p.LegalConclusion != null ? p.LegalConclusion.RiskLevel : "Не перевірено",
                p.CreatedAt));

        string sql = projected.ToQueryString();
        var rows = await projected.ToListAsync();

        if (filter.Sort == PlotSort.Price)
        {
            rows = desc
                ? rows.OrderByDescending(r => r.PriceUsd).ThenByDescending(r => r.CreatedAt).ToList()
                : rows.OrderBy(r => r.PriceUsd).ThenBy(r => r.CreatedAt).ToList();
        }
        else if (filter.Sort == PlotSort.PricePerSotka)
        {
            rows = desc
                ? rows.OrderByDescending(r => r.PricePerSotkaUsd).ThenByDescending(r => r.CreatedAt).ToList()
                : rows.OrderBy(r => r.PricePerSotkaUsd).ThenBy(r => r.CreatedAt).ToList();
        }

        return new QueryResult<PlotRow>(rows, sql);
    }

    public async Task<QueryResult<PurposeTurnoverRow>> TurnoverByPurposeAsync(
        Period period, TurnoverSort sort = TurnoverSort.Turnover, bool descending = true, int? realtorId = null)
    {
        var baseQuery = _db.SaleDeals.AsNoTracking()
            .Where(d => d.NotaryDate >= period.From && d.NotaryDate < period.To)
            .Where(d => d.Status == DealStatus.SignedAtNotary || d.Status == DealStatus.CompletedAtNotary);

        if (realtorId.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.RealtorId == realtorId.Value);
        }

        var query = from d in baseQuery
                    select new
                    {
                        d.LandPlotId,
                        Purpose = d.LandPlot != null ? d.LandPlot.Purpose : "Інше призначення",
                        Area = d.LandPlot != null ? d.LandPlot.AreaHectares : 0.0,
                        Price = d.FinalPriceUsd,
                        Commission = d.AgencyCommissionUsd
                    };

        string sql = query.ToQueryString();
        var rawDeals = await query.ToListAsync();

        var grouped = from d in rawDeals
                      group d by d.Purpose into g
                      let count = g.Count()
                      let totalArea = g.Sum(x => x.Area)
                      let totalTurnover = g.Sum(x => x.Price)
                      let totalCommission = g.Sum(x => x.Commission)
                      let avgPrice = count > 0 ? totalTurnover / count : 0m
                      let minPrice = g.Min(x => x.Price)
                      let maxPrice = g.Max(x => x.Price)
                      select new PurposeTurnoverRow(
                          g.Key,
                          count,
                          Math.Round(totalArea, 2),
                          totalTurnover,
                          totalCommission,
                          Math.Round(avgPrice, 2),
                          minPrice,
                          maxPrice);

        var rows = sort switch
        {
            TurnoverSort.Deals => descending ? grouped.OrderByDescending(r => r.DealsCount) : grouped.OrderBy(r => r.DealsCount),
            TurnoverSort.Area => descending ? grouped.OrderByDescending(r => r.TotalAreaHectares) : grouped.OrderBy(r => r.TotalAreaHectares),
            TurnoverSort.Name => descending ? grouped.OrderByDescending(r => r.Purpose) : grouped.OrderBy(r => r.Purpose),
            _ => descending ? grouped.OrderByDescending(r => r.TotalTurnoverUsd) : grouped.OrderBy(r => r.TotalTurnoverUsd)
        };

        return new QueryResult<PurposeTurnoverRow>(rows.ToList(), sql);
    }

    public async Task<QueryResult<RealtorClosedDealRow>> RealtorClosedDealsAsync(
        Period period, int? realtorId = null, bool descending = true)
    {
        var baseQuery = _db.SaleDeals.AsNoTracking()
            .Where(d => d.NotaryDate >= period.From && d.NotaryDate < period.To)
            .Where(d => d.Status == DealStatus.SignedAtNotary || d.Status == DealStatus.CompletedAtNotary);

        if (realtorId.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.RealtorId == realtorId.Value);
        }

        baseQuery = descending
            ? baseQuery.OrderByDescending(d => d.NotaryDate)
            : baseQuery.OrderBy(d => d.NotaryDate);

        var projected = baseQuery.Select(d => new RealtorClosedDealRow(
            d.Id,
            d.DraftContractNumber,
            d.NotarialRegistryNumber ?? "—",
            d.NotaryDate,
            d.LandPlot != null ? d.LandPlot.CadastralNumber : "—",
            d.LandPlot != null ? d.LandPlot.Title : "Земельна ділянка",
            d.LandPlot != null ? d.LandPlot.Purpose : "Інше призначення",
            d.LandPlot != null ? d.LandPlot.AreaHectares : 0.0,
            d.Buyer != null ? d.Buyer.FullName : "Покупець",
            d.FinalPriceUsd,
            d.AgencyCommissionUsd));

        string sql = projected.ToQueryString();
        var rows = await projected.ToListAsync();

        return new QueryResult<RealtorClosedDealRow>(rows, sql);
    }

    public async Task<QueryResult<AgentPerformanceRow>> AgentPerformanceAsync(
        Period period, AgentSort sort = AgentSort.Turnover, bool descending = true)
    {
        var query = _db.SaleDeals.AsNoTracking()
            .Where(d => d.NotaryDate >= period.From && d.NotaryDate < period.To)
            .Where(d => d.Status == DealStatus.SignedAtNotary || d.Status == DealStatus.CompletedAtNotary)
            .Select(d => new
            {
                RealtorName = d.Realtor != null ? d.Realtor.FullName : "Рієлтор не закріплений",
                RealtorPhone = d.Realtor != null ? d.Realtor.Phone : "",
                LawyerName = d.Lawyer != null ? d.Lawyer.FullName : "Юрист не закріплений",
                LawyerPhone = d.Lawyer != null ? d.Lawyer.Phone : "",
                Price = d.FinalPriceUsd,
                Commission = d.AgencyCommissionUsd,
                Area = d.LandPlot != null ? d.LandPlot.AreaHectares : 0.0
            });

        string sql = query.ToQueryString();
        var rawDeals = await query.ToListAsync();

        var realtorRows = rawDeals
            .GroupBy(d => (d.RealtorName, d.RealtorPhone))
            .Select(g => new AgentPerformanceRow(
                g.Key.RealtorName,
                "Рієлтор (Координатор)",
                g.Key.RealtorPhone,
                g.Count(),
                Math.Round(g.Sum(x => x.Area), 2),
                g.Sum(x => x.Price),
                g.Sum(x => x.Commission)));

        var lawyerRows = rawDeals
            .GroupBy(d => (d.LawyerName, d.LawyerPhone))
            .Select(g => new AgentPerformanceRow(
                g.Key.LawyerName,
                "Юрист (Правова експертиза)",
                g.Key.LawyerPhone,
                g.Count(),
                Math.Round(g.Sum(x => x.Area), 2),
                g.Sum(x => x.Price),
                g.Sum(x => x.Commission)));

        var allRows = realtorRows.Concat(lawyerRows);

        var sorted = sort switch
        {
            AgentSort.Deals => descending ? allRows.OrderByDescending(r => r.DealsCount) : allRows.OrderBy(r => r.DealsCount),
            AgentSort.Commission => descending ? allRows.OrderByDescending(r => r.TotalCommissionUsd) : allRows.OrderBy(r => r.TotalCommissionUsd),
            AgentSort.Name => descending ? allRows.OrderByDescending(r => r.AgentName) : allRows.OrderBy(r => r.AgentName),
            _ => descending ? allRows.OrderByDescending(r => r.TotalTurnoverUsd) : allRows.OrderBy(r => r.TotalTurnoverUsd)
        };

        return new QueryResult<AgentPerformanceRow>(sorted.ToList(), sql);
    }

    public async Task<QueryResult<LandBankRow>> LandBankAuditAsync(LandBankFilter filter)
    {
        var query = from p in _db.LandPlots.AsNoTracking()
                    join u in _db.Users.AsNoTracking() on p.SellerId equals u.Id
                    join c in _db.LegalConclusions.AsNoTracking() on p.Id equals c.LandPlotId into conclusions
                    from c in conclusions.DefaultIfEmpty()
                    select new
                    {
                        Plot = p,
                        Seller = u,
                        Conclusion = c,
                        DocsCount = p.Documents.Count()
                    };

        if (!string.IsNullOrWhiteSpace(filter.Region))
        {
            var region = filter.Region.Trim();
            var regionCap = char.ToUpper(region[0]) + (region.Length > 1 ? region.Substring(1) : "");
            var regionLower = char.ToLower(region[0]) + (region.Length > 1 ? region.Substring(1) : "");
            query = query.Where(x => x.Plot.Location.Contains(region) || x.Plot.Location.Contains(regionCap) || x.Plot.Location.Contains(regionLower));
        }

        if (!string.IsNullOrWhiteSpace(filter.Purpose))
        {
            var purpose = filter.Purpose.Trim();
            query = query.Where(x => x.Plot.Purpose.Contains(purpose));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Plot.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.RiskLevel))
        {
            var risk = filter.RiskLevel.Trim();
            query = query.Where(x => x.Conclusion != null && x.Conclusion.RiskLevel.Contains(risk));
        }

        if (filter.OnlyVerifiedDocs)
        {
            query = query.Where(x => x.Plot.Documents.Any(d => d.IsVerified));
        }

        query = query.OrderByDescending(x => x.Plot.CreatedAt);

        var projected = query.Select(x => new LandBankRow(
            x.Plot.Id,
            x.Plot.CadastralNumber,
            x.Plot.Title,
            x.Plot.Location,
            x.Plot.Purpose,
            x.Plot.AreaHectares,
            x.Plot.PriceUsd,
            x.Seller.FullName,
            x.Seller.Phone,
            x.Seller.BankDetails,
            x.DocsCount,
            x.Conclusion != null ? x.Conclusion.RiskLevel : "Не перевірено",
            x.Conclusion != null && x.Conclusion.IsApproved,
            x.Plot.Status,
            x.Plot.CreatedAt));

        string sql = projected.ToQueryString();
        var rows = await projected.ToListAsync();

        return new QueryResult<LandBankRow>(rows, sql);
    }

    public async Task<AppUser?> FindUserAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
    }

    public bool CheckPassword(AppUser user, string password)
    {
        return user.Authenticate(password);
    }

    public async Task<AppUser> AddUserAsync(
        string fullName, string email, string phone, UserRole role, string password, string? bankDetails)
    {
        var normalized = NormalizeEmail(email);
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == normalized))
        {
            throw new InvalidOperationException($"Користувач з електронною поштою '{email}' вже зареєстрований.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var user = new AppUser
        {
            FullName = fullName.Trim(),
            Email = normalized,
            Phone = (phone ?? string.Empty).Trim(),
            Role = role,
            BankDetails = (bankDetails ?? string.Empty).Trim(),
            IsActive = true
        };
        user.SetPassword(password);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        if (role == UserRole.Client)
        {
            var defaultRequest = new SelectionRequest
            {
                BuyerId = user.Id,
                DesiredDistrict = "Київська область",
                MinAreaHa = 0.10,
                MaxBudgetUsd = 40000m,
                Purpose = "02.01 Для будівництва житлового будинку",
                IsProcessedByRealtor = false,
                MatchedPlotsCount = 0,
                RealtorNotes = "Первинна реєстрація клієнта через розділ адміністрування",
                CreatedAt = DateTime.Now
            };
            _db.SelectionRequests.Add(defaultRequest);
            await _db.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return user;
    }

    public async Task UpdateUserAsync(
        int id,
        string fullName,
        string email,
        string phone,
        string? taxId,
        string? bankDetails,
        double maxBudget,
        string? targetPurpose,
        bool isActive)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new InvalidOperationException($"Користувача з ID={id} не знайдено.");

        if (user.Email.ToLower().Contains("admin") && !isActive)
        {
            throw new InvalidOperationException("Обліковий запис головного адміністратора не можна заблокувати.");
        }

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new InvalidOperationException("Електронна пошта не може бути порожньою.");
        }

        if (user.Email.ToLower() != normalizedEmail &&
            await _db.Users.AnyAsync(u => u.Id != id && u.Email.ToLower() == normalizedEmail))
        {
            throw new InvalidOperationException($"Користувач з поштою '{email}' вже існує.");
        }

        user.FullName = fullName.Trim();
        user.Email = normalizedEmail;
        user.Phone = (phone ?? string.Empty).Trim();
        user.TaxId = (taxId ?? string.Empty).Trim();
        user.BankDetails = (bankDetails ?? string.Empty).Trim();
        user.MaxBudget = maxBudget < 0 ? 0 : maxBudget;
        user.TargetPurpose = string.IsNullOrWhiteSpace(targetPurpose) ? null : targetPurpose.Trim();
        user.IsActive = isActive;

        await _db.SaveChangesAsync();
    }

    public async Task<string> DeleteUserAsync(int id)
    {
        var user = await _db.Users
            .Include(u => u.OwnedPlots)
            .Include(u => u.ViewingRequests)
            .FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new InvalidOperationException($"Користувача з ID={id} не знайдено.");

        if (user.Email.ToLower().Contains("admin"))
        {
            throw new InvalidOperationException("Обліковий запис головного адміністратора не підлягає видаленню.");
        }

        if (user.OwnedPlots.Any())
        {
            throw new InvalidOperationException(
                $"Неможливо вилучити {user.FullName}: користувач є власником {user.OwnedPlots.Count} ділянок. Згідно з правилом цілісності (Restrict), фінансова історія зберігається. Обліковий запис можна лише заблокувати.");
        }

        bool hasDeals = await _db.SaleDeals.AnyAsync(d => d.BuyerId == id || d.RealtorId == id || d.LawyerId == id);
        if (hasDeals)
        {
            throw new InvalidOperationException(
                $"Неможливо вилучити {user.FullName}: користувач бере участь в укладених нотаріальних угодах. Згідно з правилом цілісності (Restrict), видалення заблоковано.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var relatedSelections = await _db.SelectionRequests.Where(s => s.BuyerId == id).ToListAsync();
        if (relatedSelections.Any())
        {
            _db.SelectionRequests.RemoveRange(relatedSelections);
        }

        if (user.ViewingRequests.Any())
        {
            _db.ViewingRequests.RemoveRange(user.ViewingRequests);
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return user.FullName;
    }

    public async Task<LinqShowcaseResult> GetLinqShowcaseAsync()
    {
        var allPlots = await _db.LandPlots
            .Include(p => p.Documents)
            .Include(p => p.LegalConclusion)
            .Include(p => p.Seller)
            .Include(p => p.AssignedRealtor)
            .AsNoTracking()
            .ToListAsync();

        int totalPlotsWithAllDocsVerified = allPlots
            .Count(p => p.Documents.Any() && p.Documents.All(d => d.IsVerified));

        var allUsers = await _db.Users.AsNoTracking().ToListAsync();
        var allViewings = await _db.ViewingRequests.AsNoTracking().ToListAsync();

        int totalBuyersWithAnyViewing = allUsers
            .Count(u => u.Role == UserRole.Client && allViewings.Any(v => v.BuyerId == u.Id));

        var allDistinctPurposes = allPlots
            .Select(p => p.Purpose)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        var sellerDistricts = allPlots
            .SelectMany(p => p.Location.Split(',', StringSplitOptions.TrimEntries))
            .Where(part => part.Contains("район") || part.StartsWith("м."))
            .Distinct()
            .ToList();

        var allRequests = await _db.SelectionRequests.AsNoTracking().ToListAsync();
        var buyerRequestedDistricts = allRequests
            .Select(s => s.DesiredDistrict.Trim())
            .Distinct()
            .ToList();

        var marketIntersectRegions = sellerDistricts.Intersect(buyerRequestedDistricts).OrderBy(r => r).ToList();
        var allMarketRegionsUnion = sellerDistricts.Union(buyerRequestedDistricts).OrderBy(r => r).ToList();

        var allClients = allUsers
            .Where(u => u.Role == UserRole.Client)
            .Select(u => u.FullName)
            .ToList();

        var allDeals = await _db.SaleDeals.Include(d => d.Buyer).AsNoTracking().ToListAsync();
        var dealBuyers = allDeals
            .Where(d => d.Buyer != null)
            .Select(d => d.Buyer!.FullName)
            .Distinct()
            .ToList();

        var buyersWithoutDeals = allClients.Except(dealBuyers).OrderBy(n => n).ToList();

        var flattenedDocTitles = allPlots
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
            .SelectMany(p => p.Documents.Select(d => $"{p.CadastralNumber}: {d.Title} ({d.DocType})"))
            .Take(10)
            .ToList();

        var aggregatedCadastralRoll = allPlots
            .Where(p => p.Status == PlotStatus.VerifiedInCatalog)
            .Select(p => p.CadastralNumber)
            .Take(6)
            .Aggregate("Кадастровий реєстр сертифікованих ділянок: ", (acc, cad) => $"{acc} [{cad}]");

        var samplePlots = allPlots.Take(5).ToList();
        var benchmarkPrices = new[] { 27000m, 35000m, 40000m, 16000m, 62000m };
        var zippedBenchmarks = samplePlots
            .Zip(benchmarkPrices, (plot, bench) =>
            {
                var diff = plot.PriceUsd - bench;
                var pct = bench > 0 ? Math.Round(diff / bench * 100m, 1) : 0m;
                var verdict = pct switch
                {
                    > 10m => "Вище ринку (Завищена)",
                    < -10m => "Нижче ринку (Дисконт)",
                    _ => "Ринкова справедлива ціна"
                };
                return new BenchmarkComparisonRow(
                    plot.CadastralNumber,
                    plot.Location,
                    plot.PriceUsd,
                    bench,
                    pct,
                    verdict);
            })
            .ToList();

        var multiSortedPlots = allPlots
            .OrderBy(p => p.Location)
            .ThenByDescending(p => p.PriceUsd)
            .Take(10)
            .Select(p => new PlotRow(
                p.Id,
                p.CadastralNumber,
                p.Title,
                p.Location,
                p.AreaHectares,
                Math.Round(p.AreaHectares * 100.0, 2),
                p.Purpose,
                p.PriceUsd,
                p.AreaHectares > 0 ? Math.Round(p.PriceUsd / (decimal)(p.AreaHectares * 100.0), 0) : 0m,
                p.Status,
                p.Seller?.FullName ?? "Власник",
                p.Seller?.Phone ?? "",
                p.AssignedRealtor?.FullName ?? "Рієлтор",
                p.Documents?.Count ?? 0,
                p.LegalConclusion?.RiskLevel ?? "Не перевірено",
                p.CreatedAt))
            .ToList();

        var paginatedPlots = allPlots
            .OrderBy(p => p.Id)
            .Skip(2)
            .Take(4)
            .Select(p => new PlotRow(
                p.Id,
                p.CadastralNumber,
                p.Title,
                p.Location,
                p.AreaHectares,
                Math.Round(p.AreaHectares * 100.0, 2),
                p.Purpose,
                p.PriceUsd,
                p.AreaHectares > 0 ? Math.Round(p.PriceUsd / (decimal)(p.AreaHectares * 100.0), 0) : 0m,
                p.Status,
                p.Seller?.FullName ?? "Власник",
                p.Seller?.Phone ?? "",
                p.AssignedRealtor?.FullName ?? "Рієлтор",
                p.Documents?.Count ?? 0,
                p.LegalConclusion?.RiskLevel ?? "Не перевірено",
                p.CreatedAt))
            .ToList();

        var plinqHighValuePlots = allPlots
            .AsParallel()
            .Where(p => p.PriceUsd >= 40000m)
            .OrderByDescending(p => p.PriceUsd)
            .Select(p => $"{p.CadastralNumber} — {p.Title} (${p.PriceUsd:N0})")
            .ToList();

        return new LinqShowcaseResult(
            totalPlotsWithAllDocsVerified,
            totalBuyersWithAnyViewing,
            allDistinctPurposes,
            marketIntersectRegions,
            allMarketRegionsUnion,
            buyersWithoutDeals,
            flattenedDocTitles,
            aggregatedCadastralRoll,
            zippedBenchmarks,
            multiSortedPlots,
            paginatedPlots,
            plinqHighValuePlots);
    }

    private static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
