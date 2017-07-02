# FixedRateScheduler 

It is a task scheduler that can limit the number of threads used by the app.

There is a  demo method that helps to understand the usage. 

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

We instantiate the scheduler with

	FixedRateScheduler frs = new FixedRateScheduler(2, new TimeSpan(0, 0, 4), groupSize, totalTasks);

The first parameter is the maximum degree of parallelism, the number of maximum threads the app may use to complete the tasks.

The second parameter is the period between the completion of the groups of tasks. In this demo we run groupSize tasks every 4 seconds until the tasks completed reaches totalTasks.

# ValidationFlow

The purpose of validation flow is to demonstrate some features of the TPL (Task Parallel Library).



In computing the producer consumer problem is a classic example of a multi-process synchronization.



The problem is that we want to check if the records in a large csv file have a unique identifiers keys, before we import them into a database.



ValidationFlow<Model> is the class that uses a producer who reads the rows from the large Csv file and posts them to the target buffer



public void Produce(ITargetBlock<Model[]> target, string fileName) 



and multi-asynchronous cconsuming function 

public async Task<Int64> ConsumeMultiAsync(IReceivableSourceBlock<Model[]> source)

which searches for duplicate identifier keys.


We test the speed of the validation in Program.cs 


        static void Main(string[] args)

        {

            createCSV("SimpleTest.csv", 1250000);

            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch(); stopWatch.Start();

            Console.WriteLine("Started");

            var modelDataFlow = new ValidationFlow<TestModel>("Id",300000);

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

            CsvManager.saveResults<TestModel>("Results.csv", modelDataFlow.ValidationResult.ToList())45
        }
        
     Using a 16GB of ram I7 processor computer, it takes 7sec to find all duplicates in a 1 million and quarter of a million of records!
