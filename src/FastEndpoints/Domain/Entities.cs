public class Trainer
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string firstname { get; set; } = null!;
    public string lastname { get; set; } = null!;
}

public class Training
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Guid TrainerId { get; set; }
    public List<Rate> Rates { get; set; } = new List<Rate>();
}

public class Rate
{
    public Guid Id { get; set; }
    public Guid TrainingId { get; set; }
    public int Value { get; init; }
    public string Comment { get; init; } = string.Empty;
    public Guid AuthorId { get; init; }
}
