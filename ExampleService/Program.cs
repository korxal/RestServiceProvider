using System;

namespace ExampleService
{
    class Program
    {
        static void Main(string[] args)
        {

            var Instance = new TestService();
            RestServiceProvider.RestProvider provider = new RestServiceProvider.RestProvider();
            provider.RegisterApi(Instance);
            provider.Start(5000);
            Console.WriteLine("Init Done. Try http://localhost:5000/GetTrade?TradeDate=2021-01-01");
            Console.WriteLine("Press Enter to exit");
            Console.ReadLine();
            provider.Stop();


        }
    }
}
