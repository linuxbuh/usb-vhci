/*
 * CommandStatusWrapper.cs -- USB mass storage related classes
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

namespace Usb.Class.MassStorage
{
	public struct CommandStatusWrapper
	{
		public const int CommandStatusWrapperSignature = 0x53425355;

		private int signature;
		private int tag;
		private int dataResidue;
		private CommandBlockStatus status; 

		public CommandStatusWrapper(int tag,
		                            int dataResidue,
		                            CommandBlockStatus status)
		{
			signature = CommandStatusWrapperSignature;
			this.tag = tag;
			this.dataResidue = dataResidue;
			this.status = status;
		}

		public CommandStatusWrapper(byte[] arr, ref int pos)
		{
			if(arr == null) throw new ArgumentNullException("arr");
			signature   = (int)arr[pos++]       |
			              (int)arr[pos++] << 8  |
			              (int)arr[pos++] << 16 |
			              (int)arr[pos++] << 24;
			tag         = (int)arr[pos++]       |
			              (int)arr[pos++] << 8  |
			              (int)arr[pos++] << 16 |
			              (int)arr[pos++] << 24;
			dataResidue = (int)arr[pos++]       |
			              (int)arr[pos++] << 8  |
			              (int)arr[pos++] << 16 |
			              (int)arr[pos++] << 24;
			status = (CommandBlockStatus)arr[pos++];
		}

		public int ToBinary(byte[] arr, int index)
		{
			if(arr == null) return 13;
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
				arr[p++] = (byte)dataResidue;
				arr[p++] = (byte)(dataResidue >> 8);
				arr[p++] = (byte)(dataResidue >> 16);
				arr[p++] = (byte)(dataResidue >> 24);
				arr[p++] = (byte)(uint)status;
			}
			return 13;
		}

		public bool Validate()
		{
			if(signature != CommandStatusWrapperSignature) return false;
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

		public int DataResidue
		{
			get { return dataResidue; }
			set { dataResidue = value; }
		}

		public CommandBlockStatus Status
		{
			get { return status; }
			set { status = value; }
		}
	}
}
