using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
                _ = ProcessLinesAsync(socket);
            }
        }

        private static async Task ProcessLinesAsync(Socket socket)
        {
            Console.WriteLine($"[{socket.RemoteEndPoint}]: connected");

            var pipe = new Pipe();
            Task writing = FillPipeAsync(socket, pipe.Writer);
            Task reading = ReadPipeAsync(socket, pipe.Reader);

            await Task.WhenAll(reading, writing);

            Console.WriteLine($"[{socket.RemoteEndPoint}]: disconnected");
        }

        private static async Task FillPipeAsync(Socket socket, PipeWriter writer)
        {
            const int minimumBufferSize = 512;

            while (true)
            {
                try
                {
                    // Request a minimum of 512 bytes from the PipeWriter
                    Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // Tell the PipeWriter how much was read
                    writer.Advance(bytesRead);
                }
                catch
                {
                    break;
                }

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Signal to the reader that we're done writing
            writer.Complete();
        }

        private const int lengthPrefixSize = 2; // number of bytes in the length prefix
        private static ushort ParseLengthPrefix(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt16LittleEndian(buffer);

        private static async Task ReadPipeAsync(Socket socket, PipeReader reader)
        {
            byte[] lengthPrefixBuffer = new byte[lengthPrefixSize];

            while (true)
            {
                ReadResult result = await reader.ReadAsync();

                ReadOnlySequence<byte> buffer = result.Buffer;

                while (buffer.Length > lengthPrefixSize)
                {
                    // Read and parse the length prefix
                    buffer.Slice(0, lengthPrefixSize).CopyTo(lengthPrefixBuffer);
                    var lengthPrefix = ParseLengthPrefix(lengthPrefixBuffer);

                    // If we haven't read the entire packet yet, then wait.
                    if (buffer.Length < lengthPrefixSize + lengthPrefix)
                        break;

                    // Read the data packet
                    var line = buffer.Slice(lengthPrefixSize, lengthPrefix);
                    ProcessLine(socket, line);

                    buffer = buffer.Slice(lengthPrefixSize + lengthPrefix);
                }

                // We sliced the buffer until no more data could be processed
                // Tell the PipeReader how much we consumed and how much we left to process
                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            reader.Complete();
        }

        private static void ProcessLine(Socket socket, in ReadOnlySequence<byte> buffer)
        {
            if (_echo)
            {
                Console.Write($"[{socket.RemoteEndPoint}]: ");
                foreach (var segment in buffer)
                {
#if NETCOREAPP2_1
                Console.Write(Encoding.UTF8.GetString(segment.Span));
#else
                    Console.Write(Encoding.UTF8.GetString(segment));
#endif
                }
                Console.WriteLine();
            }
        }
    }

#if NET461
    internal static class Extensions
    {
        public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags)
        {
            var arraySegment = GetArray(memory);
            return SocketTaskExtensions.ReceiveAsync(socket, arraySegment, socketFlags);
        }

        public static string GetString(this Encoding encoding, ReadOnlyMemory<byte> memory)
        {
            var arraySegment = GetArray(memory);
            return encoding.GetString(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
        }

        private static ArraySegment<byte> GetArray(Memory<byte> memory)
        {
            return GetArray((ReadOnlyMemory<byte>)memory);
        }

        private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory)
        {
            if (!MemoryMarshal.TryGetArray(memory, out var result))
            {
                throw new InvalidOperationException("Buffer backed by array was expected");
            }

            return result;
        }
    }
#endif
}
