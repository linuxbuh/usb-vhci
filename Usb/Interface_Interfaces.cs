/*
 * Interface_Interfaces.cs -- USB related classes
 *
 * Copyright (C) 2007-2008 Conemis AG Karlsruhe Germany
 * Copyright (C) 2007-2009 Michael Singer <michael@a-singer.de>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along
 * with this program; if not, write to the Free Software Foundation, Inc.,
 * 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Usb
{
	partial class Interface :
		IDictionary<byte, AlternateSetting>,
		IDictionary,
		ICollection<KeyValuePair<byte, AlternateSetting>>,
		ICollection,
		IEnumerable<KeyValuePair<byte, AlternateSetting>>
	{
		int ICollection.Count
		{
			get { return alt.Count; }
		}

		int ICollection<KeyValuePair<byte, AlternateSetting>>.Count
		{
			get { return alt.Count; }
		}

		void IDictionary.Clear()
		{
			RemoveAllAlternateSettings();
		}

		void ICollection<KeyValuePair<byte, AlternateSetting>>.Clear()
		{
			RemoveAllAlternateSettings();
		}

		bool IDictionary<byte, AlternateSetting>.
			TryGetValue(byte key, out AlternateSetting value)
		{
			return alt.TryGetValue(key, out value);
		}

		ICollection<byte> IDictionary<byte, AlternateSetting>.Keys
		{
			get { return alt.Keys; }
		}

		ICollection<AlternateSetting> IDictionary<byte, AlternateSetting>.Values
		{
			get { return alt.Values; }
		}

		ICollection IDictionary.Keys
		{
			get { return alt.Keys; }
		}

		ICollection IDictionary.Values
		{
			get { return alt.Values; }
		}

		bool IDictionary.IsFixedSize
		{
			get { return false; }
		}

		bool IDictionary.IsReadOnly
		{
			get { return false; }
		}

		object IDictionary.this[object key]
		{
			get
			{
				if(key is byte)
				{
					byte k = (byte)key;
					if(alt.ContainsKey(k))
						return this[k];
				}
				return null;
			}
			set
			{
				if(key == null) throw new ArgumentNullException("key");
				if(!(key is byte)) throw new ArgumentException("", "key");
				if(value != null && !(value is AlternateSetting))
					throw new ArgumentException("", "value");
				byte k = (byte)key;
				this[k] = (AlternateSetting)value;
			}
		}

		void IDictionary.Add(object key, object value)
		{
			if(key == null) throw new ArgumentNullException("key");
			if(value == null) throw new ArgumentNullException("value");
			if(!(key is byte)) throw new ArgumentException("", "key");
			if(!(value is AlternateSetting))
				throw new ArgumentException("", "value");
			AddAlternateSetting((byte)key, (AlternateSetting)value); 
		}

		void IDictionary<byte, AlternateSetting>.
			Add(byte key, AlternateSetting value)
		{
			if(value == null) throw new ArgumentNullException("value");
			AddAlternateSetting(key, value); 
		}

		bool IDictionary.Contains(object key)
		{
			if(key == null) throw new ArgumentNullException("key");
			if(key is byte)
				return alt.ContainsKey((byte)key);
			return false;
		}

		void IDictionary.Remove(object key)
		{
			byte k;
			if(key is byte && alt.ContainsKey(k = (byte)key))
				RemoveAlternateSettingAt(k);
		}

		bool IDictionary<byte, AlternateSetting>.Remove(byte key)
		{
			if(alt.ContainsKey(key))
			{
				RemoveAlternateSettingAt(key);
				return true;
			}
			return false;
		}

		bool ICollection.IsSynchronized
		{
			get { return false; }
		}

		object ICollection.SyncRoot
		{
			get { return ((ICollection)alt).SyncRoot; }
		}

		bool ICollection<KeyValuePair<byte, AlternateSetting>>.IsReadOnly
		{
			get { return false; }
		}

		void ICollection<KeyValuePair<byte, AlternateSetting>>.
			Add(KeyValuePair<byte, AlternateSetting> keyValuePair)
		{
			try
			{
				AddAlternateSetting(keyValuePair.Key, keyValuePair.Value);
			}
			catch(ArgumentException e) // (ArgumentNullException is a subclass)
			{
				throw new ArgumentException("", "keyValuePair", e);
			}
		}

		bool ICollection<KeyValuePair<byte, AlternateSetting>>.
			Contains(KeyValuePair<byte, AlternateSetting> keyValuePair)
		{
			return alt.ContainsKey(keyValuePair.Key);
		}

		void ICollection<KeyValuePair<byte, AlternateSetting>>.
			CopyTo([In, Out] KeyValuePair<byte, AlternateSetting>[] array,
			       int index)
		{
			((ICollection<KeyValuePair<byte, AlternateSetting>>)alt).
				CopyTo(array, index);
		}

		bool ICollection<KeyValuePair<byte, AlternateSetting>>.
			Remove(KeyValuePair<byte, AlternateSetting> keyValuePair)
		{
			byte k = keyValuePair.Key;
			if(alt.ContainsKey(k))
			{
				RemoveAlternateSettingAt(k);
				return true;
			}
			return false;
		}

		void ICollection.CopyTo([In, Out] Array array, int index)
		{
			((ICollection)alt).CopyTo(array, index);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)alt).GetEnumerator();
		}

		IEnumerator<KeyValuePair<byte, AlternateSetting>>
			IEnumerable<KeyValuePair<byte, AlternateSetting>>.GetEnumerator()
		{
			return ((IEnumerable<KeyValuePair<byte, AlternateSetting>>)alt).
				GetEnumerator();
		}

		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			return ((IDictionary)alt).GetEnumerator();
		}

		public IEnumerator<KeyValuePair<byte, AlternateSetting>>
			GetEnumerator()
		{
			return alt.GetEnumerator();
		}
	}
}
