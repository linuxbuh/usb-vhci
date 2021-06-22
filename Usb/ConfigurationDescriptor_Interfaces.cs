/*
 * ConfigurationDescriptor_Interfaces.cs -- USB related classes
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
	partial class ConfigurationDescriptor :
		IDictionary<byte, Interface>,
		IDictionary,
		ICollection<KeyValuePair<byte, Interface>>,
		ICollection,
		IEnumerable<KeyValuePair<byte, Interface>>
	{
		int ICollection.Count
		{
			get { return ifc.Count; }
		}

		int ICollection<KeyValuePair<byte, Interface>>.Count
		{
			get { return ifc.Count; }
		}

		void IDictionary.Clear()
		{
			RemoveAllInterfaces();
		}

		void ICollection<KeyValuePair<byte, Interface>>.Clear()
		{
			RemoveAllInterfaces();
		}

		bool IDictionary<byte, Interface>.ContainsKey(byte key)
		{
			return ifc.ContainsKey(key);
		}

		bool IDictionary<byte, Interface>.TryGetValue(byte key, out Interface value)
		{
			return ifc.TryGetValue(key, out value);
		}

		ICollection<byte> IDictionary<byte, Interface>.Keys
		{
			get { return ifc.Keys; }
		}

		ICollection<Interface> IDictionary<byte, Interface>.Values
		{
			get { return ifc.Values; }
		}

		ICollection IDictionary.Keys
		{
			get { return ifc.Keys; }
		}

		ICollection IDictionary.Values
		{
			get { return ifc.Values; }
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
					if(ifc.ContainsKey(k))
						return this[k];
				}
				return null;
			}
			set
			{
				if(key == null) throw new ArgumentNullException("key");
				if(!(key is byte)) throw new ArgumentException("", "key");
				if(value != null && !(value is Interface))
					throw new ArgumentException("", "value");
				byte k = (byte)key;
				this[k] = (Interface)value;
			}
		}

		void IDictionary.Add(object key, object value)
		{
			if(key == null) throw new ArgumentNullException("key");
			if(value == null) throw new ArgumentNullException("value");
			if(!(key is byte)) throw new ArgumentException("", "key");
			if(!(value is Interface))
				throw new ArgumentException("", "value");
			AddInterface((byte)key, (Interface)value); 
		}

		void IDictionary<byte, Interface>.Add(byte key, Interface value)
		{
			if(value == null) throw new ArgumentNullException("value");
			AddInterface(key, value); 
		}

		bool IDictionary.Contains(object key)
		{
			if(key == null) throw new ArgumentNullException("key");
			if(key is byte)
				return ifc.ContainsKey((byte)key);
			return false;
		}

		void IDictionary.Remove(object key)
		{
			byte k;
			if(key is byte && ifc.ContainsKey(k = (byte)key))
				RemoveInterfaceAt(k);
		}

		bool IDictionary<byte, Interface>.Remove(byte key)
		{
			if(ifc.ContainsKey(key))
			{
				RemoveInterfaceAt(key);
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
			get { return ((ICollection)ifc).SyncRoot; }
		}

		bool ICollection<KeyValuePair<byte, Interface>>.IsReadOnly
		{
			get { return false; }
		}

		void ICollection<KeyValuePair<byte, Interface>>.
			Add(KeyValuePair<byte, Interface> keyValuePair)
		{
			try
			{
				AddInterface(keyValuePair.Key, keyValuePair.Value);
			}
			catch(ArgumentException e) // (ArgumentNullException is a subclass)
			{
				throw new ArgumentException("", "keyValuePair", e);
			}
		}

		bool ICollection<KeyValuePair<byte, Interface>>.
			Contains(KeyValuePair<byte, Interface> keyValuePair)
		{
			return ifc.ContainsKey(keyValuePair.Key);
		}

		void ICollection<KeyValuePair<byte, Interface>>.
			CopyTo([In, Out] KeyValuePair<byte, Interface>[] array, int index)
		{
			((ICollection<KeyValuePair<byte, Interface>>)ifc).
				CopyTo(array, index);
		}

		bool ICollection<KeyValuePair<byte, Interface>>.
			Remove(KeyValuePair<byte, Interface> keyValuePair)
		{
			byte k = keyValuePair.Key;
			if(ifc.ContainsKey(k))
			{
				RemoveInterfaceAt(k);
				return true;
			}
			return false;
		}

		void ICollection.CopyTo([In, Out] Array array, int index)
		{
			((ICollection)ifc).CopyTo(array, index);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)ifc).GetEnumerator();
		}

		IEnumerator<KeyValuePair<byte, Interface>>
			IEnumerable<KeyValuePair<byte, Interface>>.GetEnumerator()
		{
			return ((IEnumerable<KeyValuePair<byte, Interface>>)ifc).
				GetEnumerator();
		}

		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			return ((IDictionary)ifc).GetEnumerator();
		}

		public IEnumerator<KeyValuePair<byte, Interface>> GetEnumerator()
		{
			return ifc.GetEnumerator();
		}
	}
}
