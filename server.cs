using System;
using System.Collections.Generic;
using System.Text;

namespace HTTPServer
{
    class Server
    {
        TcpListener Listener;

        public Server(int Port)
        {
            Listener = new TcpListener(IPAddress.Any, Port);
            Listener.Start();

            while (true)
            {
                TcpClient Client = Listener.AcceptTcpClient();

                Thread Thread = new Thread(new ParameterizedThreadStart(ClientThread));

                Thread.Start(Client);
            }
        }

        ~Server()
        {
            if (Listener != null)
            {
                Listener.Stop();
            }
        }

        static void Main(string[] args)
        {
            new Server(80);

            int MaxThreadsCount = Environment.ProcessorCount * 4;

            ThreadPool.SetMaxThreads(MaxThreadsCount, MaxThreadsCount);
            ThreadPool.SetMinThreads(2, 2);
        }
    }

    class Client
    {
        public Client(TcpClient client)
        {
            string Html = "<html><body><h1>It's works!</h1></body></html>";
            string Str = "HTTP/1.1 200 OK\nContent-type: text/html\nContent-Length:" + Html.Length.ToString() + "\n\n" + Html;
            string Request = "";

            byte[] Buffer = new byte[1024];

            int Count;

            while ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
            {
                Request += Encoding.ASCII.GetString(Buffer, 0, Count);

                if (Request.IndexOf("\r\n\r\n") >= 0 || Request.Length > 4096)
                {
                    break;
                }
            }


            byte[] Buffer = Encoding.ASCII.GetBytes(Str);

            Match ReqMatch = Regex.Match(Request, @"^\w+\s+([^\s\?]+)[^\s]*\s+HTTP/.*|");

            if (ReqMatch == Match.Empty)
            {
                SendError(Client, 400);
                return;
            }

            string RequestUri = ReqMatch.Groups[1].Value;

            RequestUri = Uri.UnescapeDataString(RequestUri);

            if (RequestUri.IndexOf("..") >= 0)
            {
                SendError(Client, 400);
                return;
            }

            if (RequestUri.EndsWith("/"))
            {
                RequestUri += "index.html";
            }

            string FilePath = "www/" + RequestUri;

            if (!File.Exists(FilePath))
            {
                SendError(Client, 404);
                return;
            }

            string Extension = RequestUri.Substring(RequestUri.LastIndexOf('.'));

            string ContentType = "";

            switch (Extension)
            {
                case ".htm":
                case ".html":
                    ContentType = "text/html";
                    break;
                case ".css":
                    ContentType = "text/stylesheet";
                    break;
                case ".js":
                    ContentType = "text/javascript";
                    break;
                case ".jpg":
                    ContentType = "image/jpeg";
                    break;
                case ".jpeg":
                case ".png":
                case ".gif":
                    ContentType = "image/" + Extension.Substring(1);
                    break;
                default:
                    if (Extension.Length > 1)
                    {
                        ContentType = "application/" + Extension.Substring(1);
                    }
                    else
                    {
                        ContentType = "application/unknown";
                    }
                    break;
            }

            FileStream FS;
            try
            {
                FS = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception)
            {
                SendError(Client, 500);
                return;
            }

            string Headers = "HTTP/1.1 200 OK\nContent-Type: " + ContentType + "\nContent-Length: " + FS.Length + "\n\n";
            byte[] HeadersBuffer = Encoding.ASCII.GetBytes(Headers);
            Client.GetStream().Write(HeadersBuffer, 0, HeadersBuffer.Length);

            while (FS.Position < FS.Length)
            {
                Count = FS.Read(Buffer, 0, Buffer.Length);

                Client.GetStream().Write(Buffer, 0, Count);
            }

            FS.Close();

            Client.GetStream().Write(Buffer, 0, Buffer.Length);

            Client.Close();
        }
    }

    new Client(Listener.AcceptTcpClient());

    static void ClientThread(Object StateInfo)
    {
        new Client((TcpClient)StateInfo);
    }

    private void SendError(TcpClient Client, int Code)
    {
        string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();

        string Html = "<html><body><h1>" + CodeStr + "</h1></body></html>";

        string Str = "HTTP/1.1 " + CodeStr + "\nContent-type: text/html\nContent-Length:" + Html.Length.ToString() + "\n\n" + Html;

        byte[] Buffer = Encoding.ASCII.GetBytes(Str);

        Client.GetStream().Write(Buffer, 0, Buffer.Length);
        Client.Close();
    }
}