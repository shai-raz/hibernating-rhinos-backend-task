using System.Text;
using System.Net.Sockets;

namespace app
{
    // State object for reading client data asynchronously
    public class StateObject
    {
        // Size of receive buffer
        public const int BufferSize = 1024;

        // Receive buffer.
        public byte[] Buffer = new byte[BufferSize];

        // Received data string
        public StringBuilder Sb = new StringBuilder();

        // Client socket
        public Socket WorkSocket = null;

        // Buffer size for setting a new key
        public int SettingBufferSize = 0;

        // Buffer for setting a new key
        public string SettingKey = "";
    }
}