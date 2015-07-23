using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Temp
{
    class Program
    {
        static void Main(string[] args)
        {
        }


        public async static Task foo3(int i)
        {
            (await Task.Run(() => { return 3; })).ToString();
        }

    }
}
