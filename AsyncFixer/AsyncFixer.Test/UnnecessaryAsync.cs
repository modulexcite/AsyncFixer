using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnnecessaryAsync
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ReadKey();
        }

        public async static void foo1()
        {
            await Task.Delay(2000).ConfigureAwait(false);
        }

        public async static Task foo2()
        {
            
            Task t = Task.Delay(2000);
            await t.ConfigureAwait(false);
        }

        public async Task<Stream> GetRequestStreamAsync()
        {
            return await GetRequestStreamAsync();
        }

        public async Task<int> GetRequestStreamAsync(int b)
        {
            //int c = await Task.Run(() => { return 3; });
            if (b > 5)
            {
                return 3;
            }
            return await GetRequestStreamAsync(b);
        }

        public async Task<int> boo(int b)
        {
            if (b > 5)
            {
                return await Task.Run(() => { return 3; });
            }
            return await boo(b);
        }

        public async Task<int> RequestAsync(int b)
        {
            using (new StreamReader(""))
            {
                return await RequestAsync(b).ConfigureAwait(false);
            }
        }

        protected async void OnInitialize()
        {
            try
            {
                foo1();
                await Task.Delay(100);
                foo1();
            }
            catch (Exception)
            {
                
            }
            finally
            {
                
            }
        }

        public async Task<int> CreateTablesAsync(params Type[] types)
        {
            return await Task.Factory.StartNew(() =>
            {
                return 3;
            });
        }

        public async static Task foo3(int i)
        {
            if (i > 2)
            {
                await Task.Delay(2000);
            }
        }

        public async static Task foo4(int i)
        {
            if (i > 2)
            {
                await Task.Delay(3000);
                await Task.Delay(2000);
            }
        }

    }
}
