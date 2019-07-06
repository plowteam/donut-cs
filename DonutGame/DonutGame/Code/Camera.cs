using OpenTK;

namespace DonutGame
{
    public class Camera
    {
        public Matrix4 ViewMatrix { get; private set; }
        public Quaternion RotationQuat { get; private set; }
        public Vector3 Position { get; set; }
        public float Pitch { get; private set; }
        public float Yaw { get; private set; }

        public Camera()
        {
            ViewMatrix = Matrix4.Identity;
            RotationQuat = Quaternion.Identity;
            Position = Vector3.Zero;
            Pitch = 0;
            Yaw = 0;
        }

        public void Move(Vector3 force, float dt)
        {
            if (force.Length > 0.0f)
            {
                Position += (RotationQuat.Inverted() * force) * dt;
            }
        }

        public void LookDelta(float x, float y)
        {
            Yaw += x;
            Pitch = MathHelper.Clamp(Pitch + y, -90, 90);
        }

        public void UpdateViewMatrix()
        {
            ViewMatrix = Matrix4.CreateTranslation(Position) * Matrix4.CreateFromQuaternion(RotationQuat);
        }

        public void UpdateRotationQuat()
        {
            RotationQuat = Quaternion.FromEulerAngles(MathHelper.DegreesToRadians(Pitch), MathHelper.DegreesToRadians(Yaw), 0);
        }
    }
}
