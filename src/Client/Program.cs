using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpEcho
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var messageSize = args.FirstOrDefault();

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("Connecting to port 8087");

            clientSocket.Connect(new IPEndPoint(IPAddress.Loopback, 8087));

            if (messageSize == null)
            {
                while (true)
                {
                    var line = Console.ReadLine();
                    var message = FrameMessage(line);
                    await clientSocket.SendAsync(message, SocketFlags.None);
                }
            }
            else
            {
                var count = int.Parse(messageSize);
                var line = new string('a', count);
                var message = FrameMessage(line);
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                for (int i = 0; i < 1_000_000; i++)
                {
                    await clientSocket.SendAsync(message, SocketFlags.None);
                }
                stopwatch.Stop();

                Console.WriteLine($"Elapsed {stopwatch.Elapsed.TotalSeconds:F} sec.");
            }

            ArraySegment<byte>[] FrameMessage(string line)
            {
                var buffer = encoding.GetBytes(line);
                var lengthPrefix = BitConverter.GetBytes((ushort)buffer.Length);
                return new ArraySegment<byte>[] {
                    new ArraySegment<byte>(lengthPrefix),
                    new ArraySegment<byte>(buffer)
                };
            }
        }
    }
}
