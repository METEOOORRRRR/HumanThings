using System.IO;
using System.IO.Compression;
using System.Text;

namespace EarthRecovery
{
    public static class SnapshotCodec
    {
        public static byte[] Encode(string json)
        {
            using var output = new MemoryStream();
            using (var zip = new DeflateStream(output, CompressionLevel.Fastest, true))
            {
                byte[] input = Encoding.UTF8.GetBytes(json); zip.Write(input, 0, input.Length);
            }
            return output.ToArray();
        }
        public static string Decode(byte[] bytes)
        {
            using var input = new MemoryStream(bytes);
            using var zip = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + count > 262144) throw new InvalidDataException("Snapshot too large");
                output.Write(buffer, 0, count);
            }
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}
