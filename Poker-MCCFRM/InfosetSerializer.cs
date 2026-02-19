using FASTER.core;

namespace Poker_MCCFRM
{
    public class InfosetSerializer : BinaryObjectSerializer<Infoset>
    {
        public override void Deserialize(out Infoset obj)
        {
            int length = reader.ReadInt32();
            obj = new Infoset(length);
            
            for (int i = 0; i < length; i++)
                obj.actionCounter[i] = reader.ReadSingle();
                
            for (int i = 0; i < length; i++)
                obj.regret[i] = reader.ReadSingle();
        }

        public override void Serialize(ref Infoset obj)
        {
            writer.Write(obj.actionCounter.Length);
            
            for (int i = 0; i < obj.actionCounter.Length; i++)
                writer.Write(obj.actionCounter[i]);
                
            for (int i = 0; i < obj.regret.Length; i++)
                writer.Write(obj.regret[i]);
        }
    }
}