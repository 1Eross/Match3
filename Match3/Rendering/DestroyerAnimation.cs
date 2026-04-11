using Microsoft.Xna.Framework;

namespace Match3Easter.Rendering;

public struct DestroyerAnimation
{
    public float LaunchTime;
    public DestroyerType Type;
    public Vector2 From;
    public Vector2 To;
    public float TravelDuration;
}