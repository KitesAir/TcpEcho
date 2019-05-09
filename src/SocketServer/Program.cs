using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        private static bool _echo;

        static async Task Main(string[] args)
        {
            _echo = args.FirstOrDefault() == "echo";

            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 8087));

            Console.WriteLine("Listening on port 8087");

            listenSocket.Listen(120);

            while (true)
            {
                var socket = await listenSocket.AcceptAsync();
                _ = Task.Run(() => ProcessLines(socket));
            }
        }

        private static void ProcessLines(Socket socket)
        {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using (var stream = new NetworkStream(socket))
            using (var reader = new BinaryReader(stream))
            {
                try
                {
                    while (true)
                    {
                        var lengthPrefix = reader.ReadUInt16();
                        var lineBytes = reader.ReadBytes(lengthPrefix);
                        var line = encoding.GetString(lineBytes);
                        ProcessLine(socket, line);
                    }
                }
                catch (EndOfStreamException)
                {
                }
            }

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private static void ProcessLine(Socket socket, string s)
        {
            if (_echo)
            {
                Console.Write($"[{socket.RemoteEndPoint}]: ");
                Console.WriteLine(s);
            }
        }
    }
}
