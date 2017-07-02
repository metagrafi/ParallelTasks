using CsvHelper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Parallel
{
    class BaseModel{
        public int Id {get;set;}
    }
    class TestModel:BaseModel
    {      
        public string stringValue { get; set; }
        public float floatValue { get; set; }

    }


    class ValidationFlow<Model>
    {
        public ValidationFlow(string key,int maxItems) 
        {
            this.maxItems = maxItems;
            buffer = new BufferBlock<Model[]>();
            ValidationResult = new BlockingCollection<Model>();
            dataCollection = new BlockingCollection<Model>();
            this.key = key;
        }
        private string key;
        private int maxItems;
        public BufferBlock<Model[]> buffer;

        public BlockingCollection<Model> ValidationResult;
        private BlockingCollection<Model> dataCollection;

        

        public void Produce(ITargetBlock<Model[]> target, string fileName)
        {
            string resourceName = Path.GetFullPath(@"..\..\App_Data\") + fileName;
            using (StreamReader reader = new StreamReader(File.OpenRead(resourceName), Encoding.UTF8))
            {
                CsvReader csv = new CsvReader(reader);
                //csvReader.Configuration.BufferSize = 4096;
                csv.Configuration.WillThrowOnMissingField = false;
                var list = new List<Model>();
                while (csv.Read())
                {
                    var record = csv.GetRecord<Model>();
                    list.Add(record);
                    if (list.Count >= maxItems)
                    {
                        target.Post(list.ToArray());
                        list.Clear();
                    }
                }
                if (list.Count > 0)
                {
                    target.Post(list.ToArray());
                    list.Clear();
                }
            }
            // Set the target to the completed state to signal to the consumer
            // that no more data will be available.
            target.Complete();
        }

        public async Task<Int64> ConsumeMultiAsync(IReceivableSourceBlock<Model[]> source)
        {
            // Initialize a counter to track the number of bytes that are processed.
            Int64 recordsProcessed = 0;

            // Read from the source buffer until the source buffer has no 
            // available output data.
            while (await source.OutputAvailableAsync())
            {
                Model[] data;
                while (source.TryReceive(out data))
                {
                    foreach (var item in data)
                    {
                        dataCollection.Add(item);
                    }
                    
                    var duplicates = dataCollection.GroupBy(d => d.GetType()
                                                                .GetProperties()
                                                                .Where(p => p.Name == key)
                                                                .First()
                                                                .GetValue(d))
                                                                .SelectMany(g => g.Skip(1));
                  
                    var items = duplicates.Except(ValidationResult);
                    foreach (var item in items) ValidationResult.Add(item);
                    recordsProcessed += data.Length;
                }
            }

            return recordsProcessed;
        }
    }
}
