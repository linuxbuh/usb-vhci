/*
 * InterruptUrb.cs -- USB related classes
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
	public class InterruptUrb : Urb
	{
		private int interval;

		public InterruptUrb(long handle,
		                    byte[] buffer,
		                    int buffer_actual,
		                    UrbStatus status,
		                    UrbFlags flags,
		                    byte epadr,
		                    InterruptEndpoint ep,
		                    int interval) :
			base(handle, buffer,
			     buffer_actual, status, flags, epadr, ep)
		{
			this.interval = interval;
		}

		public override sealed EndpointType Type
		{
			get { return EndpointType.Interrupt; }
		}

		public int Interval
		{
			get { return interval; }
		}

		protected override void DumpProperties(System.Text.StringBuilder s)
		{
			base.DumpProperties(s);
			s.AppendFormat("interval={0}\n", interval.ToString());
		}
	}
}
