using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Shared.Entities;

[Table("Readings")]
[Index(
    nameof(MeterId),
    nameof(MetricCode),
    nameof(ObservedAt),
    IsDescending = new[] { false, false, true })]
public sealed class DbReading
{
    [Key]
    public long Id { get; set; }

    public Guid MeterId { get; set; }

    [ForeignKey(nameof(MeterId))]
    public DbMeter Meter { get; set; } = null!;

    [MaxLength(32)]
    public string MetricCode { get; set; } = string.Empty;

    public DateTimeOffset ObservedAt { get; set; }

    [Precision(12, 4)]
    public decimal? ValueNumeric { get; set; }

    public bool? ValueBool { get; set; }
}
