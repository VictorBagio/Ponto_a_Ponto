using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BusFleetController : MonoBehaviour
{
    [SerializeField]
    private RoadLineBuilder lineBuilder;
    [SerializeField]
    private Tilemap roadsTilemap;
    [SerializeField]
    private BusMovement busPrefab;
    [SerializeField]
    private Transform busesParent;
    [Header("Frota inicial e preço")]
    [Min(1)]
    public int initialBuses = 2;
    [Min(1)]
    public int newBusCost = 350;
    private readonly List<BusMovement> buses = new List<BusMovement>();
    public IReadOnlyList<BusMovement> Buses => buses;
    public int Revision { get; private set; }

    public int AssignedCount
    {
        get
        {
            int n = 0;
            foreach (var b in buses)
                if (b != null && b.Line != null)
                    n++;
            return n;
        }
    }

    public int OnboardCount
    {
        get
        {
            int n = 0;
            foreach (var b in buses)
                if (b != null)
                    n += b.Passengers.Count;
            return n;
        }
    }

    private void Start()
    {
        if (busPrefab == null || roadsTilemap == null || lineBuilder == null || busesParent == null)
        {
            Debug.LogError("Configure as referências de BusFleetController.", this);
            enabled = false;
            return;
        }

        CreateInitialFleet();
    }

    public void CreateInitialFleet()
    {
        for (int i = 0; i < initialBuses; i++)
            CreateBus();
    }

    private BusMovement CreateBus()
    {
        BusMovement bus = Instantiate(busPrefab, Vector3.zero, Quaternion.identity, busesParent);
        bus.gameObject.SetActive(true);
        bus.enabled = true;
        bus.Number = buses.Count + 1;
        bus.name = "Onibus_" + bus.Number;
        bus.Park();
        buses.Add(bus);
        Revision++;
        return bus;
    }

    public void BuyBus()
    {
        if (PontoGame.Instance != null && PontoGame.Instance.Spend(newBusCost))
        {
            CreateBus();
            PontoGame.Instance.Notify("Ônibus comprado e disponível na garagem.");
        }
    }

    public void AssignAvailableTo(RoadLineBuilder.LineData line)
    {
        if (PontoGame.Instance != null && !PontoGame.Instance.CanBuild)
            return;
        foreach (var bus in buses)
            if (bus != null && bus.Line == null)
            {
                if (bus.AssignLine(line, roadsTilemap))
                    Revision++;
                return;
            }

        PontoGame.Instance?.Notify("Sem ônibus na garagem. Compre outro ou remova um de outra linha.");
    }

    public void ReturnBus(BusMovement bus)
    {
        if (bus != null && (PontoGame.Instance == null || PontoGame.Instance.CanBuild))
        {
            bus.Park();
            Revision++;
        }
    }

    public void ReleaseLine(RoadLineBuilder.LineData line)
    {
        foreach (var bus in buses)
            if (bus != null && bus.Line == line)
                bus.Park();
        Revision++;
    }

    public void BeginDay()
    {
        foreach (var bus in buses)
            if (bus != null)
                bus.BeginDay();
    }

    public void ClearFleet()
    {
        foreach (var bus in buses)
            if (bus != null)
                Destroy(bus.gameObject);
        buses.Clear();
        Revision++;
    }
}
