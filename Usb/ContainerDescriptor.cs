/*
 * ContainerDescriptor.cs -- USB related classes
 *
 * Copyright (C) 2009-2010 Michael Singer <michael@a-singer.de>
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
using System.Runtime.InteropServices;

namespace Usb
{
	public abstract class ContainerDescriptor : Descriptor
	{
		protected ContainerDescriptor() : base()
		{
		}

		protected void WalkChildDescriptors(byte[] desc,
		                                    ref int pos, // points behind the
		                                                 // end of the
		                                                 // descriptor (where
		                                                 // the first child
		                                                 // descriptor may
		                                                 // begin)
		                                    Endianness endian,
		                                    object context)
		{
			if(desc == null) throw new ArgumentNullException("desc");
			int al = desc.Length;
			if(pos < 0 || pos > al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			while(pos < al)
			{
				int dl = desc[pos];
				if(dl < 2)
					throw new ArgumentException("desc[" + pos + "] is less " +
					                            "than 2",
					                            "desc");
				if(pos + dl > al)
					throw new ArgumentException();
				byte dt = desc[pos + 1];
				if(dt == 0)
					throw new ArgumentException("desc[" + (pos + 1) +" ] is 0",
					                            "desc");
				int pos_ = pos;
				if(!ParseChildDescriptor(desc, ref pos_, endian, context))
					break;
				if(pos_ < pos + dl)
					throw new InvalidOperationException("ParseChildDescriptor" +
					                                    " failed");
				pos = pos_;
			}
		}

		protected void WalkChildDescriptors(byte[] desc,
		                                    ref int pos,
		                                    Endianness endian)
		{
			WalkChildDescriptors(desc, ref pos, endian, null);
		}

		protected void WalkChildDescriptors(byte[] desc,
		                                    ref int pos,
		                                    object context)
		{
			WalkChildDescriptors(desc, ref pos, Endianness.UsbSpec, context);
		}

		protected void WalkChildDescriptors(byte[] desc,
		                                    ref int pos)
		{
			WalkChildDescriptors(desc, ref pos, null);
		}

		protected void WalkChildDescriptors(int count,
		                                    byte[] desc,
		                                    ref int pos, // points behind the
		                                                 // end of the
		                                                 // descriptor (where
		                                                 // the first child
		                                                 // descriptor may
		                                                 // begin)
		                                    Endianness endian,
		                                    object context)
		{
			if(desc == null) throw new ArgumentNullException("desc");
			int al = desc.Length;
			if(pos < 0 || pos > al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			for(int i = 0; i < count; i++)
			{
				int dl = desc[pos];
				if(dl < 2)
					throw new ArgumentException("desc[" + pos + "] is less " +
					                            "than 2",
					                            "desc");
				if(pos + dl > al)
					throw new ArgumentException();
				byte dt = desc[pos + 1];
				if(dt == 0)
					throw new ArgumentException("desc[" + (pos + 1) +" ] is 0",
					                            "desc");
				int pos_ = pos;
				if(!ParseChildDescriptor(desc, ref pos_, endian, context))
					throw new ArgumentException("There are less child " +
					                            "descriptors in desc than " +
					                            "count says.");
				if(pos_ < pos + dl)
					throw new InvalidOperationException("ParseChildDescriptor" +
					                                    " failed");
				pos = pos_;
			}
		}

		protected void WalkChildDescriptors(int count,
		                                    byte[] desc,
		                                    ref int pos,
		                                    Endianness endian)
		{
			WalkChildDescriptors(count, desc, ref pos, endian, null);
		}

		protected void WalkChildDescriptors(int count,
		                                    byte[] desc,
		                                    ref int pos,
		                                    object context)
		{
			WalkChildDescriptors(count,
			                     desc,
			                     ref pos,
			                     Endianness.UsbSpec,
			                     context);
		}

		protected void WalkChildDescriptors(int count,
		                                    byte[] desc,
		                                    ref int pos)
		{
			WalkChildDescriptors(count, desc, ref pos, null);
		}

		protected virtual bool ParseChildDescriptor(byte[] desc,
		                                            ref int pos,
		                                            Endianness endian,
		                                            object context)
		{
			return false;
		}
	}
}
