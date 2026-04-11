namespace Match3Easter.Models;

// TODO: Add metaclass BoardObject and inherit Gem from it 
public class Gem(GemType gemType, BonusType bonusType = BonusType.None)
{
    private GemType _gemType = gemType;
    internal GemType _getGemType() => _gemType;

    internal void _setGemType(GemType newGemType)
    {
        _gemType = newGemType;
        _bonusType = BonusType.None;
    }

    private BonusType _bonusType = bonusType;
    internal BonusType _getBonusType() => _bonusType;

    internal void _setBonusType(BonusType bonusType)
    {
        _bonusType = bonusType;
        _gemType = GemType.None;
    }

    // Potentially slow; Can use Stack as exclude var

    private static readonly GemType[] AllGems = Enum.GetValues<GemType>();

    public static GemType GetRandomGemType(Stack<GemType> exclude, Random random)
    {
        var gemsToChoose = AllGems.Except(exclude).ToList();
        return gemsToChoose[random.Next(0, gemsToChoose.Count)];
    }

    public bool isBonus()
    {
        return _bonusType != BonusType.None;
    }

    public override string ToString()
    {
        var symbol = _gemType switch
        {
            GemType.Circle => "○",
            GemType.Diamond => "◇",
            GemType.Pentagon => "⬠",
            GemType.Square => "□",
            GemType.Triangle => "△",
            _ => throw new ArgumentOutOfRangeException()
        };
        return symbol;
    }
}