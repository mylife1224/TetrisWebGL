using System.Collections.Generic;
using UnityEngine;

public enum TetrominoType { I, O, T, S, Z, J, L }

public class TetrisCore
{
    public const int Width = 10;
    public const int Height = 20;

    public int[,] Grid = new int[Width, Height];
    public TetrominoType CurrentType;
    public List<Vector2Int> CurrentCells;
    public Vector2Int CurrentPos;

    static readonly Dictionary<TetrominoType, Vector2Int[]> Shapes = new()
    {
        { TetrominoType.I, new[]{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0) } },
        { TetrominoType.O, new[]{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) } },
        { TetrominoType.T, new[]{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1) } },
        { TetrominoType.S, new[]{ new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(-1,1), new Vector2Int(0,1) } },
        { TetrominoType.Z, new[]{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(1,1) } },
        { TetrominoType.J, new[]{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1) } },
        { TetrominoType.L, new[]{ new Vector2Int(-1,0), new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(-1,1) } },
    };

    public static Vector2Int[] BaseCells(TetrominoType type) => Shapes[type];

    static readonly Vector2Int[] Kicks = new[]
    {
        new Vector2Int(0,0), new Vector2Int(-1,0), new Vector2Int(1,0),
        new Vector2Int(0,1), new Vector2Int(-1,1), new Vector2Int(1,1),
    };

    public void Spawn(TetrominoType type)
    {
        CurrentType = type;
        CurrentCells = new List<Vector2Int>(Shapes[type]);
        CurrentPos = new Vector2Int(Width / 2, Height - 2);
    }

    public bool TryMove(int dx, int dy)
    {
        var next = CurrentPos + new Vector2Int(dx, dy);
        if (IsValid(next, CurrentCells)) { CurrentPos = next; return true; }
        return false;
    }

    public bool StepDown() => TryMove(0, -1);

    public void HardDrop()
    {
        while (TryMove(0, -1)) { }
        LockPiece();
    }

    public bool TryRotate(int dir)
    {
        if (CurrentType == TetrominoType.O) return false; // O는 회전 불필요 (호출측 효과음 방지)
        var rotated = new List<Vector2Int>(CurrentCells.Count);
        foreach (var c in CurrentCells)
            rotated.Add(dir == 1 ? new Vector2Int(c.y, -c.x) : new Vector2Int(-c.y, c.x));
        foreach (var k in Kicks)
        {
            if (IsValid(CurrentPos + k, rotated))
            {
                CurrentCells = rotated;
                CurrentPos += k;
                return true;
            }
        }
        return false;
    }

    public bool IsValid(Vector2Int pos, List<Vector2Int> cells)
    {
        foreach (var c in cells)
        {
            int x = pos.x + c.x, y = pos.y + c.y;
            if (x < 0 || x >= Width || y < 0) return false;
            if (y < Height && Grid[x, y] != 0) return false;
        }
        return true;
    }

    public void LockPiece()
    {
        foreach (var c in CurrentCells)
        {
            int x = CurrentPos.x + c.x, y = CurrentPos.y + c.y;
            if (x >= 0 && x < Width && y >= 0 && y < Height)
                Grid[x, y] = (int)CurrentType + 1;
        }
    }

    public List<int> FindFullLines()
    {
        var lines = new List<int>();
        for (int y = 0; y < Height; y++)
        {
            bool full = true;
            for (int x = 0; x < Width; x++)
                if (Grid[x, y] == 0) { full = false; break; }
            if (full) lines.Add(y);
        }
        return lines;
    }

    public int ClearLines()
    {
        // 한 번에 압축 (오름차순逐行 삭제는 인접 줄에서 인덱스가 어긋나는 버그)
        bool[] full = new bool[Height];
        int count = 0;
        for (int y = 0; y < Height; y++)
        {
            bool f = true;
            for (int x = 0; x < Width; x++)
                if (Grid[x, y] == 0) { f = false; break; }
            full[y] = f;
            if (f) count++;
        }
        if (count == 0) return 0;
        int write = 0;
        for (int y = 0; y < Height; y++)
        {
            if (full[y]) continue;
            if (write != y)
                for (int x = 0; x < Width; x++) Grid[x, write] = Grid[x, y];
            write++;
        }
        for (int y = write; y < Height; y++)
            for (int x = 0; x < Width; x++) Grid[x, y] = 0;
        return count;
    }
}
