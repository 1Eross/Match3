using System.Diagnostics.Tracing;

namespace Match3Easter;

// TODO: Add metaclass BoardObject and inherit Gem from it 
public class Board
{
    /// <summary>
    /// Board of the game
    /// Size XSize x YSize = 9x9
    /// Grid presented by 1-D array,
    /// where cols presented as plain sequence
    /// but rows presented as y + x * YSize
    /// </summary>
    private const int XSize = 9;

    private const int YSize = 9;
    private static readonly Random _random = new Random();

    private Gem?[] _grid = new Gem [XSize * XSize];

    private Stack<Gem.GemType> _excludeList = new Stack<Gem.GemType>();

    private bool[] _dirtyRows = new bool[YSize]; // Rows Iterates over col (y)
    private bool[] _dirtyCols = new bool[XSize]; // Col Iterates over row (x)

    // CAN STORE (IDX, LAST_SEEN_NONE) TO AVOID GO THROUGH XSize
    private bool[] _hasMatchCol = new bool[XSize]; // Col Iterates over X
    private int[] _lastNoneInCol = new int[XSize]; // Col Iterates over X

    private HashSet<(int, int)> _matches = new HashSet<(int, int)>(); // (y, x)

    private Stack<Gem> _freeGems = new Stack<Gem>();

    private Gem? get_tile(int x, int y) => _grid[y + (x * YSize)];
    private void set_tile(int x, int y, Gem? gem) => _grid[y + (x * YSize)] = gem;

    // private Span<Gem?> get_col_span(int x, int y, int lenght) => _grid.AsSpan(get_tile_idx(x, y), lenght);
    private static int get_tile_idx(int x, int y) => y + (x * YSize);

    private void fill_grid_random()
        // Simple DP fill with one above and one right exclude; Probably I can optimize this
        // not to call ifExistingMatch function -> Reshuffle
    {
        for (int i = 0; i < XSize; i++)
        for (int j = 0; j < YSize; j++)
        {
            if (i == 0 && j == 0)
            {
                _grid[get_tile_idx(i, j)] = new Gem(Gem.GetRandomGemType(exclude: _excludeList, _random));
            }

            if (i == 0 && j != 0)
            {
                _excludeList.Push(get_tile(i, j - 1)!._getGemType());

                _grid[get_tile_idx(i, j)] = new Gem(Gem.GetRandomGemType(exclude: _excludeList, _random));

                _excludeList.Pop();
            }

            if (i != 0 && j == 0)
            {
                _excludeList.Push(get_tile(i - 1, j)!._getGemType());

                _grid[get_tile_idx(i, j)] = new Gem(Gem.GetRandomGemType(exclude: _excludeList, _random));

                _excludeList.Pop();
            }

            if (i == 0 || j == 0) continue;

            _excludeList.Push(get_tile(i - 1, j)!._getGemType());
            _excludeList.Push(get_tile(i, j - 1)!._getGemType());

            _grid[get_tile_idx(i, j)] = new Gem(Gem.GetRandomGemType(exclude: _excludeList, _random));

            _excludeList.Pop();
            _excludeList.Pop();
        }
    }

    public Board()
    {
        fill_grid_random();
    }

