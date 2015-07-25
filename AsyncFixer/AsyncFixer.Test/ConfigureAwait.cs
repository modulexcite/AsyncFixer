using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ConfigureAwait
{
    class Program
    {
        static void Main(string[] args)
        {
        }

        public async Task foo()
        {
            HttpWebResponse response = null;

            await Task.Delay(100);

            int a = await Task.Run(() => { return 3; });

            using (Stream stream = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string str = await reader.ReadToEndAsync();
                }
            }
        }
    }
}
