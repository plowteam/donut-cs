using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using Vector3 = OpenTK.Vector3;

namespace DonutGame
{
    public class Game : GameWindow
    {
        const string VertexShaderSource = @"
            #version 450 core

            layout(location = 0) in vec3 position;
            layout(location = 1) in vec3 normal;
            layout(location = 2) in vec2 texCoord0;
            layout(location = 3) in vec4 color;
            layout(location = 20) uniform mat4 projection;
            layout(location = 21) uniform mat4 modelView;
            layout(location = 22) uniform vec4 tint;
            layout(location = 23) uniform float zOffset;

            out vec4 vertexColor;

            void main(void)
            {
                vertexColor = color * tint;
                vec4 v = modelView * vec4(position.xyz, 1.0);
                v.w -= zOffset;
                gl_Position = projection * v;
            }
        ";

        const string FragmentShaderSource = @"
            #version 450 core
            in vec4 vertexColor;
            out vec4 outputColor;
            void main(void)
            {
                outputColor = vertexColor;
            }
        ";

        //readonly Vertex[] Vertices = new Vertex[]
        //{
        //    new Vertex(new Vector3(-0.5f, 0.0f, -0.5f), Color4.Red),
        //    new Vertex(new Vector3(0.5f, 0.0f, -0.5f), Color4.Green),
        //    new Vertex(new Vector3(0.5f, 0.0f, 0.5f), Color4.Blue),
        //    new Vertex(new Vector3(-0.5f, 0.0f, 0.5f), Color4.Yellow),
        //};

        //readonly uint[] Indices = new uint[]
        //{
        //    0, 1, 2, 2, 3, 0
        //};

        struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 TexCoord0;
            public Color4 Color;

            public static readonly int SizeOf = 48;

            public Vertex(Vector3 position)
            {
                Position = position;
                Normal = Vector3.Zero;
                TexCoord0 = Vector2.Zero;
                Color = Color4.White;
            }

            public Vertex(Vector3 position, Color4 color)
            {
                Position = position;
                Normal = Vector3.Zero;
                TexCoord0 = Vector2.Zero;
                Color = color;
            }
        }

        class Model
        {
            public List<Vertex> Vertices = new List<Vertex>();
            public List<uint> Indices = new List<uint>();
        }

        int VertexShader;
        int FragmentShader;
        int ShaderProgram;
        int VertexBufferObject;
        int IndexBufferObject;
        int VertexArrayObject;

        Matrix4 ProjectionMatrix;
        Matrix4 ModelViewMatrix;

        Camera MainCamera = new Camera();
        readonly Model HomerModel = null;

        public Game() :
            base(1280, 720, new GraphicsMode(32, 24, 0, 8), "Plow Team", GameWindowFlags.Default)
        {
            KeyDown += OnKeyDown;
            MouseMove += OnMouseMove;

            MainCamera.Position = Vector3.UnitY * -10;

            HomerModel = LoadModel("homer_m.p3d");
        }

        static void PrintHierarchy(Pure3D.Chunk chunk, int indent)
        {
            Console.WriteLine("{1}{0}", chunk.ToString(), new String('\t', indent));
            foreach (var child in chunk.Children)
                PrintHierarchy(child, indent + 1);
        }

