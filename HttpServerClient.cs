using System.Buffers;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace SimpleMultithreadedAsuncHttpServer
{
    class HttpServerClient : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly EndPoint _remoteEndPoint;
        private readonly Task _clientTask;
        private readonly Action<HttpServerClient> _disposeCallback;

        public HttpServerClient(TcpClient client, Action<HttpServerClient> disposeCallback)
        {
            _client = client;
            _stream = client.GetStream();
            _remoteEndPoint = client.Client.RemoteEndPoint;
            _disposeCallback = disposeCallback;
            _clientTask = RunReadingLoop();
        }

        const string errorTemplate = "<html><head><title>{0}</title></head><body><center><h1>{0}</h1></center><hr><center>TcpListener server</center></body></html>";

        private async Task RunReadingLoop()
        {
            try
            {
                while (true)
                {
                    (HttpRequestMessage request, HttpStatusCode status) = await ReceivePacket().ConfigureAwait(false);
                    if (request != null)
                        Console.WriteLine($"<< {request.Method.Method} {request.RequestUri}");
                    else
                        Console.WriteLine($"<< ??");
                    //Console.WriteLine(request);
                    using HttpResponseMessage response = new HttpResponseMessage(status);
                    if (request != null)
                        foreach (var c in request?.Headers.Connection)
                            response.Headers.Connection.Add(c);
                    else
                        response.Headers.Connection.Add("close");
                    if (status == HttpStatusCode.OK)
                    {
                        if (request.RequestUri.ToString() == "/")
                        {
                            Console.WriteLine(">> /");
                            response.Content = CreateHtmlContent($"<html><head><title>Главная страница</title></head><body>Привет, {_remoteEndPoint}!</body></html>");
                        }
                        else
                        {
                            response.StatusCode = HttpStatusCode.NotFound;
                            Console.WriteLine($">> {(int)response.StatusCode} {response.ReasonPhrase}");
                            response.Content = CreateHtmlContent(string.Format(errorTemplate, $"{(int)response.StatusCode} {response.ReasonPhrase}"));
                        }
                    }
                    else
                    {
                        Console.WriteLine($">> {(int)response.StatusCode} {response.ReasonPhrase}");
                        response.Content = CreateHtmlContent(string.Format(errorTemplate, $"{(int)response.StatusCode} {response.ReasonPhrase}"));
                    }
                    // Console.WriteLine(response);
                    await SendResponse(response).ConfigureAwait(false);
                    if (response.Headers.Connection.Contains("close"))
                        break;
                }
                Console.WriteLine("Подключение к " + _remoteEndPoint + " закрыто клиентом.");
                _stream.Close();
            }
            catch (HttpRequestException)
            {
                Console.WriteLine("Подключение к " + _remoteEndPoint + " разорвано клиентом.");
            }
            catch (IOException)
            {
                Console.WriteLine("Подключение к " + _remoteEndPoint + " закрыто сервером.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.GetType().Name + ": " + ex.Message);
            }
            if (!disposed)
                _disposeCallback(this);
        }

        private HttpContent CreateHtmlContent(string text)
        {
            StringContent content = new StringContent(text, Encoding.UTF8, "text/html");
            content.Headers.ContentLength = content.Headers.ContentLength;
            return content;
        }

        private async Task SendResponse(HttpResponseMessage response)
        {
            // сначала пишем header
            using (StreamWriter sw = new StreamWriter(_stream, leaveOpen: true))
            {
                sw.WriteLine($"HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                sw.Write(response.Headers);
                sw.WriteLine(response.Content?.Headers.ToString() ?? "");
            }
            // затем пишем content
            if (response.Content != null)
                await response.Content.CopyToAsync(_stream);
        }

        private async Task<(HttpRequestMessage, HttpStatusCode)> ReceivePacket()
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage();
                string requestHeader = await ReadLineAsync().ConfigureAwait(false);
                string[] headerTokens = requestHeader.Split(" ");
                if (headerTokens.Length != 3)
                    return (null, HttpStatusCode.BadRequest);
                request.Method = new HttpMethod(headerTokens[0]);
                request.RequestUri = new Uri(headerTokens[1], UriKind.Relative);
                string[] protocolTokens = headerTokens[2].Split('/');
                if (protocolTokens.Length != 2 || protocolTokens[0] != "HTTP")
                    return (null, HttpStatusCode.BadRequest);
                request.Version = Version.Parse(protocolTokens[1]);
                MemoryStream ms = new MemoryStream();
                HttpContent content = new StreamContent(ms);
                request.Content = content;
                while (true)
                {
                    string headerLine = await ReadLineAsync().ConfigureAwait(false);
                    if (headerLine.Length == 0)
                        break;
                    string[] tokens = headerLine.Split(":", 2);
                    if (tokens.Length == 2)
                    {
                        foreach (HttpRequestHeader h in Enum.GetValues(typeof(HttpRequestHeader)))
                        {
                            if (tokens[0].ToLower() == h.GetName().ToLower())
                            {
                                if ((int)h >= 10 && (int)h <= 19) // if Entity Header
                                    request.Content.Headers.Add(tokens[0], tokens[1]);
                                else
                                    request.Headers.Add(tokens[0], tokens[1]);
                                break;
                            }
                        }
                    }
                }
                long length = request.Content.Headers?.ContentLength ?? 0;

                if (length > 0)
                {
                    await CopyBytesAsync(_stream, ms, (int)length);
                    ms.Position = 0;
                }
                return (request, HttpStatusCode.OK);
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch
            {
                return (null, HttpStatusCode.InternalServerError);
            }
        }

        private async Task CopyBytesAsync(Stream source, Stream target, int count)
        {
            const int bufferSize = 65536;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while (count > 0)
                {
                    int bytesReceived = await source.ReadAsync(buffer.AsMemory(0, Math.Min(count, bufferSize)));
                    if (bytesReceived == 0)
                        break;
                    await target.WriteAsync(buffer.AsMemory(0, bytesReceived));
                    count -= bytesReceived;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task<string> ReadLineAsync() => await Task.Run(ReadLine);

        private string ReadLine()
        {
            LineState lineState = LineState.None;
            StringBuilder sb = new StringBuilder(128);
            while (true)
            {
                int b = _stream.ReadByte();
                switch (b)
                {
                    case -1:
                        throw new HttpRequestException("Подключение разорвано.");
                    case '\r':
                        if (lineState == LineState.None)
                            lineState = LineState.CR;
                        else
                            throw new ProtocolViolationException("Неожиданный CR в заголовке запроса.");
                        break;
                    case '\n':
                        if (lineState == LineState.CR)
                            lineState = LineState.LF;
                        else
                            throw new ProtocolViolationException("Неожиданный LF в заголовке запроса.");
                        break;
                    default:
                        lineState = LineState.None;
                        sb.Append((char)b);
                        break;
                }
                if (lineState == LineState.LF)
                    break;
            }
            return sb.ToString();
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
            if (_client.Connected)
            {
                _stream.Close();
                _clientTask.Wait();
            }
            if (disposing)
            {
                _client.Dispose();
            }
        }

        ~HttpServerClient() => Dispose(false);
    }
}