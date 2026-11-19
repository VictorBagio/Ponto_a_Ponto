using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(100)]
public class ParallelLineLayout : MonoBehaviour
{
    [SerializeField]
    private RoadLineBuilder lineBuilder;
    [SerializeField]
    private Tilemap roadsTilemap;
    [SerializeField, Min(0.01f)]
    private float lineWidth = 0.09f;
    [SerializeField, Min(0.01f)]
    private float laneSpacing = 0.16f;
    [SerializeField, Range(0.01f, 0.35f)]
    private float maxOffset = 0.28f;
    private int lastRevision = -1;
    private void LateUpdate()
    {
        if (lineBuilder == null || roadsTilemap == null)
            return;
        if (lastRevision != lineBuilder.Revision)
        {
            RebuildLayout();
            lastRevision = lineBuilder.Revision;
        }
    }

    private void OnValidate()
    {
        lastRevision = -1;
    }

    private static (Vector3Int, Vector3Int) EdgeKey(Vector3Int a, Vector3Int b)
    {
        return a.x < b.x || (a.x == b.x && a.y < b.y) ? (a, b) : (b, a);
    }

    private void RebuildLayout()
    {
        var lines = lineBuilder.Lines;
        var users = new Dictionary<(Vector3Int, Vector3Int), List<int>>();
        int maximum = 1;
        for (int l = 0; l < lines.Count; l++)
        {
            var cells = lines[l].cells;
            for (int i = 0; i < cells.Count - 1; i++)
            {
                var key = EdgeKey(cells[i], cells[i + 1]);
                if (!users.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    users.Add(key, list);
                }

                if (!list.Contains(l))
                    list.Add(l);
                maximum = Mathf.Max(maximum, list.Count);
            }
        }

        Vector3 origin = roadsTilemap.GetCellCenterWorld(Vector3Int.zero);
        float size = Mathf.Min(Vector3.Distance(origin, roadsTilemap.GetCellCenterWorld(Vector3Int.up)), Vector3.Distance(origin, roadsTilemap.GetCellCenterWorld(Vector3Int.right)));
        float spacing = laneSpacing * size, width = lineWidth * size;
        if (maximum > 1)
        {
            spacing = Mathf.Min(spacing, 2 * maxOffset * size / (maximum - 1));
            width = Mathf.Min(width, spacing * 0.75f);
        }

        for (int l = 0; l < lines.Count; l++)
        {
            var line = lines[l];
            var cells = line.cells;
            if (line.drawing == null || cells.Count < 2)
                continue;
            var shifts = new Vector3[cells.Count - 1];
            for (int i = 0; i < shifts.Length; i++)
            {
                var key = EdgeKey(cells[i], cells[i + 1]);
                var list = users[key];
                float offset = (list.IndexOf(l) - (list.Count - 1) * 0.5f) * spacing;
                Vector3 d = (roadsTilemap.GetCellCenterWorld(key.Item2) - roadsTilemap.GetCellCenterWorld(key.Item1)).normalized;
                shifts[i] = new Vector3(-d.y, d.x, 0) * offset;
            }

            var positions = new Vector3[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3 shift;
                if (line.isLoop && (i == 0 || i == cells.Count - 1))
                {
                    Vector3Int a = cells[0] - cells[cells.Count - 2], b = cells[1] - cells[0];
                    shift = (shifts[shifts.Length - 1] + shifts[0]) * (a.x * b.x + a.y * b.y == 0 ? 1f : 0.5f);
                }
                else if (i == 0)
                    shift = shifts[0];
                else if (i == cells.Count - 1)
                    shift = shifts[shifts.Length - 1];
                else
                {
                    Vector3Int a = cells[i] - cells[i - 1], b = cells[i + 1] - cells[i];
                    shift = (shifts[i - 1] + shifts[i]) * (a.x * b.x + a.y * b.y == 0 ? 1f : 0.5f);
                }

                positions[i] = roadsTilemap.GetCellCenterWorld(cells[i]) + shift;
            }

            line.drawing.startWidth = line.drawing.endWidth = width;
            line.drawing.positionCount = positions.Length;
            line.drawing.SetPositions(positions);
        }
    }
}
