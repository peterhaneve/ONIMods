using System;
using System.Globalization;
using System.Text;
using Xunit;
using Ryu;

public class RyuFloatTests {
    // InvariantCulture on both format and parse: keeps the test locale-independent
    // (a comma-decimal CI runner would otherwise emit/parse "5,0E-1" and fail).
    private static string Fmt(double d) {
        var sb = new StringBuilder();
        RyuFormat.ToString(sb, d, 0, provider: CultureInfo.InvariantCulture);   // precision 0 = shortest round-trip
        return sb.ToString();
    }

    [Theory]
    [InlineData(0.0, "0E0")]
    [InlineData(1.0, "1E0")]
    [InlineData(0.5, "5E-1")]
    [InlineData(-2.25, "-2.25E0")]
    public void FormatsKnownValues(double input, string expected) {
        Assert.Equal(expected, Fmt(input));
    }

    [Theory]
    [InlineData(3.14159265358979)]
    [InlineData(123456.789)]
    [InlineData(1e-7)]
    [InlineData(1e21)]
    public void RoundTrips(double input) {
        Assert.Equal(input, double.Parse(Fmt(input), CultureInfo.InvariantCulture));
    }
}
