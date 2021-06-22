/*
 * ControlUrb.cs -- USB related classes
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

namespace Usb
{
	public class ControlUrb : Urb
	{
		public const byte GET_STATUS_REQ    = 0; // _REQ was appended because GET_STATUS
		                                         // would not be CLS compliant because the
		                                         // property accessor get_Status exists
		                                         // (same name, different casing)
		public const byte CLEAR_FEATURE     = 1;
		public const byte SET_FEATURE       = 3;
		public const byte SET_ADDRESS       = 5;
		public const byte GET_DESCRIPTOR    = 6;
		public const byte SET_DESCRIPTOR    = 7;
		public const byte GET_CONFIGURATION = 8;
		public const byte SET_CONFIGURATION = 9;
		public const byte GET_INTERFACE     = 10;
		public const byte SET_INTERFACE     = 11;
		public const byte SYNCH_FRAME       = 12;

		private byte bmRequestType;
		private byte bRequest;
		private short wValue;
		private short wIndex;
		private short wLength;

		public ControlUrb(long handle,
		                  byte[] buffer,
		                  int buffer_actual,
		                  UrbStatus status,
		                  UrbFlags flags,
		                  byte epadr,
		                  ControlEndpoint ep,
		                  byte bmRequestType,
		                  byte bRequest,
		                  short wValue,
		                  short wIndex,
		                  short wLength) :
			base(handle, buffer,
			     buffer_actual, status, flags, epadr, ep)
		{
			this.bmRequestType = bmRequestType;
			this.bRequest = bRequest;
			this.wValue = wValue;
			this.wIndex = wIndex;
			this.wLength = wLength;
			if(buffer == null)
			{
				if(wLength != 0)
					throw new InvalidOperationException();
			}
			else if(buffer.Length < wLength)
				throw new InvalidOperationException();
		}
		
		public byte SetupPacketRequestType
		{
			get { return bmRequestType; }
		}

		public byte SetupPacketRequest
		{
			get { return bRequest; }
		}

		public short SetupPacketValue
		{
			get { return wValue; }
		}

		public short SetupPacketIndex
		{
			get { return wIndex; }
		}

		public short SetupPacketLength
		{
			get { return wLength; }
		}

		public override sealed EndpointType Type
		{
			get { return EndpointType.Control; }
		}
		
		public override EndpointDirection Direction
		{
			get
			{
				return ((bmRequestType & 0x80) != 0x00) ?
					EndpointDirection.In : EndpointDirection.Out;
			}
		}
		
		public SetupType SetupType
		{
			get
			{
				switch(bmRequestType & 0x60)
				{
				case 0x00: return SetupType.Standard;
				case 0x20: return SetupType.Class;
				case 0x40: return SetupType.Vendor;
				default:   return SetupType.Reserved;
				}
			}
		}

		public SetupRecipient SetupRecipient
		{
			get
			{
				switch(bmRequestType & 0x1f)
				{
				case 0x00: return SetupRecipient.Device;
				case 0x01: return SetupRecipient.Interface;
				case 0x02: return SetupRecipient.Endpoint;
				case 0x03: return SetupRecipient.Other;
				default:   return SetupRecipient.Reserved;
				}
			}
		}

		public bool IsSetupTypeStandard
		{
			get { return SetupType == SetupType.Standard; }
		}

		public bool IsSetupTypeClass
		{
			get { return SetupType == SetupType.Class; }
		}

		public bool IsSetupTypeVendor
		{
			get { return SetupType == SetupType.Vendor; }
		}

		public bool IsSetupRecipientDevice
		{
			get { return SetupRecipient == SetupRecipient.Device; }
		}

		public bool IsSetupRecipientInterface
		{
			get { return SetupRecipient == SetupRecipient.Interface; }
		}

		public bool IsSetupRecipientEndpoint
		{
			get { return SetupRecipient == SetupRecipient.Endpoint; }
		}

		public bool IsSetupRecipientOther
		{
			get { return SetupRecipient == SetupRecipient.Other; }
		}

		private static readonly string[] sr = new string[13]
		{
			"GET_STATUS",
			"CLEAR_FEATURE",
			"reserved",
			"SET_FEATURE",
			"reserved",
			"SET_ADDRESS",
			"GET_DESCRIPTOR",
			"SET_DESCRIPTOR",
			"GET_CONFIGURATION",
			"SET_CONFIGURATION",
			"GET_INTERFACE",
			"SET_INTERFACE",
			"SYNCH_FRAME"
		};
		private static readonly string[] sd = new string[9]
		{
			"invalid",
			"DEVICE",
			"CONFIGURATION",
			"STRING",
			"INTERFACE",
			"ENDPOINT",
			"DEVICE_QUALIFIER",
			"OTHER_SPEED_CONFIGURATION",
			"INTERFACE_POWER"
		};
		private static readonly string[] sf = new string[3]
		{
			"ENDPOINT_HALT",
			"DEVICE_REMOTE_WAKEUP",
			"TEST_MODE"
		};

		protected override void DumpProperties(System.Text.StringBuilder s)
		{
			base.DumpProperties(s);
			s.AppendFormat("bRequestType=0x{0:x2}({1},{2},{3}) ",
			               bmRequestType,
			               ((bmRequestType & 0x80) != 0) ? "IN" : "OUT",
			               IsSetupTypeStandard ? "STD" :
			               (IsSetupTypeClass ? "CLS" :
			                (IsSetupTypeVendor ? "VND" : "???")),
			               IsSetupRecipientDevice ? "DV" :
			               (IsSetupRecipientInterface ? "IF" :
			                (IsSetupRecipientEndpoint ? "EP" :
			                 (IsSetupRecipientOther ? "OT" : "??"))));
			s.AppendFormat("bRequest=0x{0:x2}({1})\n",
			               bRequest,
			               (IsSetupTypeStandard && bRequest < 13) ?
			               sr[bRequest] : "???");
			s.AppendFormat("wValue=0x{0:x4}", wValue);
			if(IsSetupTypeStandard)
			{
				if(bRequest == CLEAR_FEATURE || bRequest == SET_FEATURE)
					s.AppendFormat("({0})", (wValue < 3) ? sf[wValue] : "???");
				else if(bRequest == GET_DESCRIPTOR ||
				        bRequest == SET_DESCRIPTOR)
				{
					int high = unchecked((int)(ushort)wValue >> 8);
					s.AppendFormat("({0})", (high < 9) ? sd[high] : "???");
				}
			}
			s.AppendFormat(" wIndex=0x{0:x4} wLength=0x{1:x4}\n",
			               wIndex, wLength);
		}
	}
}
