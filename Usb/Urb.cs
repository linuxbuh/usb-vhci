/*
 * Urb.cs -- USB related classes
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

namespace Usb
{
	public abstract class Urb
	{
		private long handle;
		private byte[] buffer;
		private int buffer_length;
		private int buffer_actual;
		private UrbStatus status;
		private UrbFlags flags;
		private byte epadr;
		private Endpoint ep;

		public abstract EndpointType Type { get; }

		protected Urb(long handle,
		              byte[] buffer,
		              int buffer_actual,
		              UrbStatus status,
		              UrbFlags flags,
		              byte epadr,
		              Endpoint ep)
		{
			if(ep == null) throw new ArgumentNullException("ep");
			if(!ep.IsUsingAddress(epadr))
				throw new ArgumentException("ep is not using epadr", "epadr");
			this.handle = handle;
			this.buffer = buffer;
			if(buffer != null) buffer_length = buffer.Length;
			this.buffer_actual = buffer_actual;
			this.status = status;
			this.flags = flags;
			this.epadr = epadr;
			this.ep = ep;
		}

		public long Handle
		{
			get { return handle; }
		}

		public Endpoint Endpoint
		{
			get { return ep; }
		}

		public byte EndpointAddress
		{
			get { return epadr; }
		}

		public virtual EndpointDirection Direction
		{
			get
			{
				return ((epadr & 0x80) != 0x00) ?
					EndpointDirection.In : EndpointDirection.Out;
			}
		}

		public bool IsIn
		{
			get { return Direction == EndpointDirection.In; }
		}

		public bool IsOut
		{
			get { return Direction == EndpointDirection.Out; }
		}

		public UrbStatus Status
		{
			get { return status; }
			set { status = value; }
		}

		public void Ack()
		{
			status = UrbStatus.Success;
		}

		public void Stall()
		{
			status = UrbStatus.Stall;
		}

		public UrbFlags Flags
		{
			get { return flags; }
		}

		public int BufferLength
		{
			get { return buffer_length; }
		}

		public int BufferActual
		{
			get { return buffer_actual; }
			set { buffer_actual = value; }
		}

		public byte[] TransferBuffer
		{
			get { return buffer; }
		}

		public bool IsShortNotOk
		{
			get { return (flags & UrbFlags.ShortNotOk) != 0; }
		}

		// Gets always set for isochronous urbs for now
		/*
		public bool IsIsoAsap
		{
			get { return (flags & UrbFlags.IsoAsap) != 0; }
		}
		*/

		public bool IsZeroPacket
		{
			get { return (flags & UrbFlags.ZeroPacket) != 0; }
		}

		public static string GetStatusString(UrbStatus status)
		{
			return Enum.GetName(typeof(UrbStatus), status);
		}

		protected virtual void DumpProperties(System.Text.StringBuilder s)
		{
			s.AppendFormat("handle=0x{0:x16}\n", handle);
			if(ep == null) s.AppendLine("EP IS NULL!");
			else
				s.AppendFormat("epnum={0} epdir={1} eptpe={2}\n",
				               ep.Number.ToString(),
				               (((epadr & 0x80) != 0x00) ? "IN" : "OUT"),
				               ((Type == EndpointType.Control) ? "CTRL" :
				                ((Type == EndpointType.Bulk) ? "BULK" :
				                 ((Type == EndpointType.Interrupt) ? "INT" :
				                  "ISO"))));
			s.AppendFormat("status={0}({1}) flags=0x{2:x8} buflen={3}/{4}\n",
			               ((int)status).ToString(),
			               GetStatusString(status),
			               ((int)flags).ToString(),
			               buffer_actual.ToString(),
			               buffer_length).ToString();
			if(buffer == null)
				s.AppendLine("buffer is null");
			else
				s.AppendLine("buffer is an instance of byte[]");
		}

		protected virtual void DumpBuffer(System.Text.StringBuilder s,
		                                  bool full)
		{
			bool isin = IsIn;
			int max = this is ControlUrb ?
				((ControlUrb)this).SetupPacketLength : buffer_length;
			s.AppendFormat("data stage ({0}/{1} bytes {2}):\n",
			               buffer_actual.ToString(),
			               max.ToString(),
			               isin ? "received" : "transmitted");
			if(isin) max = buffer_actual;
			if(full || max <= 16)
				for(int i = 0; i < max; i++)
					s.AppendFormat("{0:x2} ", buffer[i]);
			else
			{
				for(int i = 0; i < 8; i++)
					s.AppendFormat("{0:x2} ", buffer[i]);
				s.Append("... ");
				for(int i = max - 8; i < max; i++)
					s.AppendFormat("{0:x2} ", buffer[i]);
			}
			s.AppendLine();
		}

		public void Dump(System.Text.StringBuilder s, bool buffer, bool full)
		{
			DumpProperties(s);
			if(buffer) DumpBuffer(s, full);
		}

		public void Dump(System.Text.StringBuilder s)
		{
			DumpProperties(s);
			DumpBuffer(s, true);
		}
	}
}
