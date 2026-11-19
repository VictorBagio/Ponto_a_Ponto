using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RoadLineBuilder : MonoBehaviour
{
    [Serializable]
    public class LineData
    {
        public int id;
        public bool isLoop;
        public Color color;
        public List<Vector3Int> cells;
        public List<Vector3Int> stops;
        public LineRenderer drawing;
        [NonSerialized]
        private HashSet<Vector3Int> stopLookup;
        public string Label => "Linha " + id;
        public bool ContainsStop(Vector3Int cell)
        {
            if (stopLookup == null)
                stopLookup = new HashSet<Vector3Int>(stops);
            return stopLookup.Contains(cell);
        }

        public void SetStops(List<Vector3Int> value)
        {
            stops = value;
            stopLookup = new HashSet<Vector3Int>(value);
        }
    }

    [Header("Referencias")]
    [SerializeField]
    private Tilemap roadsTilemap;
    [SerializeField]
    private Material lineMaterial;
    [SerializeField]
    private Transform linesParent;
    [Header("Visual")]
    [SerializeField, Min(0.01f)]
    private float lineWidth = 0.09f;
    [SerializeField]
    private int sortingOrder = 3;
    [SerializeField]
    private Color[] lineColors = {new Color(0.15f, 0.6f, 1), new Color(1, 0.3f, 0.3f), new Color(1, 0.8f, 0.1f), new Color(0.65f, 0.35f, 1), new Color(0.15f, 0.85f, 0.6f), new Color(1, 0.4f, 0.75f)};
    [SerializeField]
    private List<LineData> lines = new List<LineData>();
    private readonly RoadRouteDraft draft = new RoadRouteDraft();
    private LineRenderer draftDrawing;
    private Predicate<Vector3Int> roadCheck;
    private int nextId = 1;
    public int Revision { get; private set; }

    public IReadOnlyList<LineData> Lines => lines;
    public Tilemap Roads => roadsTilemap;
    public bool HasDraft => draft.Cells.Count > 0;
    public bool DraftClosed => draft.IsClosed;
    private bool CanBuild => PontoGame.Instance == null || PontoGame.Instance.CanBuild;
    private void Notify(string text)
    {
        if (PontoGame.Instance != null)
            PontoGame.Instance.Notify(text);
        else
            Debug.Log(text);
    }

    public void ExtendLine(Vector3Int cell)
    {
        if (!CanBuild)
            return;
        if (roadsTilemap == null || lineMaterial == null || linesParent == null)
        {
            Notify("Configure as referencias de RoadLineBuilder.");
            return;
        }

        if (roadCheck == null)
            roadCheck = position => roadsTilemap.HasTile(position);
        if (!draft.ExtendTo(cell, roadCheck))
            return;
        DrawDraft();
        Notify(draft.IsClosed ? "Circuito fechado. Enter confirma; Backspace corrige." : "Desenhe pelas ruas e volte ao inicio. Backspace desfaz; Esc cancela.");
    }

    public void AddStop(Vector3Int cell) => ExtendLine(cell);
    public void UndoLastCell()
    {
        if (!CanBuild || !draft.Undo())
            return;
        if (!HasDraft)
            CancelLine();
        else
            DrawDraft();
    }

    private Color CurrentColor() => lineColors == null || lineColors.Length == 0 ? Color.cyan : lineColors[(nextId - 1) % lineColors.Length];
    private LineRenderer MakeDrawing()
    {
        var obj = new GameObject("Linha_em_criacao");
        obj.transform.SetParent(linesParent, false);
        var renderer = obj.AddComponent<LineRenderer>();
        renderer.sharedMaterial = lineMaterial;
        renderer.useWorldSpace = true;
        renderer.loop = false;
        renderer.startWidth = renderer.endWidth = lineWidth;
        renderer.numCornerVertices = renderer.numCapVertices = 0;
        var tiles = roadsTilemap.GetComponent<TilemapRenderer>();
        if (tiles != null)
            renderer.sortingLayerID = tiles.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void DrawDraft()
    {
        if (draftDrawing == null)
            draftDrawing = MakeDrawing();
        Color color = CurrentColor();
        color.a = 0.6f;
        draftDrawing.startColor = draftDrawing.endColor = color;
        draftDrawing.positionCount = draft.Cells.Count;
        for (int i = 0; i < draft.Cells.Count; i++)
            draftDrawing.SetPosition(i, roadsTilemap.GetCellCenterWorld(draft.Cells[i]));
    }

    private List<Vector3Int> FindStops(IReadOnlyList<Vector3Int> cells)
    {
        var found = new List<Vector3Int>();
        var visited = new HashSet<Vector3Int>();
        var selector = PontoGame.Instance != null ? PontoGame.Instance.selector : null;
        if (selector == null)
            return found;
        for (int i = 0; i < cells.Count - 1; i++)
            if (selector.StopAt(cells[i]) != null && visited.Add(cells[i]))
                found.Add(cells[i]);
        return found;
    }

    public void ConfirmLine()
    {
        if (!CanBuild)
            return;
        if (!draft.IsClosed)
        {
            bool atStart = draft.Cells.Count > 1 && draft.Cells[0] == draft.Cells[draft.Cells.Count - 1];
            Notify(atStart ? "O fechamento faria um retorno. Continue o desenho ou arraste para tras para corrigir." : "Feche o desenho voltando a celula inicial antes de confirmar.");
            return;
        }

        List<Vector3Int> stops = FindStops(draft.Cells);
        if (stops.Count < 2)
        {
            Notify("O circuito precisa passar por pelo menos duas paradas.");
            return;
        }

        var line = new LineData{id = nextId++, isLoop = true, color = draftDrawing.startColor, cells = new List<Vector3Int>(draft.Cells), drawing = draftDrawing};
        line.SetStops(stops);
        Color color = line.color;
        color.a = 1f;
        line.color = color;
        line.drawing.startColor = line.drawing.endColor = color;
        line.drawing.name = line.Label;
        lines.Add(line);
        Revision++;
        draftDrawing = null;
        draft.Clear();
        PontoGame.Instance?.fleet.AssignAvailableTo(line);
        Notify(line.Label + " criada com " + stops.Count + " paradas automaticas.");
    }

    public void RefreshStops()
    {
        foreach (var line in lines)
            line.SetStops(FindStops(line.cells));
        Revision++;
    }

    public void CancelLine()
    {
        if (draftDrawing != null)
            Destroy(draftDrawing.gameObject);
        draftDrawing = null;
        draft.Clear();
    }

    public bool RemoveLine(LineData line)
    {
        if (!CanBuild || line == null || !lines.Remove(line))
            return false;
        CancelLine();
        PontoGame.Instance?.fleet.ReleaseLine(line);
        if (line.drawing != null)
            Destroy(line.drawing.gameObject);
        Revision++;
        return true;
    }

    public void RemoveLinesUsingStop(Vector3Int cell)
    {
        for (int i = lines.Count - 1; i >= 0; i--)
            if (lines[i].ContainsStop(cell))
                RemoveLine(lines[i]);
    }

    public void ClearLines()
    {
        if (!CanBuild)
            return;
        CancelLine();
        while (lines.Count > 0)
            RemoveLine(lines[lines.Count - 1]);
        nextId = 1;
    }

    public void RemoveNear(Vector3 position, float radius)
    {
        if (!CanBuild)
            return;
        LineData closest = null;
        float best = radius * radius;
        foreach (var line in lines)
        {
            if (line.drawing == null)
                continue;
            for (int i = 0; i < line.drawing.positionCount - 1; i++)
            {
                Vector2 a = line.drawing.GetPosition(i), b = line.drawing.GetPosition(i + 1);
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude < 0.000001f ? 0f : Mathf.Clamp01(Vector2.Dot((Vector2)position - a, ab) / ab.sqrMagnitude);
                float distance = ((Vector2)position - (a + ab * t)).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    closest = line;
                }
            }
        }

        if (closest != null)
        {
            RemoveLine(closest);
            Notify("Linha removida. Onibus devolvidos a garagem.");
        }
    }
}
