/*
 * IsochronousEndpoint.cs -- USB related classes
 *
 * Copyright (C) 2007-2008 Conemis AG Karlsruhe Germany
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
using System.Runtime.InteropServices;

namespace Usb
{
	public class IsochronousEndpoint : UnidirectionalEndpoint
	{
		private SynchronizationType syncType;
		private UsageType usageType;
		private byte additionalTransactions;

		protected override void Init(byte[] desc, ref int pos)
		{
			int p = pos;
			base.Init(desc, ref pos);
			byte d3 = desc[p + 3];
			if((d3 & 0x03) != 0x01)
				throw new ArgumentException("(desc[" + (p + 3) +
				                            "] & 0x03) != 0x01",
				                            "desc");
			switch((d3 >> 2) & 0x03)
			{
			case 0:
				syncType = SynchronizationType.NoSynchronization;
				break;
			case 1:
				syncType = SynchronizationType.Asynchronous;
				break;
			case 2:
				syncType = SynchronizationType.Adaptive;
				break;
			case 3:
				syncType = SynchronizationType.Synchronous;
				break;
			}
			switch((d3 >> 4) & 0x03)
			{
			case 0:
				usageType = UsageType.DataEndpoint;
				break;
			case 1:
				usageType = UsageType.FeedbackEndpoint;
				break;
			case 2:
				usageType = UsageType.ImplicitFeedbackDataEndpoint;
				break;
			case 3:
				usageType = UsageType.Reserved;
				break;
			}
			additionalTransactions = (byte)(uint)((desc[p + 5] >> 3) & 0x03);
		}

		private IsochronousEndpoint(int pos,
		                            out int newPos,
		                            byte[] desc) : base(desc, ref pos)
		{
			newPos = pos;
		}

		private IsochronousEndpoint(IEndpointParent parent,
		                            byte[] desc,
		                            int pos,
		                            out int newPos) :
			this(pos, out newPos, desc)
		{
			byte epadr = desc[pos + 2];
			if(epadr != 0x7f && epadr != 0xff)
			{
				if(parent == null)
					throw new ArgumentException("desc[" + (pos + 2) + "] != " +
					                            "0x7f && desc[" + (pos + 2) +
					                            "] != 0xff && parent == null");
				parent.AddEndpoint(epadr, this);
			}
			else
				Parent = parent;
		}

		public IsochronousEndpoint(IEndpointParent parent,
		                           byte[] desc,
		                           ref int pos) :
			this(parent, desc, pos, out pos)
		{
		}

		public IsochronousEndpoint(byte[] desc,
		                           ref int pos) : this(null, desc, pos, out pos)
		{
		}

		public IsochronousEndpoint(
			IEndpointParent parent,
			byte[] desc) : base(desc)
		{
			byte epadr = desc[2];
			if(epadr != 0x7f && epadr != 0xff)
			{
				if(parent == null)
					throw new ArgumentException("desc[2] != 0x7f && " +
					                            "desc[2] != 0xff && " +
					                            "parent == null");
				parent.AddEndpoint(epadr, this);
			}
			else
				Parent = parent;
		}

		public IsochronousEndpoint(byte[] desc) : this(null, desc)
		{
		}

		public IsochronousEndpoint(
			IEndpointParent parent,
			EndpointDirection dir) : base(dir)
		{
			Parent = parent;
		}

		public IsochronousEndpoint(EndpointDirection dir) : this(null, dir)
		{
		}

		[CLSCompliant(false)]
		public IsochronousEndpoint(
			IEndpointParent parent,
			EndpointDirection dir,
			SynchronizationType syncType,
			UsageType usageType,
			ushort maxPacketSize,
			byte additionalTransactions,
			byte interval) : base(dir, maxPacketSize, interval)
		{
			if(additionalTransactions > 0x03)
				throw new ArgumentOutOfRangeException(
					"additionalTransactions", additionalTransactions, "");
			this.syncType = syncType;
			this.usageType = usageType;
			this.additionalTransactions = additionalTransactions;
			Parent = parent;
		}

		[CLSCompliant(false)]
		public IsochronousEndpoint(
			EndpointDirection dir,
			SynchronizationType syncType,
			UsageType usageType,
			ushort maxPacketSize,
			byte additionalTransactions,
			byte interval) : this(null, dir, syncType, usageType, maxPacketSize,
			additionalTransactions, interval)
		{
		}

		public IsochronousEndpoint(
			IEndpointParent parent,
			EndpointDirection dir,
			SynchronizationType syncType,
			UsageType usageType,
			short maxPacketSize,
			byte additionalTransactions,
			byte interval) :
			this(parent, dir, syncType, usageType,
			     (ushort)maxPacketSize, additionalTransactions, interval)
		{
		}

		public IsochronousEndpoint(
			EndpointDirection dir,
			SynchronizationType syncType,
			UsageType usageType,
			short maxPacketSize,
			byte additionalTransactions,
			byte interval) :
			this(null, dir, syncType, usageType,
			     (ushort)maxPacketSize, additionalTransactions, interval)
		{
		}

		public sealed override EndpointType Type
		{
			get { return EndpointType.Isochronous; }
		}

		public SynchronizationType SynchronizationType
		{
			get { return syncType; }
			set
			{
				if(syncType != value)
				{
					syncType = value;
					OnSynchronizationTypeChanged(null);
				}
			}
		}

		public UsageType UsageType
		{
			get { return usageType; }
			set
			{
				if(usageType != value)
				{
					usageType = value;
					OnUsageTypeChanged(null);
				}
			}
		}

		public byte AdditionalTransactions
		{
			get { return additionalTransactions; }
			set
			{
				if(value > 0x03)
					throw new ArgumentOutOfRangeException("value", value, "");
				if(additionalTransactions != value)
				{
					additionalTransactions = value;
					OnAdditionalTransactionsChanged(null);
				}
			}
		}

		public byte Interval
		{
			get { return RawInterval; }
			set
			{
				if(RawInterval != value)
				{
					RawInterval = value;
					OnIntervalChanged(null);
				}
			}
		}

		public event EventHandler SynchronizationTypeChanged;
		public event EventHandler UsageTypeChanged;
		public event EventHandler AdditionalTransactionsChanged;
		public event EventHandler IntervalChanged;

		protected virtual void OnSynchronizationTypeChanged(EventArgs e)
		{
			if(SynchronizationTypeChanged != null)
				SynchronizationTypeChanged(this, e);
		}

		protected virtual void OnUsageTypeChanged(EventArgs e)
		{
			if(UsageTypeChanged != null)
				UsageTypeChanged(this, e);
		}

		protected virtual void OnAdditionalTransactionsChanged(EventArgs e)
		{
			if(AdditionalTransactionsChanged != null)
				AdditionalTransactionsChanged(this, e);
		}

		protected virtual void OnIntervalChanged(EventArgs e)
		{
			if(IntervalChanged != null)
				IntervalChanged(this, e);
		}

		protected override void GetDescriptorContent([In, Out] byte[] desc,
		                                             int index,
		                                             Endianness endian)
		{
			base.GetDescriptorContent(desc, index, endian);
			byte s = 0x00;
			byte u = 0x00;
			switch(syncType)
			{
			case SynchronizationType.Asynchronous:
				s = 0x01;
				break;
			case SynchronizationType.Adaptive:
				s = 0x02;
				break;
			case SynchronizationType.Synchronous:
				s = 0x03;
				break;
			}
			switch(usageType)
			{
			case UsageType.FeedbackEndpoint:
				u = 0x01;
				break;
			case UsageType.ImplicitFeedbackDataEndpoint:
				u = 0x02;
				break;
			case UsageType.Reserved:
				u = 0x03;
				break;
			}
			desc[index + 3] = (byte)(0x01 | s << 2 | u << 4);
			desc[index + 5] |= (byte)((additionalTransactions & 0x03) << 3);
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			string s, u;
			switch(syncType)
			{
			case SynchronizationType.NoSynchronization: s = "NOSYNC";   break;
			case SynchronizationType.Asynchronous:      s = "ASYNC";    break;
			case SynchronizationType.Adaptive:          s = "ADAPT";    break;
			case SynchronizationType.Synchronous:       s = "SYNC";     break;
			default:                                    s = "INVALID!"; break;
			}
			switch(usageType)
			{
			case UsageType.DataEndpoint:                 u = "DATA_EP";   break;
			case UsageType.FeedbackEndpoint:             u = "FEEDBCK_EP";break;
			case UsageType.ImplicitFeedbackDataEndpoint: u = "IMPL_FD_EP";break;
			default:                                     u = "INVALID!";  break;
			}
			base.Dump(stm, prefix);
			stm.WriteLine(prefix + "Interval:       " + Interval.ToString());
			stm.WriteLine(prefix + "AddTransOpp:    " +
			              additionalTransactions.ToString());
			stm.WriteLine(prefix + "SyncType:       " + s);
			stm.WriteLine(prefix + "UsageType:      " + u);
		}
	}
}
