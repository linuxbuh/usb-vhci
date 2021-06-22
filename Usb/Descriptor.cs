/*
 * Descriptor.cs -- USB related classes
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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Usb
{
	public abstract class Descriptor : IValidable
	{
		public const byte DeviceDescriptorType                  = 1;
		public const byte ConfigurationDescriptorType           = 2;
		public const byte StringDescriptorType                  = 3;
		public const byte InterfaceDescriptorType               = 4;
		public const byte EndpointDescriptorType                = 5;
		public const byte DeviceQualifierDescriptorType         = 6;
		public const byte OtherSpeedConfigurationDescriptorType = 7;
		public const byte InterfacePowerDescriptorType          = 8;

		public const int DeviceDescriptorLength                  = 18;
		public const int ConfigurationDescriptorLength           = 9;
		public const int InterfaceDescriptorLength               = 9;
		public const int EndpointDescriptorLength                = 7;
		public const int DeviceQualifierDescriptorLength         = 10;
		public const int OtherSpeedConfigurationDescriptorLength =
			ConfigurationDescriptorLength;

		protected Descriptor()
		{
		}

		public abstract bool Validate(ValidationMode mode);
		public abstract int GetDescriptor([In, Out] byte[] desc, int index);
		public abstract void Dump(System.IO.TextWriter stm, string prefix);

		public bool Validate()
		{
			return Validate(ValidationMode.Strict);
		}

		public int Size
		{
			get { return GetDescriptor(null); }
		}

		public int GetDescriptor([In, Out] byte[] desc)
		{
			return GetDescriptor(desc, 0);
		}

		public byte[] GetDescriptor()
		{
			byte[] desc = new byte[Size];
			GetDescriptor(desc);
			return desc;
		}

		public static void HexDump(IList<byte> data,
		                           System.IO.TextWriter stm,
		                           string prefix)
		{
			if(data == null) throw new ArgumentNullException("data");
			if(stm == null) throw new ArgumentNullException("stm");
			if(prefix == null) throw new ArgumentNullException("prefix");
			int c = data.Count;
			int l = c / 16;
			int r = c % 16;
			char[] a = new char[17];
			a[16] = '|';
			for(int i = 0; i < l; i++)
			{
				int adr = i * 16;
				stm.Write(prefix);
				stm.Write(adr.ToString("x8"));
				stm.Write(": ");
				for(int j = 0; j < 8; j++)
				{
					byte bc = data[adr + j];
					stm.Write(bc.ToString("x2"));
					if(bc >= 32 && bc < 128)
						a[j] = (char)bc;
					else
						a[j] = '.';
					stm.Write(" ");
				}
				stm.Write(" ");
				for(int j = 0; j < 8; j++)
				{
					byte bc = data[adr + j + 8];
					stm.Write(bc.ToString("x2"));
					if(bc >= 32 && bc < 128)
						a[j + 8] = (char)bc;
					else
						a[j + 8] = '.';
					stm.Write(" ");
				}
				stm.Write(" |");
				stm.WriteLine(new string(a));
			}
			if(r > 0)
			{
				int adr = l * 16;
				stm.Write(prefix);
				stm.Write(adr.ToString("x8"));
				stm.Write(": ");
				for(int j = 0; j < 8; j++)
				{
					if(r-- > 0)
					{
						byte bc = data[adr + j];
						stm.Write(bc.ToString("x2"));
						if(bc >= 32 && bc < 128)
							a[j] = (char)bc;
						else
							a[j] = '.';
						stm.Write(" ");
					}
					else
					{
						a[j] = ' ';
						stm.Write("   ");
					}
				}
				stm.Write(" ");
				for(int j = 0; j < 8; j++)
				{
					if(r-- > 0)
					{
						byte bc = data[adr + j + 8];
						stm.Write(bc.ToString("x2"));
						if(bc >= 32 && bc < 128)
							a[j + 8] = (char)bc;
						else
							a[j + 8] = '.';
						stm.Write(" ");
					}
					else
					{
						a[j + 8] = ' ';
						stm.Write("   ");
					}
				}
				stm.Write(" |");
				stm.WriteLine(new string(a));
			}
		}

		public static bool IsCustomType(byte type)
		{
			return type > 8;
		}
	}
}
