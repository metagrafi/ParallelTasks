using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Parallel
{
    class Program
    {
        static string randomString()
        {
            string path = System.IO.Path.GetRandomFileName();
            path = path.Replace(".", ""); // Remove period.
            return path;
        }
        static void createCSV(string filename, int linesNum)
        {

            string resourceName = Path.GetFullPath(@"..\..\App_Data\");

            var filePath = resourceName + filename;

            Random rnd = new Random();

            using (StreamWriter textWriter = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                var csv = new CsvWriter(textWriter);
                var list = new List<TestModel>();
                for (int i = 0; i < linesNum; i++)
                {
                    var model = new TestModel
                    {
                        Id = rnd.Next(0, int.MaxValue),
                        stringValue = randomString(),
                        floatValue = (float)rnd.NextDouble()
                    };
                    list.Add(model);
                    if (list.Count == 1000)
                    {
                        csv.WriteRecords(list);
                        list.Clear();
                    }

                }
                if (list.Count > 0)
                {
                    csv.WriteRecords(list);
                    list.Clear();
                }
            }
        }
        static void fixedRateSchedulingDemo()
        {
            var groupSize = 2;
            var totalTasks = 5;
            FixedRateScheduler frs = new FixedRateScheduler(2, new TimeSpan(0, 0, 4), groupSize, totalTasks);
            List<Task> tasks = new List<Task>();
            TaskFactory factory = new TaskFactory(frs);
            CancellationTokenSource cts = new CancellationTokenSource();
            // Use our factory to run a set of tasks. 
            Object lockObj = new Object();
            int flops = 25;

            for (int taskNum = 0; taskNum < totalTasks; taskNum++)
            {
                int iteration = taskNum;
                Task t = factory.StartNew(() =>
                {
                    Console.WriteLine("Started task {0} on thread {1}", (iteration + 1), Thread.CurrentThread.ManagedThreadId);
                    for (int i = 0; i < flops; i++)
                    {
                        lock (lockObj)
                        {
                            Console.Write("\r{0}%   ", (i + 1) * 100 / flops);
                            Thread.Sleep(50);
                        }


                    } Console.WriteLine();
                }, cts.Token);
                tasks.Add(t);
            }

            // Wait for the tasks to complete before displaying a completion message.
            Task.WaitAll(tasks.ToArray());
            cts.Dispose(); frs.Dispose();
            Console.WriteLine("\n\nSuccessful completion.");
        }
        static void ValidationFlowDemo()
        {
            //createCSV("SimpleTest.csv", 1250000);
            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch(); stopWatch.Start();
            Console.WriteLine("Started");
            var modelDataFlow = new ValidationFlow<TestModel>("Id", 300000);
            var consumer1 = modelDataFlow.ConsumeMultiAsync(modelDataFlow.buffer);
            var consumer2 = modelDataFlow.ConsumeMultiAsync(modelDataFlow.buffer);
            var consumer3 = modelDataFlow.ConsumeMultiAsync(modelDataFlow.buffer);
            var consumer4 = modelDataFlow.ConsumeMultiAsync(modelDataFlow.buffer);

            modelDataFlow.Produce(modelDataFlow.buffer, "SimpleTest.csv");
            consumer1.Wait();
            consumer2.Wait();
            consumer3.Wait();
            consumer4.Wait();
            Console.WriteLine("Consumer 1 Processed {0} models.", consumer1.Result);
            Console.WriteLine("Consumer 2 Processed {0} models.", consumer2.Result);

            Console.WriteLine("Consumer 3 Processed {0} models.", consumer3.Result);
            Console.WriteLine("Consumer 4 Processed {0} models.", consumer4.Result);

            stopWatch.Stop(); Console.WriteLine("Done!");
            Console.WriteLine("Time Elapsed {0}", stopWatch.ElapsedMilliseconds / 1000);
            CsvManager.saveResults<TestModel>("Results.csv", modelDataFlow.ValidationResult.ToList());
        }
        static void Main(string[] args)
        {
            fixedRateSchedulingDemo();
        }
    }
}
