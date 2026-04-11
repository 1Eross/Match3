namespace Match3Easter.Tools;

public class UnionFind
{
    private readonly int[] _parents;
    private readonly int[] _size;

    public UnionFind(int unionSize)
    {
        _parents = new int[unionSize];
        _size = new int[unionSize];

        for (var i = 0; i < unionSize; i++)
        {
            _parents[i] = i;
            _size[i] = 1;
        }
    }

    public int Find(int i)
    {
        while (true)
        {
            if (_parents[i] != i)
            {
                _parents[i] = _parents[_parents[i]];
                i = _parents[i];
            }

            return i;
        }
    }

    public void Union(int i, int j)
    {
        var rootI = Find(i);
        var rootJ = Find(j);
        if (rootI == rootJ) return;

        if (_size[rootI] < _size[rootJ])
        {
            _parents[rootI] = rootJ;
            _size[rootJ] += _size[rootI];
        }
        else
        {
            _parents[rootJ] = rootI;
            _size[rootI] += _size[rootJ];
        }
    }
}