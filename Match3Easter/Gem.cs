namespace Match3Easter;

public class Gem
{
    public enum GemType
    {
        Pentagon,
        Triangle,
        Square,
        Diamond,
        Circle
    }


    private GemType gemType;
    private static readonly GemType[] AllGems = Enum.GetValues<GemType>();

    internal GemType _getGemType() => gemType;
    internal void _setGemType(GemType newGemType) => gemType = newGemType;

    public Gem(GemType gemType)
    {
        this.gemType = gemType;
    }

    // Potentially slow; Can use Stack as exclude var
    public static GemType GetRandomGemType(Stack<GemType> exclude, Random random)
    {
        var gemsToChoose = AllGems.Except(exclude).ToList();
        return gemsToChoose[random.Next(0, gemsToChoose.Count)];
    }

    public override string ToString()
    {
        var symb = gemType switch
        {
            GemType.Circle => "○",
            GemType.Diamond => "◇",
            GemType.Pentagon => "⬠",
            GemType.Square => "□",
            GemType.Triangle => "△",
            _ => throw new ArgumentOutOfRangeException()
        };
        return symb;
    }
}