using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace TrainingHub.Shared.Infrastructure.Development;

/// <summary>
/// Turns the written corpus into a placed one: who owns what, when it appeared, and which of it
/// is not on offer.
/// </summary>
/// <remarks>
/// Pure and total. It reads <see cref="DevelopmentCatalog"/>, it takes a count and an instant, and
/// it answers a list — no database, no clock, no configuration, no Identity. That is what lets the
/// whole thing be checked in a unit test: every title through <c>TrainingTitle.Create</c>, every
/// address through <c>Email.Create</c>, the sizes, the uniqueness and the determinism, all without
/// a container anywhere (ADR 0079).
/// <para>
/// <b>Deterministic by construction.</b> Everything derives from one <see cref="Random"/> built on
/// <see cref="Seed"/> and from the anchor the caller passes, so two runs with the same arguments
/// produce the same people, the same trainings and the same states. The anchor is a parameter
/// rather than a call to a clock for exactly that reason — and because the data should look recent
/// whenever it is seeded, rather than frozen at whatever date this file was written.
/// </para>
/// </remarks>
public static class DevelopmentDataset
{
    /// <summary>
    /// The seed every draw derives from.
    /// </summary>
    /// <remarks>
    /// A date, so that it is obviously arbitrary rather than obviously meaningful. Changing it
    /// changes the whole dataset, which is a thing to do deliberately and never as a side effect.
    /// </remarks>
    public const int Seed = 20260814;

    /// <summary>
    /// The most trainings one seeded trainer owns.
    /// </summary>
    /// <remarks>
    /// Five, which is comfortably under the domain's own limit of ten per trainer. The distance is
    /// deliberate: a seeded catalog sitting on the quota would refuse the first training anybody
    /// created by hand, and the refusal would look like a defect rather than the rule working. An
    /// architecture rule holds this number below <c>Training.MaximumPerTrainer</c>.
    /// </remarks>
    public const int MaximumTrainingsPerTrainer = 5;

    /// <summary>
    /// The fewest trainings one seeded trainer owns.
    /// </summary>
    /// <remarks>
    /// One, never zero: a trainer with an empty catalog is a legitimate state the product allows,
    /// but seeding several of them would make every "no results" screen ambiguous — a reader could
    /// not tell an empty profile from a broken query.
    /// </remarks>
    public const int MinimumTrainingsPerTrainer = 1;

    /// <summary>
    /// How far back the oldest training is dated.
    /// </summary>
    /// <remarks>
    /// Eighteen months, spread evenly with a little noise. Without it the five hundred trainings
    /// would share one second and the catalog's newest-first order would be arbitrary — the order
    /// would still be total, because the tiebreaker is the identifier (ADR 0071), and it would
    /// prove nothing about the sort a visitor came for.
    /// </remarks>
    public static readonly TimeSpan Window = TimeSpan.FromDays(548);

    /// <summary>
    /// How often a training is taken off the catalog by its owner.
    /// </summary>
    private const double UnpublishedShare = 0.08;

    /// <summary>
    /// How often a training is kept back by the administration.
    /// </summary>
    private const double WithheldShare = 0.04;

    /// <summary>
    /// How often a trainer is under sanction.
    /// </summary>
    private const double SuspendedShare = 0.05;

    /// <summary>
    /// How often a trainer publishes no portrait.
    /// </summary>
    /// <remarks>
    /// One in five, so that the initials a profile falls back on (ADR 0074) are visible on a page
    /// of results rather than only in a unit test.
    /// </remarks>
    private const double NoPortraitShare = 0.2;

    /// <summary>
    /// How likely each catalog size is, from one training to five.
    /// </summary>
    /// <remarks>
    /// Weighted toward the middle because a catalog where every trainer owns exactly three is as
    /// unrealistic as one where they all own one. The weights are relative and need not sum to
    /// anything.
    /// </remarks>
    private static readonly int[] SizeWeights = [2, 5, 6, 4, 3];

    /// <summary>
    /// Why the administration withheld a training, in its own words.
    /// </summary>
    private static readonly string[] WithholdingReasons =
    [
        "The description promises a certification this platform does not award. Reword it and ask for a review.",
        "Reported by three participants as materially different from what was delivered.",
        "The prerequisites contradict the level the title claims, and participants arrived unable to follow.",
        "Contains third-party course material published without a license we could verify.",
        "Held pending clarification of the pricing stated in the description."
    ];

