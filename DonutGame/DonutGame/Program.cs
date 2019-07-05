using OpenTK;

namespace DonutGame
{
    public class Game : GameWindow
    {

    }

    class Program
    {
        static void Main(string[] args)
        {
            using (var game = new Game())
            {
                game.Run();
            }
        }
    }
}