    private bool IfSwappable(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);
        return (dx + dy) == 1;
    }

    private void MarkDirty(int x, int y)
    {
        _dirtyRows[y] = true;
        _dirtyCols[x] = true;
    }

    private void ClearDirtyFlags()
    {
        for (var j = 0; j < XSize; j++)
            _dirtyRows[j] = false;
        for (var i = 0; i < YSize; i++)
            _dirtyCols[i] = false;
    }

    private void ClearMatchSet()
    {
        _matches.Clear();
    }

    private void ClearMatchesFlags()
    {
        for (var i = 0; i < YSize; i++)
            _hasMatchCol[i] = false;
    }

    private void ClearLastNoneIds()
    {
        for (var i = 0; i < YSize; i++)
            _lastNoneInCol[i] = -1;
    }

    private void Swap(int x1, int y1, int x2, int y2)
    {
        var temp = get_tile(x1, y1)!;

        set_tile(x1, y1, get_tile(x2, y2)!);
        set_tile(x2, y2, temp);
    }

    public bool TrySwap(int x1, int y1, int x2, int y2)
    {
        if (!IfSwappable(x1, y1, x2, y2)) return false;

        Swap(x1, y1, x2, y2);
        MarkDirty(x1, y1);
        MarkDirty(x2, y2);
        return true;
    }

    public void SwapBack(int x1, int y1, int x2, int y2)
    {
        Swap(x1, y1, x2, y2);
    }

    private void ScanCol(int x)
    {
        var count = 1;
        var type = get_tile(x, 0)!._getGemType();

        for (var j = 1; j < YSize; j++)
        {
            var jType = get_tile(x, j)!._getGemType();
            if (jType == type) count++;
            else
            {
                type = jType;
                count = 1;
            }

            switch (count)
            {
                case 3:
                    _matches.Add((x, j));
                    _matches.Add((x, j - 1));
                    _matches.Add((x, j - 2));
                    _hasMatchCol[x] = true;
                    break;
                case > 3:
                    _matches.Add((j, x));
                    break;
            }
        }
    }

    private void ScanRow(int y)
    {
        var count = 1;
        var type = get_tile(0, y)!._getGemType();

        for (var i = 1; i < XSize; i++)
        {
            var iType = get_tile(i, y)!._getGemType();
            if (iType == type) count++;
            else
            {
                type = iType;
                count = 1;
            }

            switch (count)
            {
                case 3:
                    _matches.Add((i, y));
                    _matches.Add((i - 1, y));
                    _matches.Add((i - 2, y));
                    _hasMatchCol[i] = true;
                    break;
                case > 3:
                    _matches.Add((i, y));
                    break;
            }
        }
    }

    public bool FindMatches()
    {
        _matches.Clear();

        for (var i = 0; i < XSize; i++)
            if (_dirtyCols[i])
                ScanCol(i);

        for (var j = 0; j < YSize; j++)
            if (_dirtyRows[j])
                ScanRow(j);

        return _matches.Count > 0;
    }


    private void CollectMatch(int x, int y)
    {
        _freeGems.Push(get_tile(x, y)!);
        set_tile(x, y, null);
        _dirtyCols[x] = true;
    }

    public void CollectMatches()
    {
        foreach (var (x, y) in _matches)
        {
            CollectMatch(x, y);
        }
    }

    // TODO: Lines need to fall together
    // Wanted to do with memmove (Spans) but it doesnt work
    private void FallCol(int x)
    {
        var writePos = YSize - 1;
        for (var j = YSize - 1; j >= 0; j--)
        {
            if (get_tile(x, j) is null) continue;

            set_tile(x, writePos, get_tile(x, j));
            if (writePos != j)
            {
                set_tile(x, j, null);
                _dirtyCols[x] = true;
                _dirtyRows[writePos] = true;
            }

            writePos--;
        }

        _lastNoneInCol[x] = writePos;
    }

    public void FallCols()
    {
        for (var i = 0; i < XSize; i++)
        {
            if (!_dirtyCols[i]) continue;
            FallCol(i);
        }
    }

    private void FillCol(int x)
    {
        for (var j = _lastNoneInCol[x]; j >= 0; j--)
        {
            set_tile(x, j, _freeGems.Pop());
            _dirtyRows[j] = true;
        }

        _dirtyCols[x] = true;
    }

    // CAN STORE (IDX, LAST_SEEN_NONE)
    public void FillCols()
    {
        for (var i = 0; i < XSize; i++)
        {
            if (!_dirtyCols[i]) continue;
            FillCol(i);
        }
    }

    public void ReshuffleFreeGems()
    {
        // Update all gems types
        foreach (var gem in _freeGems)
            gem._setGemType(Gem.GetRandomGemType([], _random));
    }

    public void ClearAll()
    {
        ClearDirtyFlags();
        ClearMatchSet();
        ClearLastNoneIds();
        ClearMatchesFlags();
    }


    public override string ToString()
    {
        var result = "";
        for (var j = -1; j < YSize; j++)
        {
            for (var i = -1; i < XSize; i++)
            {
                if (i == -1 && j == -1) result += $"  ";
                else if (i == -1 && j != -1) result += $"{j} ";
                else if (i != -1 && j == -1) result += $"{i} ";
                else
                {
                    var gem = get_tile(i, j);
                    if (gem is null)
                    {
                        result += $"X ";
                        continue;
                    }


                    result += $"{get_tile(i, j)} ";
                }
            }

            result += "\n";
        }

        return result;
    }
}