        private static Vector3 ConvertVector(Pure3D.Vector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        private static Vector2 ConvertVector(Pure3D.Vector2 v)
        {
            return new Vector2(v.X, v.Y);
        }

        private Model LoadModel(string path)
        {
            var file = new Pure3D.File();
            file.Load("homer_m.p3d");
            PrintHierarchy(file.RootChunk, 0);

            var model = new Model();

            var rootChunk = file.RootChunk;
            var meshChunks = rootChunk.GetChildren<Pure3D.Chunks.Mesh>();

            foreach (var meshChunk in meshChunks)
            {
                var primChunks = meshChunk.GetChildren<Pure3D.Chunks.PrimitiveGroup>();

                foreach (var primChunk in primChunks)
                {
                    if (primChunk.PrimitiveType == Pure3D.Chunks.PrimitiveGroup.PrimitiveTypes.LineList ||
                        primChunk.PrimitiveType == Pure3D.Chunks.PrimitiveGroup.PrimitiveTypes.LineStrip)
                    {
                        continue;
                    }

                    var positionChunk = primChunk.GetChildren<Pure3D.Chunks.PositionList>().FirstOrDefault();
                    var packedNormalChunk = primChunk.GetChildren<Pure3D.Chunks.PackedNormalList>().FirstOrDefault();
                    var normalChunk = primChunk.GetChildren<Pure3D.Chunks.NormalList>().FirstOrDefault();
                    var uvChunk = primChunk.GetChildren<Pure3D.Chunks.UVList>().FirstOrDefault();
                    var indicesChunk = primChunk.GetChildren<Pure3D.Chunks.IndexList>().FirstOrDefault();
                    var matricesChunk = primChunk.GetChildren<Pure3D.Chunks.MatrixList>().FirstOrDefault();
                    var matrixPaletteChunk = primChunk.GetChildren<Pure3D.Chunks.MatrixPalette>().FirstOrDefault();
                    var shaderChunk = rootChunk.GetChildrenByName<Pure3D.Chunks.Shader>(primChunk.ShaderName).FirstOrDefault();
                    var textureParameterChunk = shaderChunk.GetChildren<Pure3D.Chunks.ShaderTextureParam>().FirstOrDefault();

                    var vertexOffset = (uint)model.Vertices.Count;

                    model.Vertices.AddRange(Enumerable.Range(0, (int)primChunk.NumVertices).Select(i =>
                    {
                        var position = ConvertVector(positionChunk.Positions[i]);
                        var normal = ConvertVector(normalChunk.Normals[i]);
                        var uv = new Vector2(uvChunk.UVs[i].X, 1.0f - uvChunk.UVs[i].Y);

                        return new Vertex
                        {
                            Position = position,
                            Normal = normal,
                            TexCoord0 = uv,
                            Color = Color4.White
                        };
                    }));

                    if (primChunk.PrimitiveType == Pure3D.Chunks.PrimitiveGroup.PrimitiveTypes.TriangleList)
                    {
                        model.Indices.AddRange(indicesChunk.Indices.Select(x => vertexOffset + x));
                    }
                    else if (primChunk.PrimitiveType == Pure3D.Chunks.PrimitiveGroup.PrimitiveTypes.TriangleStrip)
                    {
                        for (var i = 0; i < primChunk.NumIndices - 2; ++i)
                        {
                            if (i % 2 == 0)
                            {
                                var a = vertexOffset + indicesChunk.Indices[i];
                                var b = vertexOffset + indicesChunk.Indices[i + 1];
                                var c = vertexOffset + indicesChunk.Indices[i + 2];

                                if (a == b || b == c || c == a)
                                {
                                    Console.WriteLine("DEGENERATE TRIANGLE!");
                                    continue;
                                }

                                model.Indices.Add(a);
                                model.Indices.Add(b);
                                model.Indices.Add(c);
                            }
                            else
                            {
                                var a = vertexOffset + indicesChunk.Indices[i];
                                var b = vertexOffset + indicesChunk.Indices[i + 2];
                                var c = vertexOffset + indicesChunk.Indices[i + 1];

                                if (a == b || b == c || c == a)
                                {
                                    Console.WriteLine("DEGENERATE TRIANGLE!");
                                    continue;
                                }

                                model.Indices.Add(a);
                                model.Indices.Add(b);
                                model.Indices.Add(c);
                            }
                        }
                    }
                }
            }

            return model;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            VertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(VertexShader, VertexShaderSource);
            GL.CompileShader(VertexShader);

            FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(FragmentShader, FragmentShaderSource);
            GL.CompileShader(FragmentShader);

            ShaderProgram = GL.CreateProgram();
            GL.AttachShader(ShaderProgram, VertexShader);
            GL.AttachShader(ShaderProgram, FragmentShader);
            GL.LinkProgram(ShaderProgram);

            VertexBufferObject = GL.GenBuffer();
            IndexBufferObject = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, HomerModel.Vertices.Count * Vertex.SizeOf, HomerModel.Vertices.ToArray(), BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, HomerModel.Indices.Count * sizeof(uint), HomerModel.Indices.ToArray(), BufferUsageHint.StaticDraw);

            VertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(VertexArrayObject);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Vertex.SizeOf, 0); // Position
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Vertex.SizeOf, 12); // Normal
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, Vertex.SizeOf, 24); // TexCoord0
            GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, Vertex.SizeOf, 32); // Color
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);

            GL.ClearColor(Color4.Black);
        }

        protected override void OnUnload(EventArgs e)
        {
            base.OnUnload(e);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            GL.DeleteBuffer(VertexBufferObject);
            GL.DeleteBuffer(IndexBufferObject);
            GL.DeleteVertexArray(VertexArrayObject);
            GL.DeleteProgram(ShaderProgram);
            GL.DeleteShader(FragmentShader);
            GL.DeleteShader(VertexShader);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            GL.Viewport(0, 0, Width, Height);

            var aspectRatio = (float)Width / Height;
            ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(40), aspectRatio, 0.1f, 4000f);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            var dt = (float)e.Time;

            var keyState = Keyboard.GetState();

            var inputForce = Vector3.Zero;
            if (keyState.IsKeyDown(Key.W)) inputForce += Vector3.UnitZ;
            if (keyState.IsKeyDown(Key.S)) inputForce -= Vector3.UnitZ;
            if (keyState.IsKeyDown(Key.A)) inputForce += Vector3.UnitX;
            if (keyState.IsKeyDown(Key.D)) inputForce -= Vector3.UnitX;
            inputForce.Normalize();

            inputForce *= keyState.IsKeyDown(Key.ShiftLeft) ? 10.0f : 2.0f;

            MainCamera.UpdateRotationQuat();
            MainCamera.Move(inputForce, dt);
            MainCamera.UpdateViewMatrix();

            //Rot += 1.0f * dt;
        }

        float Rot = 0;

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferObject);
            GL.BindVertexArray(VertexArrayObject);

            var viewMatrix = MainCamera.ViewMatrix;
            var modelMatrix = Matrix4.CreateRotationY(Rot);

            ModelViewMatrix = modelMatrix * viewMatrix;

            GL.UseProgram(ShaderProgram);
            GL.UniformMatrix4(20, false, ref ProjectionMatrix);
            GL.UniformMatrix4(21, false, ref ModelViewMatrix);

            GL.Uniform4(22, 0.25f, 0.25f, 0.25f, 1.0f);
            GL.Uniform1(23, 0.001f);
            GL.DrawElements(PrimitiveType.Triangles, HomerModel.Indices.Count, DrawElementsType.UnsignedInt, 0);

            GL.Uniform4(22, 0.0f, 1.0f, 1.0f, 1.0f);
            GL.Uniform1(23, 0.0f);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawElements(PrimitiveType.Triangles, HomerModel.Indices.Count, DrawElementsType.UnsignedInt, 0);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            SwapBuffers();
        }

        private void OnKeyDown(object sender, KeyboardKeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Exit();
            }
        }

        private void OnMouseMove(object sender, MouseMoveEventArgs e)
        {
            var xDelta = e.XDelta * 0.5f;
            var yDelta = e.YDelta * 0.5f;

            MainCamera.LookDelta(xDelta, yDelta);
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
