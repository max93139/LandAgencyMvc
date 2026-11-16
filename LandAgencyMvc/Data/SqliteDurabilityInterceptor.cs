using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LandAgencyMvc.Data;

/// <summary>
/// Вмикає synchronous=FULL на кожному відкритому з'єднанні: SQLite скидає журнал на диск перед підтвердженням
/// транзакції, тому підтверджені зміни (Durability) переживають навіть аварійне вимкнення живлення.
/// За замовчуванням у режимі WAL діє NORMAL, який такої гарантії не дає.
/// </summary>
public sealed class SqliteDurabilityInterceptor : DbConnectionInterceptor
{
    private const string FullSynchronous = "PRAGMA synchronous = FULL;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = FullSynchronous;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Apply(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = FullSynchronous;
        command.ExecuteNonQuery();
    }
}
