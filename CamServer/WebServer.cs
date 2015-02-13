using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CamServer
{
    public class WebServer : IDisposable
    {
        Dictionary<int, bool> connections = new Dictionary<int, bool>();
        SocketServerBase socketServer;
        string Request = "";

        byte[] header1 = Encoding.UTF8.GetBytes(@"
--boundarydonotcross
Content-Type: image/jpeg
Content-Length: ");
        byte[] header2 = Encoding.UTF8.GetBytes(@"
X-Timestamp: 0000000000.000000

");

        public WebServer()
            : this(8080)
        {
        }

        public WebServer(int port)
        {
            new System.Threading.Thread(()=> {
                socketServer = new SocketServerBase(port);
                socketServer.OnRead += new SocketServerBase.ConnectionDelegate(socketServer_OnRead);
                socketServer.Active();
                socketServer.OnConnect += (soc) =>
                {
                    connections.Add(socketServer.IndexOf(soc), false);
                    MainWindow.This.Log("Pair connected. Idx: " + socketServer.IndexOf(soc).ToString() + ", conns: " + socketServer.ActiveConnections.ToString());
                };
                socketServer.OnDisconnect += (soc) =>
                {
                    connections.Remove(socketServer.IndexOf(soc));
                    MainWindow.This.Log("Pair disconnected. Idx: " + socketServer.IndexOf(soc).ToString() + ", conns: " + socketServer.ActiveConnections.ToString());
                };
            }).Start();

        }

        void socketServer_OnRead(System.Net.Sockets.Socket soc)
        {
            Request += socketServer.ReceivedText.Replace("\0", "");
            int idx = socketServer.IndexOf(soc);

            if (ParseRequest(Request, idx))
            {
                if (Request == "")
                {
                    socketServer.SendText(@"HTTP/1.0 200 OK
Connection: close
Server: MJPG-Streamer/0.2
Cache-Control: no-store, no-cache, must-revalidate, pre-check=0, post-check=0, max-age=0
Pragma: no-cache
Expires: Mon, 3 Jan 2000 12:34:56 GMT
Content-Type: multipart/x-mixed-replace;boundary=boundarydonotcross

", socketServer.IndexOf(soc));
                }
            }
            else
            {
                socketServer.SendText("HTTP/1.0 403 Forbidden", socketServer.IndexOf(soc));
            }
        }

        public void SendImg(byte[] img)
        {
            for (int i = 0; i < socketServer.ActiveConnections; i++)
            {
                if (i > 5) //to avoid too many conns
                    continue;
                socketServer.SendBytes(header1, i);
                socketServer.SendBytes(Encoding.UTF8.GetBytes(img.Length.ToString()), i);
                socketServer.SendBytes(header2, i);
                socketServer.SendBytes(img, i);
            }
        }

        private bool ParseRequest(string p, int idx)
        {
            if (p.Contains("\r\n\r\n"))
            {
                if (connections.ContainsKey(idx))
                {
                    connections[idx] = true;
                    Request = "";
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            socketServer.Deactive();
        }

        internal void DisconnectAll()
        {
            for (int i = 0; i < socketServer.ActiveConnections; i++)
            {
                socketServer.CloseConnection(i);
            }
        }
    }
}
