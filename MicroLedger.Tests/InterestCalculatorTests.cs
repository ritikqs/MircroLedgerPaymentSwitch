using Xunit;
using MicroLedger.Infrastructure;

namespace MicroLedger.Tests;

public class InterestCalculatorTests
{
    [Fact]
    public void GetDailyRate_ShouldReturnCorrectRate()
    {
        var calculator = new InterestCalculator();
        var annualRate = 0.05m; // 5%
        var expectedDailyRate = annualRate / 365;
        var actualDailyRate = calculator.GetDailyRate(annualRate);
        Assert.Equal(expectedDailyRate, actualDailyRate);
    }

    [Fact]
    public void CalculateInterest_ShouldReturnCorrectInterest()
    {
        var calculator = new InterestCalculator();
        var balance = 1000m;
        var dailyRate = 0.05m / 365; // 5% annual rate
        var expectedInterest = Math.Round(balance * dailyRate, 2);
        var actualInterest = calculator.CalculateInterest(balance, dailyRate);
        Assert.Equal(expectedInterest, actualInterest);
    }
} 