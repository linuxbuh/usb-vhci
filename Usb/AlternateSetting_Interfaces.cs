/*
 * AlternateSetting_Interfaces.cs -- USB related classes
 *
 * Copyright (C) 2007-2008 Conemis AG Karlsruhe Germany
 * Copyright (C) 2007-2015 Michael Singer <michael@a-singer.de>
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
	partial class AlternateSetting :
		IDictionary<byte, Endpoint>,
		IDictionary,
		ICollection<KeyValuePair<byte, Endpoint>>,
		ICollection,
		IEnumerable<KeyValuePair<byte, Endpoint>>
	{
		int ICollection.Count
		{
			get { return ep.Count; }
		}

		int ICollection<KeyValuePair<byte, Endpoint>>.Count
		{
			get { return ep.Count; }
		}

		void IDictionary.Clear()
		{
			RemoveAllEndpoints();
		}

		void ICollection<KeyValuePair<byte, Endpoint>>.Clear()
		{
			RemoveAllEndpoints();
		}

		bool IDictionary<byte, Endpoint>.ContainsKey(byte key)
		{
			if(ep.ContainsKey(key)) return true;
			byte other = (byte)(uint)(key ^ 0x80);
			return ep.ContainsKey(other) && ep[other] is BidirectionalEndpoint;
		}

		bool IDictionary<byte, Endpoint>.TryGetValue(
			byte key, out Endpoint value)
		{
			if(ep.TryGetValue(key, out value)) return true;
			byte other = (byte)(uint)(key ^ 0x80);
			if(ep.ContainsKey(other) && ep[other] is BidirectionalEndpoint)
				return ep.TryGetValue(other, out value);
			return false;
		}

		ICollection<byte> IDictionary<byte, Endpoint>.Keys
		{
			get { return ep.Keys; }
		}

		ICollection<Endpoint> IDictionary<byte, Endpoint>.Values
		{
			get { return ep.Values; }
		}

		ICollection IDictionary.Keys
		{
			get { return ep.Keys; }
		}

		ICollection IDictionary.Values
		{
			get { return ep.Values; }
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
					Endpoint e;
					if(ep.TryGetValue(k, out e)) return e;
					if(ep.TryGetValue((byte)(uint)(k ^ 0x80), out e) &&
						(e is BidirectionalEndpoint)) return e;
					return null;
				}
				return null;
			}
			set
			{
				if(key == null) throw new ArgumentNullException("key");
				if(!(key is byte)) throw new ArgumentException("", "key");
				if(value != null && !(value is Endpoint))
					throw new ArgumentException("", "value");
				byte k = (byte)key;
				this[k] = (Endpoint)value;
			}
		}

		void IDictionary.Add(object key, object value)
		{
			if(key == null) throw new ArgumentNullException("key");
			if(value == null) throw new ArgumentNullException("value");
			if(!(key is byte)) throw new ArgumentException("", "key");
			if(!(value is Endpoint))
				throw new ArgumentException("", "value");
			byte k = (byte)key;
			AddEndpoint(k, (Endpoint)value);
		}

		void IDictionary<byte, Endpoint>.Add(byte key, Endpoint value)
		{
			if(value == null) throw new ArgumentNullException("value");
			AddEndpoint(key, value);
		}

		bool IDictionary.Contains(object key)
		{
			if(key == null) throw new ArgumentNullException("key");
			if(key is byte)
			{
				byte k = (byte)key;
				if(ep.ContainsKey(k)) return true;
				byte other = (byte)(uint)(k ^ 0x80);
				if(ep.ContainsKey(other) && ep[other] is BidirectionalEndpoint)
					return true;
			}
			return false;
		}

		void IDictionary.Remove(object key)
		{
			if(key is byte)
			{
				byte k = (byte)key;
				if(ep.ContainsKey(k))
				{
					RemoveEndpointAt(k);
					return;
				}
				byte other = (byte)(uint)(k ^ 0x80);
				if(ep.ContainsKey(other) && ep[other] is BidirectionalEndpoint)
					RemoveEndpointAt(other);
			}
		}

		bool IDictionary<byte, Endpoint>.Remove(byte key)
		{
			if(ep.ContainsKey(key))
			{
				RemoveEndpointAt(key);
				return true;
			}
			byte other = (byte)(uint)(key ^ 0x80);
			if(ep.ContainsKey(other) && ep[other] is BidirectionalEndpoint)
			{
				RemoveEndpointAt(other);
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
			get { return ((ICollection)ep).SyncRoot; }
		}

		bool ICollection<KeyValuePair<byte, Endpoint>>.IsReadOnly
		{
			get { return false; }
		}

		void ICollection<KeyValuePair<byte, Endpoint>>.
			Add(KeyValuePair<byte, Endpoint> keyValuePair)
		{
			try
			{
				AddEndpoint(keyValuePair.Key, keyValuePair.Value);
			}
			catch(ArgumentException e) // (ArgumentNullException is a subclass)
			{
				throw new ArgumentException("", "keyValuePair", e);
			}
		}

		bool ICollection<KeyValuePair<byte, Endpoint>>.
			Contains(KeyValuePair<byte, Endpoint> keyValuePair)
		{
			byte k = keyValuePair.Key;
			if(ep.ContainsKey(k)) return true;
			byte other = (byte)(uint)(k ^ 0x80);
			return ep.ContainsKey(other) && ep[other] is BidirectionalEndpoint;
		}

		void ICollection<KeyValuePair<byte, Endpoint>>.
			CopyTo([In, Out] KeyValuePair<byte, Endpoint>[] array, int index)
		{
			((ICollection<KeyValuePair<byte, Endpoint>>)ep).
				CopyTo(array, index);
		}

		bool ICollection<KeyValuePair<byte, Endpoint>>.
			Remove(KeyValuePair<byte, Endpoint> keyValuePair)
		{
			byte k = keyValuePair.Key;
			if(ep.ContainsKey(k))
			{
				RemoveEndpointAt(k);
				return true;
			}
			byte other = (byte)(uint)(k ^ 0x80);
			if(ep.ContainsKey(other) && ep[other] is BidirectionalEndpoint)
			{
				RemoveEndpointAt(other);
				return true;
			}
			return false;
		}

		void ICollection.CopyTo([In, Out] Array array, int index)
		{
			((ICollection)ep).CopyTo(array, index);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)ep).GetEnumerator();
		}

		IEnumerator<KeyValuePair<byte, Endpoint>>
			IEnumerable<KeyValuePair<byte, Endpoint>>.GetEnumerator()
		{
			return ((IEnumerable<KeyValuePair<byte, Endpoint>>)ep).
				GetEnumerator();
		}

		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			return ((IDictionary)ep).GetEnumerator();
		}

		public IEnumerator<KeyValuePair<byte, Endpoint>> GetEnumerator()
		{
			return ep.GetEnumerator();
		}
	}
}
