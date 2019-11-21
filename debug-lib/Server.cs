using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace cflat.debug
{
	public sealed class Server
	{
		public enum ResponseType
		{
			Text,
			Json,
		}

		public interface IRequestHandler
		{
			ResponseType OnRequest(string uriLocalPath, NameValueCollection query, StringBuilder sb);
		}

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

				var sb = new StringBuilder();
				ResponseType responseType;
				lock (handler)
				{
					responseType = handler.OnRequest(context.Request.Url.LocalPath, context.Request.QueryString, sb);
				}

				var response = context.Response;
				response.ContentType = responseType switch
				{
					ResponseType.Text => "text/plain",
					ResponseType.Json => "application/json",
					_ => ""
				};
				var writer = new StreamWriter(response.OutputStream);
				writer.Write(sb.ToString());
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
