public class PassengerData
{
    public BusStop nextExit;
    public bool journeyStarted;
    public readonly DemandRegion origin;
    public readonly DemandRegion destination;
    public readonly float createdAt;
    public readonly PontoTrip trip;
    public PassengerData(DemandRegion origin, DemandRegion destination, float createdAt)
    {
        this.origin = origin;
        this.destination = destination;
        this.createdAt = createdAt;
        trip = new PontoTrip(destination.Id);
    }
}
