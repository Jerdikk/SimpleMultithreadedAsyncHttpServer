namespace SimpleMultithreadedAsuncHttpServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (HttpServer server = new HttpServer(8080)) // порт 8080
            {
                Task servertask = server.ListenAsync();
                while (true)
                {
                    string input = Console.ReadLine();
                    if (input == "stop")
                    {
                        Console.WriteLine("Остановка сервера...");
                        server.source.Cancel();
                        server.Stop();
                        break;
                    }
                }
                await servertask;
            }
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey(true);
        }
    }

    enum LineState
    {
        None,
        LF,
        CR
    }
}