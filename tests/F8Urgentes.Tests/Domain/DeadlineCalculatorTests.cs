using F8Urgentes.Domain;

namespace F8Urgentes.Tests.Domain;

public sealed class DeadlineCalculatorTests
{
    [Fact]
    public void AddBusinessDays_SkipsWeekends()
    {
        // Friday 2024-06-14 + 1 business day => Monday 2024-06-17
        var result = DeadlineCalculator.AddBusinessDays(new DateOnly(2024, 6, 14), 1);
        Assert.Equal(new DateOnly(2024, 6, 17), result);
    }

    [Fact]
    public void AddBusinessDays_FifteenDaysFromMonday()
    {
        // Monday 2024-06-03 + 15 business days => Monday 2024-06-24 (3 weekends skipped)
        var result = DeadlineCalculator.AddBusinessDays(new DateOnly(2024, 6, 3), 15);
        Assert.Equal(new DateOnly(2024, 6, 24), result);
    }

    [Fact]
    public void BusinessDaysRemaining_ReturnsPositiveWhenBeforeDeadline()
    {
        var today = new DateOnly(2024, 6, 3);
        var deadline = new DateOnly(2024, 6, 10);

        var remaining = DeadlineCalculator.BusinessDaysRemaining(today, deadline);

        Assert.Equal(5, remaining);
    }

    [Fact]
    public void BusinessDaysRemaining_ReturnsNegativeWhenOverdue()
    {
        var today = new DateOnly(2024, 6, 10);
        var deadline = new DateOnly(2024, 6, 3);

        var remaining = DeadlineCalculator.BusinessDaysRemaining(today, deadline);

        Assert.Equal(-5, remaining);
    }

    [Fact]
    public void BusinessDaysRemaining_ReturnsZeroWhenSameDay()
    {
        var day = new DateOnly(2024, 6, 3);
        Assert.Equal(0, DeadlineCalculator.BusinessDaysRemaining(day, day));
    }
}
