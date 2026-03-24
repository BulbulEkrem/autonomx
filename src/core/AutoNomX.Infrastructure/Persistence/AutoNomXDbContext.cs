using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoNomX.Infrastructure.Persistence;

public class AutoNomXDbContext(DbContextOptions<AutoNomXDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<AgentDefinition> Agents => Set<AgentDefinition>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<CoderWorker> CoderWorkers => Set<CoderWorker>();
    public DbSet<AgentHistory> AgentHistories => Set<AgentHistory>();
    public DbSet<AgentMetrics> AgentMetrics => Set<AgentMetrics>();
    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();
    public DbSet<ProjectFile> ProjectFiles => Set<ProjectFile>();
    public DbSet<ChangeLog> ChangeLogs => Set<ChangeLog>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutoNomXDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
