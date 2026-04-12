using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using Match3Easter.Models;
using Match3Easter.Tools;

namespace Match3Easter;

public class Board
{
    /// <summary>
    /// Board of the game
    /// Size XSize x YSize = 9x9
    /// Grid presented by 1-D array,
    /// where cols presented as plain sequence
    /// but rows presented as y + x * YSize
    /// </summary>
    ///
    private const int XSize = 8;

    private const int YSize = 8;

    private readonly Gem?[] _grid = new Gem [XSize * XSize];

    // Board fill
    private static readonly Random Random = new Random();
    private readonly Stack<GemType> _excludeList = []; // Keep GemType.None after grid generation

    // Auxiliary arrays
    private readonly bool[] _dirtyRows = new bool[YSize]; // Rows Iterates over col (y)
    private readonly bool[] _dirtyCols = new bool[XSize]; // Col Iterates over row (x)
    private readonly bool[] _hasMatchCol = new bool[XSize]; // Check if col have match, to fall
    private readonly int[] _lastNoneInCol = Enumerable.Repeat(-1, XSize).ToArray(); // Col Iterates over X
    // ↑ CAN STORE (IDX, LAST_SEEN_NONE) TO AVOID GO THROUGH XSize

    // Match
    private readonly HashSet<(int, int)> _protectedForRemoval = []; // (y, x)
    private readonly Stack<Gem> _freeGems = [];
    private readonly List<Match> _foundMatches = [];
    private readonly Dictionary<int, List<int>> _unionGroups = new();

    //Destruction
    private readonly Queue<DestructionEvent> _destructionEventQueue = new(); // (x, y)
    private readonly HashSet<(int, int)> _scheduleForRemoval = [];

    // Score
    private int _score = 0;

// Getters Setters
    public int GetXSize() => XSize;
    public int GetYSize() => YSize;

    private static int _GetGemIdx(int x, int y) => y + (x * YSize);
    public Gem? GetGem(int x, int y) => _grid[y + (x * YSize)];
    private void _SetGem(int x, int y, Gem? gem) => _grid[y + (x * YSize)] = gem;

    public bool InBounds(int x, int y) => x is >= 0 and < XSize && y is >= 0 and < YSize;

    public bool IsProtectedForRemoval(int x, int y) => _protectedForRemoval.Contains((x, y));

    public bool TryDequeueDestructionEvent([MaybeNullWhen(false)] out DestructionEvent result) =>
        _destructionEventQueue.TryDequeue(out result);

    public int GetScore() => _score;

// private Span<Gem?> get_col_span(int x, int y, int lenght) => _grid.AsSpan(get_tile_idx(x, y), lenght);

    private void fill_grid_random()
        // Simple DP fill with one above and one right exclude; Probably I can optimize this
        // not to call ifExistingMatch function -> Reshuffle
    {
        _excludeList.Push(GemType.None);
        for (var i = 0; i < XSize; i++)
        for (var j = 0; j < YSize; j++)
        {
            if (i == 0 && j == 0)
            {
                _grid[_GetGemIdx(i, j)] = new Gem(Gem.GetRandomGemType(exclude: _excludeList, Random));
            }

            if (i == 0 && j != 0)
            {
                _excludeList.Push(GetGem(i, j - 1)!._getGemType());

                _grid[_GetGemIdx(i, j)] = new Gem(Gem.GetRandomGemType(exclude: _excludeList, Random));

                _excludeList.Pop();
            }

            if (i != 0 && j == 0)
            {
                _excludeList.Push(GetGem(i - 1, j)!._getGemType());

                _grid[_GetGemIdx(i, j)] = new Gem(Gem.GetRandomGemType(exclude: _excludeList, Random));

                _excludeList.Pop();
            }

            if (i == 0 || j == 0) continue;

            _excludeList.Push(GetGem(i - 1, j)!._getGemType());
            _excludeList.Push(GetGem(i, j - 1)!._getGemType());

            _grid[_GetGemIdx(i, j)] = new Gem(Gem.GetRandomGemType(exclude: _excludeList, Random));

            _excludeList.Pop();
            _excludeList.Pop();
        }

        _excludeList.Clear();
    }

