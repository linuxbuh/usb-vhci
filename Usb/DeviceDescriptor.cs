/*
 * DeviceDescriptor.cs -- USB related classes
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
	public partial class DeviceDescriptor : RegularDescriptor, IEndpointParent
	{
		// List of all configurations of this device.
		// The Key is the value of the bConfigurationValue field of the
		// configuration descriptor.
		private SortedDictionary<byte, ConfigurationDescriptor> conf =
			new SortedDictionary<byte, ConfigurationDescriptor>();

		// Default control endpoint - stores the bMaxPacketSize0 field of this
		// device descriptor..
		private ControlEndpoint ep0;

		// USB Specification Release Number in Binary-Coded Decimal (i.e.,
		// 2.10 is 210H).
		// This field identifies the release of the USB
		// Specification with which the device and its
		// descriptors are compliant.
		private ushort bcdUSB;

		// Vendor ID (assigned by the USB-IF)
		private ushort idVendor;

		// Product ID (assigned by the manufacturer)
		private ushort idProduct;

		// Device release number in binary-coded decimal
		private ushort bcdDevice;

		// Stores the key of the active configuration. 0 if none is active.
		private byte actConfKey;

		// Class code (assigned by the USB-IF).
		// If this field is reset to zero, each interface
		// within a configuration specifies its own
		// class information and the various
		// interfaces operate independently.
		// If this field is set to a value between 1 and
		// FEH, the device supports different class
		// specifications on different interfaces and
		// the interfaces may not operate
		// independently. This value identifies the
		// class definition used for the aggregate
		// interfaces.
		// If this field is set to FFH, the device class
		// is vendor-specific.
		// ---
		// Subclass code (assigned by the USB-IF).
		// These codes are qualified by the value of
		// the bDeviceClass field.
		// If the bDeviceClass field is reset to zero,
		// this field must also be reset to zero.
		// If the bDeviceClass field is not set to FFH,
		// all values are reserved for assignment by
		// the USB-IF.
		// ---
		// Protocol code (assigned by the USB-IF).
		// These codes are qualified by the value of
		// the bDeviceClass and the
		// bDeviceSubClass fields. If a device
		// supports class-specific protocols on a
		// device basis as opposed to an interface
		// basis, this code identifies the protocols
		// that the device uses as defined by the
		// specification of the device class.
		// If this field is reset to zero, the device
		// does not use class-specific protocols on a
		// device basis. However, it may use class-
		// specific protocols on an interface basis.
		// If this field is set to FFH, the device uses a
		// vendor-specific protocol on a device basis.
		private ClassCodeTriple cct;

		// Index of string descriptor describing manufacturer
		private byte iManufacturer;

		// Index of string descriptor describing product
		private byte iProduct;

		// Index of string descriptor describing the device’s serial number
		private byte iSerialNumber;

		// true for DEVICE_QUALIFIER
		private bool qualifier;

		public DeviceDescriptor() : this(false)
		{
		}

		public DeviceDescriptor(bool qualifier) : base()
		{
			Init(qualifier);
		}

		// Called by the constructors for doing some common stuff.
		private void Init(byte[] desc, ref int pos, Endianness endian)
		{
			int al = desc.Length;
			if(pos < 0 || pos >= al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			int l = desc[pos];
			// See USB specs 2.0 section 9.5:
			//   "If a descriptor returns with a value in its
			//    length field that is less than defined by this
			//    specification, the descriptor is invalid and
			//    should be rejected by the host."
			qualifier =
				desc[pos + 1] ==
					Descriptor.DeviceQualifierDescriptorType;
			int sl = qualifier ?
			         Descriptor.DeviceQualifierDescriptorLength :
			         Descriptor.DeviceDescriptorLength;
			if(l < sl)
				throw new ArgumentException("desc len invalid", "desc");
			if(al < pos + l)
				throw new ArgumentException();
			if(!qualifier &&
			   desc[pos + 1] != Descriptor.DeviceDescriptorType)
				throw new ArgumentException("desc type invalid", "desc");
			actConfKey = 0;
			cct = new ClassCodeTriple(desc[pos + 4],
			                          desc[pos + 5],
			                          desc[pos + 6]);
			if(endian == Endianness.System)
			{
				bcdUSB = BitConverter.ToUInt16(desc, pos + 2);
				if(!qualifier)
				{
					idVendor  = BitConverter.ToUInt16(desc, pos + 8);
					idProduct = BitConverter.ToUInt16(desc, pos + 10);
					bcdDevice = BitConverter.ToUInt16(desc, pos + 12);
				}
			}
			else
			{
				bcdUSB = (ushort)(desc[pos + 2] | desc[pos + 3] << 8);
				if(!qualifier)
				{
					idVendor  = (ushort)(desc[pos + 8]  |
					                     desc[pos + 9]  << 8);
					idProduct = (ushort)(desc[pos + 10] |
					                     desc[pos + 11] << 8);
					bcdDevice = (ushort)(desc[pos + 12] |
					                     desc[pos + 13] << 8);
				}
			}
			if(!qualifier)
			{
				iManufacturer = desc[pos + 14];
				iProduct      = desc[pos + 15];
				iSerialNumber = desc[pos + 16];
			}
			new ControlEndpoint(this, desc[pos + 7], 0);
			ParseTail(desc, ref pos);
		}

		private void Init(bool qualifier)
		{
			this.qualifier = qualifier;
			bcdUSB = 0x0200;
			new ControlEndpoint(this, 64, 0);
		}

		private void Init(byte[][] config, Endianness endian)
		{
			int c = config.Length;
			for(int i = 0; i < c; i++)
			{
				int l = config[i].Length;
				if(l == 0)
					throw new ArgumentException("config[" +
					                            i.ToString() +
					                            "] len is 0", "desc");
				if(config[i] == null ||
				   (l < Descriptor.ConfigurationDescriptorLength ||
				    config[i][1] != Descriptor.ConfigurationDescriptorType)
				   &&
				   (l <
				    Descriptor.OtherSpeedConfigurationDescriptorLength ||
				    config[i][1] !=
				    Descriptor.OtherSpeedConfigurationDescriptorType))
					throw new ArgumentException(
						"invalid Configuration Descriptor", "config");
				new ConfigurationDescriptor(this, config[i], endian);
			}
		}

		public DeviceDescriptor(byte[] desc,
		                        ref int pos,
		                        Endianness endian) : base()
		{
			if(desc == null)
				throw new ArgumentNullException("desc");
			int p = pos;
			Init(desc, ref p, endian);
			ParseCustomDescriptors(desc, ref p);
			WalkChildDescriptors(desc[qualifier ? 8 : 17],
			                     desc,
			                     ref p,
			                     endian);
			pos = p; // if no exception thrown so far, refresh pos
		}

		public DeviceDescriptor(byte[] desc, Endianness endian) : base()
		{
			if(desc != null)
			{
				int l = desc.Length;
				if(l == 0 || desc[0] > l)
					throw new ArgumentException("len invalid", "desc");
				int p = 0;
				Init(desc, ref p, endian);
				ParseCustomDescriptors(desc, ref p);
				WalkChildDescriptors(desc[qualifier ? 8 : 17],
				                     desc,
				                     ref p,
				                     endian);
				if(p != l)
					throw new ArgumentException("len invalid", "desc");
			}
			else
				Init(false);
		}

		public DeviceDescriptor(byte[] desc,
		                        byte[][] config,
		                        Endianness endian) : base()
		{
			if(desc != null)
			{
				int l = desc.Length;
				if(l == 0 || desc[0] > l)
					throw new ArgumentException("len invalid", "desc");
				int p = 0;
				Init(desc, ref p, endian);
				ParseCustomDescriptors(desc, ref p);
				if(p != l)
					throw new ArgumentException("len invalid", "desc");
				if(config != null)
				{
					if(desc[qualifier ? 8 : 17] != config.Length)
						throw new ArgumentException(
							"bNumConfigurations != config.Length", "config");
					Init(config, endian);
				}
				else
				{
					if(desc[qualifier ? 8 : 17] != 0x00)
						throw new ArgumentException("desc[" +
						                            (qualifier ? 8 : 17) +
						                            "] != 0x00 && " +
						                            "config == null");
				}
			}
			else
			{
				Init(false);
				if(config != null)
					Init(config, endian);
			}
		}

		protected override bool ParseChildDescriptor(byte[] desc,
		                                             ref int pos,
		                                             Endianness endian,
		                                             object context)
		{
			if(desc[pos] < Descriptor.ConfigurationDescriptorLength ||
			   desc[pos + 1] != Descriptor.ConfigurationDescriptorType)
				return false;
			new ConfigurationDescriptor(this, desc, ref pos, endian);
			return true;
		}

		public override bool Validate(ValidationMode mode)
		{
			int c = conf.Count;
			if(c < 1 || c > byte.MaxValue) return false;
			if(GetLocalDescriptor(null) > byte.MaxValue)
				return false;
			if(mode == ValidationMode.Strict)
			{
				if(!cct.ValidateForDevice())
					return false;
				for(int i = 1; i <= c; i++)
					if(!conf.ContainsKey((byte)i)) return false;
				bool n = cct.BaseClass == 0;
				foreach(ConfigurationDescriptor co in conf.Values)
					foreach(Interface i in co.Interfaces)
						foreach(AlternateSetting a in i.AlternateSettings)
							if(n ?
							   a.InterfaceClass == 0 :
							   a.InterfaceClass != 0)
								return false;
			}
			else if(conf.ContainsKey(0)) return false;
			if(!IsBcd(bcdUSB)) return false;
			if(ep0 == null || !ep0.Validate(mode)) return false;
			if(ep0.BaseAddress != 0x00) return false;
			short mps = ep0.MaxPacketSize;
			if(mps != 8 && mps != 16 && mps != 32 && mps != 64) return false;
			if(ep0.MaxNakRate != 0) return false;
			if(qualifier)
			{
				if(mode == ValidationMode.Strict)
				{
					// DEVICE_QUALIFIER doesn't have those
					if(idVendor      != 0 ||
					   idProduct     != 0 ||
					   bcdDevice     != 0 ||
					   iManufacturer != 0 ||
					   iProduct      != 0 ||
					   iSerialNumber != 0)
						return false;
				}
				// DEVICE_QUALIFIER doesn't exist in USB specification < 2.0
				// (see USB specs section 9.6.2)
				if(bcdUSB >> 8 < 2) return false;
				foreach(ConfigurationDescriptor cfg in conf.Values)
				{
					if(!cfg.Validate(mode)) return false;
					if(!cfg.IsOtherSpeed) return false;
				}
			}
			else
			{
				if(!IsBcd(bcdDevice)) return false;
				foreach(ConfigurationDescriptor cfg in conf.Values)
				{
					if(!cfg.Validate(mode)) return false;
					if(cfg.IsOtherSpeed) return false;
				}
			}
			return true;
		}

		private static bool IsBcd(ushort bcd)
		{
			return (bcd & 0xf) <= 9 &&
				(bcd >> 4 & 0xf) <= 9 &&
				(bcd >> 8 & 0xf) <= 9 &&
				(bcd >> 16 & 0xf) <= 9;
		}

		public bool Validate(DataRate dataRate)
		{
			return Validate(ValidationMode.Strict, dataRate);
		}

		public virtual bool Validate(ValidationMode mode, DataRate dataRate)
		{
			bool v = Validate(mode);
			if(!v) return false;
			bool lowspeed = dataRate == DataRate.Low;
			bool highspeed = dataRate == DataRate.High;
			bool superspeed = dataRate == DataRate.Super;
			short mps = ep0.MaxPacketSize;
			if(lowspeed && mps != 8) return false;
			// high-speed doesn't exist in  USB specification < 2.0
			// (see USB specs section 9.6.1)
			if(highspeed && (mps != 64 || bcdUSB >> 8 < 2)) return false;
			// super-speed doesn't exist in USB specification < 3.0
			if(superspeed && (mps != 64 || bcdUSB >> 8 < 3)) return false;
			return true;
		}

		public bool IsQualifier
		{
			get { return qualifier; }
			set { qualifier = value; }
		}

		public byte ActiveConfigurationIndex
		{
			get { return actConfKey; }
			set
			{
				if(value != 0 && !conf.ContainsKey(value))
					throw new KeyNotFoundException();
				if(actConfKey != value)
				{
					actConfKey = value;
					OnActiveConfigurationChanged(null);
				}
			}
		}

		public ConfigurationDescriptor ActiveConfiguration
		{
			get
			{
				if(actConfKey == 0) return null;
				return conf[actConfKey];
			}
			set
			{
				ActiveConfigurationIndex = (value == null) ? (byte)0U :
					GetIndexOfConfiguration(value);
			}
		}

		public event EventHandler ActiveConfigurationChanged;

		protected virtual void OnActiveConfigurationChanged(EventArgs e)
		{
			if(ActiveConfigurationChanged != null)
				ActiveConfigurationChanged(this, e);
		}

		public byte GetIndexOfConfiguration(ConfigurationDescriptor config)
		{
			if(config == null) throw new ArgumentNullException("config");
			foreach(KeyValuePair<byte, ConfigurationDescriptor> pair in conf)
				if(pair.Value == config)
					return pair.Key;
			throw new InvalidOperationException();
		}

		public bool ContainsConfiguration(ConfigurationDescriptor config)
		{
			if(config == null) throw new ArgumentNullException("config");
			return conf.ContainsValue(config);
		}

		public ControlEndpoint EndpointZero
		{
			get { return ep0; }
			set
			{
				if(ep0 == value) return;
				if(ep0 != null)
				{
					if(ep0.Parent == this) ep0.Parent = null;
					ep0 = null;
				}
				if(value != null)
				{
					if(value.Parent == this)
						ep0 = value;
					else
						value.Parent = this;
				}
			}
		}

		public bool IsUsingEndpoint(byte epadr)
		{
			if(epadr == 0x00 || epadr == 0x80) return ep0 != null;
			if(actConfKey != 0) return conf[actConfKey].IsUsingEndpoint(epadr);
			return false;
		}

		public Endpoint TryGetEndpoint(byte epadr)
		{
			if(epadr == 0x00 || epadr == 0x80) return ep0;
			if(actConfKey != 0) return conf[actConfKey].TryGetEndpoint(epadr);
			return null;
		}

		public ICollection<byte> ConfigurationIndices
		{
			get { return conf.Keys; }
		}

		public ICollection<ConfigurationDescriptor> Configurations
		{
			get { return conf.Values; }
		}

		public int ConfigurationsCount
		{
			get { return conf.Count; }
		}

		public ConfigurationDescriptor this[byte index]
		{
			get
			{
				if(index == 0) throw new ArgumentException("", "index");
				return conf[index];
			}
			set
			{
				if(index == 0) throw new ArgumentException("", "index");
				if(value == null) throw new ArgumentNullException("value");
				DeviceDescriptor parent = value.Device;
				if(parent != null && parent != this)
					throw new InvalidOperationException();
				if(conf.ContainsKey(index))
				{
					if(conf[index] == value) return;
					RemoveConfiguration(conf[index]);
				}
				AddConfiguration(index, value);
			}
		}

		public void RemoveAllConfigurations()
		{
			int c = conf.Count;
			if(c > 0)
			{
				ConfigurationDescriptor[] arr = new ConfigurationDescriptor[c];
				conf.Values.CopyTo(arr, 0);
				// Raises the ConfigurationRemoved event on every iteration.
				foreach(ConfigurationDescriptor config in arr)
					RemoveConfiguration(config);
			}
			// Not really necessary
			conf.Clear();
		}

		public bool IsUsingConfigurationIndex(byte index)
		{
			return conf.ContainsKey(index);
		}

		public event EventHandler BcdUsbChanged;
		public event EventHandler VendorIDChanged;
		public event EventHandler ProductIDChanged;
		public event EventHandler BcdDeviceChanged;
		public event EventHandler DeviceClassChanged;
		public event EventHandler DeviceSubClassChanged;
		public event EventHandler DeviceProtocolChanged;
		public event EventHandler ManufacturerStringIndexChanged;
		public event EventHandler ProductStringIndexChanged;
		public event EventHandler SerialNumberStringIndexChanged;

		protected virtual void OnBcdUsbChanged(EventArgs e)
		{
			if(BcdUsbChanged != null)
				BcdUsbChanged(this, e);
		}

		protected virtual void OnVendorIDChanged(EventArgs e)
		{
			if(VendorIDChanged != null)
				VendorIDChanged(this, e);
		}

		protected virtual void OnProductIDChanged(EventArgs e)
		{
			if(ProductIDChanged != null)
				ProductIDChanged(this, e);
		}

		protected virtual void OnBcdDeviceChanged(EventArgs e)
		{
			if(BcdDeviceChanged != null)
				BcdDeviceChanged(this, e);
		}

		protected virtual void OnDeviceClassChanged(EventArgs e)
		{
			if(DeviceClassChanged != null)
				DeviceClassChanged(this, e);
		}

		protected virtual void OnDeviceSubClassChanged(EventArgs e)
		{
			if(DeviceSubClassChanged != null)
				DeviceSubClassChanged(this, e);
		}

		protected virtual void OnDeviceProtocolChanged(EventArgs e)
		{
			if(DeviceProtocolChanged != null)
				DeviceProtocolChanged(this, e);
		}

		protected virtual void OnManufacturerStringIndexChanged(EventArgs e)
		{
			if(ManufacturerStringIndexChanged != null)
				ManufacturerStringIndexChanged(this, e);
		}

		protected virtual void OnProductStringIndexChanged(EventArgs e)
		{
			if(ProductStringIndexChanged != null)
				ProductStringIndexChanged(this, e);
		}

		protected virtual void OnSerialNumberStringIndexChanged(EventArgs e)
		{
			if(SerialNumberStringIndexChanged != null)
				SerialNumberStringIndexChanged(this, e);
		}

		public short BcdUsb
		{
			get { return unchecked((short)bcdUSB); }
			set
			{
				if(bcdUSB != unchecked((ushort)value))
				{
					bcdUSB = unchecked((ushort)value);
					OnBcdUsbChanged(null);
				}
			}
		}

		public short VendorID
		{
			get { return unchecked((short)idVendor); }
			set
			{
				if(idVendor != unchecked((ushort)value))
				{
					idVendor = unchecked((ushort)value);
					OnVendorIDChanged(null);
				}
			}
		}

		public short ProductID
		{
			get { return unchecked((short)idProduct); }
			set
			{
				if(idProduct != unchecked((ushort)value))
				{
					idProduct = unchecked((ushort)value);
					OnProductIDChanged(null);
				}
			}
		}

		public short BcdDevice
		{
			get { return unchecked((short)bcdDevice); }
			set
			{
				if(bcdDevice != unchecked((ushort)value))
				{
					bcdDevice = unchecked((ushort)value);
					OnBcdDeviceChanged(null);
				}
			}
		}

		public byte DeviceClass
		{
			get { return cct.BaseClass; }
			set
			{
				if(cct.BaseClass != value)
				{
					cct.BaseClass = value;
					OnDeviceClassChanged(null);
				}
			}
		}

		public byte DeviceSubClass
		{
			get { return cct.SubClass; }
			set
			{
				if(cct.SubClass != value)
				{
					cct.SubClass = value;
					OnDeviceSubClassChanged(null);
				}
			}
		}

		public byte DeviceProtocol
		{
			get { return cct.Protocol; }
			set
			{
				if(cct.Protocol != value)
				{
					cct.Protocol = value;
					OnDeviceProtocolChanged(null);
				}
			}
		}

		public ClassCodeTriple ClassCodeTriple
		{
			get { return cct; }
			set
			{
				bool diffBase = cct.BaseClass != value.BaseClass;
				bool diffSub  = cct.SubClass  != value.SubClass;
				bool diffProt = cct.Protocol  != value.Protocol;
				cct = value;
				if(diffBase) OnDeviceClassChanged(null);
				if(diffSub)  OnDeviceSubClassChanged(null);
				if(diffProt) OnDeviceProtocolChanged(null);
			}
		}

		public byte ManufacturerStringIndex
		{
			get { return iManufacturer; }
			set
			{
				if(iManufacturer != value)
				{
					iManufacturer = value;
					OnManufacturerStringIndexChanged(null);
				}
			}
		}

		public byte ProductStringIndex
		{
			get { return iProduct; }
			set
			{
				if(iProduct != value)
				{
					iProduct = value;
					OnProductStringIndexChanged(null);
				}
			}
		}

		public byte SerialNumberStringIndex
		{
			get { return iSerialNumber; }
			set
			{
				if(iSerialNumber != value)
				{
					iSerialNumber = value;
					OnSerialNumberStringIndexChanged(null);
				}
			}
		}

		public void AddConfiguration(byte index, ConfigurationDescriptor config)
		{
			if(index == 0) throw new ArgumentException("", "index");
			if(config == null) throw new ArgumentNullException("config");
			if(conf.ContainsKey(index) || conf.ContainsValue(config))
				throw new InvalidOperationException();
			DeviceDescriptor parent = config.Device;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			conf.Add(index, config);
			if(parent != this) config.Device = this;
			OnConfigurationAdded(new CollectionModifiedEventArgs
			                     <ConfigurationDescriptor>(config));
		}

		public void AppendConfiguration(ConfigurationDescriptor config)
		{
			if(config == null) throw new ArgumentNullException("config");
			if(conf.ContainsValue(config))
				throw new InvalidOperationException();
			DeviceDescriptor parent = config.Device;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			byte index = 0;
			for(int i = 1; i <= 255; i++)
			{
				if(!conf.ContainsKey((byte)i))
				{
					index = (byte)i;
					break;
				}
			}
			if(index == 0) throw new InvalidOperationException();
			conf.Add(index, config);
			if(parent != this) config.Device = this;
			OnConfigurationAdded(new CollectionModifiedEventArgs
			                     <ConfigurationDescriptor>(config));
		}

		public void RemoveConfiguration(ConfigurationDescriptor config)
		{
			if(config == null) throw new ArgumentNullException("config");
			byte index = 0;
			DeviceDescriptor parent = config.Device;
			try
			{
				index = GetIndexOfConfiguration(config);
			}
			catch(InvalidOperationException)
			{
				if(parent == this) config.Device = null;
				throw;
			}
			if(actConfKey == index) ActiveConfiguration = null;
			conf.Remove(index);
			if(parent == this) config.Device = null;
			OnConfigurationRemoved(new CollectionModifiedEventArgs
			                       <ConfigurationDescriptor>(config));
		}

		public void RemoveConfigurationAt(byte index)
		{
			if(index == 0) throw new ArgumentException("", "index");
			ConfigurationDescriptor config = conf[index];
			DeviceDescriptor parent = config.Device;
			if(actConfKey == index) ActiveConfiguration = null;
			conf.Remove(index);
			if(parent == this) config.Device = null;
			OnConfigurationRemoved(new CollectionModifiedEventArgs
			                       <ConfigurationDescriptor>(config));
		}

		public event
			EventHandler<CollectionModifiedEventArgs<ConfigurationDescriptor>>
				ConfigurationAdded;

		public event
			EventHandler<CollectionModifiedEventArgs<ConfigurationDescriptor>>
				ConfigurationRemoved;

		protected virtual void OnConfigurationAdded(
			CollectionModifiedEventArgs<ConfigurationDescriptor> e)
		{
			if(ConfigurationAdded != null) ConfigurationAdded(this, e);
		}

		protected virtual void OnConfigurationRemoved(
			CollectionModifiedEventArgs<ConfigurationDescriptor> e)
		{
			if(ConfigurationRemoved != null) ConfigurationRemoved(this, e);
		}

		byte IEndpointParent.GetBaseAddressOfEndpoint(Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			if(endpoint == ep0) return 0x00;
			throw new InvalidOperationException();
		}

		bool IEndpointParent.ContainsEndpoint(Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			return endpoint == ep0;
		}

		void IEndpointParent.AddEndpoint(byte epadr, Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			if(!Endpoint.ValidateAddressForInstance(epadr, endpoint))
				throw new ArgumentException(
					"epadr is not valid for endpoint", "epadr");
			if(epadr != 0x00 && epadr != 0x80)
				throw new InvalidOperationException();
			if(ep0 != null) throw new InvalidOperationException();
			if(!(endpoint is ControlEndpoint))
				throw new InvalidOperationException();
			IEndpointParent parent = endpoint.Parent;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			ep0 = (ControlEndpoint)endpoint;
			if(parent != this) endpoint.Parent = this;
			OnEndpointZeroChanged(null);
		}

		void IEndpointParent.AppendEndpoint(Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			if(ep0 != null) throw new InvalidOperationException();
			if(!(endpoint is ControlEndpoint))
				throw new InvalidOperationException();
			IEndpointParent parent = endpoint.Parent;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			ep0 = (ControlEndpoint)endpoint;
			if(parent != this) endpoint.Parent = this;
			OnEndpointZeroChanged(null);
		}

		void IEndpointParent.RemoveEndpoint(Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			IEndpointParent parent = endpoint.Parent;
			if(ep0 != endpoint)
			{
				if(parent == this) endpoint.Parent = null;
				throw new InvalidOperationException();
			}
			ep0 = null;
			if(parent == this) endpoint.Parent = null;
			OnEndpointZeroChanged(null);
		}

		void IEndpointParent.RemoveEndpointAt(byte epadr)
		{
			if(ep0 == null || (epadr != 0x00 && epadr != 0x80))
				throw new KeyNotFoundException();
			Endpoint endpoint = ep0;
			ep0 = null;
			IEndpointParent parent = endpoint.Parent;
			if(parent == this) endpoint.Parent = null;
			OnEndpointZeroChanged(null);
		}

		public event EventHandler EndpointZeroChanged;

		protected virtual void OnEndpointZeroChanged(EventArgs e)
		{
			if(EndpointZeroChanged != null)
				EndpointZeroChanged(this, e);
		}

		public override sealed int RegularSize
		{
			get
			{
				return qualifier ?
					Descriptor.DeviceQualifierDescriptorLength :
					Descriptor.DeviceDescriptorLength;
			}
		}

		protected override void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian)
		{
			int cc = conf.Count;
			if(cc > byte.MaxValue)
				throw new InvalidOperationException();
			desc[index + 1] = qualifier ?
			                  Descriptor.DeviceQualifierDescriptorType :
			                  Descriptor.DeviceDescriptorType;
			desc[index + 4] = cct.BaseClass;
			desc[index + 5] = cct.SubClass;
			desc[index + 6] = cct.Protocol;
			if(ep0 != null) desc[index + 7] = (byte)ep0.MaxPacketSize;
			else desc[index + 7] = 8;
			if(endian == Endianness.System)
			{
				BitConverter.GetBytes(bcdUSB).CopyTo(desc, index + 2);
				if(!qualifier)
				{
					BitConverter.GetBytes(idVendor).CopyTo(desc, index + 8);
					BitConverter.GetBytes(idProduct).CopyTo(desc, index + 10);
					BitConverter.GetBytes(bcdDevice).CopyTo(desc, index + 12);
				}
			}
			else
			{
				desc[index + 2] = unchecked((byte)bcdUSB);
				desc[index + 3] = unchecked((byte)(bcdUSB >> 8));
				if(!qualifier)
				{
					desc[index + 8] = unchecked((byte)idVendor);
					desc[index + 9] = unchecked((byte)(idVendor >> 8));
					desc[index + 10] = unchecked((byte)idProduct);
					desc[index + 11] = unchecked((byte)(idProduct >> 8));
					desc[index + 12] = unchecked((byte)bcdDevice);
					desc[index + 13] = unchecked((byte)(bcdDevice >> 8));
				}
			}
			if(!qualifier)
			{
				desc[index + 14] = iManufacturer;
				desc[index + 15] = iProduct;
				desc[index + 16] = iSerialNumber;
				desc[index + 17] = (byte)cc;
			}
			else
			{
				desc[index + 8] = (byte)cc;
				desc[index + 9] = 0;
			}
		}

		protected override int GetSubDescriptors([In, Out] byte[] desc,
		                                         int index,
		                                         Endianness endian)
		{
			int dtl = 0;
			foreach(ConfigurationDescriptor config in conf.Values)
				dtl += config.GetDescriptor(null);
			if(desc == null)
				return dtl;
			int al = desc.Length;
			if(conf.Count > 0)
			{
				if(index < 0 || index >= al)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(al < dtl + index)
					throw new InvalidOperationException();
				foreach(ConfigurationDescriptor config in conf.Values)
					index += config.GetDescriptor(desc, index, endian);
			}
			return dtl;
		}

		public byte[][] GetConfigurationDescriptors(Endianness endian)
		{
			int c = conf.Count;
			byte[][] configs = new byte[c][];
			int i = 0;
			foreach(ConfigurationDescriptor config in conf.Values)
				configs[i++] = config.GetDescriptor(endian);
			return configs;
		}

		public byte[][] GetConfigurationDescriptors()
		{
			return GetConfigurationDescriptors(Endianness.UsbSpec);
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			if(stm == null) throw new ArgumentNullException("stm");
			if(prefix == null) throw new ArgumentNullException("prefix");
			string cs = cct.BaseClassString();
			string ps = cct.ProtocolString();
			cs = cs == null ? "" : " (" + cs + ")";
			ps = ps == null ? "" : " (" + ps + ")";
			stm.WriteLine(prefix + "BcdUSB:         0x" +
			              bcdUSB.ToString("x4"));
			stm.WriteLine(prefix + "DeviceClass:    0x" +
			              cct.BaseClass.ToString("x2") + cs);
			stm.WriteLine(prefix + "DeviceSubClass: 0x" +
			              cct.SubClass.ToString("x2"));
			stm.WriteLine(prefix + "DeviceProtocol: 0x" +
			              cct.Protocol.ToString("x2") + ps);
			if(!qualifier)
			{
				stm.WriteLine(prefix + "VendorID:       0x" +
				              idVendor.ToString("x4"));
				stm.WriteLine(prefix + "ProductID:      0x" +
				              idProduct.ToString("x4"));
				stm.WriteLine(prefix + "BcdDevice:      0x" +
				              bcdDevice.ToString("x4"));
				stm.WriteLine(prefix + "ManufactStrIdx: " +
				              iManufacturer.ToString());
				stm.WriteLine(prefix + "ProductStrIdx:  " +
				              iProduct.ToString());
				stm.WriteLine(prefix + "SerNumStrIdx:   " +
				              iSerialNumber.ToString());
			}
			if(ep0 != null)
			{
				stm.WriteLine(prefix + "> [Endpoint 0x" +
				              ep0.BaseAddress.ToString("x2") + "]");
				ep0.Dump(stm, prefix + "  ");
			}
			stm.WriteLine(prefix + "ConfigCount:    " + conf.Count.ToString());
			base.Dump(stm, prefix);
			string cfgPrefix = prefix + "  ";
			foreach(KeyValuePair<byte, ConfigurationDescriptor> kvp in conf)
			{
				ConfigurationDescriptor cnf = kvp.Value;
				string os = cnf.IsOtherSpeed ? "OtherSpeed" : "";
				stm.WriteLine(prefix + "> [" + os + "Configuration " +
				              kvp.Key.ToString() + "]");
				cnf.Dump(stm, cfgPrefix);
			}
		}

		public static void TransferDescriptorActiveState(DeviceDescriptor src,
		                                                 DeviceDescriptor dst)
		{
			if(src == null) throw new ArgumentNullException("src");
			if(dst == null) throw new ArgumentNullException("dst");
			foreach(ConfigurationDescriptor conf in src.Configurations)
			{
				ConfigurationDescriptor dstConf = dst[conf.Index];
				dstConf.IsActive = conf.IsActive;
				foreach(Interface ifc in conf.Interfaces)
				{
					Interface dstIfc = dstConf[ifc.Index];
					dstIfc.Owner = ifc.Owner;
					dstIfc.IsActive = ifc.IsActive;
					foreach(AlternateSetting aifc in ifc.AlternateSettings)
					{
						AlternateSetting dstAifc = dstIfc[aifc.Index];
						dstAifc.IsActive = aifc.IsActive;
					}
				}
			}
		}
	}
}
