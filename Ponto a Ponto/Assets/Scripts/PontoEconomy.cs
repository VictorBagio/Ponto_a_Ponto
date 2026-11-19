using System;

public sealed class PontoEconomy
{
    public int Day { get; private set; } = 1;
    public int Cash { get; private set; }

    public int Revenue { get; private set; }

    public int Completed { get; private set; }

    public int FailedDays { get; private set; }

    public float TotalTripTime { get; private set; }

    public bool HasFinishedDay { get; private set; }

    public bool IsDayOpen { get; private set; }

    public bool IsGameOver { get; private set; }

    public void Reset(int startingCash)
    {
        Day = 1;
        Cash = Math.Max(0, startingCash);
        Revenue = Completed = FailedDays = 0;
        TotalTripTime = 0;
        HasFinishedDay = IsDayOpen = IsGameOver = false;
    }

    public bool BeginDay()
    {
        if (IsDayOpen || IsGameOver)
            return false;
        if (HasFinishedDay)
            Day++;
        Revenue = Completed = 0;
        TotalTripTime = 0;
        HasFinishedDay = false;
        IsDayOpen = true;
        return true;
    }

    public bool Spend(int amount)
    {
        if (amount < 0 || Cash < amount || IsDayOpen || IsGameOver)
            return false;
        Cash -= amount;
        return true;
    }

    public void RecordTrip(int fare, float journeyTime)
    {
        if (!IsDayOpen || IsGameOver)
            return;
        Revenue += Math.Max(0, fare);
        Completed++;
        TotalTripTime += Math.Max(0, journeyTime);
    }

    public bool CloseDay(int target, int maxFailures)
    {
        if (!IsDayOpen)
            return IsGameOver;
        Cash += Revenue;
        if (Revenue < target)
            FailedDays++;
        IsDayOpen = false;
        HasFinishedDay = true;
        IsGameOver = FailedDays >= Math.Max(1, maxFailures);
        return IsGameOver;
    }
}

public sealed class PontoTrip
{
    public int DestinationId { get; }

    public bool Completed { get; private set; }

    public PontoTrip(int destinationId)
    {
        DestinationId = destinationId;
    }

    public bool TryCompleteAt(int stopId)
    {
        if (Completed || stopId != DestinationId)
            return false;
        Completed = true;
        return true;
    }
}
