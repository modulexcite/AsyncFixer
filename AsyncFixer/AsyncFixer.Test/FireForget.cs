using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireForget
{
    class Program
    {
        static void Main(string[] args)
        {
        }

        static async void foo()
        {
            await Task.Delay(300);
            await Task.Delay(100);
        }
    }
}
