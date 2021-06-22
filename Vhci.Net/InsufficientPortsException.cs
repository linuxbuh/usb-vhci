/*
 * InsufficientPortsException.cs -- VHCI network communication classes
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
using System.Runtime.Serialization;

namespace Vhci.Net
{
	[Serializable]
	public class InsufficientPortsException : Exception
	{
		public InsufficientPortsException() :
			base("The remote host controller has not enough free ports left.")
		{
		}

		public InsufficientPortsException(string message) : base(message)
		{
		}

		public InsufficientPortsException(string message, Exception inner) :
			base(message, inner)
		{
		}

		protected InsufficientPortsException(SerializationInfo info,
		                                     StreamingContext context) :
			base(info, context)
		{
		}
	}
}
