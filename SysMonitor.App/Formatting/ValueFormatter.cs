namespace SysMonitor.App.Formatting;

/// <summary>
/// Turns sensor values into display text, like "52 °C" or "11.5 / 15.9 GB".
/// Missing values become a dash.
/// </summary>
internal static class ValueFormatter
{
    /// <summary>Shown when a value is missing (sensor not found, or no reading yet).</summary>
    public const string MissingValue = "—";

    /// <summary>
    /// Turns a sensor value into display text like "52 °C", or a dash if there's no value.
    /// </summary>
    /// <param name="value">The sensor value, or null if unavailable.</param>
    /// <param name="unit">The unit to show after the number.</param>
    /// <param name="decimalFormat">How to format the number: "0" = no decimals (default), "0.00" = two.</param>
    public static string FormatValue(float? value, string unit, string decimalFormat = "0")
    {
        if (value is float actualValue)
        {
            return $"{actualValue.ToString(decimalFormat)} {unit}";
        }

        return MissingValue;
    }

    /// <summary>
    /// Turns a used/total pair into text like "12.3 / 32.0 GB", or a dash if either value is missing.
    /// </summary>
    /// <param name="usedGigabytes">Amount in use, in GB.</param>
    /// <param name="totalGigabytes">Total amount, in GB.</param>
    public static string FormatUsage(double? usedGigabytes, double? totalGigabytes)
    {
        if (usedGigabytes is double used && totalGigabytes is double total)
        {
            return $"{used:0.0} / {total:0.0} GB";
        }

        return MissingValue;
    }
}
