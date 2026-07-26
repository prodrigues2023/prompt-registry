using PromptRegistry.Drills;
using Xunit;

namespace PromptRegistry.Tests;

// The two self-contained drills double as automated tests of the client's degradation behaviour.
// (The rollback drill needs a live registry, so it is exercised by scripts, not here.)
public class DrillTests
{
    [Fact]
    public async Task Fallback_serves_bundled_at_cold_start_then_stale_when_warm()
        => Assert.True(await FallbackDrill.RunAsync());

    [Fact]
    public async Task Fleet_briefly_disagrees_during_a_refresh_then_converges()
        => Assert.True(await FleetDrill.RunAsync());
}
