using System;

public sealed class PontoRegionDemand
{
    public int Quota { get; private set; }

    public int Issued { get; private set; }

    public void BeginDay(int quota)
    {
        Quota = Math.Max(0, quota);
        Issued = 0;
    }

    public bool TryIssue()
    {
        if (Issued >= Quota)
            return false;
        Issued++;
        return true;
    }
}

public static class PontoDemandRules
{
    public static int DestinationIndex(int origin, int request, int count)
    {
        if (count < 2)
            return -1;
        int offset = 1 + Math.Abs(request % (count - 1));
        return (origin + offset) % count;
    }

    public static int RevenueTarget(int dailyRequests, int fare, float fraction)
    {
        if (dailyRequests <= 0 || fare <= 0)
            return 0;
        double ratio = Math.Round(Math.Max(0.1, Math.Min(1.0, fraction)), 4);
        int maximumRevenue = dailyRequests * fare;
        return Math.Min(maximumRevenue, (int)(Math.Ceiling(maximumRevenue * ratio / 10.0) * 10));
    }
}