    public Board()
    {
        fill_grid_random();
        _excludeList.Push(GemType.None);
    }

// Dirty
    public void MarkDirty(int x, int y)
    {
        _dirtyRows[y] = true;
        _dirtyCols[x] = true;
    }

// Swap

    public static bool IsSwappable(int x1, int y1, int x2, int y2)
    {
        if (!(x1 is >= 0 and < XSize &&
              y1 is >= 0 and < YSize &&
              x2 is >= 0 and < XSize &&
              y2 is >= 0 and < YSize)
           ) return false;


        var dx = Math.Abs(x1 - x2);
        var dy = Math.Abs(y1 - y2);
        return (dx + dy) == 1;
    }

    public void Swap(int x1, int y1, int x2, int y2) // Always move from x1,y1 to x2,y2
    {
        var gem1 = GetGem(x1, y1)!;

        _SetGem(x1, y1, GetGem(x2, y2)!);
        _SetGem(x2, y2, gem1);
    }

    public Action<int, int, float>? ResolveBonusActivator(BonusType bonusType)
    {
        return (bonusType) switch
        {
            (BonusType.TripleLineHorizontal) => ActivateTripleHorizontalLines,
            (BonusType.TripleLineVertical) => ActivateTripleVerticalLines,
            (BonusType.Cross) => ActivateCross,
            (BonusType.LineHorizontal) => ActivateHorizontalLine,
            (BonusType.LineVertical) => ActivateVerticalLine,
            (BonusType.Bomb) => ActivateBomb,
            (BonusType.DoubleBomb) => ActivateDoubleBomb,
            (BonusType.None) => null
        };
    }

// Bonus activation
    private void ActivateBonus()
    {
    }

    private void ActivateCross(int x1, int y1, float baseTime)
    {
        // On axis where Line moves -> should explode cross
        MarkLineCross(x1, y1, x1, y1, 0f); // Self el on x1,y1

        for (var i = x1 + 1; i <= XSize; i++)
            MarkLineHorizontal(i, y1, x1, y1, baseTime);
        for (var i = x1 - 1; i >= 0; i--)
            MarkLineHorizontal(i, y1, x1, y1, baseTime);
        for (var j = y1 + 1; j <= YSize; j++)
            MarkLineVertical(x1, j, x1, y1, baseTime);
        for (var j = y1 - 1; j >= 0; j--)
            MarkLineVertical(x1, j, x1, y1, baseTime);
    }

// TODO: CHECK CORRECTNESS
    private void ActivateTripleHorizontalLines(int x1, int y1, float baseTime)
    {
        // On axis x where goes Line -> should mark y + 1 / y / y - 1 
        // On axis which from Line will be included in destroy area

        for (var j = y1 - 1; j <= y1 + 1; j++)
        {
            if (!InBounds(x1, j)) continue;
            MarkLineHorizontal(x1, j, x1, j, 0f);

            for (var i = x1 + 1; i <= XSize; i++)
                MarkLineHorizontal(i, j, x1, j, baseTime);
            for (var i = x1 - 1; i >= 0; i--)
                MarkLineHorizontal(i, j, x1, j, baseTime);
        }
    }

// TODO: CHECK CORRECTNESS
    private void ActivateTripleVerticalLines(int x1, int y1, float baseTime)
    {
        // On axis y where goes Line -> should mark x - 1 / x / x + 1 
        // On axis which from Line will be included in destroy area 
        for (var i = x1 - 1; i <= x1 + 1; i++)
        {
            if (!InBounds(x1, i)) continue;
            MarkLineVertical(i, y1, i, y1, 0f);

            for (var j = y1 + 1; j <= YSize; j++)
                MarkLineVertical(i, j, i, y1, baseTime);
            for (var j = y1 - 1; j >= 0; j--)
                MarkLineVertical(i, j, i, y1, baseTime);
        }
    }

// TODO: CHECK CORRECTNESS
    private void ActivateHorizontalLine(int x1, int y1, float baseTime)
    {
        // On axis where goes Line -> should mark all line on deletion
        // On axis where goes Simple gem -> should mark dirty if it not in destroy are
        MarkLineHorizontal(x1, y1, x1, y1, 0f);
        for (var i = x1 + 1; i <= XSize; i++)
            MarkLineHorizontal(i, y1, x1, y1, baseTime);
        for (var i = x1 - 1; i >= 0; i--)
            MarkLineHorizontal(i, y1, x1, y1, baseTime);
    }

// TODO: CHECK CORRECTNESS
    private void ActivateVerticalLine(int x1, int y1, float baseTime)
    {
        // On axis where goes Line -> should mark all line on deletion
        // On axis where goes Simple gem -> should mark dirty if it not in destroy are
        MarkLineVertical(x1, y1, x1, y1, 0);

        for (var j = y1 + 1; j <= YSize; j++)
            MarkLineVertical(x1, j, x1, y1, baseTime);
        for (var j = y1 - 1; j >= 0; j--)
            MarkLineVertical(x1, j, x1, y1, baseTime);
    }

// TODO: CHECK CORRECTNESS
    private void ActivateBomb(int x1, int y1, float baseTime)
    {
        // On axis where goes Bomb -> should mark all in Square 3x3 on deletion
        // On axis where goes Simple gem -> should mark dirty

        MarkBomb(x1, y1, x1, y1, 0);

        for (var i = x1 - 1; i <= x1 + 1; i++)
        for (var j = y1 - 1; j <= y1 + 1; j++)
        {
            if (i == x1 && j == y1) continue;
            if (!InBounds(i, j)) continue;
            MarkBomb(i, j, x1, y1, baseTime);
        }
    }


