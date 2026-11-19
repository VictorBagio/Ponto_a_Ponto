using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PontoGame : MonoBehaviour
{
    public enum Phase
    {
        Planning,
        Running,
        Closing,
        GameOver
    }

    public static PontoGame Instance { get; private set; }

    [Header("Referências")]
    public RoadGridSelector selector;
    public RoadLineBuilder lineBuilder;
    public BusFleetController fleet;
    public DemandRegion[] regions;
    [Header("Economia e dias")]
    [Min(10)]
    public float dayDuration = 180f;
    [Min(1)]
    public int fare = 4;
    [Min(0)]
    public int startingCash = 500;
    [Min(1)]
    public int maxFailedDays = 5;
    [Tooltip("A meta usa a demanda das regiões ativas, nunca a quantidade de pontos.")]
    [Range(0.1f, 1f)]
    public float targetFraction = 0.8f;
    [Header("Filas")]
    [Min(1)]
    public int maxWaitingPerStop = 40;
    [Header("Crescimento da demanda")]
    [Min(0)]
    public int extraRequestsPerDay = 2;
    [Header("Velocidades da simulacao")]
    [SerializeField, Min(1)]
    private int normalSpeed = 1;
    [SerializeField, Min(1)]
    private int fastSpeed = 5;
    [SerializeField, Min(1)]
    private int veryFastSpeed = 10;
    private int speedIndex;
    private readonly PontoEconomy economy = new PontoEconomy();
    private readonly List<DemandRegion> activeRegions = new List<DemandRegion>();
    public Phase State { get; private set; } = Phase.Planning;
    public int Day => economy.Day;
    public int DisplayDay => State == Phase.Planning && HasFinishedDay && !ShowSummary ? Day + 1 : Day;
    public int Cash => economy.Cash;
    public int Revenue => economy.Revenue;
    public int Completed => economy.Completed;
    public int FailedDays => economy.FailedDays;
    public bool HasFinishedDay => economy.HasFinishedDay;
    public float TotalTripTime => economy.TotalTripTime;
    public float Remaining { get; private set; }

    public float SimulationClock { get; private set; }

    public bool Paused { get; private set; }

    public int SimulationSpeed { get; private set; } = 1;
    public bool ShowSummary { get; private set; }

    public int UnservedAtClose { get; private set; }

    public int WalkedToday { get; private set; }

    public int Target => TargetForDay(Day);
    public bool CanBuild => State == Phase.Planning && !ShowSummary;
    public float Delta => (State == Phase.Running || State == Phase.Closing) && !Paused ? Time.deltaTime * SimulationSpeed : 0f;
    public string Message { get; private set; } = "Observe as regiões: coloque pontos próximos e conecte seus destinos.";
    public int OutsideStopCount
    {
        get
        {
            int n = 0;
            if (regions != null)
                foreach (var r in regions)
                    if (r != null)
                        n += r.Pending.Count;
            return n;
        }
    }

    public int WaitingCount
    {
        get
        {
            int n = OutsideStopCount;
            if (selector != null)
                foreach (var s in selector.Stops)
                    n += s.Waiting.Count;
            return n;
        }
    }

    public int DemandForDay(int day)
    {
        int count = 0, total = 0;
        if (regions != null)
            foreach (var r in regions)
                if (r != null && r.IsActive(day))
                {
                    count++;
                    total += RequestsForDay(r, day);
                }

        return count >= 2 ? total : 0;
    }

    public int RequestsForDay(DemandRegion region, int day)
    {
        if (region == null || !region.IsActive(day))
            return 0;
        int lastUnlock = 1;
        if (regions != null)
            foreach (var item in regions)
                if (item != null)
                    lastUnlock = Mathf.Max(lastUnlock, item.activationDay);
        return Mathf.Max(0, region.requestsPerDay) + Mathf.Max(0, day - lastUnlock) * Mathf.Max(0, extraRequestsPerDay);
    }

    public int TargetForDay(int day) => PontoDemandRules.RevenueTarget(DemandForDay(day), fare, targetFraction);
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        economy.Reset(startingCash);
        Remaining = dayDuration;
        ResetSimulationSpeed();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Notify(string text)
    {
        Message = text;
    }

    public bool Spend(int amount)
    {
        if (!CanBuild)
        {
            Notify("Compras e alterações ficam disponíveis no planejamento.");
            return false;
        }

        if (!economy.Spend(amount))
        {
            Notify("Saldo insuficiente.");
            return false;
        }

        return true;
    }

    public BusStop FindStopFor(DemandRegion region)
    {
        if (region == null || selector == null)
            return null;
        BusStop best = null;
        float score = float.PositiveInfinity;
        foreach (var stop in selector.Stops)
        {
            if (!region.Covers(stop.transform.position) || stop.Waiting.Count >= maxWaitingPerStop)
                continue;
            float distance = ((Vector2)(stop.transform.position - region.transform.position)).sqrMagnitude;
            if (distance < score)
            {
                score = distance;
                best = stop;
            }
        }

        return best;
    }

    public void BeginDay()
    {
        if (!CanBuild)
            return;
        int nextDay = HasFinishedDay ? Day + 1 : Day;
        activeRegions.Clear();
        if (regions != null)
            foreach (var r in regions)
                if (r != null && r.IsActive(nextDay))
                    activeRegions.Add(r);
        if (activeRegions.Count < 2)
        {
            Notify("Configure pelo menos duas regiões ativas. Execute Configurar cena atual.");
            return;
        }

        if (selector.Stops.Count < 2 || fleet.AssignedCount == 0)
        {
            Notify("Crie duas paradas, conecte uma linha e coloque um ônibus nela.");
            return;
        }

        if (!economy.BeginDay())
            return;
        WalkedToday = 0;
        Remaining = dayDuration;
        Paused = false;
        ResetSimulationSpeed();
        lineBuilder.CancelLine();
        lineBuilder.RefreshStops();
        PassengerRouter.ClearCache();
        State = Phase.Running;
        foreach (var region in activeRegions)
        {
            region.Demand.BeginDay(RequestsForDay(region, Day));
            region.SpawnTimer = 0;
        }

        GenerateDemand(0f);
        DistributePending();
        fleet.BeginDay();
        Notify("Dia " + Day + ": " + DemandForDay(Day) + " novas viagens regionais. Meta R$ " + Target + ".");
    }

    private void Update()
    {
        if (selector == null || fleet == null || lineBuilder == null)
            return;
        if (PontoInput.Pressed(KeyCode.Space) && (State == Phase.Running || State == Phase.Closing))
            TogglePause();
        float dt = Delta;
        if (dt <= 0)
            return;
        SimulationClock += dt;
        if (State == Phase.Running)
        {
            Remaining = Mathf.Max(0, Remaining - dt);
            if (Remaining <= 0)
            {
                State = Phase.Closing;
                Notify("Encerrando: concluindo as viagens e baldeacoes ja iniciadas.");
            }
            else
            {
                GenerateDemand(dt);
                DistributePending();
            }
        }

        if (State == Phase.Closing && fleet.OnboardCount == 0 && !PassengerRouter.HasWaitingTransfers(this))
            FinishDay();
    }

    private void GenerateDemand(float dt)
    {
        for (int originIndex = 0; originIndex < activeRegions.Count; originIndex++)
        {
            DemandRegion origin = activeRegions[originIndex];
            origin.SpawnTimer -= dt;
            float interval = dayDuration * 0.85f / Mathf.Max(1, origin.Demand.Quota);
            while (origin.SpawnTimer <= 0 && origin.Demand.Issued < origin.Demand.Quota)
            {
                int request = origin.Demand.Issued;
                if (!origin.Demand.TryIssue())
                    break;
                int destIndex = PontoDemandRules.DestinationIndex(originIndex, request, activeRegions.Count);
                if (destIndex >= 0)
                    origin.Pending.Add(new PassengerData(origin, activeRegions[destIndex], SimulationClock));
                origin.SpawnTimer += interval;
            }
        }
    }

    private void DistributePending()
    {
        foreach (var region in activeRegions)
        {
            for (int i = 0; i < region.Pending.Count;)
            {
                PassengerData p = region.Pending[i];
                BusStop stop = FindStopFor(region);
                if (stop == null)
                    break;
                region.Pending.RemoveAt(i);
                if (p.destination != null && p.destination.Covers(stop.transform.position))
                {
                    WalkedToday++;
                    continue;
                }

                stop.Waiting.Add(p);
                stop.RefreshVisual();
            }
        }
    }

    public void Service(BusMovement bus, BusStop stop)
    {
        if (State != Phase.Running && State != Phase.Closing)
            return;
        for (int i = bus.Passengers.Count - 1; i >= 0; i--)
        {
            PassengerData p = bus.Passengers[i];
            if (p.destination == null)
            {
                bus.Passengers.RemoveAt(i);
                continue;
            }

            if (p.destination.Covers(stop.transform.position))
            {
                bus.Passengers.RemoveAt(i);
                p.nextExit = null;
                p.journeyStarted = false;
                if (p.trip.TryCompleteAt(p.destination.Id))
                    economy.RecordTrip(fare, SimulationClock - p.createdAt);
            }
            else if (p.nextExit == stop)
            {
                bus.Passengers.RemoveAt(i);
                p.nextExit = null;
                stop.Waiting.Add(p);
            }
        }

        for (int i = 0; i < stop.Waiting.Count && bus.Passengers.Count < bus.Capacity;)
        {
            PassengerData p = stop.Waiting[i];
            if (p.destination == null)
            {
                stop.Waiting.RemoveAt(i);
                continue;
            }

            if (State == Phase.Closing && !p.journeyStarted)
            {
                i++;
                continue;
            }

            if (!PassengerRouter.TryFindRide(this, stop, p.destination, out var ride) || ride.line != bus.Line || !bus.CanServeAhead(ride.exit.Cell))
            {
                i++;
                continue;
            }

            p.nextExit = ride.exit;
            p.journeyStarted = true;
            stop.Waiting.RemoveAt(i);
            bus.Passengers.Add(p);
        }

        stop.RefreshVisual();
    }

    private void FinishDay()
    {
        bool gameOver = economy.CloseDay(Target, maxFailedDays);
        UnservedAtClose = WaitingCount;
        ShowSummary = true;
        Paused = false;
        State = gameOver ? Phase.GameOver : Phase.Planning;
        Notify(gameOver ? "Operação encerrada após cinco falhas acumuladas." : "Receita recebida. A demanda não atendida permanece nos bairros e nas filas.");
    }

    public void DismissSummary()
    {
        if (State == Phase.Planning)
            ShowSummary = false;
    }

    public void TogglePause()
    {
        if (State == Phase.Running || State == Phase.Closing)
            Paused = !Paused;
    }

    public void CycleSpeed()
    {
        speedIndex = (speedIndex + 1) % 3;
        ApplySimulationSpeed();
    }

    private void ResetSimulationSpeed()
    {
        speedIndex = 0;
        ApplySimulationSpeed();
    }

    private void ApplySimulationSpeed()
    {
        switch (speedIndex)
        {
            case 1:
                SimulationSpeed = Mathf.Max(1, fastSpeed);
                break;
            case 2:
                SimulationSpeed = Mathf.Max(1, veryFastSpeed);
                break;
            default:
                SimulationSpeed = Mathf.Max(1, normalSpeed);
                break;
        }
    }

    private void OnValidate()
    {
        normalSpeed = Mathf.Max(1, normalSpeed);
        fastSpeed = Mathf.Max(1, fastSpeed);
        veryFastSpeed = Mathf.Max(1, veryFastSpeed);
        if (Application.isPlaying)
            ApplySimulationSpeed();
    }

    public void ResetGame()
    {
        State = Phase.Planning;
        ShowSummary = false;
        fleet.ClearFleet();
        lineBuilder.ClearLines();
        selector.ClearStops();
        economy.Reset(startingCash);
        SimulationClock = 0;
        Remaining = dayDuration;
        WalkedToday = 0;
        if (regions != null)
            foreach (var r in regions)
                if (r != null)
                {
                    r.Pending.Clear();
                    r.Demand.BeginDay(0);
                }

        activeRegions.Clear();
        Paused = false;
        ResetSimulationSpeed();
        fleet.CreateInitialFleet();
        PontoDayOneTutorial.Instance?.RestartTutorial();
        Notify("Nova partida: conecte as regiões ativas.");
    }
}
