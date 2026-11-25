using SyncForge.Core.Models;
using SyncForge.Core.Scheduling;

namespace SyncForge.Core.Tests;

public class ScheduleCalculatorTests
{
    private static DateTimeOffset Local(int y, int m, int d, int h, int min = 0) =>
        new(new DateTime(y, m, d, h, min, 0, DateTimeKind.Local));

    [Fact]
    public void None_HasNoNextRun()
    {
        Assert.Null(ScheduleCalculator.NextAfter(new AutomationSettings(), DateTimeOffset.Now));
        Assert.Equal((false, (DateTimeOffset?)null), ScheduleCalculator.Evaluate(new AutomationSettings(), DateTimeOffset.Now, DateTimeOffset.Now));
    }

    [Fact]
    public void Interval_AddsMinutes()
    {
        var s = new AutomationSettings { Schedule = ScheduleKind.Interval, IntervalMinutes = 90 };
        var anchor = Local(2026, 11, 16, 10);
        Assert.Equal(Local(2026, 11, 16, 11, 30), ScheduleCalculator.NextAfter(s, anchor));
    }

    [Theory]
    [InlineData(10, 16, 18)] // vor 18 Uhr -> heute 18 Uhr
    [InlineData(19, 17, 18)] // nach 18 Uhr -> morgen 18 Uhr
    [InlineData(18, 17, 18)] // genau 18 Uhr -> morgen (strikt danach)
    public void Daily_NextOccurrence(int anchorHour, int expectedDay, int expectedHour)
    {
        var s = new AutomationSettings { Schedule = ScheduleKind.Daily, TimeOfDay = new TimeOnly(18, 0) };
        Assert.Equal(Local(2026, 11, expectedDay, expectedHour), ScheduleCalculator.NextAfter(s, Local(2026, 11, 16, anchorHour)));
    }

    [Fact]
    public void Weekly_SkipsDisallowedDays()
    {
        // 20.11.2026 ist ein Freitag; nur Montag erlaubt -> 23.11.
        var s = new AutomationSettings { Schedule = ScheduleKind.Weekly, TimeOfDay = new TimeOnly(8, 30), Days = [DayOfWeek.Monday] };
        Assert.Equal(Local(2026, 11, 23, 8, 30), ScheduleCalculator.NextAfter(s, Local(2026, 11, 20, 12)));
    }

    [Fact]
    public void Weekly_WithoutDays_HasNoNextRun()
    {
        var s = new AutomationSettings { Schedule = ScheduleKind.Weekly, Days = [] };
        Assert.Null(ScheduleCalculator.NextAfter(s, DateTimeOffset.Now));
    }

    [Fact]
    public void Evaluate_NotYetDue()
    {
        var s = new AutomationSettings { Schedule = ScheduleKind.Interval, IntervalMinutes = 60 };
        var (due, next) = ScheduleCalculator.Evaluate(s, Local(2026, 11, 16, 10), Local(2026, 11, 16, 10, 30));
        Assert.False(due);
        Assert.Equal(Local(2026, 11, 16, 11), next);
    }

    [Fact]
    public void Evaluate_DueOnTime_NextFromNow()
    {
        var s = new AutomationSettings { Schedule = ScheduleKind.Interval, IntervalMinutes = 60 };
        var (due, next) = ScheduleCalculator.Evaluate(s, Local(2026, 11, 16, 10), Local(2026, 11, 16, 11, 1));
        Assert.True(due);
        Assert.Equal(Local(2026, 11, 16, 12, 1), next);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Evaluate_MissedLongAgo_RespectsCatchUp(bool catchUp, bool expectedDue)
    {
        var s = new AutomationSettings { Schedule = ScheduleKind.Daily, TimeOfDay = new TimeOnly(18, 0), CatchUpMissed = catchUp };
        // Termin 16.11. 18:00 verpasst, App startet am 18.11. um 9:00
        var (due, next) = ScheduleCalculator.Evaluate(s, Local(2026, 11, 16, 12), Local(2026, 11, 18, 9));
        Assert.Equal(expectedDue, due);
        Assert.Equal(Local(2026, 11, 18, 18), next); // nur ein Nachholen, dann normal weiter
    }

    [Fact]
    public void Evaluate_SlightlyLate_RunsEvenWithoutCatchUp()
    {
        var s = new AutomationSettings { Schedule = ScheduleKind.Daily, TimeOfDay = new TimeOnly(18, 0), CatchUpMissed = false };
        var (due, _) = ScheduleCalculator.Evaluate(s, Local(2026, 11, 16, 12), Local(2026, 11, 16, 18, 5));
        Assert.True(due);
    }

    [Fact]
    public void Profile_RoundTrips_AutomationSettings()
    {
        var p = new SyncProfile();
        p.Automation.Schedule = ScheduleKind.Weekly;
        p.Automation.TimeOfDay = new TimeOnly(7, 45);
        p.Automation.Days = [DayOfWeek.Saturday];
        var clone = p.Clone();
        Assert.Equal(ScheduleKind.Weekly, clone.Automation.Schedule);
        Assert.Equal(new TimeOnly(7, 45), clone.Automation.TimeOfDay);
        Assert.Equal([DayOfWeek.Saturday], clone.Automation.Days);
    }
}
