using OpenTK;
using System.Collections.Generic;

namespace DonutGame
{
    class Animation
    {
        public abstract class ValueKey<T> where T : struct
        {
            public float Key;
            public T Value;

            public ValueKey(float key, T value)
            {
                Key = key;
                Value = value;
            }

            public virtual T Lerp(T next, float fraction)
            {
                throw new System.NotImplementedException();
            }
        }

        public class VectorKey : ValueKey<Vector3>
        {
            public VectorKey(float key, Vector3 value) : base(key, value)
            {
            }

            public override Vector3 Lerp(Vector3 next, float fraction)
            {
                return Vector3.Lerp(Value, next, fraction);
            }
        }

        public class QuaternionKey : ValueKey<Quaternion>
        {
            public QuaternionKey(float key, Quaternion value) : base(key, value)
            {
            }

            public override Quaternion Lerp(Quaternion next, float fraction)
            {
                return Quaternion.Slerp(Value, next, fraction);
            }
        }

        public class ValueKeyCurve<T> where T : struct
        {
            private List<ValueKey<T>> KeyValues = new List<ValueKey<T>>();

            public void Add(float key, T value = default)
            {
                if (value is Vector3 v)
                {
                    KeyValues.Add(new VectorKey(key, v) as ValueKey<T>);
                }
                else if (value is Quaternion q)
                {
                    KeyValues.Add(new QuaternionKey(key, q) as ValueKey<T>);
                }
            }

            public T Evalulate(float time, T defaultValue = default)
            {
                var count = KeyValues.Count;
                if (count == 0) return defaultValue;

                var lastIndex = KeyValues.Count - 1;
                time = time % KeyValues[lastIndex].Key;

                var index = GetKeyValueIndex(time);
                if (index == -1) return KeyValues[0].Value;

                if (index == lastIndex)
                {
                    return KeyValues[lastIndex].Value;
                }

                var nextIndex = (index == lastIndex) ? 0 : index + 1;
                var prevPoint = KeyValues[index];
                var nextPoint = KeyValues[nextIndex];
                var delta = nextPoint.Key - prevPoint.Key;

                if (delta > 0.0f)
                {
                    var fraction = (time - prevPoint.Key) / delta;
                    fraction = MathHelper.Clamp(fraction, 0.0f, 1.0f);

                    return prevPoint.Lerp(nextPoint.Value, fraction);
                }
                else
                {
                    return KeyValues[index].Value;
                }
            }

            private int GetKeyValueIndex(float time)
            {
                var count = KeyValues.Count;
                var lastIndex = count - 1;

                if (time < KeyValues[0].Key)
                {
                    return -1;
                }

                if (time >= KeyValues[lastIndex].Key)
                {
                    return lastIndex;
                }

                var minIndex = 0;
                var maxIndex = count;

                while (maxIndex - minIndex > 1)
                {
                    var midIndex = (minIndex + maxIndex) / 2;

                    if (KeyValues[midIndex].Key <= time)
                    {
                        minIndex = midIndex;
                    }
                    else
                    {
                        maxIndex = midIndex;
                    }
                }

                return minIndex;
            }
        }

        public class Track
        {
            public string Name;

            public ValueKeyCurve<Vector3> PositionKeys { get; } = new ValueKeyCurve<Vector3>();
            public ValueKeyCurve<Quaternion> RotationKeys { get; } = new ValueKeyCurve<Quaternion>();
            public ValueKeyCurve<Vector3> ScaleKeys { get; } = new ValueKeyCurve<Vector3>();

            public Matrix4 Evalulate(float time)
            {
                return Matrix4.CreateFromQuaternion(RotationKeys.Evalulate(time)) *
                       Matrix4.CreateTranslation(PositionKeys.Evalulate(time));
            }
        }

        public string Name { get; set; }
        public float Length { get; set; }
        public int FrameCount { get; set; }
        public List<Track> Tracks { get; } = new List<Track>();
    }
}
