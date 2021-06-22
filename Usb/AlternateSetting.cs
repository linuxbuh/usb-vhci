/*
 * AlternateSetting.cs -- USB related classes
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
using System.IO;
using System.Runtime.InteropServices;

namespace Usb
{
	public partial class AlternateSetting : RegularDescriptor, IEndpointParent
	{
		private Interface parent;
		private Dictionary<byte, Endpoint> ep =
			new Dictionary<byte, Endpoint>();
		private bool lastState;

		// Class code (assigned by the USB-IF).
		// A value of zero is reserved for future
		// standardization.
		// If this field is set to FFH, the interface
		// class is vendor-specific.
		// All other values are reserved for
		// assignment by the USB-IF.
		// ---
		// Subclass code (assigned by the USB-IF).
		// These codes are qualified by the value of
		// the bInterfaceClass field.
		// If the bInterfaceClass field is reset to zero,
		// this field must also be reset to zero.
		// If the bInterfaceClass field is not set to
		// FFH, all values are reserved for
		// assignment by the USB-IF.
		// ---
		// Protocol code (assigned by the USB).
		// These codes are qualified by the value of
		// the bInterfaceClass and the
		// bInterfaceSubClass fields. If an interface
		// supports class-specific requests, this code
		// identifies the protocols that the device
		// uses as defined by the specification of the
		// device class.
		// If this field is reset to zero, the device
		// does not use a class-specific protocol on
		// this interface.
		// If this field is set to FFH, the device uses
		// a vendor-specific protocol for this
		// interface.
		private ClassCodeTriple cct;

		// Index of string descriptor describing this interface
		private byte iInterface;

		public AlternateSetting() : base()
		{
		}

		// Called by the constructors for doing some common stuff.
		private void Init(byte[] desc, ref int pos)
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
				throw new ArgumentException("desc[" + pos + "] is less than " +
				                            Descriptor.
				                            InterfaceDescriptorLength,
				                            "desc");
			if(al < pos + l)
				throw new ArgumentException();
			if(desc[pos + 1] != Descriptor.InterfaceDescriptorType)
				throw new ArgumentException("desc[" + (pos + 1) + "] is not "+
				                            Descriptor.InterfaceDescriptorType,
				                            "desc");
			cct = new ClassCodeTriple(desc[pos + 5],
			                          desc[pos + 6],
			                          desc[pos + 7]);
			iInterface = desc[pos + 8];
			ParseTail(desc, ref pos);
		}

		public AlternateSetting(Interface parent,
		                        byte[] desc,
		                        ref int pos,
		                        bool ignoreAltIndex) : base()
		{
			if(desc == null)
				throw new ArgumentNullException("desc");
			int p = pos;
			Init(desc, ref p);
			ParseCustomDescriptors(desc, ref p);
			WalkChildDescriptors(desc[pos + 4], desc, ref p);
			if(!ignoreAltIndex)
			{
				if(parent == null)
					throw new ArgumentException("!ignoreAltIndex && " +
					                            "parent == null");
				parent.AddAlternateSetting(desc[pos + 3], this);
			}
			else
				Interface = parent;
			pos = p; // if no exception thrown so far, refresh pos
		}

		public AlternateSetting(Interface parent,
		                        byte[] desc,
		                        ref int pos) :
			this(parent, desc, ref pos, false)
		{
		}

		public AlternateSetting(byte[] desc,
		                        ref int pos) :
			this(null, desc, ref pos, true)
		{
		}

		public AlternateSetting(Interface parent) : base()
		{
			Interface = parent;
		}

		public AlternateSetting(Interface parent,
		                        byte[] desc,
		                        bool ignoreAltIndex) : base()
		{
			if(desc != null)
			{
				int l = desc.Length;
				if(l == 0 || desc[0] > l)
					throw new ArgumentException("len invalid", "desc");
				int p = 0;
				Init(desc, ref p);
				ParseCustomDescriptors(desc, ref p);
				WalkChildDescriptors(desc[4], desc, ref p);
				if(p != l)
					throw new ArgumentException("len invalid", "desc");
				if(!ignoreAltIndex)
				{
					if(parent == null)
						throw new ArgumentException("!ignoreAltIndex && " +
						                            "parent == null");
					parent.AddAlternateSetting(desc[3], this);
				}
				else
					Interface = parent;
			}
			else
			{
				if(!ignoreAltIndex)
					throw new ArgumentNullException("desc");
				Interface = parent;
			}
		}

		public AlternateSetting(Interface parent,
		                        byte[] desc) : this(parent, desc, false)
		{
		}

		public AlternateSetting(byte[] desc) : this(null, desc, true)
		{
		}

		protected override bool ParseChildDescriptor(byte[] desc,
		                                             ref int pos,
		                                             Endianness endian,
		                                             object context)
		{
			if(desc[pos] < Descriptor.EndpointDescriptorLength ||
			   desc[pos + 1] != Descriptor.EndpointDescriptorType)
				return false;
			Endpoint.Create(this, desc, ref pos);
			return true;
		}

		public override bool Validate(ValidationMode mode)
		{
			if(mode == ValidationMode.Strict)
			{
				if(!cct.ValidateForInterface())
					return false;
			}
			// TODO: implement
			return true;
		}

		public bool IsUsingEndpoint(byte epadr)
		{
			return !IsBaseAddressFree(epadr, false);
		}

		public Endpoint TryGetEndpoint(byte epadr)
		{
			if(!Endpoint.ValidateAddress(epadr))
				throw new ArgumentException(
					"invalid endpoint address", "epadr");
			Endpoint ep;
			if(this.ep.TryGetValue(epadr, out ep)) return ep;
			if(this.ep.TryGetValue((byte)(uint)(epadr ^ 0x80), out ep) &&
				(ep is BidirectionalEndpoint)) return ep;
			return null;
		}

		public bool IsActive
		{
			get
			{
				if(parent == null) return false;
				else return parent.ActiveAlternateSetting == this;
			}
			set
			{
				if(value != true) throw new ArgumentException("", "value");
				if(parent == null) throw new InvalidOperationException();
				parent.ActiveAlternateSetting = this;
			}
		}

		public byte GetBaseAddressOfEndpoint(Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			foreach(KeyValuePair<byte, Endpoint> pair in ep)
				if(pair.Value == endpoint)
					return pair.Key;
			throw new InvalidOperationException();
		}

		public bool ContainsEndpoint(Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			return ep.ContainsValue(endpoint);
		}
		
		public Interface Interface
		{
			get { return parent; }
			set
			{
				if(value == parent) return;
				if(parent != null)
				{
					Interface ifc = parent;
					parent = null;
					ifc.ActiveAlternateSettingChanged -=
						parent_ActiveAlternateSettingChanged;
					ifc.RemoveAlternateSetting(this);
				}
				if(value == null)
				{
					if(lastState)
					{
						lastState = false;
						OnIsActiveChanged(null);
					}
					OnInterfaceChanged(null);
					return;
				}
				parent = value;
				if(!value.ContainsAlternateSetting(this))
				{
					try
					{
						value.AppendAlternateSetting(this);
					}
					catch
					{
						parent = null;
						throw;
					}
				}
				value.ActiveAlternateSettingChanged +=
					parent_ActiveAlternateSettingChanged;
				OnInterfaceChanged(null);
				parent_ActiveAlternateSettingChanged(value, null);
			}
		}

		private void parent_ActiveAlternateSettingChanged
			(object sender, EventArgs e)
		{
			if(parent != null && sender == parent)
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
				return parent.GetIndexOfAlternateSetting(this);
			}
		}

		public byte InterfaceClass
		{
			get { return cct.BaseClass; }
			set
			{
				if(cct.BaseClass != value)
				{
					cct.BaseClass = value;
					OnInterfaceClassChanged(null);
				}
			}
		}

		public byte InterfaceSubClass
		{
			get { return cct.SubClass; }
			set
			{
				if(cct.SubClass != value)
				{
					cct.SubClass = value;
					OnInterfaceSubClassChanged(null);
				}
			}
		}

		public byte InterfaceProtocol
		{
			get { return cct.Protocol; }
			set
			{
				if(cct.Protocol != value)
				{
					cct.Protocol = value;
					OnInterfaceProtocolChanged(null);
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
				if(diffBase) OnInterfaceClassChanged(null);
				if(diffSub)  OnInterfaceSubClassChanged(null);
				if(diffProt) OnInterfaceProtocolChanged(null);
			}
		}

		public byte StringDescriptorIndex
		{
			get { return iInterface; }
			set
			{
				if(iInterface != value)
				{
					iInterface = value;
					OnStringDescriptorIndexChanged(null);
				}
			}
		}

		public event EventHandler InterfaceClassChanged;
		public event EventHandler InterfaceSubClassChanged;
		public event EventHandler InterfaceProtocolChanged;
		public event EventHandler StringDescriptorIndexChanged;

		protected virtual void OnInterfaceClassChanged(EventArgs e)
		{
			if(InterfaceClassChanged != null)
				InterfaceClassChanged(this, e);
		}

		protected virtual void OnInterfaceSubClassChanged(EventArgs e)
		{
			if(InterfaceSubClassChanged != null)
				InterfaceSubClassChanged(this, e);
		}

		protected virtual void OnInterfaceProtocolChanged(EventArgs e)
		{
			if(InterfaceProtocolChanged != null)
				InterfaceProtocolChanged(this, e);
		}

		protected virtual void OnStringDescriptorIndexChanged(EventArgs e)
		{
			if(StringDescriptorIndexChanged != null)
				StringDescriptorIndexChanged(this, e);
		}

		public ICollection<byte> EndpointBaseAddresses
		{
			get { return ep.Keys; }
		}

		public ICollection<Endpoint> Endpoints
		{
			get { return ep.Values; }
		}

		public int EndpointCount
		{
			get { return ep.Count; }
		}

		public Endpoint this[byte epadr]
		{
			get
			{
				Endpoint ep = TryGetEndpoint(epadr);
				if(ep == null) throw new KeyNotFoundException();
				return ep;
			}
			set
			{
				if(!Endpoint.ValidateAddress(epadr))
					throw new ArgumentException("", "epadr");
				if(value == null) throw new ArgumentNullException("value");
				IEndpointParent parent = value.Parent;
				if(parent != null && parent != this)
					throw new InvalidOperationException();
				bool newIsBi = value is BidirectionalEndpoint;
				if(newIsBi) epadr = (byte)(uint)(epadr & 0x7f);
				byte other = (byte)(uint)(epadr ^ 0x80);
				Endpoint old = TryGetEndpoint(epadr);
				Endpoint oldOther = TryGetEndpoint(other);
				if(old == value) return;
				if(old != null)
					RemoveEndpoint(old);
				// (from old==oldOther follows that old and oldOther are
				//  bidirectional)
				if(newIsBi && oldOther != null && old != oldOther)
					RemoveEndpoint(oldOther);
				AddEndpoint(epadr, value);
			}
		}

		public void RemoveAllEndpoints()
		{
			int c = ep.Count;
			if(c > 0)
			{
				Endpoint[] arr = new Endpoint[c];
				ep.Values.CopyTo(arr, 0);
				// Raises the EndpointRemoved event on every iteration.
				foreach(Endpoint endpoint in arr)
					RemoveEndpoint(endpoint);
			}
			// Not really necessary
			ep.Clear();
		}

		public bool IsBaseAddressFree(byte epadr, bool bi)
		{
			if(!Endpoint.ValidateAddress(epadr))
				throw new ArgumentException(
					"invalid endpoint address", "epadr");
			if((epadr & 0x7f) == 0x00) return false; // EP0 is reserved
			byte other = (byte)(uint)(epadr ^ 0x80);
			if(bi) return !ep.ContainsKey(epadr) && !ep.ContainsKey(other);
			if(ep.ContainsKey(epadr)) return false;
			if(ep.ContainsKey(other))
				return !ep[other].ConflictsWithUnidirectional(epadr);
			return true;
		}

		public bool GetNextFreeBaseAddress(byte epadr, bool bi, out byte next)
		{
			next = 0xff;
			if(!Endpoint.ValidateAddress(epadr))
				throw new ArgumentException(
					"invalid endpoint address", "epadr");
			if(bi) epadr = (byte)(uint)(epadr & 0x7f);
			while(Endpoint.ValidateAddress(epadr))
			{
				if(IsBaseAddressFree(epadr, bi))
				{
					next = epadr;
					return true;
				}
				epadr++;
			}
			return false;
		}

		public byte GetNextFreeBaseAddress(byte epadr, bool bi)
		{
			byte next;
			if(!GetNextFreeBaseAddress(epadr, bi, out next))
				throw new KeyNotFoundException();
			return next;
		}

		public bool GetFreeBidirectionalBaseAddress(out byte epadr)
		{
			return GetNextFreeBaseAddress(0x01, true, out epadr);
		}

		public bool GetFreeUnidirectionalBaseAddress(
			EndpointDirection dir, out byte epadr)
		{
			return GetNextFreeBaseAddress(
				(dir == EndpointDirection.In) ? (byte)0x81U : (byte)0x01U,
				false, out epadr);
		}

		public byte GetFreeBidirectionalBaseAddress()
		{
			byte epadr;
			if(!GetFreeBidirectionalBaseAddress(out epadr))
				throw new KeyNotFoundException();
			return epadr;
		}

		public byte GetFreeUnidirectionalBaseAddress(EndpointDirection dir)
		{
			byte epadr;
			if(!GetFreeUnidirectionalBaseAddress(dir, out epadr))
				throw new KeyNotFoundException();
			return epadr;
		}

		public void AddEndpoint(byte epadr, Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			if(!Endpoint.ValidateAddressForInstance(epadr, endpoint))
				throw new ArgumentException(
					"epadr is not valid for endpoint", "epadr");
			bool bi = endpoint is BidirectionalEndpoint;
			if(bi) epadr = (byte)(uint)(epadr & 0x7f);
			if(!IsBaseAddressFree(epadr, bi))
				throw new InvalidOperationException();
			if(ep.ContainsValue(endpoint))
				throw new InvalidOperationException();
			IEndpointParent parent = endpoint.Parent;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			ep.Add(epadr, endpoint);
			if(parent != this) endpoint.Parent = this;
			OnEndpointAdded(new CollectionModifiedEventArgs<Endpoint>
			                (endpoint));
		}

		public void AppendEndpoint(Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			bool bi = endpoint is BidirectionalEndpoint;
			if(ep.ContainsValue(endpoint))
				throw new InvalidOperationException();
			IEndpointParent parent = endpoint.Parent;
			if(parent != null && parent != this)
				throw new InvalidOperationException();
			byte epadr;
			if(bi)
			{
				if(!GetFreeBidirectionalBaseAddress(out epadr))
					throw new InvalidOperationException();
			}
			else
			{
				if(!GetFreeUnidirectionalBaseAddress(
					((UnidirectionalEndpoint)endpoint).Direction,
					out epadr))
					throw new InvalidOperationException();
			}
			ep.Add(epadr, endpoint);
			if(parent != this) endpoint.Parent = this;
			OnEndpointAdded(new CollectionModifiedEventArgs<Endpoint>
			                (endpoint));
		}

		public void RemoveEndpoint(Endpoint endpoint)
		{
			if(endpoint == null) throw new ArgumentNullException("endpoint");
			byte epadr = 0;
			IEndpointParent parent = endpoint.Parent;
			try
			{
				epadr = GetBaseAddressOfEndpoint(endpoint);
			}
			catch(InvalidOperationException)
			{
				if(parent == this) endpoint.Parent = null;
				throw;
			}
			ep.Remove(epadr);
			if(parent == this) endpoint.Parent = null;
			OnEndpointRemoved(new CollectionModifiedEventArgs<Endpoint>
			                  (endpoint));
		}

		public void RemoveEndpointAt(byte epadr)
		{
			Endpoint endpoint = TryGetEndpoint(epadr);
			if(endpoint == null) throw new KeyNotFoundException();
			RemoveEndpoint(endpoint);
		}

		public event EventHandler<CollectionModifiedEventArgs<Endpoint>>
			EndpointAdded;

		public event EventHandler<CollectionModifiedEventArgs<Endpoint>>
			EndpointRemoved;
		
		protected virtual void
			OnEndpointAdded(CollectionModifiedEventArgs<Endpoint> e)
		{
			if(EndpointAdded != null)
				EndpointAdded(this, e);
		}

		protected virtual void
			OnEndpointRemoved(CollectionModifiedEventArgs<Endpoint> e)
		{
			if(EndpointRemoved != null)
				EndpointRemoved(this, e);
		}

		public event EventHandler InterfaceChanged;

		protected virtual void OnInterfaceChanged(EventArgs e)
		{
			if(InterfaceChanged != null)
				InterfaceChanged(this, e);
		}

		public override sealed int RegularSize
		{
			get { return Descriptor.InterfaceDescriptorLength; }
		}

		protected override void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian)
		{
			int ec = ep.Count;
			if(ec > byte.MaxValue)
				throw new InvalidOperationException();
			desc[index + 1] = Descriptor.InterfaceDescriptorType;
			desc[index + 2] = parent != null && parent.Configuration != null ?
			                  Interface.Index : (byte)0U;
			desc[index + 3] = parent != null ? Index : (byte)0U;
			desc[index + 4] = (byte)ec;
			desc[index + 5] = cct.BaseClass;
			desc[index + 6] = cct.SubClass;
			desc[index + 7] = cct.Protocol;
			desc[index + 8] = iInterface;
		}

		protected override int GetSubDescriptors([In, Out] byte[] desc,
		                                         int index,
		                                         Endianness endian)
		{
			int dtl = 0;
			foreach(Endpoint endpoint in ep.Values)
				dtl += endpoint.GetDescriptor(null);
			if(desc == null)
				return dtl;
			int al = desc.Length;
			if(ep.Count > 0)
			{
				if(index < 0 || index >= al)
					throw new ArgumentOutOfRangeException("index", index, "");
				if(al < dtl + index)
					throw new InvalidOperationException();
				SortedDictionary<byte, Endpoint> sorted =
					new SortedDictionary<byte, Endpoint>
						(ep, Endpoint.BaseAddressComparer);
				foreach(Endpoint endpoint in sorted.Values)
					index += endpoint.GetDescriptor(desc, index);
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
			string cs = cct.BaseClassString();
			string ps = cct.ProtocolString();
			cs = cs == null ? "" : " (" + cs + ")";
			ps = ps == null ? "" : " (" + ps + ")";
			stm.WriteLine(prefix + "Index:          " + index);
			stm.WriteLine(prefix + "IsActive:       " + IsActive.ToString());
			stm.WriteLine(prefix + "IfcClass:       0x" +
			              cct.BaseClass.ToString("x2") + cs);
			stm.WriteLine(prefix + "IfcSubClass:    0x" +
			              cct.SubClass.ToString("x2"));
			stm.WriteLine(prefix + "IfcProtocol     0x" +
			              cct.Protocol.ToString("x2") + ps);
			stm.WriteLine(prefix + "StrDescIndex:   " + iInterface.ToString());
			stm.WriteLine(prefix + "EndpointCount:  " + ep.Count.ToString());
			base.Dump(stm, prefix);
			string epPrefix = prefix + "  ";
			foreach(KeyValuePair<byte, Endpoint> kvp in ep)
			{
				stm.WriteLine(prefix + "> [Endpoint 0x" +
				              kvp.Key.ToString("x2") + "]");
				kvp.Value.Dump(stm, epPrefix);
			}
		}
	}
}
