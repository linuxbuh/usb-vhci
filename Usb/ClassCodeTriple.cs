/*
 * ClassCodeTriple.cs -- USB related classes
 *
 * Copyright (C) 2009 Michael Singer <michael@a-singer.de>
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
	// siehe http://www.usb.org/developers/defined_class
	public struct ClassCodeTriple : IEquatable<ClassCodeTriple>, IValidable
	{
		public const byte BaseClassNull                 = 0x00;
		public const byte BaseClassAudio                = 0x01;
		public const byte BaseClassCommunications       = 0x02;
		public const byte BaseClassHumanInterfaceDevice = 0x03;
		public const byte BaseClassPhysical             = 0x05;
		public const byte BaseClassImage                = 0x06;
		public const byte BaseClassPrinter              = 0x07;
		public const byte BaseClassMassStorage          = 0x08;
		public const byte BaseClassHub                  = 0x09;
		public const byte BaseClassCdcData              = 0x0a;
		public const byte BaseClassSmartCard            = 0x0b;
		public const byte BaseClassContentSecurity      = 0x0d;
		public const byte BaseClassVideo                = 0x0e;
		public const byte BaseClassPersonalHealthcare   = 0x0f;
		public const byte BaseClassDiagnosticDevice     = 0xdc;
		public const byte BaseClassWirelessController   = 0xe0;
		public const byte BaseClassMiscellaneous        = 0xef;
		public const byte BaseClassApplicationSpecific  = 0xfe;
		public const byte BaseClassVendorSpecific       = 0xff;

		public const byte HubProtocolFullSpeed           = 0x00;
		public const byte HubProtocolHighSpeedSingleTT   = 0x01;
		public const byte HubProtocolHighSpeedMultipleTT = 0x02;

		public static readonly ClassCodeTriple Null =
			new ClassCodeTriple(BaseClassNull, 0, 0);

		private byte baseClass;
		private byte subClass;
		private byte protocol;

		public ClassCodeTriple(byte baseClass, byte subClass, byte protocol)
		{
			this.baseClass = baseClass;
			this.subClass = subClass;
			this.protocol = protocol;
		}

		public byte BaseClass
		{
			get { return baseClass; }
			set { baseClass = value; }
		}

		public byte SubClass
		{
			get { return subClass; }
			set { subClass = value; }
		}

		public byte Protocol
		{
			get { return protocol; }
			set { protocol = value; }
		}

		public bool Validate()
		{
			switch(baseClass)
			{
			case BaseClassNull:
				return subClass == 0 && protocol == 0;
			case BaseClassAudio:
			case BaseClassCommunications:
			case BaseClassHumanInterfaceDevice:
			case BaseClassPhysical:
				return true;
			case BaseClassImage:
				return subClass == 1 && protocol == 1;
			case BaseClassPrinter:
			case BaseClassMassStorage:
				return true;
			case BaseClassHub:
				return subClass == 0 &&
					(protocol == HubProtocolFullSpeed ||
					 protocol == HubProtocolHighSpeedSingleTT ||
					 protocol == HubProtocolHighSpeedMultipleTT);
			case BaseClassCdcData:
			case BaseClassSmartCard:
				return true;
			case BaseClassContentSecurity:
				return subClass == 0 && protocol == 0;
			case BaseClassVideo:
			case BaseClassPersonalHealthcare:
				return true;
			case BaseClassDiagnosticDevice:
				return subClass == 1 && protocol == 1;
			case BaseClassWirelessController:
				return (subClass == 1 || subClass == 2) &&
					(protocol == 1 || protocol == 2 || protocol == 3);
			case BaseClassMiscellaneous:
				return (subClass == 1 || subClass == 2) &&
					(protocol == 1 || protocol == 2) ||
					subClass == 3 && protocol == 1;
			case BaseClassApplicationSpecific:
				return subClass == 1 && protocol == 1 ||
					subClass == 2 && protocol == 0 ||
					subClass == 3 && (protocol == 0 || protocol == 1);
			case BaseClassVendorSpecific:
				return true;
			default:
				return false;
			}
		}

		public bool ValidateForDevice()
		{
			if(!Validate()) return false;
			switch(baseClass)
			{
			case BaseClassNull:
			case BaseClassCommunications:
			case BaseClassHub:
			case BaseClassDiagnosticDevice:
			case BaseClassVendorSpecific:
				return true;
			case BaseClassMiscellaneous:
				return subClass == 1 || subClass == 2;
			default:
				return false;
			}
		}

		public bool ValidateForInterface()
		{
			if(!Validate()) return false;
			return !ValidateForDevice();
		}

		public static string BaseClassString(byte baseClass)
		{
			switch(baseClass)
			{
			case BaseClassAudio:
				return "Audio";
			case BaseClassCommunications:
				return "Communications and CDC Control";
			case BaseClassHumanInterfaceDevice:
				return "HID";
			case BaseClassPhysical:
				return "Physical";
			case BaseClassImage:
				return "Image";
			case BaseClassPrinter:
				return "Printer";
			case BaseClassMassStorage:
				return "Mass Storage";
			case BaseClassHub:
				return "Hub";
			case BaseClassCdcData:
				return "CDC-Data";
			case BaseClassSmartCard:
				return "Smart Card";
			case BaseClassContentSecurity:
				return "Content Security";
			case BaseClassVideo:
				return "Video";
			case BaseClassPersonalHealthcare:
				return "Personal Healthcare";
			case BaseClassDiagnosticDevice:
				return "Diagnostic Device";
			case BaseClassWirelessController:
				return "Wireless Controller";
			case BaseClassMiscellaneous:
				return "Miscellaneous";
			case BaseClassApplicationSpecific:
				return "Application Specific";
			case BaseClassVendorSpecific:
				return "Vendor Specific";
			default:
				return null;
			}
		}

		public static string ProtocolString(byte baseClass,
		                                    byte subClass,
		                                    byte protocol)
		{
			switch(baseClass)
			{
			case BaseClassHub:
				if(subClass == 0)
				{
					switch(protocol)
					{
					case HubProtocolFullSpeed:
						return "Full speed hub";
					case HubProtocolHighSpeedSingleTT:
						return "High speed hub with single TT";
					case HubProtocolHighSpeedMultipleTT:
						return "High speed hub with multiple TTs";
					}
				}
				break;
			case BaseClassWirelessController:
				if(subClass == 1)
				{
					switch(protocol)
					{
					case 1:
						return "Bluetooth Programming Interface";
					case 2:
						return "UWB Radio Control Interface";
					case 3:
						return "Remote NDIS";
					}
				}
				else if(subClass == 2)
				{
					switch(protocol)
					{
					case 1:
						return "Host Wire Adapter Control/Data interface";
					case 2:
						return "Device Wire Adapter Control/Data interface";
					case 3:
						return "Device Wire Adapter Isochronous interface";
					}
				}
				break;
			case BaseClassMiscellaneous:
				switch(subClass)
				{
				case 1:
					if(protocol == 1)
						return "Active Sync device";
					else if(protocol == 2)
						return "Palm Sync";
					break;
				case 2:
					if(protocol == 1)
						return "Interface Association Descriptor";
					else if(protocol == 2)
						return "Wire Adapter Multifunction Peripheral " +
						       "programming interface";
					break;
				case 3:
					if(protocol == 1)
						return "Cable Based Association Framework";
					break;
				}
				break;
			case BaseClassApplicationSpecific:
				switch(subClass)
				{
				case 1:
					if(protocol == 1)
						return "Device Firmware Upgrade";
					break;
				case 2:
					if(protocol == 0)
						return "IRDA Bridge device";
					break;
				case 3:
					if(protocol == 0)
						return "USB Test and Measurement Device";
					else if(protocol == 1)
						return "USB Test and Measurement Device conforming " +
						       "to the USBTMC USB488 Subclass Specification";
					break;
				}
				break;
			}
			return null;
		}

		public string BaseClassString()
		{
			return BaseClassString(baseClass);
		}

		public string ProtocolString()
		{
			return ProtocolString(baseClass, subClass, protocol);
		}

		public override string ToString()
		{
			return string.Format("[ClassCodeTriple: BaseClass={0:x2}, " +
			                                       "SubClass={1:x2}, " +
			                                       "Protocol={2:x2}]",
			                     baseClass, subClass, protocol);
		}

		public static bool Equals(ClassCodeTriple o1, ClassCodeTriple o2)
		{
			return o1.Equals(o2);
		}

		public bool Equals(ClassCodeTriple o)
		{
			return baseClass == o.baseClass &&
				subClass == o.subClass &&
				protocol == o.protocol;
		}

		public override bool Equals(object o)
		{
			if(o is ClassCodeTriple) return Equals((ClassCodeTriple)o);
			return false;
		}

		public override int GetHashCode()
		{
			return baseClass << 16 | subClass << 8 | protocol;
		}

		public static bool operator ==(ClassCodeTriple o1, ClassCodeTriple o2)
		{
			return o1.Equals(o2);
		}

		public static bool operator !=(ClassCodeTriple o1, ClassCodeTriple o2)
		{
			return !o1.Equals(o2);
		}
	}
}
