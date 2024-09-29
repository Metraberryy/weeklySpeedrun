using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WeeklyIL.Database;

public class UserEntity
{
    public ulong Id { get; set; }
    public uint WeeklyWins { get; set; }
}

public class WeekEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public ulong Id { get; set; }
    public ulong GuildId { get; set; }
    public uint StartTimestamp { get; set; }
    public string Level { get; set; }
}

public class ScoreEntity
{
    public ulong Id { get; set; }
    public ulong UserId { get; set; }
    public ulong WeekId { get; set; }
    public uint? TimeMs { get; set; }
    public string? Video { get; set; }
    public bool Verified { get; set; }
}