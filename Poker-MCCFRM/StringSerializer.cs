using FASTER.core;
using System.Text;

namespace Poker_MCCFRM
{
    public class StringSerializer : BinaryObjectSerializer<string>
    {
        public override void Deserialize(out string obj)
        {
            int length = reader.ReadInt32();
            byte[] bytes = reader.ReadBytes(length);
            obj = Encoding.ASCII.GetString(bytes);
        }

        public override void Serialize(ref string obj)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(obj);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}