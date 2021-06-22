/*
 * CommandBlockWrapper.cs -- USB mass storage related classes
 *
 * Copyright (C) 2009 Conemis AG Karlsruhe Germany
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
using System.Runtime.InteropServices;

namespace Usb.Class.MassStorage
{
	public struct CommandBlockWrapper
	{
		public const int CommandBlockWrapperSignature = 0x43425355;

		private int signature;
		private int tag;
		private int dataTransferLength;
		private CommandBlockWrapperFlags flags;
		private byte lun;
		private byte commandBlockLength;

		public CommandBlockWrapper(int tag,
		                           int dataTransferLength,
		                           DataTransferDirection dir,
		                           byte lun,
		                           byte commandBlockLength)
		{
			if((lun & ~0x0f) != 0)
				throw new ArgumentOutOfRangeException("lun", lun, "");
			if(commandBlockLength == 0 || commandBlockLength > 16)
				throw new ArgumentOutOfRangeException("commandBlockLength",
				                                      commandBlockLength, "");
			signature = CommandBlockWrapperSignature;
			this.tag = tag;
			this.dataTransferLength = dataTransferLength;
			flags = (dir == DataTransferDirection.In) ?
			        CommandBlockWrapperFlags.Direction :
			        CommandBlockWrapperFlags.None;
			this.lun = lun;
			this.commandBlockLength = commandBlockLength;
		}

		public CommandBlockWrapper(byte[] arr, ref int pos)
		{
			if(arr == null) throw new ArgumentNullException("arr");
			signature          = (int)arr[pos++]       |
			                     (int)arr[pos++] << 8  |
			                     (int)arr[pos++] << 16 |
			                     (int)arr[pos++] << 24;
			tag                = (int)arr[pos++]       |
			                     (int)arr[pos++] << 8  |
			                     (int)arr[pos++] << 16 |
			                     (int)arr[pos++] << 24;
			dataTransferLength = (int)arr[pos++]       |
			                     (int)arr[pos++] << 8  |
			                     (int)arr[pos++] << 16 |
			                     (int)arr[pos++] << 24;
			flags = (CommandBlockWrapperFlags)arr[pos++];
			lun = (byte)(arr[pos++] & 0x0f);
			commandBlockLength = (byte)(arr[pos++] & 0x1f);
		}

		public int ToBinary(byte[] arr, int index)
		{
			if(arr == null) return 15;
			int p = index;
			unchecked
			{
				arr[p++] = (byte)signature;
				arr[p++] = (byte)(signature >> 8);
				arr[p++] = (byte)(signature >> 16);
				arr[p++] = (byte)(signature >> 24);
				arr[p++] = (byte)tag;
				arr[p++] = (byte)(tag >> 8);
				arr[p++] = (byte)(tag >> 16);
				arr[p++] = (byte)(tag >> 24);
				arr[p++] = (byte)dataTransferLength;
				arr[p++] = (byte)(dataTransferLength >> 8);
				arr[p++] = (byte)(dataTransferLength >> 16);
				arr[p++] = (byte)(dataTransferLength >> 24);
				arr[p++] = (byte)(uint)flags;
			}
			arr[p++] = (byte)(lun & 0x0f);
			arr[p++] = (byte)(commandBlockLength & 0x1f);
			return 15;
		}

		public bool Validate()
		{
			if(signature != CommandBlockWrapperSignature) return false;
			if(commandBlockLength == 0 || commandBlockLength > 16) return false;
			return true;
		}

		public int Signature
		{
			get { return signature; }
			set { signature = value; }
		}

		public int Tag
		{
			get { return tag; }
			set { tag = value; }
		}

		public int DataTransferLength
		{
			get { return dataTransferLength; }
			set { dataTransferLength = value; }
		}

		public CommandBlockWrapperFlags Flags
		{
			get { return flags; }
			set { flags = value; }
		}

		public DataTransferDirection DataTransferDirection
		{
			get
			{
				return ((flags & CommandBlockWrapperFlags.Direction) != 0) ?
				       DataTransferDirection.In :
				       DataTransferDirection.Out;
			}
			set
			{
				if(value == DataTransferDirection.In)
					flags |= CommandBlockWrapperFlags.Direction;
				else
					flags &= ~CommandBlockWrapperFlags.Direction;
			}
		}

		public byte Lun
		{
			get { return lun; }
			set
			{
				if((value & ~0x0f) != 0)
					throw new ArgumentOutOfRangeException("value", value, "");
				lun = value;
			}
		}

		public byte CommandBlockLength
		{
			get { return commandBlockLength; }
			set
			{
				if(value == 0 || value > 16)
					throw new ArgumentOutOfRangeException("value", value, "");
				commandBlockLength = value;
			}
		}
	}
}
