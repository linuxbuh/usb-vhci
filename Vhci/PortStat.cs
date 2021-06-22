/*
 * PortStat.cs -- VHCI related classes
 *
 * Copyright (C) 2007-2008 Conemis AG Karlsruhe Germany
 * Copyright (C) 2007-2016 Michael Singer <michael@a-singer.de>
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
	public struct PortStat
	{
		public static readonly PortStat Zero =
			new PortStat(PortStatStatusFlags.None,
			             PortStatChangeFlags.None,
			             PortStatFlags.None);

		private PortStatStatusFlags status;
		private PortStatChangeFlags change;
		private PortStatFlags flags;

		public PortStat(PortStatStatusFlags status,
		                PortStatChangeFlags change,
		                PortStatFlags flags)
		{
			this.status = status;
			this.change = change;
			this.flags = flags;
		}
		
		public PortStatStatusFlags Status
		{
			get { return status; }
			set { status = value; }
		}

		public PortStatChangeFlags Change
		{
			get { return change; }
			set { change = value; }
		}

		public PortStatFlags Flags
		{
			get { return flags; }
			set { flags = value; }
		}
		
		public bool Resuming
		{
			get { return (flags & PortStatFlags.Resuming) != 0; }
			set
			{
				flags &= ~PortStatFlags.Resuming;
				if(value) flags |= PortStatFlags.Resuming;
			}
		}

		public bool Connection
		{
			get { return (status & PortStatStatusFlags.Connection) != 0; }
			set
			{
				status &= ~PortStatStatusFlags.Connection;
				if(value) status |= PortStatStatusFlags.Connection;
			}
		}

		public bool Enable
		{
			get { return (status & PortStatStatusFlags.Enable) != 0; }
			set
			{
				status &= ~PortStatStatusFlags.Enable;
				if(value) status |= PortStatStatusFlags.Enable;
			}
		}

		public bool Suspend
		{
			get { return (status & PortStatStatusFlags.Suspend) != 0; }
			set
			{
				status &= ~PortStatStatusFlags.Suspend;
				if(value) status |= PortStatStatusFlags.Suspend;
			}
		}

		public bool Overcurrent
		{
			get { return (status & PortStatStatusFlags.Overcurrent) != 0; }
			set
			{
				status &= ~PortStatStatusFlags.Overcurrent;
				if(value) status |= PortStatStatusFlags.Overcurrent;
			}
		}

		public bool Reset
		{
			get { return (status & PortStatStatusFlags.Reset) != 0; }
			set
			{
				status &= ~PortStatStatusFlags.Reset;
				if(value) status |= PortStatStatusFlags.Reset;
			}
		}

		public bool Power
		{
			get { return (status & PortStatStatusFlags.Power) != 0; }
			set
			{
				status &= ~PortStatStatusFlags.Power;
				if(value) status |= PortStatStatusFlags.Power;
			}
		}

		public bool LowSpeed
		{
			get { return (status & PortStatStatusFlags.LowSpeed) != 0; }
			set
			{
				status &= ~PortStatStatusFlags.LowSpeed;
				if(value) status |= PortStatStatusFlags.LowSpeed;
			}
		}

		public bool HighSpeed
		{
			get { return (status & PortStatStatusFlags.HighSpeed) != 0; }
			set
			{
				status &= ~PortStatStatusFlags.HighSpeed;
				if(value) status |= PortStatStatusFlags.HighSpeed;
			}
		}

		public bool ConnectionChanged
		{
			get { return (change & PortStatChangeFlags.Connection) != 0; }
			set
			{
				change &= ~PortStatChangeFlags.Connection;
				if(value) change |= PortStatChangeFlags.Connection;
			}
		}

		public bool EnableChanged
		{
			get { return (change & PortStatChangeFlags.Enable) != 0; }
			set
			{
				change &= ~PortStatChangeFlags.Enable;
				if(value) change |= PortStatChangeFlags.Enable;
			}
		}

		public bool SuspendChanged
		{
			get { return (change & PortStatChangeFlags.Suspend) != 0; }
			set
			{
				change &= ~PortStatChangeFlags.Suspend;
				if(value) change |= PortStatChangeFlags.Suspend;
			}
		}

		public bool OvercurrentChanged
		{
			get { return (change & PortStatChangeFlags.Overcurrent) != 0; }
			set
			{
				change &= ~PortStatChangeFlags.Overcurrent;
				if(value) change |= PortStatChangeFlags.Overcurrent;
			}
		}

		public bool ResetChanged
		{
			get { return (change & PortStatChangeFlags.Reset) != 0; }
			set
			{
				change &= ~PortStatChangeFlags.Reset;
				if(value) change |= PortStatChangeFlags.Reset;
			}
		}

		public override string ToString()
		{
			return "{ 0x" +
				((int)status).ToString("x4") + ", 0x" +
				((int)change).ToString("x4") + ", 0x" +
				((int)flags).ToString("x2") + " }";
		}
	}
}
