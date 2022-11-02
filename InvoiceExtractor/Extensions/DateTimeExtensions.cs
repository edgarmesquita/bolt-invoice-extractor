namespace Bolt.Business.InvoiceExtractor.Extensions;

public static class DateTimeExtensions
{
    public static DateTime FromUnixTime(this long unixTime)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddTicks(unixTime);
    }
    
    public static DateTime FromUnixTimeInSeconds(this long unixTimeInSeconds)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeInSeconds);
    }
    
    public static long ToUnixTime(this DateTime date)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return Convert.ToInt64((date - epoch).Ticks);
    }
    
    public static long ToUnixTime(DateTimeOffset date)
    {
        var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, 0, new TimeSpan(0));
        return Convert.ToInt64((date - epoch).Ticks);
    }
    
    public static long ToUnixTimeInSeconds(this DateTime date)
    {
        return Convert.ToInt64((date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
    }
}