/*
 * PortStatChangeFlags.cs -- VHCI related classes
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

namespace Vhci
{
	[Serializable]
	[Flags]
	public enum PortStatChangeFlags
	{
		None        = 0x00000000,
		Connection  = 0x00000001,
		Enable      = 0x00000002,
		Suspend     = 0x00000004,
		Overcurrent = 0x00000008,
		Reset       = 0x00000010
	}
}
