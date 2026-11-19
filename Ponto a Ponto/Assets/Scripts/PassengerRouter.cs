using System.Collections.Generic;

public static class PassengerRouter
{
    public struct Ride
    {
        public RoadLineBuilder.LineData line;
        public BusStop exit;
        public Ride(RoadLineBuilder.LineData line, BusStop exit)
        {
            this.line = line;
            this.exit = exit;
        }
    }

    private static PontoGame cachedGame;
    private static int lineRevision = -1, fleetRevision = -1;
    private static TransitGraph graph;
    private static readonly Dictionary<int, RoadLineBuilder.LineData> lines = new Dictionary<int, RoadLineBuilder.LineData>();
    private static readonly Dictionary<int, BusStop> stops = new Dictionary<int, BusStop>();
    private static readonly Dictionary<(BusStop, DemandRegion), Ride> cache = new Dictionary<(BusStop, DemandRegion), Ride>();
    public static void ClearCache()
    {
        cachedGame = null;
        graph = null;
        lineRevision = fleetRevision = -1;
        lines.Clear();
        stops.Clear();
        cache.Clear();
    }

    private static void EnsureNetwork(PontoGame game)
    {
        if (cachedGame == game && graph != null && lineRevision == game.lineBuilder.Revision && fleetRevision == game.fleet.Revision)
            return;
        ClearCache();
        cachedGame = game;
        lineRevision = game.lineBuilder.Revision;
        fleetRevision = game.fleet.Revision;
        graph = new TransitGraph();
        foreach (var stop in game.selector.Stops)
            stops[stop.Id] = stop;
        foreach (var bus in game.fleet.Buses)
        {
            var line = bus != null ? bus.Line : null;
            if (line == null || !line.isLoop || lines.ContainsKey(line.id))
                continue;
            lines.Add(line.id, line);
            var served = new List<BusStop>();
            var visited = new HashSet<BusStop>();
            for (int i = 0; i < line.cells.Count - 1; i++)
            {
                BusStop stop = game.selector.StopAt(line.cells[i]);
                if (stop != null && visited.Add(stop))
                    served.Add(stop);
            }

            for (int i = 0; i < served.Count; i++)
                for (int step = 1; step < served.Count; step++)
                    graph.Connect(served[i].Id, served[(i + step) % served.Count].Id, line.id);
        }
    }

    public static bool TryFindRide(PontoGame game, BusStop origin, DemandRegion destination, out Ride firstRide)
    {
        firstRide = default;
        if (game == null || origin == null || destination == null || game.fleet == null || game.selector == null || game.lineBuilder == null)
            return false;
        EnsureNetwork(game);
        var key = (origin, destination);
        if (cache.TryGetValue(key, out firstRide))
            return firstRide.line != null;
        var targets = new HashSet<int>();
        foreach (var stop in game.selector.Stops)
            if (destination.Covers(stop.transform.position))
                targets.Add(stop.Id);
        if (graph.TryFindRide(origin.Id, targets, out var result))
            firstRide = new Ride(lines[result.lineId], stops[result.exitId]);
        cache[key] = firstRide;
        return firstRide.line != null;
    }

    public static bool HasWaitingTransfers(PontoGame game)
    {
        bool found = false;
        foreach (var stop in game.selector.Stops)
            foreach (var passenger in stop.Waiting)
            {
                if (!passenger.journeyStarted)
                    continue;
                if (TryFindRide(game, stop, passenger.destination, out _))
                    found = true;
                else
                    passenger.journeyStarted = false;
            }

        return found;
    }
}
