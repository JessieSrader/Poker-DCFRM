using FASTER.core;
using System;
using System.IO;

namespace Poker_MCCFRM
{
    public class FasterNodeStore : IDisposable
    {
        private FasterKV<string, Infoset> _store;
        private ClientSession<string, Infoset, Infoset, Infoset, Empty, IFunctions<string, Infoset, Infoset, Infoset, Empty>> _session;
        private readonly string _checkpointDir;

        public FasterNodeStore(string checkpointDir = "fasterdb")
        {
            _checkpointDir = checkpointDir;
            
            // Configure log settings
            var logSettings = new LogSettings
            {
                LogDevice = Devices.CreateLogDevice(Path.Combine(checkpointDir, "hlog.log")),
                ObjectLogDevice = Devices.CreateLogDevice(Path.Combine(checkpointDir, "hlog.obj.log")),
                PageSizeBits = 25, // 32MB pages
                MemorySizeBits = 30, // 1GB in-memory buffer
                SegmentSizeBits = 30 // 1GB segments
            };

            var checkpointSettings = new CheckpointSettings
            {
                CheckpointDir = checkpointDir
            };

            // Create FASTER store
            _store = new FasterKV<string, Infoset>(
                size: 1L << 25, // 33 million entries
                logSettings: logSettings,
                checkpointSettings: checkpointSettings,
                serializerSettings: new SerializerSettings<string, Infoset>
                {
                    keySerializer = () => new StringSerializer(),
                    valueSerializer = () => new InfosetSerializer()
                }
            );

            _session = _store.For(new SimpleFunctions<string, Infoset>()).NewSession<SimpleFunctions<string, Infoset>>();
        }

        public bool TryGet(string key, out Infoset value)
        {
            var (status, output) = _session.Read(key);
            value = output;
            return status.Found;
        }

        public void Upsert(string key, Infoset value)
        {
            _session.Upsert(key, value);
        }

        public void TakeCheckpoint()
        {
            _store.TakeFullCheckpoint(out _);
            _store.CompleteCheckpointAsync().GetAwaiter().GetResult();
        }

        public void Recover()
        {
            _store.Recover();
        }

        public void Dispose()
        {
            _session?.Dispose();
            _store?.Dispose();
        }
    }
}