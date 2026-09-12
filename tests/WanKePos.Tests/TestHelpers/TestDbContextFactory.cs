using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WanKePos.Infrastructure.Data;

namespace WanKePos.Tests.TestHelpers;

public static class TestDbContextFactory
{
    public static (PosDbContext Context, SqliteConnection Connection) CreateInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new PosDbContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}
