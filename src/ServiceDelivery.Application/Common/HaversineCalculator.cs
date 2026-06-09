namespace ServiceDelivery.Application.Common;

public static class HaversineCalculator
{
    private const double EarthRadiusMiles = 3958.8;
    public const double AssumedSpeedMph = 60.0;

    public static double DistanceMiles(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMiles * c;
    }

    public static double EtaMinutes(double distanceMiles)
        => distanceMiles / AssumedSpeedMph * 60.0;

    private static double ToRadians(double degrees)
        => degrees * Math.PI / 180.0;
}
