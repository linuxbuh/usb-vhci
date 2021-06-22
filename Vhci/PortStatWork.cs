/*
 * PortStatWork.cs -- VHCI related classes
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
	public class PortStatWork : Work
	{
		PortStat stat;
		PortStatWorkTriggerFlags triggerFlags;

		public PortStatWork(byte port, PortStat stat) : base(port)
		{
			this.stat = stat;
			this.triggerFlags = PortStatWorkTriggerFlags.None;
		}

		public PortStatWork(byte port, PortStat stat, PortStat prev) : this(port, stat)
		{
			if(!stat.Enable && prev.Enable)     triggerFlags |= PortStatWorkTriggerFlags.Disable;
			if(stat.Suspend && !prev.Suspend)   triggerFlags |= PortStatWorkTriggerFlags.Suspend;
			if(stat.Resuming && !prev.Resuming) triggerFlags |= PortStatWorkTriggerFlags.Resuming;
			if(stat.Reset/* && !prev.Reset*/)   triggerFlags |= PortStatWorkTriggerFlags.Reset;
			if(stat.Power && !prev.Power)       triggerFlags |= PortStatWorkTriggerFlags.PowerOn;
			else if(!stat.Power && prev.Power)  triggerFlags |= PortStatWorkTriggerFlags.PowerOff;
		}

		public PortStat PortStat
		{
			get { return stat; }
		}

		public PortStatWorkTriggerFlags TriggerFlags
		{
			get { return triggerFlags; }
		}

		public bool TriggersDisable
		{
			get { return (triggerFlags & PortStatWorkTriggerFlags.Disable) != 0; }
		}

		public bool TriggersSuspend
		{
			get { return (triggerFlags & PortStatWorkTriggerFlags.Suspend) != 0; }
		}

		public bool TriggersResuming
		{
			get { return (triggerFlags & PortStatWorkTriggerFlags.Resuming) != 0; }
		}

		public bool TriggersReset
		{
			get { return (triggerFlags & PortStatWorkTriggerFlags.Reset) != 0; }
		}

		public bool TriggersPowerOn
		{
			get { return (triggerFlags & PortStatWorkTriggerFlags.PowerOn) != 0; }
		}

		public bool TriggersPowerOff
		{
			get { return (triggerFlags & PortStatWorkTriggerFlags.PowerOff) != 0; }
		}
	}
}
