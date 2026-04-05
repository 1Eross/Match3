using System;

namespace Match3Easter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            void LogicLoop()
            {
            }

            void DrawLoop()
            {
            }

            var board = new Board();

            while (true)
            {
                Console.Clear();
                Console.WriteLine(board);
                Console.Write("Swap (x1, y1, x2, y2): ");
                var input = Console.ReadLine()?.Split(' ');
                if (input is not { Length: 4 }) continue;
                if (!int.TryParse(input[0], out var x1) ||
                    !int.TryParse(input[1], out var y1) ||
                    !int.TryParse(input[2], out var x2) ||
                    !int.TryParse(input[3], out var y2))
                {
                    Console.WriteLine("Invalid input");
                    continue;
                }

                if (!board.TrySwap(x1, y1, x2, y2))
                {
                    Console.WriteLine("Not neighbors");
                    Console.ReadKey();
                    continue;
                }

                if (!board.FindMatches())
                {
                    board.SwapBack(x1, y1, x2, y2);
                    Console.WriteLine("No matches");
                    Console.ReadKey();
                    continue;
                }

                while (board.FindMatches())
                {
                    Console.Clear();
                    Console.WriteLine(board);
                    Console.Write("Matches Found");
                    Console.ReadKey();
                    board.CollectMatches();
                    board.ReshuffleFreeGems();
                    board.FallCols();
                    board.FillCols();

                    Console.Clear();
                    Console.WriteLine(board);
                    Console.Write("After gravity");
                    Console.ReadKey();
                }

                board.ClearAll();
            }
        }
    }
}