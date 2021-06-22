/*
 * DeviceDescriptor_Interfaces.cs -- USB related classes
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
	partial class DeviceDescriptor :
		IDictionary<byte, ConfigurationDescriptor>,
		IDictionary,
		ICollection<KeyValuePair<byte, ConfigurationDescriptor>>,
		ICollection,
		IEnumerable<KeyValuePair<byte, ConfigurationDescriptor>>
	{
		int ICollection.Count
		{
			get { return conf.Count; }
		}

		int ICollection<KeyValuePair<byte, ConfigurationDescriptor>>.Count
		{
			get { return conf.Count; }
		}

		void IDictionary.Clear()
		{
			RemoveAllConfigurations();
		}

		void ICollection<KeyValuePair<byte, ConfigurationDescriptor>>.Clear()
		{
			RemoveAllConfigurations();
		}

		bool IDictionary<byte, ConfigurationDescriptor>.ContainsKey(byte key)
		{
			return conf.ContainsKey(key);
		}

		bool IDictionary<byte, ConfigurationDescriptor>.
			TryGetValue(byte key, out ConfigurationDescriptor value)
		{
			return conf.TryGetValue(key, out value);
		}

		ICollection<byte> IDictionary<byte, ConfigurationDescriptor>.Keys
		{
			get { return conf.Keys; }
		}

		ICollection<ConfigurationDescriptor>
			IDictionary<byte, ConfigurationDescriptor>.Values
		{
			get { return conf.Values; }
		}

		ICollection IDictionary.Keys
		{
			get { return conf.Keys; }
		}

		ICollection IDictionary.Values
		{
			get { return conf.Values; }
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
					if(k == 0) throw new ArgumentException("", "key");
					if(conf.ContainsKey(k))
						return this[k];
				}
				return null;
			}
			set
			{
				if(key == null) throw new ArgumentNullException("key");
				if(!(key is byte)) throw new ArgumentException("", "key");
				if(value != null && !(value is ConfigurationDescriptor))
					throw new ArgumentException("", "value");
				byte k = (byte)key;
				if(k == 0) throw new ArgumentException("", "key");
				this[k] = (ConfigurationDescriptor)value;
			}
		}

		void IDictionary.Add(object key, object value)
		{
			if(key == null) throw new ArgumentNullException("key");
			if(value == null) throw new ArgumentNullException("value");
			if(!(key is byte)) throw new ArgumentException("", "key");
			if(!(value is ConfigurationDescriptor))
				throw new ArgumentException("", "value");
			byte k = (byte)key;
			if(k == 0) throw new ArgumentException("", "key");
			AddConfiguration(k, (ConfigurationDescriptor)value); 
		}

		void IDictionary<byte, ConfigurationDescriptor>.
			Add(byte key, ConfigurationDescriptor value)
		{
			if(value == null) throw new ArgumentNullException("value");
			if(key == 0) throw new ArgumentException("", "key");
			AddConfiguration(key, value); 
		}

		bool IDictionary.Contains(object key)
		{
			if(key == null) throw new ArgumentNullException("key");
			if(key is byte)
				return conf.ContainsKey((byte)key);
			return false;
		}
		
		void IDictionary.Remove(object key)
		{
			byte k;
			if(key is byte && conf.ContainsKey(k = (byte)key))
				RemoveConfigurationAt(k);
		}

		bool IDictionary<byte, ConfigurationDescriptor>.Remove(byte key)
		{
			if(conf.ContainsKey(key))
			{
				RemoveConfigurationAt(key);
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
			get { return ((ICollection)conf).SyncRoot; }
		}

		bool ICollection<KeyValuePair<byte, ConfigurationDescriptor>>.IsReadOnly
		{
			get { return false; }
		}

		void ICollection<KeyValuePair<byte, ConfigurationDescriptor>>.
			Add(KeyValuePair<byte, ConfigurationDescriptor> keyValuePair)
		{
			try
			{
				AddConfiguration(keyValuePair.Key, keyValuePair.Value);
			}
			catch(ArgumentException e) // (ArgumentNullException is a subclass)
			{
				throw new ArgumentException("", "keyValuePair", e);
			}
		}

		bool ICollection<KeyValuePair<byte, ConfigurationDescriptor>>.
			Contains(KeyValuePair<byte, ConfigurationDescriptor> keyValuePair)
		{
			return conf.ContainsKey(keyValuePair.Key);
		}

		void ICollection<KeyValuePair<byte, ConfigurationDescriptor>>.CopyTo
			([In, Out] KeyValuePair<byte, ConfigurationDescriptor>[] array,
			 int index)
		{
			((ICollection<KeyValuePair<byte, ConfigurationDescriptor>>)conf).
				CopyTo(array, index);
		}

		bool ICollection<KeyValuePair<byte, ConfigurationDescriptor>>.
			Remove(KeyValuePair<byte, ConfigurationDescriptor> keyValuePair)
		{
			byte k = keyValuePair.Key;
			if(conf.ContainsKey(k))
			{
				RemoveConfigurationAt(k);
				return true;
			}
			return false;
		}

		void ICollection.CopyTo([In, Out] Array array, int index)
		{
			((ICollection)conf).CopyTo(array, index);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)conf).GetEnumerator();
		}

		IEnumerator<KeyValuePair<byte, ConfigurationDescriptor>>
			IEnumerable<KeyValuePair<byte, ConfigurationDescriptor>>.
				GetEnumerator()
		{
			return ((IEnumerable<KeyValuePair<byte,
			         ConfigurationDescriptor>>)conf).
				GetEnumerator();
		}

		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			return ((IDictionary)conf).GetEnumerator();
		}

		public IEnumerator<KeyValuePair<byte, ConfigurationDescriptor>>
			GetEnumerator()
		{
			return conf.GetEnumerator();
		}
	}
}
