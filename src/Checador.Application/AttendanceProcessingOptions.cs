namespace Checador.Application;

public sealed class AttendanceProcessingOptions
{
    public decimal StandardWorkDayHours { get; set; } = 8m;
    public int RoundToMinutes { get; set; } = 5;
    public bool UseStrictFourPunchPolicy { get; set; } = false;
    public bool EnableNightShiftSupport { get; set; } = true;
    public int NightShiftScheduleCode { get; set; } = 3;
    public int NightShiftStartHour { get; set; } = 18;
    public int NightShiftRegularEndHour { get; set; } = 7;
    public int NightShiftCarryToPreviousDayUntilHour { get; set; } = 12;
    public decimal NightShiftStandardHours { get; set; } = 9.60m;
    public int Schedule1ExtraCountFromHour { get; set; } = 7;
    public int Schedule2ExtraCountFromHour { get; set; } = 8;
    public int Schedule3ExtraCountFromHour { get; set; } = 18;
    public decimal Schedule1ExtraMaxHours { get; set; } = 2.4m;
    public decimal Schedule2ExtraMaxHours { get; set; } = 2.4m;
    public decimal Schedule3ExtraMaxHours { get; set; } = 3.6m;
}
