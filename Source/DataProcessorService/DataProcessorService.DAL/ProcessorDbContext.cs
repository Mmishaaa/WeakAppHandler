using Microsoft.EntityFrameworkCore;
using Shared.Entities;

namespace DataProcessorService.DAL;

public sealed class ProcessorDbContext(DbContextOptions<ProcessorDbContext> options) : DbContext(options)
{
    public DbSet<DbMeter> Meters { get; set; } = null!;

    public DbSet<DbReading> Readings { get; set; } = null!;

    public DbSet<DbProcessedMessage> ProcessedMessages { get; set; } = null!;
}
