using SportsPlatform.Modules.SportsConfiguration.Generators;

namespace SportsPlatform.UnitTests.SportsConfiguration.Generators;

// The PRNG is the module's own so a stored seed reproduces a draw on every .NET version (S05 D10): these pins are the
// algorithms' published outputs, and a change to either fails here.
public sealed class SeededRandomTests
{
    [Fact]
    public void SplitMix64_gives_its_published_outputs()
    {
        // The reference vector for seed 1234567 (Vigna's splitmix64.c).
        var state = 1234567UL;
        ulong[] outputs = [.. Enumerable.Range(0, 5).Select(_ => SeededRandom.SplitMix64(ref state))];

        outputs.ShouldBe([6457827717110365317UL, 3203168211198807973UL, 9817491932198370423UL, 4593380528125082431UL, 16408922859458223821UL]);
    }

    [Fact]
    public void Xoshiro256_star_star_gives_its_published_outputs()
    {
        // From the state 1, 2, 3, 4 (Vigna's xoshiro256starstar.c).
        var random = SeededRandom.FromState(1, 2, 3, 4);
        ulong[] outputs = [.. Enumerable.Range(0, 4).Select(_ => random.NextUInt64())];

        outputs.ShouldBe([11520UL, 0UL, 1509978240UL, 1215971899390074240UL]);
    }

    [Fact]
    public void A_seed_gives_the_same_sequence_and_another_seed_another()
    {
        int[] Draw(ulong seed)
        {
            var random = new SeededRandom(seed);
            return [.. Enumerable.Range(0, 20).Select(_ => random.NextInt(1000))];
        }

        Draw(42).ShouldBe(Draw(42));
        Draw(42).ShouldNotBe(Draw(43));
    }

    [Fact]
    public void Bounded_draws_stay_in_range_and_reach_every_value()
    {
        var random = new SeededRandom(7);
        var seen = new HashSet<int>();
        for (var i = 0; i < 2000; i++)
        {
            var value = random.NextInt(13);
            value.ShouldBeInRange(0, 12);
            seen.Add(value);
        }

        seen.Count.ShouldBe(13);
        Should.Throw<ArgumentOutOfRangeException>(() => random.NextInt(0));
    }

    [Fact]
    public void A_shuffle_is_a_permutation_and_leaves_its_input_alone()
    {
        int[] items = [.. Enumerable.Range(1, 30)];

        var shuffled = new SeededRandom(99).Shuffle(items);

        shuffled.Order().ShouldBe(items);
        shuffled.ShouldNotBe(items);
        items.ShouldBe(Enumerable.Range(1, 30));
    }

    [Fact]
    public void An_all_zero_state_is_refused()
    {
        Should.Throw<ArgumentException>(() => SeededRandom.FromState(0, 0, 0, 0));
    }
}