    private void ActivateDoubleBomb(int x1, int y1, float baseTime)
    {
        MarkDoubleBomb(x1, y1, x1, y1, 0);

        for (var i = x1 - 2; i <= x1 + 2; i++)
        for (var j = y1 - 2; j <= y1 + 2; j++)
        {
            if (i == x1 && j == y1) continue;
            if (!InBounds(i, j)) continue;
            MarkDoubleBomb(i, j, x1, y1, baseTime);
        }
    }

    public void MergeBonus(int x1, int y1, int x2, int y2)
    {
        var gem1 = GetGem(x1, y1)!;
        var gem2 = GetGem(x2, y2)!;

        var gem2BonusType = ResolveBonusType(gem1._getBonusType(), gem2._getBonusType());
        gem2._setBonusType(gem2BonusType);


        _SetGem(x1, y1, null);
        _freeGems.Push(gem1);
    }

    private void MarkDoubleBomb(int x1, int y1, int xOrigin, int yOrigin, float baseTime = 0f)
    {
        _scheduleForRemoval.Add((x1, y1));
        _destructionEventQueue.Enqueue(new DestructionEvent
        {
            X = x1, Y = y1, Cause = DestructionCause.DoubleBomb, XOrigin = xOrigin, YOrigin = yOrigin,
            BaseTime = baseTime
        });
    }

    private void MarkBomb(int x1, int y1, int xOrigin, int yOrigin, float baseTime = 0f)
    {
        _scheduleForRemoval.Add((x1, y1));
        _destructionEventQueue.Enqueue(new DestructionEvent
        {
            X = x1, Y = y1, Cause = DestructionCause.Bomb, XOrigin = xOrigin, YOrigin = yOrigin,
            BaseTime = baseTime
        });
    }

    private void MarkLineHorizontal(int x1, int y1, int xOrigin, int yOrigin, float baseTime = 0f)
    {
        if (!InBounds(x1, y1)) return;
        _scheduleForRemoval.Add((x1, y1));
        _destructionEventQueue.Enqueue(new DestructionEvent
        {
            X = x1, Y = y1, Cause = DestructionCause.LineHorizontal, XOrigin = xOrigin, YOrigin = yOrigin,
            BaseTime = baseTime
        });
    }

    private void MarkLineVertical(int x1, int y1, int xOrigin, int yOrigin, float baseTime = 0f)
    {
        if (!InBounds(x1, y1)) return;
        _scheduleForRemoval.Add((x1, y1));
        _destructionEventQueue.Enqueue(new DestructionEvent
        {
            X = x1, Y = y1, Cause = DestructionCause.LineVertical, XOrigin = xOrigin, YOrigin = yOrigin,
            BaseTime = baseTime
        });
    }

