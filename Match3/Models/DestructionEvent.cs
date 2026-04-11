namespace Match3Easter.Models;

public struct DestructionEvent
{
    public int X, Y;
    public DestructionCause Cause;
    public int XOrigin, YOrigin;
    public float BaseTime;
}