using CsvHelper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parallel
{
    public class CsvManager
    {
        
        public static void saveResults<M>(string filename, ICollection<M> vResults)
        {
            string resourceName = Path.GetFullPath(@"..\..\App_Data\");

            var filePath = resourceName + filename;

            Random rnd = new Random();

            using (StreamWriter textWriter = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                var csv = new CsvWriter(textWriter);
                csv.WriteRecords(vResults);
               
            }
        }
       
    }
}
