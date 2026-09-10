using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Entities;

[Table("ProcessedMessages")]
public sealed class DbProcessedMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid MessageId { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
