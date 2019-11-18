using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace cflat.debug
{
	public sealed class DebugServer : IDebugger
	{
		public const int DefaultPort = 14747;

		private readonly TcpListener server;
		private readonly Thread listenThread;

		private bool listen = true;
		private Buffer<Source> sources = new Buffer<Source>();

		public DebugServer(int port)
		{
			server = new TcpListener(IPAddress.Any, port);
			server.Start();

			listenThread = new Thread(Listen);
			listenThread.Start(this);
		}

		~DebugServer()
		{
			Stop();
		}

		public void Stop()
		{
			listen = false;
			listenThread.Join(1000);
		}

		private static void Listen(object obj)
		{
			var self = obj as DebugServer;
			while (self.listen)
			{
				m_client = self.server.AcceptTcpClient();
				m_stream = m_client.GetStream();
				ThreadPool.QueueUserWorkItem(RunClient);
			}
		}

		public void OnGetSources(Buffer<Source> sources)
		{
			this.sources = sources;
		}

		public void OnDebugHook(VirtualMachine vm)
		{
		}
	}
}