    private void MarkLineCross(int x1, int y1, int xOrigin, int yOrigin, float baseTime = 0f)
    {
        if (!InBounds(x1, y1)) return;
        _scheduleForRemoval.Add((x1, y1));
        _destructionEventQueue.Enqueue(new DestructionEvent
        {
            X = x1, Y = y1, Cause = DestructionCause.LineCross, XOrigin = xOrigin, YOrigin = yOrigin,
            BaseTime = baseTime
        });
    }

    // BONUSES

    public BonusType ResolveBonusType(BonusType bonusType1, BonusType bonusType2 = BonusType.None)
    {
        return (bonusType1, bonusType2) switch
        {
            (BonusType.Bomb, BonusType.LineHorizontal) or
                (BonusType.LineHorizontal, BonusType.Bomb) or
                (BonusType.TripleLineVertical, BonusType.None) or
                (BonusType.None, BonusType.TripleLineVertical) =>
                BonusType.TripleLineHorizontal,

            (BonusType.Bomb, BonusType.LineVertical) or
                (BonusType.LineVertical, BonusType.Bomb) or
                (BonusType.TripleLineHorizontal, BonusType.None) or
                (BonusType.None, BonusType.TripleLineHorizontal) =>
                BonusType.TripleLineVertical,

            (BonusType.LineVertical, BonusType.LineHorizontal) or
                (BonusType.LineHorizontal, BonusType.LineVertical) or
                (BonusType.LineVertical, BonusType.LineVertical) or
                (BonusType.LineHorizontal, BonusType.LineHorizontal) or
                (BonusType.Cross, BonusType.None) or
                (BonusType.None, BonusType.Cross) =>
                BonusType.Cross,


            (BonusType.None, BonusType.LineHorizontal) or
                (BonusType.LineHorizontal, BonusType.None) =>
                BonusType.LineHorizontal,


            (BonusType.None, BonusType.LineVertical) or
                (BonusType.LineVertical, BonusType.None) =>
                BonusType.LineVertical,


            (BonusType.None, BonusType.Bomb) or
                (BonusType.Bomb, BonusType.None) =>
                BonusType.Bomb,

            (BonusType.Bomb, BonusType.Bomb) or
                (BonusType.DoubleBomb, BonusType.None) or
                (BonusType.None, BonusType.DoubleBomb) =>
                BonusType.DoubleBomb,

            (_, _) => BonusType.None
        };
    }

    // Double passthrough, can make all in one method
    public List<(int toX, int toY, int fromX, int fromY)> CalcBonusAnimPositions(int swapX1, int swapY1, int swapX2,
        int swapY2)
    {
        var result = new List<(int x, int y, int fromX, int fromY)>();

        foreach (var indices in _unionGroups.Values)
        {
            var matchGroup = indices.Select(m => _foundMatches[m]).ToList();

            (int x, int y)? bonusPos;

            if (matchGroup.Count > 1)
            {
                bonusPos = FindIntersections(matchGroup);
                if (bonusPos == null) continue;
            }
            else
            {
                var match = matchGroup[0];
                if (match.Length < 4) continue;

                // Бонус вместо перемещённого гема, иначе центр матча
                bonusPos = null;
                foreach (var cell in match.GetCells())
                {
                    if (cell == (swapX1, swapY1) || cell == (swapX2, swapY2))
                    {
                        bonusPos = cell;
                        break;
                    }
                }

                bonusPos ??= (match.StartX + (match.IsHorizontal ? match.Length / 2 : 0),
                    match.StartY + (match.IsHorizontal ? 0 : match.Length / 2));
            }

            var (bx, by) = bonusPos.Value;

            foreach (var match in matchGroup)
            foreach (var cell in match.GetCells())
            {
                result.Add((bx, by, cell.X, cell.Y));
            }
        }

        return result;
    }

    public bool HasBonusMatches()
    {
        return _unionGroups.Values.Any(m => m.Count > 1 ||
                                            _foundMatches[m[0]].Length > 0);
    }

