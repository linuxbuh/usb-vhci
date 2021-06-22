/*
 * Hcd.cs -- VHCI related classes
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
using System.Threading;
using System.Collections.Generic;

namespace Vhci
{
	public abstract class Hcd : IDisposable
	{
		// Normally gets raised by the background thread
		public event EventHandler WorkEnqueued
		{
			add    { lock(Lock) _WorkEnqueued += value; }
			remove { lock(Lock) _WorkEnqueued -= value; }
		}
		private EventHandler _WorkEnqueued;

		private Thread bgThread;
		private volatile bool threadShutdown;
		private readonly object threadSync;

		private byte portCount;
		protected readonly object Lock;
		private Queue<Work> inbox;
		private LinkedList<Work> processing;

		protected abstract void BGWork();

		// caller has Lock
		protected abstract byte AddressFromPort(byte port);
		// caller has Lock
		protected abstract byte PortFromAddress(byte address);

		// caller has Lock
		protected virtual void CancelingWork(Work work, bool inProgress) { }
		// caller has Lock
		protected virtual void FinishingWork(Work work) { }

		public abstract PortStat GetPortStat(byte port);
		public abstract void PortConnect(byte port,
		                                 Usb.DeviceDescriptor device,
		                                 Usb.DataRate dataRate);
		public abstract void PortDisconnect(byte port);
		public abstract void PortDisable(byte port);
		public abstract void PortResumed(byte port);
		public abstract void PortOvercurrent(byte port, bool setOC);
		public abstract void PortResetDone(byte port, bool enable);
		public void PortResetDone(byte port) { PortResetDone(port, true); }

		protected Hcd(byte ports)
		{
			if(ports == 0) throw new ArgumentException("", "ports");
			threadSync = new object();
			Lock = new object();
			portCount = ports;
			inbox = new Queue<Work>();
			processing = new LinkedList<Work>();
		}

		~Hcd()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if(disposing)
				JoinBGThread();
		}

		// Normally gets called by the backgroung thread.
		// caller has Lock
		protected virtual void OnWorkEnqueued(EventArgs e)
		{
			if(_WorkEnqueued != null)
				_WorkEnqueued(this, e);
		}

		// caller has Lock
		protected void EnqueueWork(Work work)
		{
			inbox.Enqueue(work);
		}

		protected void InitBGThread()
		{
			lock(threadSync)
			{
				if(bgThread != null)
					throw new InvalidOperationException("thread exists");
				Thread t = new Thread(BGThreadStart);
				t.Name = "Hcd_bgThread";
				t.Priority = ThreadPriority.BelowNormal;
				t.IsBackground = true;
				t.Start();
				bgThread = t;
			}
		}

		protected void JoinBGThread()
		{
			lock(threadSync)
			{
				if(bgThread == null || !bgThread.IsAlive)
					return;
				threadShutdown = true;
				bgThread.Join();
				threadShutdown = false;
				bgThread = null;
			}
		}

		private void BGThreadStart()
		{
			while(!threadShutdown)
				BGWork();
		}

		public byte PortCount
		{
			get { return portCount; }
		}

		public bool NextWork(out Work work)
		{
			work = null;
			lock(Lock)
			{
				int len = inbox.Count;
				while(len > 0)
				{
					Work w = inbox.Dequeue();
					if(!w.Canceled)
					{
						processing.AddLast(w);
						work = w;
						return len != 1;
					}
					len = inbox.Count;
				}
			}
			return false;
		}

		public void FinishWork(Work work)
		{
			lock(Lock)
			{
				FinishingWork(work);
				processing.Remove(work);
			}
		}

		public bool CancelProcessUrbWork(long handle)
		{
			// Returns true only when found in the processing list.
			// (When found in inbox or not found at all, then false.)
			lock(Lock)
			{
				ProcessUrbWork work = null;
				foreach(Work w in inbox)
				{
					ProcessUrbWork uw = w as ProcessUrbWork;
					if(uw != null)
					{
						if(uw.Urb.Handle == handle)
						{
							work = uw;
							break;
						}
					}
				}
				if(work == null)
				{
					foreach(Work w in processing)
					{
						ProcessUrbWork uw = w as ProcessUrbWork;
						if(uw != null)
						{
							if(uw.Urb.Handle == handle)
							{
								work = uw;
								break;
							}
						}
					}
					if(work != null)
					{
						CancelingWork(work, true);
						return true;
					}
				}
				else
				{
					work.Cancel();
					CancelingWork(work, false);
					FinishingWork(work);
				}
			}
			return false;
		}
	}
}
