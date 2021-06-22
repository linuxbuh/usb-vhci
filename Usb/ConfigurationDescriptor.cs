/*
 * ConfigurationDescriptor.cs -- USB related classes
 *
 * Copyright (C) 2007-2009 Conemis AG Karlsruhe Germany
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
	public partial class ConfigurationDescriptor : RegularDescriptor
	{
		// Stores the reference to the device descriptor
		private DeviceDescriptor parent;

		// List of all interfaces of this configuration.
		// The Key is the value of the bInterfaceNumber field of interface descriptor.
		private Dictionary<byte, Interface> ifc =
			new Dictionary<byte, Interface>();

		// Used in parent_ActiveConfigChanged(object, EventArgs) for checking if
		// the IsActive property of this ConfigurationDescriptor instance has changed.
		private bool lastState;

		// Index of string descriptor describing this configuration
		private byte iConfiguration;

		// Stores if the USB device may be self-powered. If a device can operate
		// self-powered and bus-powered, this field is true also.
		private bool selfPowered;

		// True if the USB device is able to wake the host from suspend mode.
		private bool remoteWakeup;

		// Maximum power consumption of the USB
		// device from the bus in this specific
		// configuration when the device is fully
		// operational. Expressed in 2 mA units
		// (i.e., 50 = 100 mA).
		// Note: A device configuration reports whether
		// the configuration is bus-powered or self-
		// powered. Device status reports whether the
		// device is currently self-powered. If a device is
		// disconnected from its external power source, it
		// updates device status to indicate that it is no
		// longer self-powered.
		// A device may not increase its power draw
		// from the bus, when it loses its external power
		// source, beyond the amount reported by its
		// configuration.
		// If a device can continue to operate when
		// disconnected from its external power source, it
		// continues to do so. If the device cannot
		// continue to operate, it fails operations it can
		// no longer support. The USB System Software
		// may determine the cause of the failure by
		// checking the status and noting the loss of the
		// device’s power source.
		private byte bMaxPower;

		// true for OTHER_SPEED_CONFIGURATION
		private bool otherSpeed;

		public ConfigurationDescriptor() : this(false)
		{
		}

		public ConfigurationDescriptor(DeviceDescriptor parent) :
			this(parent, false)
		{
		}

		public ConfigurationDescriptor(bool otherSpeed) : this(null, otherSpeed)
		{
		}

		public ConfigurationDescriptor(DeviceDescriptor parent,
		                               bool otherSpeed) : base()
		{
			this.otherSpeed = otherSpeed;
			Device = parent;
		}

		// Called by the constructors for doing some common stuff.
		private void Init(byte[] desc,
		                  ref int pos,
		                  out ushort totalLength,
		                  Endianness endian)
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
			otherSpeed =
				desc[pos + 1] ==
					Descriptor.OtherSpeedConfigurationDescriptorType;
			int sl = otherSpeed ?
			         Descriptor.OtherSpeedConfigurationDescriptorLength :
			         Descriptor.ConfigurationDescriptorLength;
			if(l < sl)
				throw new ArgumentException("desc len invalid", "desc");
			if(al < pos + l)
				throw new ArgumentException();
			if(!otherSpeed &&
			   desc[pos + 1] != Descriptor.ConfigurationDescriptorType)
				throw new ArgumentException("desc type invalid", "desc");
			if(endian == Endianness.System)
				totalLength = BitConverter.ToUInt16(desc, pos + 2);
			else
				totalLength = unchecked((ushort)(desc[pos + 2] |
				                                 desc[pos + 3] << 8));
			if(al < pos + totalLength)
				throw new ArgumentException();
			if(totalLength < desc[pos])
				throw new ArgumentException();
			iConfiguration = desc[pos + 6];
			selfPowered = (desc[pos + 7] & 0x40) != 0x00;
			remoteWakeup = (desc[pos + 7] & 0x20) != 0x00;
			bMaxPower = desc[pos + 8];
			ParseTail(desc, ref pos);
		}

		public ConfigurationDescriptor(DeviceDescriptor parent,
		                               byte[] desc,
		                               ref int pos,
		                               Endianness endian) : base()
		{
			if(desc == null)
				throw new ArgumentNullException("desc");
			ushort totalLen;
			int p = pos;
			Init(desc, ref p, out totalLen, endian);
			ParseCustomDescriptors(desc, ref p);
			WalkChildDescriptors(desc[pos + 4], desc, ref p, endian);
			// TODO: Check for endpoint conflicts between interfaces
			byte confIndex = desc[pos + 5];
			if(confIndex != 0x00)
			{
				if(parent == null)
					throw new ArgumentException("desc[" + (pos + 5) + "] != " +
					                            "0x00 && parent == null");
				parent.AddConfiguration(confIndex, this);
			}
			else
				Device = parent;
			pos = p; // if no exception thrown so far, refresh pos
		}

		public ConfigurationDescriptor(byte[] desc,
		                               ref int pos,
		                               Endianness endian) :
			this(null, desc, ref pos, endian)
		{
		}

		public ConfigurationDescriptor(DeviceDescriptor parent,
		                               byte[] desc,
		                               Endianness endian) : base()
		{
			if(desc != null)
			{
				int l = desc.Length;
				if(l == 0 || desc[0] > l)
					throw new ArgumentException("len invalid", "desc");
				ushort totalLen;
				int p = 0;
				Init(desc, ref p, out totalLen, endian);
				ParseCustomDescriptors(desc, ref p);
				WalkChildDescriptors(desc[4], desc, ref p, endian);
				if(p != l)
					throw new ArgumentException("len invalid", "desc");
				// TODO: Check for endpoint conflicts between interfaces
				byte confIndex = desc[5];
				if(confIndex != 0x00)
				{
					if(parent == null)
						throw new ArgumentException("desc[5] != 0x0 && " +
						                            "parent == null");
					parent.AddConfiguration(confIndex, this);
				}
				else
					Device = parent;
			}
			else
				Device = parent;
		}

		public ConfigurationDescriptor(byte[] desc,
		                               Endianness endian) :
			this(null, desc, endian)
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
			new Interface(this, desc, ref pos);
			return true;
		}

		public override bool Validate(ValidationMode mode)
		{
			int c = ifc.Count;
			if(c < 1 || c > byte.MaxValue) return false;
			if(mode == ValidationMode.Strict)
			{
				for(int i = 0; i < c; i++)
					if(!ifc.ContainsKey((byte)i)) return false;
			}
			foreach(Interface i in ifc.Values)
				if(!i.Validate(mode)) return false;
			return true;
		}

		public bool IsOtherSpeed
		{
			get { return otherSpeed; }
			set { otherSpeed = value; }
		}

		public bool IsActive
		{
			get
			{
				if(parent == null) return false;
				else return parent.ActiveConfiguration == this;
			}
			set
			{
				if(parent == null) throw new InvalidOperationException();
				parent.ActiveConfiguration = this;
			}
		}

		public byte GetIndexOfInterface(Interface ifc)
		{
			if(ifc == null) throw new ArgumentNullException("ifc");
			foreach(KeyValuePair<byte, Interface> pair in this.ifc)
				if(pair.Value == ifc)
					return pair.Key;
			throw new InvalidOperationException();
		}

		public bool ContainsInterface(Interface ifc)
		{
			if(ifc == null) throw new ArgumentNullException("ifc");
			return this.ifc.ContainsValue(ifc);
		}

		public bool IsUsingEndpoint(byte epadr)
		{
			foreach(KeyValuePair<byte, Interface> pair in ifc)
				if(pair.Value.IsActive && pair.Value.IsUsingEndpoint(epadr))
					return true;
			return false;
		}

		public Endpoint TryGetEndpoint(byte epadr)
		{
			foreach(KeyValuePair<byte, Interface> pair in ifc)
			{
				if(pair.Value.IsActive)
				{
					Endpoint ep = pair.Value.TryGetEndpoint(epadr);
					if(ep != null) return ep;
				}
			}
			return null;
		}

		public DeviceDescriptor Device
		{
			get { return parent; }
			set
			{
				if(value == parent) return;
				if(parent != null)
				{
					DeviceDescriptor dev = parent;
					parent = null;
					dev.ActiveConfigurationChanged -=
						parent_ActiveConfigChanged;
					dev.RemoveConfiguration(this);
				}
				if(value == null)
				{
					if(lastState)
					{
						lastState = false;
						OnIsActiveChanged(null);
					}
					OnDeviceChanged(null);
					return;
				}
				parent = value;
				if(!value.ContainsConfiguration(this))
				{
					try
					{
						value.AppendConfiguration(this);
					}
					catch
					{
						parent = null;
						throw;
					}
				}
				value.ActiveConfigurationChanged +=
					parent_ActiveConfigChanged;
				OnDeviceChanged(null);
				parent_ActiveConfigChanged(value, null);
			}
		}

		private void parent_ActiveConfigChanged
			(object sender, EventArgs e)
		{
			if(sender != null && sender == parent)
			{
				if(IsActive != lastState)
				{
					lastState = !lastState;
					OnIsActiveChanged(null);
				}
			}
		}

		public event EventHandler IsActiveChanged;

		protected virtual void OnIsActiveChanged(EventArgs e)
		{
			if(IsActiveChanged != null)
				IsActiveChanged(this, e);
		}

		public byte Index
		{
			get
			{
				if(parent == null) throw new InvalidOperationException();
				return parent.GetIndexOfConfiguration(this);
			}
		}

		public ICollection<byte> InterfaceIndices
		{
			get { return ifc.Keys; }
		}

		public ICollection<Interface> Interfaces
		{
			get { return ifc.Values; }
		}

		public int InterfacesCount
		{
			get { return ifc.Count; }
		}

		public Interface this[byte index]
		{
			get { return ifc[index]; }
			set
			{
				if(value == null) throw new ArgumentNullException("value");
				ConfigurationDescriptor parent = value.Configuration;
				if(parent != null && parent != this)
					throw new InvalidOperationException();
				if(ifc.ContainsKey(index))
				{
					if(ifc[index] == value) return;
					RemoveInterface(ifc[index]);
				}
				AddInterface(index, value);
			}
		}

		public void RemoveAllInterfaces()
		{
			int c = ifc.Count;
			if(c > 0)
			{
				Interface[] arr = new Interface[c];
				ifc.Values.CopyTo(arr, 0);
				// Raises the InterfaceRemoved event on every iteration.
				foreach(Interface iface in arr)
					RemoveInterface(iface);
			}
			// Not really necessary
			ifc.Clear();
		}

		public bool IsUsingInterfaceIndex(byte index)
		{
			return ifc.ContainsKey(index);
		}

		public void AddInterface(byte index, Interface ifc)
		{
			if(ifc == null) throw new ArgumentNullException("ifc");
			if(this.ifc.ContainsKey(index) || this.ifc.ContainsValue(ifc))
				throw new InvalidOperationException();
			ConfigurationDescriptor parent = ifc.Configuration;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			this.ifc.Add(index, ifc);
			if(parent != this) ifc.Configuration = this;
			OnInterfaceAdded(new CollectionModifiedEventArgs<Interface>(ifc));
		}

		public void AppendInterface(Interface ifc)
		{
			if(ifc == null) throw new ArgumentNullException("ifc");
			if(this.ifc.ContainsValue(ifc))
				throw new InvalidOperationException();
			ConfigurationDescriptor parent = ifc.Configuration;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			byte index = 0;
			bool found = false;
			for(int i = 0; i <= 255; i++)
			{
				if(!this.ifc.ContainsKey((byte)i))
				{
					index = (byte)i;
					found = true;
					break;
				}
			}
			if(!found) throw new InvalidOperationException();
			this.ifc.Add(index, ifc);
			if(parent != this) ifc.Configuration = this;
			OnInterfaceAdded(new CollectionModifiedEventArgs<Interface>(ifc));
		}

		public void RemoveInterface(Interface ifc)
		{
			if(ifc == null) throw new ArgumentNullException("ifc");
			byte index = 0;
			ConfigurationDescriptor parent = ifc.Configuration;
			try
			{
				index = GetIndexOfInterface(ifc);
			}
			catch(InvalidOperationException)
			{
				if(parent == this) ifc.Configuration = null;
				throw;
			}
			ifc.IsActive = false;
			this.ifc.Remove(index);
			if(parent == this) ifc.Configuration = null;
			OnInterfaceRemoved(new CollectionModifiedEventArgs<Interface>(ifc));
		}

		public void RemoveInterfaceAt(byte index)
		{
			Interface iface = ifc[index];
			ConfigurationDescriptor parent = iface.Configuration;
			iface.IsActive = false;
			ifc.Remove(index);
			if(parent == this) iface.Configuration = null;
			OnInterfaceRemoved(new CollectionModifiedEventArgs<Interface>
			                   (iface));
		}

		public event EventHandler<CollectionModifiedEventArgs<Interface>>
			InterfaceAdded;

		public event EventHandler<CollectionModifiedEventArgs<Interface>>
			InterfaceRemoved;

		protected virtual void
			OnInterfaceAdded(CollectionModifiedEventArgs<Interface> e)
		{
			if(InterfaceAdded != null) InterfaceAdded(this, e);
		}

		protected virtual void
			OnInterfaceRemoved(CollectionModifiedEventArgs<Interface> e)
		{
			if(InterfaceRemoved != null) InterfaceRemoved(this, e);
		}

		public event EventHandler DeviceChanged;

		protected virtual void OnDeviceChanged(EventArgs e)
		{
			if(DeviceChanged != null)
				DeviceChanged(this, e);
		}

		public event EventHandler StringDescriptorIndexChanged;
		public event EventHandler SelfPoweredChanged;
		public event EventHandler RemoteWakeupChanged;
		public event EventHandler MaxPowerChanged;

		protected virtual void OnStringDescriptorIndexChanged(EventArgs e)
		{
			if(StringDescriptorIndexChanged != null)
				StringDescriptorIndexChanged(this, e);
		}

		protected virtual void OnSelfPoweredChanged(EventArgs e)
		{
			if(SelfPoweredChanged != null)
				SelfPoweredChanged(this, e);
		}

		protected virtual void OnRemoteWakeupChanged(EventArgs e)
		{
			if(RemoteWakeupChanged != null)
				RemoteWakeupChanged(this, e);
		}

		protected virtual void OnMaxPowerChanged(EventArgs e)
		{
			if(MaxPowerChanged != null)
				MaxPowerChanged(this, e);
		}

		public byte StringDescriptorIndex
		{
			get { return iConfiguration; }
			set
			{
				if(iConfiguration != value)
				{
					iConfiguration = value;
					OnStringDescriptorIndexChanged(null);
				}
			}
		}

		public bool SelfPowered
		{
			get { return selfPowered; }
			set
			{
				if(selfPowered != value)
				{
					selfPowered = value;
					OnSelfPoweredChanged(null);
				}
			}
		}

		public bool RemoteWakeup
		{
			get { return remoteWakeup; }
			set
			{
				if(remoteWakeup != value)
				{
					remoteWakeup = value;
					OnRemoteWakeupChanged(null);
				}
			}
		}

		public byte MaxPower
		{
			get { return bMaxPower; }
			set
			{
				if(bMaxPower != value)
				{
					bMaxPower = value;
					OnMaxPowerChanged(null);
				}
			}
		}

		public override sealed int RegularSize
		{
			get
			{
				return otherSpeed ?
					Descriptor.OtherSpeedConfigurationDescriptorLength :
					Descriptor.ConfigurationDescriptorLength;
			}
		}

		protected override void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian)
		{
			int dtl = GetDescriptor(null);
			int ic = ifc.Count;
			if(dtl > ushort.MaxValue)
				throw new InvalidOperationException();
			if(ic > byte.MaxValue)
				throw new InvalidOperationException();
			desc[index + 1] = otherSpeed ?
			                  Descriptor.OtherSpeedConfigurationDescriptorType :
			                  Descriptor.ConfigurationDescriptorType;
			if(endian == Endianness.System)
			{
				BitConverter.GetBytes((ushort)dtl).CopyTo(desc, index + 2);
			}
			else
			{
				desc[index + 2] = unchecked((byte)dtl);
				desc[index + 3] = unchecked((byte)(dtl >> 8));
			}
			desc[index + 4] = (byte)ic;
			desc[index + 5] = (parent != null) ? Index : (byte)0U;
			desc[index + 6] = iConfiguration;
			desc[index + 7] =
				(byte)(0x80U |                       // (reserved: set to one)
				       (selfPowered ? 0x20U : 0U) |  // attr
				       (remoteWakeup ? 0x10U : 0U)); //   "
			desc[index + 8] = bMaxPower;
		}

		protected override int GetSubDescriptors([In, Out] byte[] desc,
		                                         int index,
		                                         Endianness endian)
		{
			int dtl = 0;
			foreach(Interface iface in ifc.Values)
				dtl += iface.GetDescriptor(null);
			if(desc == null)
				return dtl;
			int al = desc.Length;
			if(ifc.Count > 0)
			{
				if(index < 0 || index >= al)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(al < dtl + index)
					throw new InvalidOperationException();
				SortedDictionary<byte, Interface> sorted =
					new SortedDictionary<byte, Interface>(ifc);
				foreach(Interface iface in sorted.Values)
					index += iface.GetDescriptor(desc, index);
			}
			return dtl;
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			if(stm == null) throw new ArgumentNullException("stm");
			if(prefix == null) throw new ArgumentNullException("prefix");
			string index;
			if(parent != null) index = Index.ToString();
			else index = "[no parent]";
			stm.WriteLine(prefix + "Index:          " + index);
			stm.WriteLine(prefix + "IsActive:       " + IsActive.ToString());
			stm.WriteLine(prefix + "StrDescIndex:   " +
			              iConfiguration.ToString());
			stm.WriteLine(prefix + "SelfPowered:    " + selfPowered.ToString());
			stm.WriteLine(prefix + "RemoteWakeup:   " +
			              remoteWakeup.ToString());
			stm.WriteLine(prefix + "MaxPower:       " + bMaxPower.ToString() +
			              " (" + (bMaxPower * 2).ToString() + " mA)");
			stm.WriteLine(prefix + "InterfaceCount: " + ifc.Count.ToString());
			base.Dump(stm, prefix);
			string ifcPrefix = prefix + "  ";
			foreach(KeyValuePair<byte, Interface> kvp in ifc)
			{
				stm.WriteLine(prefix + "> [Interface " +
				              kvp.Key.ToString() + "]");
				kvp.Value.Dump(stm, ifcPrefix);
			}
		}
	}
}