    public (int x, int y)? FindIntersections(List<Match> intersections)
    {
        foreach (var h in intersections.Where(m => m.IsHorizontal))
        foreach (var v in intersections.Where(m => !m.IsHorizontal))
        {
            if (h.Intersects(v))
            {
                return (v.StartX, h.StartY);
            }
        }

        return null;
    }

    public void CreateBonuses(int swapX1, int swapY1, int swapX2, int swapY2)
    {
        foreach (var indices in _unionGroups.Values)
        {
            var matches = indices.Select(i => _foundMatches[i]).ToList();
            CreateBonus(matches, swapX1, swapY1, swapX2, swapY2);
        }
    }


    public void CreateBonus(List<Match> matchGroup, int swapX1, int swapY1, int swapX2, int swapY2)
    {
        (int x, int y)? bonusPos;
        BonusType bonusType;

        if (matchGroup.Count > 1)
        {
            bonusPos = FindIntersections(matchGroup);
            if (bonusPos == null) return;
            bonusType = BonusType.Bomb;
        }
        else
        {
            var match = matchGroup[0];
            if (match.Length < 4) return;

            // Бонус вместо перемещённого гема, иначе центр матча
            bonusPos = null;
            foreach (var cell in match.GetCells())
            {
                if (cell == (swapX1, swapY1) || cell == (swapX2, swapY2))
                {
                    bonusPos = cell;
                    break;
                }
            }

            bonusPos ??= (match.StartX + (match.IsHorizontal ? match.Length / 2 : 0),
                match.StartY + (match.IsHorizontal ? 0 : match.Length / 2));

            bonusType = match.Length switch
            {
                4 => match.IsHorizontal ? BonusType.LineHorizontal : BonusType.LineVertical,
                >= 5 => BonusType.Bomb,
                _ => BonusType.None
            };
        }

        var (bx, by) = bonusPos.Value;
        var gem = GetGem(bx, by)!;
        gem._setBonusType(bonusType);
        _protectedForRemoval.Add((bx, by));
    }


    public void FindUnions()
    {
        _unionGroups.Clear();

        var unionFind = new UnionFind(_foundMatches.Count);

        for (var i = 0; i < _foundMatches.Count; i++)
        for (var j = i + 1; j < _foundMatches.Count; j++)
        {
            if (_foundMatches[i].Intersects(_foundMatches[j]))
            {
                unionFind.Union(i, j);
            }
        }

        for (var i = 0; i < _foundMatches.Count; i++)
        {
            var root = unionFind.Find(i);
            if (!_unionGroups.ContainsKey(root)) _unionGroups[root] = [];
            _unionGroups[root].Add(i);
        }
    }

// Finding Matches

    private void ScanCol(int x)
    {
        var count = 1;
        var type = GetGem(x, 0)!._getGemType();

        for (var y = 1; y < YSize; y++)
        {
            var gem = GetGem(x, y);
            if (gem is null) continue;

            var jType = gem._getGemType();
            if (jType == type) count++;
            else
            {
                if (count >= 3)
                {
                    _foundMatches.Add(new Match
                        { StartX = x, StartY = y - count, Length = count, IsHorizontal = false });
                    _hasMatchCol[x] = true;
                }

                type = jType;
                count = 1;
            }
        }

        if (count >= 3)
        {
            _foundMatches.Add(new Match
                { StartX = x, StartY = YSize - count, Length = count, IsHorizontal = false });
            _hasMatchCol[x] = true;
        }
    }

    private void ScanRow(int y)
    {
        var count = 1;

        var type = GetGem(0, y)!._getGemType();

        for (var x = 1; x < XSize; x++)
        {
            var gem = GetGem(x, y);
            if (gem is null) continue;

            var iType = gem._getGemType();
            if (iType == type) count++;
            else
            {
                if (count >= 3)
                {
                    _foundMatches.Add(new Match
                        { StartX = x - count, StartY = y, Length = count, IsHorizontal = true });
                    for (var i = x - count; i <= x; i++) _hasMatchCol[i] = true;
                }

                type = iType;
                count = 1;
            }
        }

        if (count >= 3)
        {
            _foundMatches.Add(new Match { StartX = XSize - count, StartY = y, Length = count, IsHorizontal = true });
            for (var i = XSize - count; i < XSize; i++) _hasMatchCol[i] = true;
        }
    }

