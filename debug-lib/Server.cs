using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading;

namespace cflat.debug
{
	public interface IRequestHandler
	{
		void OnRequest(string uriLocalPath, NameValueCollection query, JsonWriter writer);
	}

	public sealed class Server
	{
		private readonly HttpListener server;
		private readonly Thread thread;
		private readonly IRequestHandler handler;
		private bool serve;

		public Server(int port, IRequestHandler handler)
		{
			server = new HttpListener();
			//server.Prefixes.Add(string.Concat("http://+:", port, "/"));
			server.Prefixes.Add(string.Concat("http://localhost:", port, "/"));

			thread = new Thread(Listen);
			thread.IsBackground = true;

			this.handler = handler;

			serve = false;
		}

		public void Start()
		{
			serve = true;
			server.Start();
			thread.Start();
		}

		public void Stop()
		{
			serve = false;
			server.Stop();
			thread.Join(200);
		}

		private void Listen()
		{
			while (serve)
			{
				var result = server.BeginGetContext(OnGetContext, server);
				result.AsyncWaitHandle.WaitOne();
				Thread.Sleep(100);
			}
		}

		private void OnGetContext(System.IAsyncResult result)
		{
			try
			{
				var context = server.EndGetContext(result);

				var json = JsonWriter.New();
				lock (handler)
				{
					handler.OnRequest(context.Request.Url.LocalPath, context.Request.QueryString, json);
				}

				var response = context.Response;
				response.ContentType = "application/json";
				var writer = new StreamWriter(response.OutputStream);
				writer.Write(json.ToString());
				writer.Flush();

				context.Response.Close();
			}
			catch
			{
				serve = false;
			}
		}
	}
}
