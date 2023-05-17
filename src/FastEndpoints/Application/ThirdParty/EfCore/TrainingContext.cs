using Microsoft.EntityFrameworkCore;

namespace BLRefactoring.FastEndpoints.Application.ThirdParty.EfCore;

public class TrainingContext : DbContext
{
    public TrainingContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Trainer> Trainers { get; set; } = default!;
    public DbSet<Training> Trainings { get; set; } = default!;
}
