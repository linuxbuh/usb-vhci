/*
 * StatefullEnumerator_RootHub.cs -- USB device enumeration interfaces
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

namespace Usb.Enumeration
{
	partial class StatefullEnumerator
	{
		protected class RootHub : Hub
		{
			public RootHub(StatefullEnumerator enm, IHub inner) :
				base(enm, null, inner)
			{
			}

			public override bool IsRoot
			{
				get { return true; }
			}

			public override IHub Root
			{
				get { return this; }
			}
		}
	}
}
