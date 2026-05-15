using FluentAssertions;
using Microsoft.Extensions.Configuration;
using QuantamAnalytics.Infrastructure.Features;

namespace QuantamAnalytics.Tests;

public sealed class AppSettingsFeatureGateTests
{
    [Fact]
    public void Returns_fallback_when_flag_is_unset()
    {
        var gate = Build();

        gate.IsEnabled("AnythingMissing", fallback: false).Should().BeFalse();
        gate.IsEnabled("AnythingMissing", fallback: true).Should().BeTrue();
    }

    [Fact]
    public void Returns_true_when_flag_is_true()
    {
        var gate = Build(("Features:Beta", "true"));

        gate.IsEnabled("Beta").Should().BeTrue();
    }

    [Fact]
    public void Returns_false_when_flag_is_false()
    {
        var gate = Build(("Features:Beta", "false"));

        gate.IsEnabled("Beta", fallback: true).Should().BeFalse();
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("on")]
    [InlineData("")]
    public void Falls_back_on_non_bool_values(string raw)
    {
        var gate = Build(("Features:Beta", raw));

        gate.IsEnabled("Beta", fallback: false).Should().BeFalse();
    }

    [Fact]
    public void Rejects_blank_flag_name()
    {
        var gate = Build();

        var act = () => gate.IsEnabled("   ");

        act.Should().Throw<ArgumentException>();
    }

    private static AppSettingsFeatureGate Build(params (string key, string value)[] entries)
    {
        var dict = entries.ToDictionary(e => e.key, e => (string?)e.value);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
        return new AppSettingsFeatureGate(config);
    }
}
