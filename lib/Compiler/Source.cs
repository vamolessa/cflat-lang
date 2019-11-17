namespace cflat
{
	public readonly struct Source
	{
		public readonly Uri uri;
		public readonly string content;

		public Source(Uri uri, string content)
		{
			this.uri = uri;
			this.content = content;
		}
	}

	public readonly struct Uri
	{
		public readonly string value;

		public static Uri Resolve(Uri baseUri, string path)
		{
			return path.StartsWith("/") ?
				new Uri(path) :
				new Uri(baseUri.GetPrefix() + path);
		}

		public Uri(string value)
		{
			this.value = value.StartsWith("/") ? value : "/" + value;
		}

		private string GetPrefix()
		{
			var prefixLength = value.LastIndexOf("/") + 1;
			return value.Substring(0, prefixLength);
		}
	}
}