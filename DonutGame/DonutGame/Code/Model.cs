﻿using OpenTK;
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
        public int BoneIndex;

        public static readonly int SizeOf = 68;

        public Vertex(Vector3 position)
        {
            Position = position;
            Normal = Vector3.Zero;
            TexCoord0 = Vector2.Zero;
            Color = Color4.White;
            BoneWeights = Vector4.Zero;
            BoneIndex = 0;
        }

        public Vertex(Vector3 position, Color4 color)
        {
            Position = position;
            Normal = Vector3.Zero;
            TexCoord0 = Vector2.Zero;
            Color = color;
            BoneWeights = Vector4.Zero;
            BoneIndex = 0;
        }
    }

    struct Bone
    {
        public Matrix4 Transform;
        public int Parent;
    }

    class Model
    {
        public List<Vertex> Vertices = new List<Vertex>();
        public List<uint> Indices = new List<uint>();
        public List<Bone> Bones = new List<Bone>();
    }
}