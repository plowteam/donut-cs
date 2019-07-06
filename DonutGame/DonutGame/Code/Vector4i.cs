using System.Runtime.InteropServices;

namespace DonutGame
{
    [StructLayout(LayoutKind.Sequential)]
    struct Vector4i
    {
        public int X;
        public int Y;
        public int Z;
        public int W;

        public Vector4i(int x, int y, int z, int w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Vector4i(int v)
        {
            X = v;
            Y = v;
            Z = v;
            W = v;
        }
    }
}
