using OpenTK;
using OpenTK.Graphics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DonutGame
{
    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord0;
        public Color4 Color;
        public Vector4 BoneWeights;
        public Vector4i BoneIndices;

        public static readonly int SizeOf = 80;

        public Vertex(Vector3 position)
        {
            Position = position;
            Normal = Vector3.Zero;
            TexCoord0 = Vector2.Zero;
            Color = Color4.White;
            BoneWeights = Vector4.One;
            BoneIndices = new Vector4i(1, 0, 0, 0);
        }

        public Vertex(Vector3 position, Color4 color)
        {
            Position = position;
            Normal = Vector3.Zero;
            TexCoord0 = Vector2.Zero;
            Color = color;
            BoneWeights = Vector4.One;
            BoneIndices = new Vector4i(1, 0, 0, 0);
        }
    }

    struct Bone
    {
        public string Name;
        public Matrix4 Transform;
        public int Parent;
    }

    class Model
    {
        public List<Vertex> Vertices = new List<Vertex>();
        public List<uint> Indices = new List<uint>();
        public List<Bone> Bones = new List<Bone>();
        public List<Animation> Animations = new List<Animation>();
    }
}
