using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace DonutGame
{
    public class Game : GameWindow
    {
        public Game() :
            base(1280, 720, GraphicsMode.Default, "Plow Team", GameWindowFlags.FixedWindow)
        {
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.ClearColor(Color4.Yellow);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            SwapBuffers();
        }
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