    /// <summary>
    /// Why the administration suspended a trainer, in its own words.
    /// </summary>
    private static readonly string[] SuspensionReasons =
    [
        "Repeated breaches of the content policy after two written warnings.",
        "Participant complaints about sessions that were booked and never delivered.",
        "Suspended pending the outcome of a dispute over course material ownership.",
        "The contact address on this profile has bounced for six weeks and no reply was received."
    ];

    /// <summary>
    /// Places the corpus: draws as many trainers as it takes to own exactly <paramref name="trainings"/>
    /// trainings, gives each of them their profile and their catalog, and dates everything.
    /// </summary>
    /// <param name="trainings">How many trainings the dataset holds in total. Must be positive.</param>
    /// <param name="anchorUtc">The instant the newest training is dated at, in UTC.</param>
    /// <returns>The trainers, each owning between one and five trainings.</returns>
    /// <remarks>
    /// The count is exact rather than approximate, and it is exact by construction rather than by
    /// correction: sizes are drawn while more than <see cref="MaximumTrainingsPerTrainer"/>
    /// trainings remain to be placed, and the last trainer takes whatever is left — which is
    /// therefore between one and five. Nobody ends up with zero, nobody exceeds the maximum, and no
    /// trainer is added or trimmed afterwards to make the total come out right.
    /// </remarks>
    [SuppressMessage("Security", "S2245:Make sure that using this pseudorandom number generator is safe here",
        Justification = "The constant seed is the decision, not an oversight: two runs of the seeder have to produce the same five hundred trainings, so that a screenshot keeps matching the database and a test can assert what was built (ADR 0079). A cryptographic generator would make that impossible by construction. Nothing drawn here protects anything — a catalog size, a hue, which sentence a biography closes with — and the one credential the seeder writes comes from configuration rather than from this. `.editorconfig` demotes CA5394 over this folder on the same argument; the attribute is what carries it to the analyzer that reads a quality profile instead.")]
    public static IReadOnlyList<SeededTrainer> Build(int trainings, DateTime anchorUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(trainings, MinimumTrainingsPerTrainer);

        var random = new Random(Seed);

        var sizes = DrawSizes(trainings, random);
        var schedule = Schedule(trainings, anchorUtc, random);

        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var placed = 0;

        var trainers = new List<SeededTrainer>(sizes.Count);

        for (var index = 0; index < sizes.Count; index++)
        {
            var area = DevelopmentCatalog.Areas[index % DevelopmentCatalog.Areas.Count];

            var firstname = Pick(DevelopmentCatalog.Firstnames, random);
            var lastname = Pick(DevelopmentCatalog.Lastnames, random);
            var username = Reserve(firstname, lastname, usernames);

            var owned = new List<SeededTraining>(sizes[index]);

            foreach (var blueprint in Choose(area.Blueprints, sizes[index], random))
            {
                owned.Add(Place(blueprint, schedule[placed++], random));
            }

            trainers.Add(new SeededTrainer(
                username,
                $"{username}@traininghub.local",
                firstname,
                lastname,
                $"{Pick(area.Biographies, random)} {Pick(DevelopmentCatalog.TeachingNotes, random)}",
                area.Name,
                random.NextDouble() < NoPortraitShare ? null : random.Next(360),
                random.NextDouble() < SuspendedShare ? Pick(SuspensionReasons, random) : null,
                owned));
        }

        return trainers;
    }

    /// <summary>
    /// How many trainings each trainer owns, summing to exactly <paramref name="trainings"/>.
    /// </summary>
    private static List<int> DrawSizes(int trainings, Random random)
    {
        var sizes = new List<int>();
        var remaining = trainings;

        while (remaining > MaximumTrainingsPerTrainer)
        {
            var size = Math.Min(Weighted(random), remaining - MinimumTrainingsPerTrainer);
            sizes.Add(size);
            remaining -= size;
        }

        sizes.Add(remaining);

        return sizes;
    }

