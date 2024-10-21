using System;
using System.Collections.Generic;
using System.Linq;
using Uno;
using Uno.Extensions.ValueType;

namespace NvGet.Extensions
{
	public static class DictionaryExtensions
	{
		public static Dictionary<TKey, TValue> GetItems<TKey, TValue>(
			this IDictionary<TKey, TValue> dictionary,
			params TKey[] keys
		) => dictionary
			.Where(g => keys.Contains(g.Key))
			.ToDictionary(g => g.Key, g => g.Value);

		public static void AddOrUpdate<TKey, TValue>(
			this IDictionary<TKey, TValue> dictionary,
			TKey key,
			Func<TValue, TValue> update
		)
		{
			if(dictionary.TryGetValue(key, out var existing))
			{
				dictionary[key] = update(existing);
			}
			else
			{
				dictionary.Add(key, update(default));
			}
		}
	}
}
