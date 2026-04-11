using Microsoft.Xna.Framework;

namespace Match3Easter
{
    class Program
    {
        static void Main(string[] args)
        {
            using var game = new Match3Game();
            game.Run();
        }
    }
}