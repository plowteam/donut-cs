using OpenTK;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DonutGame
{
    class Animation
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct VectorKey
        {
            public Vector3 Value;
            public float Time;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct QuaternionKey
        {
            public Quaternion Value;
            public float Time;
        }

        public class Track
        {
            public string Name;

            public List<VectorKey> PositionKeys { get; } = new List<VectorKey>();
            public List<QuaternionKey> RotationKeys { get; } = new List<QuaternionKey>();
            public List<VectorKey> ScaleKeys { get; } = new List<VectorKey>();

            public Matrix4 T;
            public Matrix4 Transform()
            {
                return Matrix4.CreateFromQuaternion(RotationKeys[0].Value) * Matrix4.CreateTranslation(PositionKeys[0].Value);
            }
        }

        public string Name { get; set; }
        public float Length { get; set; }
        public int FrameCount { get; set; }
        public List<Track> Tracks { get; } = new List<Track>();
    }
}
