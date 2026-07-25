using IssueBridge.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.Data;

public class IssueBridgeDbContext : DbContext
{
    public IssueBridgeDbContext(DbContextOptions<IssueBridgeDbContext> options) : base(options)
    {
    }

    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<LocalTaskInfo> LocalTaskInfos => Set<LocalTaskInfo>();
    public DbSet<AssistantQueryLog> AssistantQueryLogs => Set<AssistantQueryLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Issue>(entity =>
        {
            entity.HasIndex(i => i.GitHubIssueId).IsUnique();

            entity.HasOne(i => i.LocalTaskInfo)
                .WithOne(l => l.Issue)
                .HasForeignKey<LocalTaskInfo>(l => l.IssueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocalTaskInfo>(entity =>
        {
            entity.HasKey(l => l.IssueId);
        });
    }
}
