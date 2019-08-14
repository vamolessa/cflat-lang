public static class ArrayHelper
{
	public static void SwapRanges<T>(this T[] self, int index, int pivot, int count)
	{
		self.Reverse(index, pivot - index);
		self.Reverse(pivot, count - pivot);
		self.Reverse(index, count);
	}

	public static void Reverse<T>(this T[] self, int index, int count)
	{
		var i = index;
		var j = index + count - 1;
		while (i < j)
		{
			var temp = self[i];
			self[i] = self[j];
			self[j] = temp;
			i++;
			j--;
		}
	}
}