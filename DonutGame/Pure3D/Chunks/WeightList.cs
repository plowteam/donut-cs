using System.IO;

namespace Pure3D.Chunks
{
    [ChunkType(65548)]
    public class WeightList : Chunk
    {
        public float[][] Weights;

        public WeightList(File file, uint type) : base(file, type)
        {
        }

        public override void ReadHeader(Stream stream, long length)
        {
            BinaryReader reader = new BinaryReader(stream);
            uint len = reader.ReadUInt32();
            Weights = new float[len][];
            for (int i = 0; i < len; i++)
            {
                Weights[i] = new float[3];
                Weights[i][0] = reader.ReadSingle();
                Weights[i][1] = reader.ReadSingle();
                Weights[i][2] = reader.ReadSingle();
            }
        }

        public override string ToString()
        {
            return $"Weight List ({Weights.Length})";
        }
    }
}