using IssueBridge.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Tests.TestHelpers;

// Wraps a single open SQLite ":memory:" connection so each test gets a real
// SQLite database (transactions, constraints, etc. all behave like production)
// that disappears the moment the connection closes.
public sealed class SqliteInMemoryContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteInMemoryContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public IssueBridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IssueBridgeDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new IssueBridgeDbContext(options);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
