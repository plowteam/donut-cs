using OpenTK;
using System.Collections.Generic;

namespace DonutGame
{
    class Animation
    {
        public class ValueKey<T>
        {
            public float Key;
            public T Value;

            public ValueKey(float key, T value)
            {
                Key = key;
                Value = value;
            }
        }

        public class VectorKey : ValueKey<Vector3>
        {
            public VectorKey(float key, Vector3 value) : base(key, value)
            {
            }
        }

        public class QuaternionKey : ValueKey<Quaternion>
        {
            public QuaternionKey(float key, Quaternion value) : base(key, value)
            {
            }
        }

        public class ValueKeyCurve<T>
        {
            private List<ValueKey<T>> KeyValues = new List<ValueKey<T>>();

            public void Add(float key, T value)
            {
                KeyValues.Add(new ValueKey<T>(key, value));
            }

            public T Evalulate(float time)
            {
                return KeyValues[0].Value;
            }
        }

        public class Track
        {
            public string Name;

            public ValueKeyCurve<Vector3> PositionKeys { get; } = new ValueKeyCurve<Vector3>();
            public ValueKeyCurve<Quaternion> RotationKeys { get; } = new ValueKeyCurve<Quaternion>();
            public ValueKeyCurve<Vector3> ScaleKeys { get; } = new ValueKeyCurve<Vector3>();

            public Matrix4 Transform()
            {
                return Matrix4.CreateFromQuaternion(RotationKeys.Evalulate(0)) * Matrix4.CreateTranslation(PositionKeys.Evalulate(0));
            }
        }

        public string Name { get; set; }
        public float Length { get; set; }
        public int FrameCount { get; set; }
        public List<Track> Tracks { get; } = new List<Track>();
    }
}
