using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Specialized;

namespace app
{
    // State object for reading client data asynchronously
    public class StateObject
    {
        // Size of receive buffer
        public const int BufferSize = 1024;

        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];

        // Received data string
        public StringBuilder sb = new StringBuilder();

        // Client socket
        public Socket workSocket = null;

        // Buffer size for setting a new key
        public int setBufferSize = 0;

        // Buffer for setting a new key
        public string setKey = "";
    }

    public class AsynchronousSocketListener
    {
        private const int MAX_VALUE_BYTES = 1000000 * 128; // 128MB
        private static int currentValueBytes = 0;
        private const int PORT = 10011;
        private const string OK = "OK";
        private const string MISSING = "MISSING";
        private static OrderedDictionary storage = new OrderedDictionary();

        // Thread signal.  
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public static void StartListening()
        {
            IPAddress ipAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PORT);

            // Create a TCP/IP socket
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // Bind the socket to the local endpoint and listen for incoming connections
                listener.Bind(localEndPoint);
                listener.Listen();

                while (true)
                {
                    // Set the event to nonsignaled state. 
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections. 
                    Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                    // Wait until a connection is made before continuing.
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\r\nPress ENTER to continue...");
            Console.Read();
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue
            allDone.Set();

            // Get the socket that handles the client request
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object
            StateObject state = new StateObject();
            state.workSocket = handler;

            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket, until there's \r\n
            // or until the connection is closed.
            try
            {
                int bytesRead = handler.EndReceive(ar);
                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.UTF8.GetString(state.buffer, 0, bytesRead));

                    content = state.sb.ToString();
                    if (content.IndexOf("\r\n") > -1) // End of request
                    {
                        HandleRequest(state, content);
                    }
                    else // Keep reading
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void HandleRequest(StateObject state, string content)
        {
            // Retrieve the handler socket from the asynchronous state object  
            Socket handler = state.workSocket;

            // Split the content by spaces
            var request = content.Trim().Split(' ');

            if (request[0] == "get") /* GET */
            {
                Console.WriteLine("get request received");
                // Check if the request is valid
                if (request.Length != 2)
                {
                    Console.WriteLine("Error: Usage - get <key>");
                    Send(state, "Error: Usage - get <key>\r\n");
                    return;
                }

                string key = request[1].Trim();
                if (storage.Contains(key))
                {
                    Console.WriteLine("Found key {0}", key);

                    string message = $"{OK} {((string)storage[key]).Length}\r\n{storage[key]}";
                    Send(state, message);
                }
                else
                {
                    Console.WriteLine("Key {0} not found", key);
                    Send(state, $"{MISSING}\r\n");
                }
            }
            else if (request[0] == "set") /* SET */
            {
                Console.WriteLine("set request received");
                int size;

                // check if the request is valid
                if (request.Length != 3)
                {
                    Console.WriteLine("Error: Usage - set <key> <size>");
                    Send(state, "Error: Usage - set <key> <size>\r\n");
                    return;
                }
                else if (!int.TryParse(request[2].Trim(), out size))
                {
                    Console.WriteLine("Error: Size has to be a number");
                    Send(state, $"Error: Size has to be a number (Received: {request[2]})\r\n");
                    return;
                }

                if (size > MAX_VALUE_BYTES)
                {
                    Console.WriteLine("Error: Size has to be less than {0}", MAX_VALUE_BYTES);
                    Send(state, $"Error: Size has to be less than {MAX_VALUE_BYTES}\r\n");
                    return;
                }

                var key = request[1];

                // if size is greater than MAX_VALUE_BYTES,
                // try removing oldest key, until there's enough space
                while (currentValueBytes + size > MAX_VALUE_BYTES)
                {
                    foreach (var o in storage.Keys)
                    {
                        currentValueBytes -= ((string)storage[o]).Length;
                        storage.Remove(o);
                        break;
                    }
                }

                // Reset string builder
                state.sb.Clear();

                // Wait for size bytes from client
                state.setBufferSize = size;
                state.setKey = key;
                handler.BeginReceive(state.buffer, 0, state.setBufferSize, 0,
                    new AsyncCallback(SetCallback), state);

            }
            else
            {
                Console.WriteLine("Unknown request received");
                Send(state, "Unknown request received\r\n");
            }
        }

        private static void SetCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object. 
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            try
            {
                // Read setBufferSize bytes from the client socket
                int bytesRead = handler.EndReceive(ar);
                if (bytesRead > 0)
                {
                    state.sb.Append(Encoding.UTF8.GetString(state.buffer, 0, bytesRead));

                    content = state.sb.ToString();
                    if (content.Length == state.setBufferSize) // bufferSize bytes have been read
                    {
                        storage[state.setKey] = state.sb.ToString();
                        currentValueBytes += state.setBufferSize;
                        Console.WriteLine($"Set {state.setKey} to {state.sb.ToString()}");
                        Send(state, $"{OK}\r\n");
                    }
                    else // Keep reading
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(SetCallback), state);
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /* Send a message to the client through the socket */
        private static void Send(StateObject state, String data)
        {
            // Convert the string data to byte data using UTF8 encoding.  
            byte[] byteData = Encoding.UTF8.GetBytes(data);

            // Retrieve the  handler socket from the asynchronous state object.  
            Socket handler = state.workSocket;

            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), state);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the handler socket  
                // from the asynchronous state object.  
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                ReceiveNewInput(state);
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /* Reset the String builder and set the socket to receive more input */
        private static void ReceiveNewInput(StateObject state)
        {
            // Retrieve the handler socket from the asynchronous state object.  
            Socket handler = state.workSocket;

            state.sb.Clear();
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public static int Main(String[] args)
        {
            StartListening();
            return 0;
        }
    }
}