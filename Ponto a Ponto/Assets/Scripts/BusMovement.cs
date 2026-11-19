using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(200)]
public class BusMovement : MonoBehaviour
{
    [SerializeField, Min(0.1f)]
    private float speed = 3f;
    [SerializeField, Min(0.1f)]
    private float stopDuration = 2f;
    [SerializeField]
    private SpriteRenderer busRenderer;
    [Header("Melhorias individuais")]
    [SerializeField]
    private int capacityLevel;
    [SerializeField]
    private int speedLevel;
    [SerializeField]
    private int boardingLevel;
    public int Number { get; set; }

    public int CapacityLevel => capacityLevel;
    public int SpeedLevel => speedLevel;
    public int BoardingLevel => boardingLevel;
    public int Capacity => 12 + 4 * capacityLevel;
    public float Speed => speed * (1f + 0.2f * speedLevel);
    public float StopDuration => Mathf.Max(0.2f, stopDuration * Mathf.Pow(0.8f, boardingLevel));
    public readonly List<PassengerData> Passengers = new List<PassengerData>();
    public RoadLineBuilder.LineData Line { get; private set; }

    private Tilemap roads;
    private Vector3[] logicalPositions;
    private int currentIndex, targetIndex;
    private float progress, stopTimer;
    private bool ready;
    private void Awake()
    {
        if (busRenderer == null)
            busRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    public bool AssignLine(RoadLineBuilder.LineData line, Tilemap tilemap)
    {
        if (line == null || !line.isLoop || line.cells == null || line.cells.Count < 5 || tilemap == null)
            return false;
        if (line.cells[0] != line.cells[line.cells.Count - 1])
            return false;
        Line = line;
        roads = tilemap;
        logicalPositions = new Vector3[line.cells.Count];
        for (int i = 0; i < logicalPositions.Length; i++)
            logicalPositions[i] = tilemap.GetCellCenterWorld(line.cells[i]);
        currentIndex = 0;
        targetIndex = 1;
        progress = stopTimer = 0;
        ready = false;
        if (busRenderer != null)
        {
            busRenderer.enabled = true;
            Color c = line.color;
            c.a = 1;
            busRenderer.color = c;
        }

        RenderPosition();
        return true;
    }

    public void Park()
    {
        Line = null;
        ready = false;
        Passengers.Clear();
        if (busRenderer != null)
            busRenderer.enabled = false;
    }

    public void BeginDay()
    {
        if (Line == null)
            return;
        if (!ready || progress <= 0.000001f)
        {
            ready = true;
            ServiceCurrentStop();
        }
    }

    public bool CanServeAhead(Vector3Int destination)
    {
        return Line != null && Line.ContainsStop(destination) && destination != Line.cells[currentIndex];
    }

    public bool CanServeAhead(DemandRegion destination)
    {
        if (Line == null || destination == null || roads == null)
            return false;
        foreach (Vector3Int cell in Line.stops)
        {
            if (cell != Line.cells[currentIndex] && destination.Covers(roads.GetCellCenterWorld(cell)))
                return true;
        }

        return false;
    }

    private void ServiceCurrentStop()
    {
        if (Line == null || !Line.ContainsStop(Line.cells[currentIndex]))
            return;
        var game = PontoGame.Instance;
        if (game == null)
            return;
        BusStop stop = game.selector.StopAt(Line.cells[currentIndex]);
        if (stop == null)
            return;
        game.Service(this, stop);
        stopTimer = StopDuration;
    }

    private void LateUpdate()
    {
        if (Line == null || logicalPositions == null)
            return;
        var game = PontoGame.Instance;
        float dt = game == null ? 0f : game.Delta;
        if (dt > 0 && !ready)
        {
            ready = true;
            ServiceCurrentStop();
        }

        if (stopTimer > 0)
        {
            float wait = Mathf.Min(dt, stopTimer);
            stopTimer -= wait;
            dt -= wait;
        }

        int safety = 0;
        while (dt > 0 && Speed > 0 && safety++ < 2048)
        {
            float length = Mathf.Max(0.0001f, Vector3.Distance(logicalPositions[currentIndex], logicalPositions[targetIndex]));
            float arrivalTime = (1 - progress) * length / Speed;
            if (dt < arrivalTime)
            {
                progress += dt * Speed / length;
                break;
            }

            dt -= arrivalTime;
            currentIndex = targetIndex;
            progress = 0;
            if (currentIndex == logicalPositions.Length - 1)
                currentIndex = 0;
            targetIndex = currentIndex + 1;
            ServiceCurrentStop();
            if (stopTimer > 0)
            {
                float wait = Mathf.Min(stopTimer, dt);
                stopTimer -= wait;
                dt -= wait;
            }

            if (stopTimer > 0)
                break;
        }

        RenderPosition();
    }

    private Vector3 Visual(int index) => Line.drawing != null && Line.drawing.positionCount == logicalPositions.Length ? Line.drawing.GetPosition(index) : logicalPositions[index];
    private void RenderPosition()
    {
        if (Line != null && logicalPositions != null)
            transform.position = Vector3.Lerp(Visual(currentIndex), Visual(targetIndex), progress);
    }

    public int UpgradeCost(int type)
    {
        int level = type == 0 ? capacityLevel : type == 1 ? speedLevel : boardingLevel;
        return level >= 3 ? -1 : (type == 0 ? 150 : type == 1 ? 175 : 100) * (level + 1);
    }

    public void Upgrade(int type)
    {
        int cost = UpgradeCost(type);
        if (cost < 0 || PontoGame.Instance == null || !PontoGame.Instance.Spend(cost))
            return;
        if (type == 0)
            capacityLevel++;
        else if (type == 1)
            speedLevel++;
        else
            boardingLevel++;
        PontoGame.Instance.Notify("Ônibus " + Number + " melhorado.");
    }
}
