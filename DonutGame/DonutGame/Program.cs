using System;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using Vector3 = OpenTK.Vector3;

namespace DonutGame
{
    public class Game : GameWindow
    {
        // A simple vertex shader possible. Just passes through the position vector.
        const string VertexShaderSource = @"
            #version 450 core

            layout(location = 0) in vec4 position;
            layout(location = 20) uniform mat4 projection;
            layout(location = 21) uniform mat4 modelView;

            void main(void)
            {
                gl_Position = projection * modelView * position;
            }
        ";

        // A simple fragment shader. Just a constant red color.
        const string FragmentShaderSource = @"
            #version 450 core
            out vec4 outputColor;
            void main(void)
            {
                outputColor = vec4(1.0, 1.0, 1.0, 1.0);
            }
        ";

        // Points of a triangle in normalized device coordinates.
        readonly float[] Points = new float[]
        {
            -0.5f, 0.0f, -0.5f, 1.0f,
            0.5f, 0.0f, -0.5f, 1.0f,
            0.5f, 0.0f, 0.5f, 1.0f,

            0.5f, 0.0f, 0.5f, 1.0f,
            -0.5f, 0.0f, 0.5f, 1.0f,
            -0.5f, 0.0f, -0.5f, 1.0f
        };

        int VertexShader;
        int FragmentShader;
        int ShaderProgram;
        int VertexBufferObject;
        int VertexArrayObject;

        Matrix4 ProjectionMatrix;
        Matrix4 ModelViewMatrix;

        Camera MainCamera = new Camera();

        public Game() :
            base(1280, 720, new GraphicsMode(32, 24, 0, 8), "Plow Team", GameWindowFlags.Default)
        {
            KeyDown += OnKeyDown;
            MouseMove += OnMouseMove;

            MainCamera.Position = Vector3.UnitY * -10;

            var file = new Pure3D.File();
            file.Load("homer_m.p3d");
            PrintHierarchy(file.RootChunk, 0);
        }

        static void PrintHierarchy(Pure3D.Chunk chunk, int indent)
        {
            Console.WriteLine("{1}{0}", chunk.ToString(), new String('\t', indent));
            foreach (var child in chunk.Children)
                PrintHierarchy(child, indent + 1);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Load the source of the vertex shader and compile it.
            VertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(VertexShader, VertexShaderSource);
            GL.CompileShader(VertexShader);

            // Load the source of the fragment shader and compile it.
            FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(FragmentShader, FragmentShaderSource);
            GL.CompileShader(FragmentShader);

            // Create the shader program, attach the vertex and fragment shaders and link the program.
            ShaderProgram = GL.CreateProgram();
            GL.AttachShader(ShaderProgram, VertexShader);
            GL.AttachShader(ShaderProgram, FragmentShader);
            GL.LinkProgram(ShaderProgram);

            // Create the vertex buffer object (VBO) for the vertex data.
            VertexBufferObject = GL.GenBuffer();
            // Bind the VBO and copy the vertex data into it.
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, Points.Length * sizeof(float), Points, BufferUsageHint.StaticDraw);

            // Retrive the position location from the program.
            var positionLocation = GL.GetAttribLocation(ShaderProgram, "position");

            // Create the vertex array object (VAO) for the program.
            VertexArrayObject = GL.GenVertexArray();
            // Bind the VAO and setup the position attribute.
            GL.BindVertexArray(VertexArrayObject);
            GL.VertexAttribPointer(positionLocation, 4, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(positionLocation);

            GL.ClearColor(Color4.Black);
        }

        protected override void OnUnload(EventArgs e)
        {
            base.OnUnload(e);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            GL.DeleteBuffer(VertexBufferObject);
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
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BindVertexArray(VertexArrayObject);

            var viewMatrix = MainCamera.ViewMatrix;
            var modelMatrix = Matrix4.CreateTranslation(Vector3.Zero);

            ModelViewMatrix = modelMatrix * viewMatrix;

            GL.UseProgram(ShaderProgram);
            GL.UniformMatrix4(20, false, ref ProjectionMatrix);
            GL.UniformMatrix4(21, false, ref ModelViewMatrix);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
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
