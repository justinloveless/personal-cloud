namespace Api.Infrastructure.Models;

public class Note
{
    public int Id { get; set; }
    public string Text { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
