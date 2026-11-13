using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yoink.Domain.Entities;
using Yoink.Infrastructure.Persistence;

namespace Yoink.Tests;

public class HistoryLibraryRemovalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removing_history_removes_matching_library_records_but_keeps_files(bool clearAll)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<YoinkDbContext>().UseSqlite(connection).Options;
        var factory = new Factory(options);
        var file = Path.GetTempFileName();
        try
        {
            int historyId;
            await using (var db = new YoinkDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                var history = new DownloadHistoryEntry { SourceUrl = "https://example.com/video/1", OutputPath = file };
                db.History.Add(history);
                db.Library.AddRange(
                    new LibraryEntry { SourceUrl = history.SourceUrl, FilePath = "another-quality.mp4" },
                    new LibraryEntry { SourceUrl = "", FilePath = file },
                    new LibraryEntry { SourceUrl = "https://example.com/unrelated", FilePath = "unrelated.mp4" },
                    new LibraryEntry { SourceUrl = "", FilePath = "no-source.mp4" });
                await db.SaveChangesAsync();
                historyId = history.Id;
            }
            var repository = new HistoryRepository(factory);
            if (clearAll) await repository.ClearAsync();
            else await repository.DeleteAsync(historyId);
            await using var result = new YoinkDbContext(options);
            Assert.Empty(await result.History.ToListAsync());
            Assert.Equal(new[] { "no-source.mp4", "unrelated.mp4" },
                await result.Library.OrderBy(item => item.FilePath).Select(item => item.FilePath).ToArrayAsync());
            Assert.True(File.Exists(file));
        }
        finally { File.Delete(file); }
    }

    private sealed class Factory(DbContextOptions<YoinkDbContext> options) : IDbContextFactory<YoinkDbContext>
    {
        public YoinkDbContext CreateDbContext() => new(options);
    }
}
