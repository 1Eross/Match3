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

            void MainLoop()
            {
                // LogicLoop();
                // DrawLoop();
            }

            // var board = new Board();
            // Console.WriteLine(board);

            int?[] intList = [null, 8, 7, null, null, 4, 3, 2, 1];
            int downPointer = 0;
            int upPointer = 0;
            for (var j = intList.Length - 1; j >= 0; j--)
                if (intList[j] is null)
                {
                    downPointer = j;
                    break;
                }

            for (var j = downPointer - 1; j >= 0; j--)
                if (intList[j] is not null)
                {
                    upPointer = j;
                    break;
                }

            Console.WriteLine($"U:{upPointer}");
            Console.WriteLine($"D:{downPointer}");

            var notEmptySpanToFillWith = intList.AsSpan(0, upPointer + 1);
            // Need to get empty span from up_pointer to down_pointer of col
            var emptySpanToBeFilled = intList.AsSpan(upPointer + 1, downPointer - upPointer);

            Console.WriteLine($"[{string.Join(", ", notEmptySpanToFillWith.ToArray())}]");
            Console.WriteLine($"[{string.Join(", ", emptySpanToBeFilled.ToArray())}]");

            // notEmptySpanToFillWith.Slice(upPointer, emptySpanToBeFilled.Length)
            // .CopyTo(emptySpanToBeFilled);
            notEmptySpanToFillWith.CopyTo(emptySpanToBeFilled);

            Console.WriteLine($"[{string.Join(", ", intList)}]");
        }
    }
}