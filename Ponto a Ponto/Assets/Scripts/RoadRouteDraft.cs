using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoadRouteDraft
{
    private readonly List<Vector3Int> cells = new List<Vector3Int>();
    public IReadOnlyList<Vector3Int> Cells => cells;
    public bool IsClosed => cells.Count >= 5 && cells[0] == cells[cells.Count - 1];
    public void Clear()
    {
        cells.Clear();
    }

    public bool Undo()
    {
        if (cells.Count == 0)
            return false;
        cells.RemoveAt(cells.Count - 1);
        return true;
    }

    public bool ExtendTo(Vector3Int target, Predicate<Vector3Int> isRoad)
    {
        target.z = 0;
        if (isRoad == null || !isRoad(target))
            return false;
        if (cells.Count == 0)
        {
            cells.Add(target);
            return true;
        }

        Vector3Int start = cells[cells.Count - 1];
        int dx = Math.Abs(target.x - start.x);
        int dy = Math.Abs(target.y - start.y);
        int sx = Math.Sign(target.x - start.x);
        int sy = Math.Sign(target.y - start.y);
        int movedX = 0;
        int movedY = 0;
        bool changed = false;
        Vector3Int current = start;
        while (movedX < dx || movedY < dy)
        {
            bool stepX = movedX < dx && (movedY == dy || (2L * movedX + 1) * dy <= (2L * movedY + 1) * dx);
            Vector3Int next = current + (stepX ? new Vector3Int(sx, 0, 0) : new Vector3Int(0, sy, 0));
            if (!TryAppend(next, isRoad))
                break;
            changed = true;
            current = next;
            if (stepX)
                movedX++;
            else
                movedY++;
        }

        return changed;
    }

    private bool TryAppend(Vector3Int next, Predicate<Vector3Int> isRoad)
    {
        if (!isRoad(next))
            return false;
        Vector3Int last = cells[cells.Count - 1];
        int distance = Math.Abs(next.x - last.x) + Math.Abs(next.y - last.y);
        if (distance != 1)
            return false;
        if (cells.Count >= 2 && next == cells[cells.Count - 2])
            return Undo();
        cells.Add(next);
        return true;
    }
}
