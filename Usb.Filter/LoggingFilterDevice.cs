/*
 * LoggingFilterDevice.cs -- USB device filter classes
 *
 * Copyright (C) 2010 Michael Singer <michael@a-singer.de>
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

namespace Usb.Filter
{
	public class LoggingFilterDevice : FilterDeviceBase
	{
		private readonly System.IO.TextWriter log;

		public LoggingFilterDevice(System.IO.TextWriter log) : this(null, log)
		{
		}

		public LoggingFilterDevice(Usb.IUsbDevice inner,
		                           System.IO.TextWriter log) : base(inner)
		{
			if(log == null) throw new ArgumentNullException("log");
			this.log = log;
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing)
			{
				log.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
