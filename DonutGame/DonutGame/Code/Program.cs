using System;
using System.Linq;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

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
            layout(location = 4) in vec4 boneWeights;
            layout(location = 5) in int boneIndex;

            layout(location = 20) uniform mat4 projection;
            layout(location = 21) uniform mat4 modelView;
            layout(location = 22) uniform vec4 tint;
            layout(location = 23) uniform float zOffset;
            layout(location = 24) uniform samplerBuffer boneBuffer;

            mat4 GetMatrix(int index)
            {
	            return mat4(texelFetch(boneBuffer, (index * 4) + 0),
				            texelFetch(boneBuffer, (index * 4) + 1),
				            texelFetch(boneBuffer, (index * 4) + 2),
				            texelFetch(boneBuffer, (index * 4) + 3));
            }

            out vec4 vertexColor;

            void main(void)
            {
                vertexColor = color * tint;

                mat4 boneMatrix = GetMatrix(boneIndex) * boneWeights[0];

                vec4 vertex = boneMatrix * vec4(position.xyz, 1.0);
                vertex = modelView * vertex;
                vertex.w -= zOffset;

                gl_Position = projection * vertex;
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

        int VertexShader;
        int FragmentShader;
        int ShaderProgram;
        int VertexBufferObject;
        int IndexBufferObject;
        int TextureBufferObject;
        int Texture;
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

            HomerModel = LoadModel("homer");
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

        private static Quaternion ConvertQuaternion(Pure3D.Quaternion v)
        {
            return new Quaternion(v.X, v.Y, v.Z, v.W);
        }

        protected static Matrix4 ConvertMatrix(Pure3D.Matrix m)
        {
            return new Matrix4(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44);
        }

        private Model LoadModel(string name)
        {
            var file = new Pure3D.File();
            file.Load($"{name}_m.p3d");
            PrintHierarchy(file.RootChunk, 0);

            var animFile = new Pure3D.File();
            animFile.Load($"{name}_a.p3d");
            PrintHierarchy(animFile.RootChunk.GetChildren<Pure3D.Chunks.Animation>().FirstOrDefault(), 0);

            var model = new Model();

            var rootChunk = file.RootChunk;
            var meshChunks = rootChunk.GetChildren<Pure3D.Chunks.Mesh>();
            var skeletonChunk = rootChunk.GetChildren<Pure3D.Chunks.Skeleton>()[0];
            var jointChunks = skeletonChunk.GetChildren<Pure3D.Chunks.SkeletonJoint>();

            for (var i = 0; i < jointChunks.Length; ++i)
            {
                var jointChunk = jointChunks[i];
                var boneMatrix = ConvertMatrix(jointChunk.RestPose);
                Console.WriteLine($"{i} {jointChunk.Name}, parent {jointChunk.SkeletonParent}");

                model.Bones.Add(new Bone
                {
                    Name = jointChunk.Name,
                    Transform = boneMatrix,
                    Parent = (int)jointChunk.SkeletonParent,
                });
            }

            for (var index = 0; index < model.Bones.Count; ++index)
            {
                var bone = model.Bones[index];
                var boneParent = bone.Parent;
                bone.Transform *= model.Bones[boneParent].Transform;
                model.Bones[index] = bone;
            }

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

                        var bone0 = 0;

                        if (matricesChunk != null && matrixPaletteChunk != null)
                        {
                            var matrixIndex = matricesChunk.Matrices[i];
                            var jointIndex0 = matrixPaletteChunk.Matrices[matrixIndex[0]];
                            var jointIndex1 = matrixPaletteChunk.Matrices[matrixIndex[1]];
                            var jointIndex2 = matrixPaletteChunk.Matrices[matrixIndex[2]];
                            var jointIndex3 = matrixPaletteChunk.Matrices[matrixIndex[3]];

                            bone0 = (int)jointIndex3;
                        }

                        return new Vertex
                        {
                            Position = position,
                            Normal = normal,
                            TexCoord0 = uv,
                            Color = Color4.White,
                            BoneWeights = new Vector4(1, 0, 0, 0),
                            BoneIndex = bone0,
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

            var animChunks = animFile.RootChunk.GetChildren<Pure3D.Chunks.Animation>();
            foreach (var animChunk in animChunks)
            {
                LoadAnimation(model, animChunk);
            }

            return model;
        }

        private Animation LoadAnimation(Model model, Pure3D.Chunks.Animation animChunk)
        {
            if (animChunk == null) return null;

            var anim = new Animation()
            {
                Name = animChunk.Name,
            };

            Console.WriteLine(anim.Name);

            model.Animations.Add(anim);

            anim.FrameCount = (int)animChunk.NumberOfFrames;
            anim.FrameRate = animChunk.FrameRate;
            anim.Length = anim.FrameCount / animChunk.FrameRate;

            var animGroupListChunk = animChunk.GetChildren<Pure3D.Chunks.AnimationGroupList>().FirstOrDefault();
            var animGroupChunks = animGroupListChunk.GetChildren<Pure3D.Chunks.AnimationGroup>();

            anim.Tracks.AddRange(model.Bones.Select(x => new Animation.Track { Name = x.Name }));

            for (var trackIndex = 0; trackIndex < anim.Tracks.Count; ++trackIndex)
            {
                var animTrack = anim.Tracks[trackIndex];
                var bone = model.Bones[trackIndex];
                Pure3D.Chunks.AnimationGroup animGroupChunk = animGroupChunks.Where(x => x.Name == animTrack.Name).FirstOrDefault();

                var boneTransform = bone.Transform;
                boneTransform *= model.Bones[bone.Parent].Transform.Inverted();

                if (animGroupChunk == null)
                {
                    animTrack.PositionKeys.Add(0.0f, boneTransform.ExtractTranslation());
                    animTrack.RotationKeys.Add(0.0f, boneTransform.ExtractRotation());
                    animTrack.ScaleKeys.Add(0.0f, Vector3.Zero);

                    continue;
                }

                var positionChannelChunk = animGroupChunk.GetChildren<Pure3D.Chunks.Vector3Channel>().FirstOrDefault();
                var position2dChannelChunk = animGroupChunk.GetChildren<Pure3D.Chunks.Vector2Channel>().FirstOrDefault();
                var rotationChannelChunk = animGroupChunk.GetChildren<Pure3D.Chunks.CompressedQuaternionChannel>().FirstOrDefault();

                if (positionChannelChunk != null)
                {
                    for (var valueIndex = 0; valueIndex < positionChannelChunk.Values.Length; ++valueIndex)
                    {
                        var value = positionChannelChunk.Values[valueIndex];
                        var position = ConvertVector(value);

                        animTrack.PositionKeys.Add(positionChannelChunk.Frames[valueIndex], position);
                    }
                }
                else if (position2dChannelChunk != null)
                {
                    var constant = ConvertVector(position2dChannelChunk.Constants);

                    for (var valueIndex = 0; valueIndex < position2dChannelChunk.Values.Length; ++valueIndex)
                    {
                        var value = position2dChannelChunk.Values[valueIndex];
                        var position = (constant + new Vector3(value.X, 0, value.Y));

                        animTrack.PositionKeys.Add(position2dChannelChunk.Frames[valueIndex], position);
                    }
                }
                else
                {
                    animTrack.PositionKeys.Add(0.0f, boneTransform.ExtractTranslation());
                }

                if (rotationChannelChunk != null)
                {
                    for (var valueIndex = 0; valueIndex < rotationChannelChunk.Values.Length; ++valueIndex)
                    {
                        var value = rotationChannelChunk.Values[valueIndex];
                        var rotation = ConvertQuaternion(value);

                        animTrack.RotationKeys.Add(rotationChannelChunk.Frames[valueIndex], rotation);
                    }
                }
                else
                {
                    animTrack.RotationKeys.Add(0.0f, boneTransform.ExtractRotation());
                }

                animTrack.ScaleKeys.Add(0.0f, Vector3.One);
            }

            return anim;
        }

        private void UpdateAnimation(int animIndex, float time)
        {
            var animation = HomerModel.Animations[animIndex % HomerModel.Animations.Count];
            var poseMatrices = new Matrix4[HomerModel.Bones.Count];
            poseMatrices[0] = animation.Tracks[0].Evalulate(time);

            for (var index = 0; index < poseMatrices.Length; ++index)
            {
                var bone = HomerModel.Bones[index];
                var boneParent = bone.Parent;

                poseMatrices[index] = animation.Tracks[index].Evalulate(time) * poseMatrices[boneParent];
            }

            var boneMatrices = new Matrix4[HomerModel.Bones.Count];
            for (var i = 0; i < boneMatrices.Length; ++i)
            {
                var boneMatrix = HomerModel.Bones[i].Transform;
                boneMatrices[i] = (boneMatrix.Inverted() * poseMatrices[i]);
            }

            GL.BindBuffer(BufferTarget.TextureBuffer, TextureBufferObject);
            GL.BufferData(BufferTarget.TextureBuffer, boneMatrices.Length * 64, boneMatrices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);

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
            TextureBufferObject = GL.GenBuffer();
            Texture = GL.GenTexture();

            UpdateAnimation(AnimIndex, 0.0f);

            GL.BindTexture(TextureTarget.TextureBuffer, Texture);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, TextureBufferObject);
            GL.BindTexture(TextureTarget.TextureBuffer, 0);

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
            GL.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, Vertex.SizeOf, 48); // Bone Weights
            GL.VertexAttribIPointer(5, 1, VertexAttribIntegerType.Int, Vertex.SizeOf, (IntPtr)64); // Bone Indices
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            GL.ClearColor(Color4.Black);
        }

        protected override void OnUnload(EventArgs e)
        {
            base.OnUnload(e);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
            GL.BindTexture(TextureTarget.TextureBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            GL.DeleteBuffer(VertexBufferObject);
            GL.DeleteBuffer(IndexBufferObject);
            GL.DeleteBuffer(TextureBufferObject);
            GL.DeleteTexture(Texture);
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

        float AnimTime = 0;
        int AnimIndex = 19;

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

            var anim = HomerModel.Animations[AnimIndex % HomerModel.Animations.Count];
            AnimTime += dt * anim.FrameRate;
            UpdateAnimation(AnimIndex, AnimTime);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Enable(EnableCap.DepthTest);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.Enable(EnableCap.Texture1D);
            GL.Enable(EnableCap.Texture2D);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferObject);
            GL.BindVertexArray(VertexArrayObject);

            var viewMatrix = MainCamera.ViewMatrix;
            var modelMatrix = Matrix4.CreateRotationY(0);

            ModelViewMatrix = modelMatrix * viewMatrix;

            GL.UseProgram(ShaderProgram);
            GL.UniformMatrix4(20, false, ref ProjectionMatrix);
            GL.UniformMatrix4(21, false, ref ModelViewMatrix);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.TextureBuffer, Texture);
            GL.Uniform1(24, 0);

            GL.Uniform4(22, 0.25f, 0.25f, 0.25f, 1.0f);
            GL.Uniform1(23, 0.001f);
            GL.DrawElements(PrimitiveType.Triangles, HomerModel.Indices.Count, DrawElementsType.UnsignedInt, 0);
          
            GL.Uniform4(22, 0.0f, 1.0f, 1.0f, 1.0f);
            GL.Uniform1(23, 0.0f);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawElements(PrimitiveType.Triangles, HomerModel.Indices.Count, DrawElementsType.UnsignedInt, 0);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.BindTexture(TextureTarget.TextureBuffer, 0);

            SwapBuffers();
        }

        private void OnKeyDown(object sender, KeyboardKeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Exit();
            }
            else if (e.Key == Key.Right)
            {
                AnimIndex++;
                Console.WriteLine($"{AnimIndex}");
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

        private static void NativeChunkGen()
        {
            foreach (var type in typeof(Pure3D.Chunk)
                .Assembly
                .GetTypes()
                .Where(x => x.IsSubclassOf(typeof(Pure3D.Chunk))))
            {
                var attribute = (Pure3D.ChunkType)Attribute.GetCustomAttribute(type, typeof(Pure3D.ChunkType));
                if (attribute == null) continue;

                Console.WriteLine($"{type.Name} = {attribute.TypeID},"); // For enum gen
                Console.WriteLine($"{{{attribute.TypeID}, \"{type.Name}\"}},"); // For named enum gen
            }
        }
    }
}