    /// <summary>
    /// A catalog size between one and five, drawn against <see cref="SizeWeights"/>.
    /// </summary>
    private static int Weighted(Random random)
    {
        var draw = random.Next(SizeWeights.Sum());

        for (var size = 0; size < SizeWeights.Length; size++)
        {
            draw -= SizeWeights[size];

            if (draw < 0)
            {
                return size + MinimumTrainingsPerTrainer;
            }
        }

        return MaximumTrainingsPerTrainer;
    }

    /// <summary>
    /// Every training's creation instant, oldest first, strictly increasing.
    /// </summary>
    /// <remarks>
    /// Evenly spaced across <see cref="Window"/>, with noise bounded at half the spacing so that
    /// the order can never be disturbed by it. Strictness matters because the catalog's newest-first
    /// page is one of the things this dataset exists to make testable by hand.
    /// <para>
    /// The noise runs backwards, never forwards. Added, it would push the last training past the
    /// anchor — a training created in the future, which is not a rounding detail: the anchor is the
    /// moment the seeder runs, and a row dated after it is one the audit interceptor would never
    /// have written.
    /// </para>
    /// </remarks>
    private static DateTime[] Schedule(int trainings, DateTime anchorUtc, Random random)
    {
        var schedule = new DateTime[trainings];

        if (trainings == 1)
        {
            schedule[0] = anchorUtc;
            return schedule;
        }

        var spacing = Window / (trainings - 1);
        var noise = Math.Max(1, spacing.Ticks / 2);

        for (var index = 0; index < trainings; index++)
        {
            var back = spacing * (trainings - 1 - index);
            schedule[index] = anchorUtc - back - TimeSpan.FromTicks(random.NextInt64(noise));
        }

        return schedule;
    }

    /// <summary>
    /// Gives a blueprint its instant and its state.
    /// </summary>
    private static SeededTraining Place(TrainingBlueprint blueprint, DateTime createdOnUtc, Random random)
    {
        var draw = random.NextDouble();

        var withdrawn = draw < UnpublishedShare;
        var withheld = !withdrawn && draw < UnpublishedShare + WithheldShare;

        return new SeededTraining(
            blueprint.Title,
            blueprint.Description,
            blueprint.Prerequisites,
            blueprint.AcquiredSkills,
            blueprint.Topics,
            createdOnUtc,
            withdrawn,
            withheld ? Pick(WithholdingReasons, random) : null);
    }

    /// <summary>
    /// As many distinct blueprints as the trainer owns, in the order they will be created.
    /// </summary>
    /// <remarks>
    /// Distinct on purpose: a title is unique per trainer, so a trainer offered the same blueprint
    /// twice would meet the domain's own refusal — correctly, and after the seeder had already
    /// written half of them.
    /// </remarks>
    private static IEnumerable<TrainingBlueprint> Choose(
        IReadOnlyList<TrainingBlueprint> blueprints, int count, Random random)
    {
        var order = Enumerable.Range(0, blueprints.Count).ToArray();

        for (var index = order.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        return order.Take(count).Select(position => blueprints[position]);
    }

    /// <summary>
    /// The username this person gets, kept distinct from everyone already named.
    /// </summary>
    /// <remarks>
    /// Identity's default character set admits letters, digits and a handful of punctuation, and no
    /// accent among them — so the display name keeps its diacritics and the account name is the
    /// folded form. A collision costs a number, which is what a real registration would do too.
    /// </remarks>
    private static string Reserve(string firstname, string lastname, HashSet<string> taken)
    {
        var stem = $"{Fold(firstname)}.{Fold(lastname)}";
        var username = stem;
        var suffix = 2;

        while (!taken.Add(username))
        {
            username = string.Create(CultureInfo.InvariantCulture, $"{stem}{suffix++}");
        }

        return username;
    }

    /// <summary>
    /// A name reduced to the lowercase letters an account name may carry.
    /// </summary>
    private static string Fold(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                && char.IsAsciiLetter(character))
            {
                folded.Append(char.ToLowerInvariant(character));
            }
        }

        return folded.ToString();
    }

    /// <summary>
    /// One item, drawn uniformly.
    /// </summary>
    private static T Pick<T>(IReadOnlyList<T> items, Random random) => items[random.Next(items.Count)];
}
