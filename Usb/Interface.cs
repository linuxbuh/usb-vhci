/*
 * Interface.cs -- USB related classes
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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Usb
{
	public partial class Interface : ContainerDescriptor
	{
		private ConfigurationDescriptor parent;
		private Dictionary<byte, AlternateSetting> alt =
			new Dictionary<byte, AlternateSetting>();
		private byte actAltKey;
		private bool active;
		private string owner;

		public Interface() : base()
		{
		}

		public Interface(ConfigurationDescriptor parent) : base()
		{
			Configuration = parent;
		}

		private static void Init(byte[] desc, ref int pos)
		{
			int al = desc.Length;
			if(pos < 0 || pos >= al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			int l = desc[pos];
			// See USB specs 2.0 section 9.5:
			//   "If a descriptor returns with a value in its length
			//    field that is less than defined by this
			//    specification, the descriptor is invalid and
			//    should be rejected by the host."
			if(l < Descriptor.InterfaceDescriptorLength)
				throw new ArgumentException("desc len invalid", "desc");
			if(al < pos + l)
				throw new ArgumentException();
			if(desc[pos + 1] != Descriptor.InterfaceDescriptorType)
				throw new ArgumentException("desc type invalid", "desc");
		}

		public Interface(ConfigurationDescriptor parent,
		                 byte[] desc,
		                 ref int pos,
		                 bool ignoreIfcIndex) : base()
		{
			if(desc == null)
				throw new ArgumentNullException("desc");
			int p = pos;
			Init(desc, ref p);
			WalkChildDescriptors(desc, ref p, desc[pos + 2]);
			if(!ignoreIfcIndex)
			{
				if(parent == null)
					throw new ArgumentException("!ignoreIfcIndex && " +
					                            "parent == null");
				parent.AddInterface(desc[pos + 2], this);
			}
			else
				Configuration = parent;
			pos = p; // if no exception thrown so far, refresh pos
		}

		public Interface(ConfigurationDescriptor parent,
		                 byte[] desc,
		                 ref int pos) :
			this(parent, desc, ref pos, false)
		{
		}

		public Interface(byte[] desc, ref int pos) :
			this(null, desc, ref pos, true)
		{
		}

		public Interface(ConfigurationDescriptor parent,
		                 byte[] desc,
		                 bool ignoreIfcIndex) : base()
		{
			if(desc != null)
			{
				int l = desc.Length;
				if(l == 0 || desc[0] > l)
					throw new ArgumentException("len invalid", "desc");
				int p = 0;
				Init(desc, ref p);
				WalkChildDescriptors(desc, ref p, desc[2]);
				if(p != l)
					throw new ArgumentException("len invalid", "desc");
				if(!ignoreIfcIndex)
				{
					if(parent == null)
						throw new ArgumentException("!ignoreIfcIndex && " +
						                            "parent == null");
					parent.AddInterface(desc[2], this);
				}
				else
					Configuration = parent;
			}
			else
			{
				if(!ignoreIfcIndex)
					throw new ArgumentException("!ignoreIfcIndex && " +
					                            "desc == null");
				Configuration = parent;
			}
		}

		public Interface(ConfigurationDescriptor parent,
		                 byte[] desc) : this(parent, desc, false)
		{
		}

		public Interface(byte[] desc) : this(null, desc, true)
		{
		}

		protected override bool ParseChildDescriptor(byte[] desc,
		                                             ref int pos,
		                                             Endianness endian,
		                                             object context)
		{
			if(desc[pos] < Descriptor.InterfaceDescriptorLength ||
			   desc[pos + 1] != Descriptor.InterfaceDescriptorType)
				return false;
			if((byte)context != desc[pos + 2])
				return false;
			new AlternateSetting(this, desc, ref pos);
			return true;
		}

		public override bool Validate(ValidationMode mode)
		{
			// TODO: implement
			return true;
		}

		public bool IsUsingEndpoint(byte epadr)
		{
			if(alt.Count > 0)
				return alt[actAltKey].IsUsingEndpoint(epadr);
			else
				return false;
		}

		public Endpoint TryGetEndpoint(byte epadr)
		{
			if(alt.Count > 0)
				return alt[actAltKey].TryGetEndpoint(epadr);
			else
				return null;
		}

		public byte ActiveAlternateSettingIndex
		{
			get
			{
				if(alt.Count > 0)
					return actAltKey;
				else
					throw new InvalidOperationException();
			}
			set
			{
				if(!alt.ContainsKey(value))
					throw new KeyNotFoundException();
				if(actAltKey != value)
				{
					actAltKey = value;
					OnActiveAlternateSettingChanged(null);
				}
			}
		}

		public AlternateSetting ActiveAlternateSetting
		{
			get
			{
				if(alt.Count > 0)
					return alt[actAltKey];
				else
					throw new InvalidOperationException();
			}
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				ActiveAlternateSettingIndex = GetIndexOfAlternateSetting(value);
			}
		}

		public bool IsActive
		{
			get { return active; }
			set
			{
				if(active != value)
				{
					active = value;
					OnIsActiveChanged(null);
				}
			}
		}

		public string Owner
		{
			get { return owner; }
			set { owner = value; }
		}

		public event EventHandler ActiveAlternateSettingChanged;
		public event EventHandler IsActiveChanged;

		protected virtual void OnActiveAlternateSettingChanged(EventArgs e)
		{
			if(ActiveAlternateSettingChanged != null)
				ActiveAlternateSettingChanged(this, e);
		}

		protected virtual void OnIsActiveChanged(EventArgs e)
		{
			if(IsActiveChanged != null)
				IsActiveChanged(this, e);
		}

		public byte GetIndexOfAlternateSetting(AlternateSetting aifc)
		{
			if(aifc == null) throw new ArgumentNullException("aifc");
			foreach(KeyValuePair<byte, AlternateSetting> pair in alt)
				if(pair.Value == aifc)
					return pair.Key;
			throw new InvalidOperationException();
		}

		public bool ContainsAlternateSetting(AlternateSetting aifc)
		{
			if(aifc == null) throw new ArgumentNullException("aifc");
			return alt.ContainsValue(aifc);
		}

		public ConfigurationDescriptor Configuration
		{
			get { return parent; }
			set
			{
				if(value == parent) return;
				if(parent != null)
				{
					ConfigurationDescriptor conf = parent;
					parent = null;
					conf.RemoveInterface(this);
				}
				if(value == null)
				{
					OnConfigurationChanged(null);
					return;
				}
				parent = value;
				if(!value.ContainsInterface(this))
				{
					try
					{
						value.AppendInterface(this);
					}
					catch
					{
						parent = null;
						throw;
					}
				}
				OnConfigurationChanged(null);
			}
		}

		public byte Index
		{
			get
			{
				if(parent == null) throw new InvalidOperationException();
				return parent.GetIndexOfInterface(this);
			}
		}

		public ICollection<byte> AlternateSettingIndices
		{
			get { return alt.Keys; }
		}

		public ICollection<AlternateSetting> AlternateSettings
		{
			get { return alt.Values; }
		}

		public int AlternateSettingsCount
		{
			get { return alt.Count; }
		}

		public AlternateSetting this[byte index]
		{
			get { return alt[index]; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				Interface parent = value.Interface;
				if(parent != null && parent != this)
					throw new InvalidOperationException();
				if(alt.ContainsKey(index))
				{
					AlternateSetting prev = alt[index];
					if(prev == value) return;
					if(alt.ContainsValue(value))
						throw new InvalidOperationException();
					bool active = index == actAltKey;
					alt[index] = value;
					Interface pparent = prev.Interface;
					if(pparent == this) prev.Interface = null;
					if(parent != this) value.Interface = this;
					OnAlternateSettingRemoved(new CollectionModifiedEventArgs
					                          <AlternateSetting>(prev));
					OnAlternateSettingAdded(new CollectionModifiedEventArgs
					                        <AlternateSetting>(value));
					if(active) OnActiveAlternateSettingChanged(null);
				}
				else
				{
					AddAlternateSetting(index, value);
				}
			}
		}

		public void RemoveAllAlternateSettings()
		{
			int c = alt.Count;
			if(c > 0)
			{
				byte[] arr = new byte[c];
				alt.Keys.CopyTo(arr, 0);
				// Activate the alternate setting which gets removed last.
				// We do this to minimize the amount of calls to the
				// ActiveAlternateSetting event.
				ActiveAlternateSettingIndex = arr[c - 1];
				foreach(byte index in arr)
					RemoveAlternateSettingAt(index);
			}
			// Not really necessary
			alt.Clear();
		}

		bool IDictionary<byte, AlternateSetting>.ContainsKey(byte key)
		{
			return alt.ContainsKey(key);
		}

		public bool IsUsingAlternateSettingIndex(byte index)
		{
			return alt.ContainsKey(index);
		}

		public void AddAlternateSetting(byte index, AlternateSetting aifc)
		{
			if(aifc == null) throw new ArgumentNullException("aifc");
			if(alt.ContainsKey(index) || alt.ContainsValue(aifc))
				throw new InvalidOperationException();
			Interface parent = aifc.Interface;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			bool first = alt.Count == 0;
			alt.Add(index, aifc);
			if(parent != this) aifc.Interface = this;
			if(first) actAltKey = index;
			OnAlternateSettingAdded(new CollectionModifiedEventArgs
			                        <AlternateSetting>(aifc));
			if(first) OnActiveAlternateSettingChanged(null);
		}

		public void AppendAlternateSetting(AlternateSetting aifc)
		{
			if(aifc == null) throw new ArgumentNullException("aifc");
			if(alt.ContainsValue(aifc))
				throw new InvalidOperationException();
			Interface parent = aifc.Interface;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			byte index = 0;
			bool found = false;
			for(int i = 0; i <= 255; i++)
			{
				if(!alt.ContainsKey((byte)i))
				{
					index = (byte)i;
					found = true;
					break;
				}
			}
			if(!found) throw new InvalidOperationException();
			bool first = alt.Count == 0;
			alt.Add(index, aifc);
			if(parent != this) aifc.Interface = this;
			if(first) actAltKey = index;
			OnAlternateSettingAdded(new CollectionModifiedEventArgs
			                        <AlternateSetting>(aifc));
			if(first) OnActiveAlternateSettingChanged(null);
		}

		private void ResetActiveAlternateSetting()
		{
			if(alt.Count > 0)
			{
				actAltKey = byte.MaxValue;
				foreach(byte index in alt.Keys)
					if(index < actAltKey)
						actAltKey = index;
			}
			else
			{
				actAltKey = 0;
			}
			OnActiveAlternateSettingChanged(null);
		}

		public void RemoveAlternateSetting(AlternateSetting aifc)
		{
			if(aifc == null) throw new ArgumentNullException("aifc");
			byte index = 0;
			Interface parent = aifc.Interface;
			try
			{
				index = GetIndexOfAlternateSetting(aifc);
			}
			catch(InvalidOperationException)
			{
				if(parent == this) aifc.Interface = null;
				throw;
			}
			bool wasActive = actAltKey == index;
			alt.Remove(index);
			if(parent == this) aifc.Interface = null;
			if(wasActive)
				ResetActiveAlternateSetting();
			OnAlternateSettingRemoved(new CollectionModifiedEventArgs
			                          <AlternateSetting>(aifc));
		}

		public void RemoveAlternateSettingAt(byte index)
		{
			AlternateSetting aifc = alt[index];
			Interface parent = aifc.Interface;
			bool wasActive = actAltKey == index;
			alt.Remove(index);
			if(parent == this) aifc.Interface = null;
			if(wasActive)
				ResetActiveAlternateSetting();
			OnAlternateSettingRemoved(new CollectionModifiedEventArgs
			                          <AlternateSetting>(aifc));
		}

		public event EventHandler<CollectionModifiedEventArgs<AlternateSetting>>
			AlternateSettingAdded;

		public event EventHandler<CollectionModifiedEventArgs<AlternateSetting>>
			AlternateSettingRemoved;

		public event EventHandler ConfigurationChanged;

		protected virtual void OnAlternateSettingAdded(
			CollectionModifiedEventArgs<AlternateSetting> e)
		{
			if(AlternateSettingAdded != null)
				AlternateSettingAdded(this, e);
		}

		protected virtual void OnAlternateSettingRemoved(
			CollectionModifiedEventArgs<AlternateSetting> e)
		{
			if(AlternateSettingRemoved != null)
				AlternateSettingRemoved(this, e);
		}

		protected virtual void OnConfigurationChanged(EventArgs e)
		{
			if(ConfigurationChanged != null)
				ConfigurationChanged(this, e);
		}

		public override int GetDescriptor([In, Out] byte[] desc, int index)
		{
			if(alt.Count > 0)
			{
				int dtl = 0;
				foreach(AlternateSetting aifc in alt.Values)
					dtl += aifc.GetDescriptor(null);
				if(desc == null)
					return dtl;
				int al = desc.Length;
				if(index < 0 || index >= al)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(al < dtl + index)
					throw new InvalidOperationException();
				int pos = index;
				SortedDictionary<byte, AlternateSetting> sorted =
					new SortedDictionary<byte, AlternateSetting>(alt);
				foreach(AlternateSetting aifc in sorted.Values)
					pos += aifc.GetDescriptor(desc, pos);
				return dtl;
			}
			else throw new InvalidOperationException();
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			if(stm == null) throw new ArgumentNullException("stm");
			if(prefix == null) throw new ArgumentNullException("prefix");
			string index, owner;
			if(parent != null) index = Index.ToString();
			else index = "[no parent]";
			if(this.owner != null) owner = this.owner;
			else owner = "[no owner]";
			stm.WriteLine(prefix + "Index:          " + index);
			stm.WriteLine(prefix + "IsActive:       " + IsActive.ToString());
			stm.WriteLine(prefix + "OwningDriver:   " + owner.ToString());
			stm.WriteLine(prefix + "AltSettingCnt:  " + alt.Count.ToString());
			string altPrefix = prefix + "  ";
			foreach(KeyValuePair<byte, AlternateSetting> kvp in alt)
			{
				stm.WriteLine(prefix + "> [AlternateSetting " +
				              kvp.Key.ToString() + "]");
				kvp.Value.Dump(stm, altPrefix);
			}
		}
	}
}
