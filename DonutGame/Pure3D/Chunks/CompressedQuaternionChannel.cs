using System.IO;
using System.Text;

namespace Pure3D.Chunks
{
    [ChunkType(1184017)]
    public class CompressedQuaternionChannel : Chunk
    {
        public uint Version;
        public uint NumberOfFrames;
        public string Parameter;
        public Quaternion[] Values;
        public ushort[] Frames;

        public CompressedQuaternionChannel(File file, uint type) : base(file, type)
        {
        }

        public override void ReadHeader(Stream stream, long length)
        {
            var streamPosition = stream.Position;

            BinaryReader reader = new BinaryReader(stream);
            Version = reader.ReadUInt32();
            Parameter = Util.ZeroTerminate(Encoding.ASCII.GetString(reader.ReadBytes(4)));
            NumberOfFrames = reader.ReadUInt32();

            Frames = new ushort[NumberOfFrames];
            for (int i = 0; i < NumberOfFrames; i++)
            {
                Frames[i] = reader.ReadUInt16();
            }

            Values = new Quaternion[NumberOfFrames];
            for (int i = 0; i < NumberOfFrames; i++)
            {
                var w = reader.ReadInt16() / (float)short.MaxValue;
                Values[i] = new Quaternion
                (
                    reader.ReadInt16() / (float)short.MaxValue,
                    reader.ReadInt16() / (float)short.MaxValue,
                    reader.ReadInt16() / (float)short.MaxValue,
                    w
                );
            }
        }

        public override string ToString()
        {
            return $"Compressed Quaternion Channel: {Parameter}, {NumberOfFrames} Frames";
        }
    }
}
