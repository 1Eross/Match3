namespace Match3Easter.Models;

public struct Match
{
    public int StartX, StartY;
    public int Length;
    public bool IsHorizontal;

    public IEnumerable<(int X, int Y)> GetCells()
    {
        for (var i = 0; i < Length; i++)
        {
            if (IsHorizontal)
                yield return (StartX + i, StartY);
            else
                yield return (StartX, StartY + i);
        }
    }

    public bool Intersects(Match other)
    {
        if (IsHorizontal == other.IsHorizontal) return false;
        var h = IsHorizontal ? this : other;
        var v = IsHorizontal ? other : this;

        // Vertical lays in horizontal run: Check by X
        // Horizontal lays in vertical run: Check by Y
        return v.StartX >= h.StartX && v.StartX < h.StartX + h.Length &&
               h.StartY >= v.StartY && h.StartY < v.StartY + v.Length;
    }
}