/*
 * UnidirectionalEndpoint.cs -- USB related classes
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
using System.Runtime.InteropServices;

namespace Usb
{
	public abstract class UnidirectionalEndpoint : Endpoint
	{
		private EndpointDirection dir;

		protected override void Init(byte[] desc, ref int pos)
		{
			int p = pos;
			base.Init(desc, ref pos);
			dir = ((desc[p + 2] & 0x80) != 0x00) ?
				EndpointDirection.In : EndpointDirection.Out;
		}

		protected UnidirectionalEndpoint(byte[] desc, ref int pos) :
			base(desc, ref pos)
		{
		}

		protected UnidirectionalEndpoint(byte[] desc) : base(desc)
		{
			if(desc == null) throw new ArgumentNullException("desc");
		}

		[CLSCompliant(false)]
		protected UnidirectionalEndpoint(
			EndpointDirection dir,
			ushort maxPacketSize,
			byte interval) : base(maxPacketSize, interval)
		{
			this.dir = dir;
		}

		protected UnidirectionalEndpoint(
			EndpointDirection dir,
			short maxPacketSize,
			byte interval) : base((ushort)maxPacketSize, interval)
		{
			this.dir = dir;
		}
		
		protected UnidirectionalEndpoint(
			EndpointDirection dir) : base()
		{
			this.dir = dir;
		}
		
		public EndpointDirection Direction
		{
			get { return dir; }
		}
		
		public bool IsIn
		{
			get { return dir == EndpointDirection.In; }
		}
		
		public bool IsOut
		{
			get { return dir == EndpointDirection.Out; }
		}
		
		public override sealed byte BaseAddress
		{
			get
			{
				if(Parent == null)
					throw new InvalidOperationException("Parent == null");
				return Parent.GetBaseAddressOfEndpoint(this);
			}
		}
		
		public override sealed bool IsUsingAddress(byte epadr)
		{
			return BaseAddress == epadr;
		}

		protected override sealed bool IsAddressValid(byte epadr)
		{
			return base.IsAddressValid(epadr) &&
				ValidateAddressDirection(epadr, dir);
		}

		private static bool ValidateAddressDirection(
			byte epadr, EndpointDirection dir)
		{
			return
				((dir == EndpointDirection.In) && ((epadr & 0x80) != 0x00)) ||
				((dir == EndpointDirection.Out) && ((epadr & 0x80) == 0x00));
		}

		public static bool ValidateAddress(byte epadr, EndpointDirection dir)
		{
			return Endpoint.ValidateAddress(epadr) &&
				ValidateAddressDirection(epadr, dir);
		}

		protected override void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian)
		{
			base.GetDescriptorContent(desc, index, endian);
			if(Parent == null)
				desc[index + 2] = (dir == EndpointDirection.In) ?
					(byte)0xffU : (byte)0x7fU;
#if DEBUG
			else
			{
				if(System.Diagnostics.Debugger.IsAttached)
				{
					if(IsIn && (desc[index + 2] & 0x80) == 0x00 ||
						IsOut && (desc[index + 2] & 0x80) != 0x00)
					{
						System.Diagnostics.Debug.WriteLine(
							"Direction missmatch!");
						System.Diagnostics.Debugger.Break();
					}
				}
			}
#endif
		}
	}
}
