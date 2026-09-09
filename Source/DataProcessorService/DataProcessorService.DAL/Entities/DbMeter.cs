using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataProcessorService.DAL.Entities;

[Table("Meters")]
[Index(nameof(Location), nameof(MeterType), IsUnique = true)]
public sealed class DbMeter
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(64)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(32)]
    public string MeterType { get; set; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