    public bool FindMatches()
    {
        _foundMatches.Clear();

        for (var i = 0; i < XSize; i++)
            if (_dirtyCols[i])
                ScanCol(i);

        for (var j = 0; j < YSize; j++)
            if (_dirtyRows[j])
                ScanRow(j);

        var foundMatches = _foundMatches.Count > 0;
        if (foundMatches) FindUnions();
        return foundMatches;
    }


// Call destruction of everything in queue

    public void CallDestruction()
    {
        foreach (var (x, y) in _scheduleForRemoval)
        {
            if (_protectedForRemoval.Contains((x, y))) continue;
            var gem = GetGem(x, y);
            if (gem is null) continue;

            _score += 10;

            _freeGems.Push(gem);
            _SetGem(x, y, null);
            _dirtyCols[x] = true;
        }

        _scheduleForRemoval.Clear();
        _protectedForRemoval.Clear();
    }

// Work with Queue


// Collecting Matches
    private void MarkMatch(Match match)
    {
        // Stored in stack, can call "new"
        foreach (var (x, y) in match.GetCells())
        {
            _scheduleForRemoval.Add((x, y));
            _destructionEventQueue.Enqueue(new DestructionEvent
            {
                X = x,
                Y = y,
                Cause = DestructionCause.Match,
                XOrigin = x,
                YOrigin = y,
            });
        }
    }

    public void MarkMatches()
    {
        foreach (var match in _foundMatches)
        {
            MarkMatch(match);
        }
    }

// Falling
    public List<(int x, int fromY, int toY)> CalcFallPositions()
    {
        var result = new List<(int, int, int)>();

        for (var x = 0; x < XSize; x++)
        {
            if (!_hasMatchCol[x]) continue;

            var writePos = YSize - 1;
            for (var y = YSize - 1; y >= 0; y--)
            {
                if (GetGem(x, y) is null) continue;

                if (writePos != y)
                    result.Add((x, y, writePos));

                writePos--;
            }
        }

        return result;
    }

    private void FallCol(int x)
        // null, null, 4, null, 3, 4, 5 -> null, null, null, 4, 3, 4, 5
        //                                              ↑
        //                                           write_pos
    {
        var writePos = YSize - 1;
        for (var j = YSize - 1; j >= 0; j--)
        {
            if (GetGem(x, j) is null) continue;

            _SetGem(x, writePos, GetGem(x, j));
            if (writePos != j)
            {
                _SetGem(x, j, null);
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


// Reshuffling
    public void ReshuffleFreeGems()
    {
        // Update all gems types
        foreach (var gem in _freeGems)
        {
            gem._setGemType(Gem.GetRandomGemType(_excludeList, Random));
        }
    }

// Filling
    public List<(int x, int fromY, int toY)> CalcFillPositions()
    {
        var result = new List<(int, int, int)>();

        for (var x = 0; x < XSize; x++)
        {
            if (!_hasMatchCol[x]) continue;
            for (var y = _lastNoneInCol[x]; y >= 0; y--)
                result.Add((x, y - _lastNoneInCol[x] - 1, y));
        }

        return result;
    }


    private void FillCol(int x)
    {
        for (var j = _lastNoneInCol[x]; j >= 0; j--)
        {
            _SetGem(x, j, _freeGems.Pop());
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


// Cleaners
    private void ClearDirtyFlags()
    {
        for (var j = 0; j < XSize; j++)
            _dirtyRows[j] = false;
        for (var i = 0; i < YSize; i++)
            _dirtyCols[i] = false;
    }

    private void ClearMatchSet()
    {
        _foundMatches.Clear();
    }

    private void ClearLastNoneIds()
    {
        for (var i = 0; i < YSize; i++)
            _lastNoneInCol[i] = -1;
    }

    public void ClearAll()
    {
        ClearDirtyFlags();
        ClearMatchSet();
        ClearLastNoneIds();
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
                    var gem = GetGem(i, j);
                    if (gem is null)
                    {
                        result += $"X ";
                        continue;
                    }


                    result += $"{GetGem(i, j)} ";
                }
            }

            result += "\n";
        }

        return result;
    }
}