using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RoadGridSelector : MonoBehaviour
{
    public enum ToolMode
    {
        Stops,
        Lines,
        Remove
    }

    [Header("Referencias")]
    [SerializeField]
    private Camera worldCamera;
    [SerializeField]
    private Tilemap roadsTilemap;
    [SerializeField]
    private SpriteRenderer cellPreview;
    [SerializeField]
    private RoadLineBuilder lineBuilder;
    [Header("Criacao de paradas")]
    [SerializeField]
    private GameObject busStopPrefab;
    [SerializeField]
    private Transform busStopsParent;
    [SerializeField]
    private bool placementEnabled = true;
    [Header("Previa")]
    [SerializeField]
    private Color validColor = new Color(0f, 1f, 0.3f, 0.4f);
    [SerializeField]
    private Color invalidColor = new Color(1f, 0.15f, 0.15f, 0.3f);
    private readonly Dictionary<Vector3Int, BusStop> placedStops = new Dictionary<Vector3Int, BusStop>();
    private readonly List<BusStop> stops = new List<BusStop>();
    private int nextId = 1;
    private bool drawingStroke;
    public ToolMode Mode { get; private set; }

    public IReadOnlyList<BusStop> Stops => stops;
    public Tilemap Roads => roadsTilemap;
    public Vector3Int SelectedCell { get; private set; }

    public bool HasSelection { get; private set; }

    private bool CanBuild => PontoGame.Instance == null || PontoGame.Instance.CanBuild;
    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
        Mode = placementEnabled ? ToolMode.Stops : ToolMode.Lines;
        HidePreview();
    }

    private void Update()
    {
        if (!CanBuild || worldCamera == null || roadsTilemap == null || lineBuilder == null)
        {
            drawingStroke = false;
            HidePreview();
            return;
        }

        if (PontoInput.Pressed(KeyCode.Escape))
        {
            drawingStroke = false;
            lineBuilder.CancelLine();
        }

        if (!PontoInput.LeftHeld)
            drawingStroke = false;
        if (PontoInput.OverUI || PontoInput.MiddleHeld || !Application.isFocused)
        {
            drawingStroke = false;
            HidePreview();
            return;
        }

        if (PontoInput.Pressed(KeyCode.Alpha1))
            SetMode(0);
        if (PontoInput.Pressed(KeyCode.Alpha2))
            SetMode(1);
        if (PontoInput.Pressed(KeyCode.Alpha3))
            SetMode(2);
        if (Mode == ToolMode.Lines)
        {
            if (PontoInput.Pressed(KeyCode.Backspace))
            {
                drawingStroke = false;
                lineBuilder.UndoLastCell();
            }

            if (PontoInput.Pressed(KeyCode.Return))
            {
                drawingStroke = false;
                lineBuilder.ConfirmLine();
            }
        }

        if (!PontoInput.MapPoint(worldCamera, roadsTilemap.transform.position.z, out Vector3 position))
        {
            drawingStroke = false;
            HidePreview();
            return;
        }

        Vector3Int cell = roadsTilemap.WorldToCell(position);
        cell.z = 0;
        bool road = roadsTilemap.HasTile(cell);
        bool hasStop = placedStops.ContainsKey(cell);
        bool valid = road && (Mode != ToolMode.Stops || !hasStop);
        if (cellPreview != null)
        {
            cellPreview.enabled = Mode != ToolMode.Remove;
            cellPreview.transform.position = roadsTilemap.GetCellCenterWorld(cell);
            cellPreview.color = valid ? validColor : invalidColor;
        }

        if (Mode == ToolMode.Lines)
        {
            if (PontoInput.LeftClick && road)
                drawingStroke = true;
            if (drawingStroke && PontoInput.LeftHeld && road)
                lineBuilder.ExtendLine(cell);
            return;
        }

        if (!PontoInput.LeftClick)
            return;
        SelectedCell = cell;
        HasSelection = true;
        if (Mode == ToolMode.Stops)
        {
            if (valid)
                CreateStop(cell);
        }
        else if (hasStop)
        {
            RemoveStop(cell);
            PontoGame.Instance?.Notify("Parada removida. As linhas continuam no mesmo percurso.");
        }
        else
        {
            float radius = 12f * worldCamera.orthographicSize * 2f / Mathf.Max(1, worldCamera.pixelHeight);
            lineBuilder.RemoveNear(position, radius);
        }
    }

    public BusStop CreateStop(Vector3Int cell)
    {
        if (!CanBuild)
            return null;
        if (placedStops.TryGetValue(cell, out BusStop existing))
            return existing;
        if (roadsTilemap == null || !roadsTilemap.HasTile(cell))
            return null;
        Vector3 center = roadsTilemap.GetCellCenterWorld(cell);
        GameObject obj;
        if (busStopPrefab != null)
            obj = Instantiate(busStopPrefab, center, Quaternion.identity, busStopsParent);
        else
        {
            obj = new GameObject("Parada");
            obj.transform.SetParent(busStopsParent, false);
            obj.transform.position = center;
        }

        obj.SetActive(true);
        BusStop stop = obj.GetComponent<BusStop>();
        if (stop == null)
            stop = obj.AddComponent<BusStop>();
        stop.Configure(cell, nextId++);
        obj.name = "Parada_" + stop.Label;
        placedStops.Add(cell, stop);
        stops.Add(stop);
        lineBuilder?.RefreshStops();
        return stop;
    }

    public bool TryGetStop(Vector3Int cell, out GameObject stop)
    {
        bool found = placedStops.TryGetValue(cell, out BusStop value) && value != null;
        stop = found ? value.gameObject : null;
        return found;
    }

    public BusStop StopAt(Vector3Int cell)
    {
        placedStops.TryGetValue(cell, out BusStop value);
        return value;
    }

    public bool RemoveStop(Vector3Int cell)
    {
        if (!CanBuild || !placedStops.TryGetValue(cell, out BusStop stop))
            return false;
        foreach (PassengerData passenger in stop.Waiting)
        {
            passenger.nextExit = null;
            passenger.journeyStarted = false;
            if (passenger.origin != null)
                passenger.origin.Pending.Add(passenger);
        }

        stop.Waiting.Clear();
        placedStops.Remove(cell);
        stops.Remove(stop);
        Destroy(stop.gameObject);
        lineBuilder?.RefreshStops();
        return true;
    }

    public void ClearStops()
    {
        foreach (BusStop stop in stops)
            if (stop != null)
                Destroy(stop.gameObject);
        stops.Clear();
        placedStops.Clear();
        nextId = 1;
        lineBuilder?.RefreshStops();
    }

    public void SetMode(int mode)
    {
        if (!CanBuild)
            return;
        ToolMode newMode = (ToolMode)Mathf.Clamp(mode, 0, 2);
        if (newMode != Mode)
        {
            drawingStroke = false;
            lineBuilder?.CancelLine();
        }

        Mode = newMode;
        placementEnabled = Mode == ToolMode.Stops;
    }

    public void SetPlacementEnabled(bool value) => SetMode(value ? 0 : 1);
    private void HidePreview()
    {
        if (cellPreview != null)
            cellPreview.enabled = false;
    }

    private void OnDisable()
    {
        drawingStroke = false;
        HidePreview();
    }
}
