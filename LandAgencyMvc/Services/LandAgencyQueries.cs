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

    public async Task<List<TableStatsRow>> GetTableStatsAsync()
    {
        return new List<TableStatsRow>
        {
            new("Users", "AppUser", await _db.Users.CountAsync(), "Користувачі системи (клієнти-продавці, покупці, рієлтори, юристи, адміністратор)"),
            new("LandPlots", "LandPlot", await _db.LandPlots.CountAsync(), "Земельні ділянки з кадастровими номерами, площами, цінами та статусами лістингу"),
            new("PlotDocuments", "PlotDocument", await _db.PlotDocuments.CountAsync(), "Правовстановлюючі документи (витяги ДРРП, державні акти) з SHA-256 хешами"),
            new("LegalConclusions", "LegalConclusion", await _db.LegalConclusions.CountAsync(), "Юридичні висновки Due Diligence перевірки у реєстрах ДЗК та ДРРП"),
            new("ViewingRequests", "ViewingRequest", await _db.ViewingRequests.CountAsync(), "Заявки на виїзний огляд межових знаків земельних ділянок на місцевості"),
            new("SelectionRequests", "SelectionRequest", await _db.SelectionRequests.CountAsync(), "Заявки покупців на автоматичний підбір ділянок за параметрами пошуку"),
            new("SaleDeals", "SaleDeal", await _db.SaleDeals.CountAsync(), "Нотаріальні договори купівлі-продажу ділянок з розрахунком 3% комісії"),
            new("Payments", "Payment", await _db.Payments.CountAsync(), "Безготівкові платіжні інструкції та квитанції IBAN на оплату вартості та депозитів")
        };
    }

    public async Task<QueryResult<PlotRow>> FilterPlotsAsync(PlotFilter filter)
    {
        var query = _db.LandPlots.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Region))
        {
            var region = filter.Region.Trim();
            query = query.Where(p => p.Location.Contains(region));
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
            query = query.Where(p => (p.Seller != null && (p.Seller.FullName.Contains(text) || p.Seller.Email.Contains(text) || p.Seller.Phone.Contains(text)))
                                  || (p.AssignedRealtor != null && (p.AssignedRealtor.FullName.Contains(text) || p.AssignedRealtor.Email.Contains(text))));
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
            PlotSort.Price => desc ? query.OrderByDescending(p => p.PriceUsd) : query.OrderBy(p => p.PriceUsd),
            PlotSort.Area => desc ? query.OrderByDescending(p => p.AreaHectares) : query.OrderBy(p => p.AreaHectares),
            PlotSort.Title => desc ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
            PlotSort.Location => desc ? query.OrderByDescending(p => p.Location) : query.OrderBy(p => p.Location),
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

        if (filter.Sort == PlotSort.PricePerSotka)
        {
            rows = desc
                ? rows.OrderByDescending(r => r.PricePerSotkaUsd).ToList()
                : rows.OrderBy(r => r.PricePerSotkaUsd).ToList();
        }

        return new QueryResult<PlotRow>(rows, sql);
    }

    public async Task<QueryResult<PurposeTurnoverRow>> TurnoverByPurposeAsync(
        Period period, TurnoverSort sort = TurnoverSort.Turnover, bool descending = true)
    {
        var query = from d in _db.SaleDeals.AsNoTracking()
                    where d.NotaryDate >= period.From && d.NotaryDate < period.To
                    where d.Status == DealStatus.SignedAtNotary || d.Status == DealStatus.CompletedAtNotary
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
            query = query.Where(x => x.Plot.Location.Contains(region));
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
            query = query.Where(x => x.DocsCount > 0);
        }

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
            x.Plot.Status));

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

    public async Task UpdateUserAsync(int id, string fullName, string phone, bool isActive, string? newPassword)
    {
        var user = await _db.Users.FindAsync(id)
            ?? throw new InvalidOperationException($"Користувача з ID={id} не знайдено.");

        if (user.Email.ToLower().Contains("admin") && !isActive)
        {
            throw new InvalidOperationException("Обліковий запис головного адміністратора не можна заблокувати.");
        }

        user.FullName = fullName.Trim();
        user.Phone = (phone ?? string.Empty).Trim();
        user.IsActive = isActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            user.SetPassword(newPassword.Trim());
        }

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

    private static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
