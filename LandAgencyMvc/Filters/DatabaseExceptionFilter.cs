using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace LandAgencyMvc.Filters;

/// <summary>
/// Перетворює помилки збереження даних на зрозуміле повідомлення. Транзакція вже скасована, тому користувач
/// повертається на попередню сторінку: конфлікт паралельного редагування або порушення правил цілісності
/// (унікальність, зовнішній ключ, CHECK) не закінчуються сторінкою помилки.
/// </summary>
public sealed class DatabaseExceptionFilter : IExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly ILogger<DatabaseExceptionFilter> _logger;

    public DatabaseExceptionFilter(ITempDataDictionaryFactory tempDataFactory, ILogger<DatabaseExceptionFilter> logger)
    {
        _tempDataFactory = tempDataFactory;
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        string? message = context.Exception switch
        {
            DbUpdateConcurrencyException =>
                "Ці дані щойно змінив інший користувач. Сторінку оновлено, перегляньте актуальні значення й повторіть дію.",
            DbUpdateException =>
                "Операцію не виконано: вона порушує правила цілісності бази даних (дублікат, пов'язані записи або недопустиме значення). Усі зміни скасовано.",
            _ => null
        };

        if (message == null)
        {
            return;
        }

        _logger.LogWarning(context.Exception, "Збереження скасовано: {Path}", context.HttpContext.Request.Path);

        _tempDataFactory.GetTempData(context.HttpContext)["ErrorMessage"] = message;
        context.Result = new LocalRedirectResult(GetReturnPath(context.HttpContext.Request));
        context.ExceptionHandled = true;
    }

    private static string GetReturnPath(HttpRequest request)
    {
        var referer = request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return uri.PathAndQuery;
        }

        return "/";
    }
}
