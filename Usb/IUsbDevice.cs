/*
 * IUsbDevice.cs -- USB related classes
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
	public interface IUsbDevice : IDisposable
	{
		DeviceDescriptor Device { get; }
		DataRate DataRate { get; }
		DeviceState State { get; }
		byte Address { get; }
		void ForgetAsyncUrb(Urb urb);
		void CancelAsyncUrb(Urb urb);
		void AsyncSubmitUrb(Urb urb, System.Threading.EventWaitHandle ewh);
		Urb ReapAnyAsyncUrb(int millisecondsTimeout);
		Urb ReapAnyAsyncUrb();
		Urb ReapAnyAsyncUrb(TimeSpan timeout);
		bool ReapAsyncUrb(Urb urb);
		void ProcControlUrb(ControlUrb urb);
		void ProcBulkUrb(BulkUrb urb);
	}
}
