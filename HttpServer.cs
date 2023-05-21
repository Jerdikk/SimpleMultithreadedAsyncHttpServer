using System.Net.Sockets;
using System.Net;

namespace SimpleMultithreadedAsuncHttpServer
{
    class HttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly List<HttpServerClient> _clients;

        public HttpServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _clients = new List<HttpServerClient>();
        }

        public async Task ListenAsync()
        {
            try
            {
                _listener.Start();
                Console.WriteLine("Сервер стартовал на " + _listener.LocalEndpoint);
                while (true)
                {
                    try
                    {
                        TcpClient client = await _listener.AcceptTcpClientAsync();
                        Console.WriteLine("Подключение: " + client.Client.RemoteEndPoint + " > " + client.Client.LocalEndPoint);
                        lock (_clients)
                        {
                            _clients.Add(new HttpServerClient(client, c => { lock (_clients) { _clients.Remove(c); } c.Dispose(); }));
                        }
                    }
                    catch (Exception ex) { Console.WriteLine(ex.Message); break; }
                }
            }
            catch (ObjectDisposedException ex)
            {
                if (ex.ObjectName.EndsWith("Socket"))
                    Console.WriteLine("Сервер остановлен.");
                else
                    throw ex;
            }
        }

        public void Stop()
        {
            _listener.Stop();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                throw new ObjectDisposedException(typeof(HttpServer).FullName);
            disposed = true;
            _listener.Stop();
            if (disposing)
            {
                Console.WriteLine("Отключаю подключенных клиентов...");
                lock (_clients)
                {
                    foreach (HttpServerClient client in _clients)
                    {
                        client.Dispose();
                    }
                }
                Console.WriteLine("Клиенты отключены.");
            }
        }

        ~HttpServer() => Dispose(false);
    }
}