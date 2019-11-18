using System.Text;

namespace cflat.debug
{
	public readonly struct JsonWriter
	{
		public readonly struct ObjectScope : System.IDisposable
		{
			private readonly StringBuilder sb;

			public ObjectScope(StringBuilder w)
			{
				this.sb = w;
				w.Append("{");
			}

			public void Boolean(string key, bool value)
			{
				sb
					.Append('"')
					.Append(key)
					.Append("\":")
					.Append(value ? "true," : "false,");
			}

			public void Number(string key, float value)
			{
				sb
					.Append('"')
					.Append(key)
					.Append("\":")
					.Append(value)
					.Append(',');
			}

			public void String(string key, string value)
			{
				sb
					.Append('"')
					.Append(key)
					.Append("\":\"")
					.Append(value)
					.Append("\",");
			}

			public ObjectScope Object(string key)
			{
				sb
					.Append('"')
					.Append(key)
					.Append("\":");
				return new ObjectScope(sb);
			}

			public ArrayScope Array(string key)
			{
				sb
					.Append('"')
					.Append(key)
					.Append("\":");
				return new ArrayScope(sb);
			}

			public void Dispose()
			{
				if (sb[sb.Length - 1] == ',')
					sb.Remove(sb.Length - 1, 1);
				sb.Append("},");
			}
		}

		public readonly struct ArrayScope : System.IDisposable
		{
			private readonly StringBuilder sb;

			public ArrayScope(StringBuilder w)
			{
				this.sb = w;

				w.Append("[");
			}

			public void Boolean(bool value)
			{
				sb.Append(value ? "true," : "false,");
			}

			public void Number(float value)
			{
				sb.Append(value).Append(',');
			}

			public void String(string value)
			{
				sb.Append('\"').Append(value).Append("\",");
			}

			public ObjectScope Object
			{
				get { return new ObjectScope(sb); }
			}

			public ArrayScope Array
			{
				get { return new ArrayScope(sb); }
			}

			public void Dispose()
			{
				if (sb[sb.Length - 1] == ',')
					sb.Remove(sb.Length - 1, 1);
				sb.Append("],");
			}
		}

		private readonly StringBuilder sb;

		public static JsonWriter New()
		{
			return new JsonWriter(new StringBuilder());
		}

		public JsonWriter(StringBuilder sb)
		{
			this.sb = sb;
		}

		public override string ToString()
		{
			if (sb[sb.Length - 1] == ',')
				sb.Remove(sb.Length - 1, 1);
			return sb.ToString();
		}

		public ObjectScope Object
		{
			get { return new ObjectScope(sb); }
		}

		public ArrayScope Array
		{
			get { return new ArrayScope(sb); }
		}
	}
}