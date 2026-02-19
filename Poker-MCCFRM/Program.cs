    using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SnapCall;

namespace Poker_MCCFRM
{
    static class Program
    {
        static void Main(string[] args)
        {
            CreateIndexers();
            Global.handEvaluator = new Evaluator();
            CalculateInformationAbstraction();
            TrainDiscountedCFR();
        }

        private static void CreateIndexers()
        {
            Console.Write("Creating 2 card index... ");
            Global.indexer_2 = new HandIndexer(new int[1] { 2 });
            Console.WriteLine(Global.indexer_2.roundSize[0] + " non-isomorphic hands found");

            Console.Write("Creating 2 & 3 card index... ");
            Global.indexer_2_3 = new HandIndexer(new int[2] { 2, 3 });
            Console.WriteLine(Global.indexer_2_3.roundSize[1] + " non-isomorphic hands found");

            Console.Write("Creating 2 & 4 card index... ");
            Global.indexer_2_4 = new HandIndexer(new int[2] { 2, 4 });
            Console.WriteLine(Global.indexer_2_4.roundSize[1] + " non-isomorphic hands found");

            Console.Write("Creating 2 & 5 card index... ");
            Global.indexer_2_5 = new HandIndexer(new int[2] { 2, 5 });
            Console.WriteLine(Global.indexer_2_5.roundSize[1] + " non-isomorphic hands found");

            Console.Write("Creating 2 & 5 & 2 card index... ");
            Global.indexer_2_5_2 = new HandIndexer(new int[3] { 2, 5, 2 });
            Console.WriteLine(Global.indexer_2_5_2.roundSize[2] + " non-isomorphic hands found");

            Console.Write("Creating 2 & 3 & 1 card index... ");
            Global.indexer_2_3_1 = new HandIndexer(new int[3] { 2, 3, 1 });
            Console.WriteLine(Global.indexer_2_3_1.roundSize[2] + " non-isomorphic hands found");

            Console.Write("Creating 2 & 3 & 1 & 1 card index... ");
            Global.indexer_2_3_1_1 = new HandIndexer(new int[4] { 2, 3, 1, 1 });
            Console.WriteLine(Global.indexer_2_3_1_1.roundSize[3] + " non-isomorphic hands found");
        }
        
        private static void CalculateInformationAbstraction()
        {
            Console.WriteLine("Calculating information abstractions... ");
            OCHSTable.Init();
            EMDTable.Init();
        }
        
        private static void TrainDiscountedCFR()
        {
            Console.WriteLine("Starting Discounted Counterfactual Regret Minimization (DCFR)...");

            long StrategyInterval = 1000;
            long StrategyDiscountInterval = 10000;
            long SaveToDiskInterval = 1000000;
            long testGamesInterval = 100000;

            long globalIteration = 0;
            object lockObj = new object();
            bool running = true;

            LoadFromFile();

            Trainer mainTrainer = new Trainer(0);
            mainTrainer.EnumerateActionSpace();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // Create worker tasks
            var tasks = new Task[Global.NOF_THREADS];
            
            for (int threadIdx = 0; threadIdx < Global.NOF_THREADS; threadIdx++)
            {
                int index = threadIdx; // Capture for closure
                tasks[index] = Task.Run(() =>
                {
                    Trainer trainer = new Trainer(index);
                    
                    while (running)
                    {
                        long currentIteration = Interlocked.Increment(ref globalIteration);
                        
                        // Each thread does CFR traversals
                        for (int traverser = 0; traverser < Global.nofPlayers; traverser++)
                        {
                            trainer.TraverseDiscountedCFR(traverser, (int)currentIteration);
                        }
                    }
                });
            }

            // Main monitoring/maintenance task
            Task maintenanceTask = Task.Run(() =>
            {
                while (running)
                {
                    long currentIteration = Interlocked.Read(ref globalIteration);
                    
                    // Progress reporting
                    if (currentIteration % 1000 == 0)
                    {
                        Console.WriteLine("Training steps " + currentIteration);
                    }

                    // Statistics and game display
                    if (currentIteration % testGamesInterval == 0)
                    {
                        mainTrainer.PrintStartingHandsChart();
                        mainTrainer.PrintStatistics(currentIteration);

                        Console.WriteLine("Sample games (against self)");
                        for (int z = 0; z < 20; z++)
                        {
                            mainTrainer.PlayOneGame();
                        }

                        Console.WriteLine("Iterations per second: {0}", 
                            1000 * currentIteration / (stopwatch.ElapsedMilliseconds + 1));
                        Console.WriteLine();
                    }
                    
                    // Strategy updates
                    if (currentIteration % StrategyInterval == 0)
                    {
                        for (int traverser = 0; traverser < Global.nofPlayers; traverser++)
                        {
                            mainTrainer.UpdateStrategy(traverser);
                        }
                    }
                    
                    // Apply strategy discounting
                    if (currentIteration % StrategyDiscountInterval == 0)
                    {
                        mainTrainer.DiscountInfosetsStrategies((int)currentIteration);
                    }
                    
                    // Save to disk
                    if (currentIteration % SaveToDiskInterval == 0)
                    {
                        Console.WriteLine("Saving nodeMap to disk...");
                        SaveToFile();
                    }
                    
                    Thread.Sleep(10); // Prevent tight loop
                }
            });

            Task.WaitAll(tasks);
        }
        
        private static void WritePlotStatistics(float bbWins)
        {
            using (StreamWriter file = new StreamWriter("progress.txt", true))
            {
                file.WriteLine(Math.Round(bbWins, 2));
            }
        }
        
        private static void SaveToFile()
        {
            Console.WriteLine("Saving dictionary to file {0}", "nodeMap.txt");

            using FileStream fs = File.OpenWrite("nodeMap.txt");
            using BinaryWriter writer = new BinaryWriter(fs);
            foreach (var pair in Global.nodeMap)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(pair.Key);

                writer.Write(bytes.Length);
                writer.Write(bytes);

                writer.Write(pair.Value.actionCounter.Length);
                for (int i = 0; i < pair.Value.actionCounter.Length; i++)
                    writer.Write(pair.Value.actionCounter[i]);

                for (int i = 0; i < pair.Value.regret.Length; i++)
                    writer.Write(pair.Value.regret[i]);
            }
        }
        
        private static void LoadFromFile()
        {
            if (!File.Exists("nodeMap.txt"))
                return;
            Console.WriteLine("Loading nodes from file nodeMap.txt...");
            using FileStream fs = File.OpenRead("nodeMap.txt");
            using BinaryReader reader = new BinaryReader(fs);
            Global.nodeMap = new ConcurrentDictionary<string, Infoset>();

            try
            {
                while (true)
                {
                    int keyLength = reader.ReadInt32();
                    byte[] key = reader.ReadBytes(keyLength);
                    string keyString = Encoding.ASCII.GetString(key);
                    int valueLength = reader.ReadInt32();

                    Infoset infoset = new Infoset(valueLength);
                    for (int i = 0; i < valueLength; i++)
                    {
                        infoset.actionCounter[i] = reader.ReadInt32();
                    }
                    for (int i = 0; i < valueLength; i++)
                    {
                        infoset.regret[i] = reader.ReadInt32();
                    }
                    Global.nodeMap.TryAdd(keyString, infoset);
                }
            }
            catch (EndOfStreamException e)
            {
                return;
            }
        }
        
        private static byte[] SerializeToBytes<T>(T item)
        {
            var formatter = new BinaryFormatter();
            using var stream = new MemoryStream();
            formatter.Serialize(stream, item);
            stream.Seek(0, SeekOrigin.Begin);
            return stream.ToArray();
        }
    }
}
