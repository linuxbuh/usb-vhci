/*
 * IsochronousUrb.cs -- USB related classes
 *
 * Copyright (C) 2007-2008 Conemis AG Karlsruhe Germany
 * Copyright (C) 2007-2009 Michael Singer <michael@a-singer.de>
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
	public class IsochronousUrb : Urb
	{
		private int interval;
		private int error_count;
		private int packet_count;
		private IsochronousPacket[] iso_packets;

		public IsochronousUrb(long handle,
		                      byte[] buffer,
		                      int buffer_actual,
		                      UrbStatus status,
		                      UrbFlags flags,
		                      byte epadr,
		                      IsochronousEndpoint ep,
		                      int interval,
		                      int error_count,
		                      IsochronousPacket[] iso_packets) :
			base(handle, buffer,
			     buffer_actual, status, flags, epadr, ep)
		{
			this.interval = interval;
			this.error_count = error_count;
			this.iso_packets = iso_packets;
			if(iso_packets != null) packet_count = iso_packets.Length;
		}

		public override sealed EndpointType Type
		{
			get { return EndpointType.Isochronous; }
		}

		public int Interval
		{
			get { return interval; }
		}

		public int ErrorCount
		{
			get { return error_count; }
			set { error_count = value; }
		}

		public int PacketCount
		{
			get { return packet_count; }
		}

		public IsochronousPacket[] IsoPackets
		{
			get { return iso_packets; }
		}

		protected override void DumpProperties(System.Text.StringBuilder s)
		{
			base.DumpProperties(s);
			s.AppendFormat("interval={0} err={1} packets={2}\n",
			               interval.ToString(),
			               error_count.ToString(),
			               packet_count.ToString());
			for(int j = 0; j < packet_count; j++)
			{
				s.AppendFormat
					("PACKET{0}: offset={1} pktlen={2}/{3} status={4}({5})\n",
					 j.ToString(),
					 iso_packets[j].Offset.ToString(),
					 iso_packets[j].PacketActual.ToString(),
					 iso_packets[j].PacketLength.ToString(),
					 ((int)iso_packets[j].Status).ToString(),
					 Urb.GetStatusString(iso_packets[j].Status));
			}
		}

		protected override void DumpBuffer(System.Text.StringBuilder s,
		                                   bool full)
		{
			bool isin = IsIn;
			byte[] buffer = TransferBuffer;
			string dir = isin ? "received" : "transmitted";
			for(int j = 0; j < packet_count; j++)
			{
				int max = iso_packets[j].PacketLength;
				int act = iso_packets[j].PacketActual;
				s.AppendFormat("PACKET{0}: data stage ({1}/{2} bytes {3}):\n",
				               j.ToString(),
				               act.ToString(), max.ToString(),
				               dir);
				if(isin) max = act;
				int offset = iso_packets[j].Offset;
				if(full || max <= 16)
					for(int i = offset; i < max + offset; i++)
						s.AppendFormat("{0:x2} ", buffer[i]);
				else
				{
					for(int i = offset; i < 8 + offset; i++)
						s.AppendFormat("{0:x2} ", buffer[i]);
					s.Append("... ");
					for(int i = max + offset - 8; i < max + offset; i++)
						s.AppendFormat("{0:x2} ", buffer[i]);
				}
				s.AppendLine();
			}
		}
	}
}
