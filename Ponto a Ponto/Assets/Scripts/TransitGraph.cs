using System.Collections.Generic;

public sealed class TransitGraph
{
    public struct Ride
    {
        public int lineId;
        public int exitId;
        public Ride(int lineId, int exitId)
        {
            this.lineId = lineId;
            this.exitId = exitId;
        }
    }

    private readonly Dictionary<int, List<Ride>> connections = new Dictionary<int, List<Ride>>();
    public void Connect(int origin, int destination, int lineId)
    {
        if (origin == destination)
            return;
        if (!connections.TryGetValue(origin, out var list))
        {
            list = new List<Ride>();
            connections.Add(origin, list);
        }

        list.Add(new Ride(lineId, destination));
    }

    public bool TryFindRide(int origin, ISet<int> destinations, out Ride ride)
    {
        ride = default;
        var queue = new Queue<int>();
        var visited = new HashSet<int>{origin};
        var first = new Dictionary<int, Ride>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current != origin && destinations.Contains(current))
            {
                ride = first[current];
                return true;
            }

            if (!connections.TryGetValue(current, out var edges))
                continue;
            foreach (var edge in edges)
            {
                if (!visited.Add(edge.exitId))
                    continue;
                first[edge.exitId] = current == origin ? edge : first[current];
                queue.Enqueue(edge.exitId);
            }
        }

        return false;
    }
}
