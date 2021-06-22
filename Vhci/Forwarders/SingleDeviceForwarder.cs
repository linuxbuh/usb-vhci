/*
 * SingleDeviceForwarder.cs -- URB forwarder
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
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace Vhci.Forwarders
{
	public class SingleDeviceForwarder : IForwarder
	{
		private Vhci.Hcd hcd;
		private Usb.IUsbDevice dev;
		private bool running;
		private bool shutdown;
		private AutoResetEvent ev;
		private Usb.DataRate dataRate;

		private Dictionary<Usb.Urb, Vhci.ProcessUrbWork> urbs;

		public event EventHandler DoEvents;

		public SingleDeviceForwarder(Vhci.Hcd hcd, Usb.IUsbDevice dev)
		{
			if(hcd == null) throw new ArgumentNullException("hcd");
			if(dev == null) throw new ArgumentNullException("dev");
			this.hcd = hcd;
			this.dev = dev;
			dataRate = dev.DataRate;
		}

		public SingleDeviceForwarder(Vhci.Hcd hcd,
		                             Usb.IUsbDevice dev,
		                             Usb.DataRate dataRate)
		{
			if(hcd == null) throw new ArgumentNullException("hcd");
			if(dev == null) throw new ArgumentNullException("dev");
			this.hcd = hcd;
			this.dev = dev;
			this.dataRate = dataRate;
		}

		public Vhci.Hcd Hcd
		{
			get { return hcd; }
		}

		public Usb.IUsbDevice UsbDevice
		{
			get { return dev; }
		}

		public Usb.DataRate DataRate
		{
			get { return dataRate; }
		}

		public void Stop()
		{
			if(!running) throw new InvalidOperationException();
			shutdown = true;
			hcd.WorkEnqueued -= WorkEnqueuedHandler;
		}

		private void WorkEnqueuedHandler(object sender, EventArgs e)
		{
			if(sender == hcd) ev.Set();
		}

		public bool IsRunning
		{
			get { return running; }
		}

		protected virtual void OnDoEvents(EventArgs e)
		{
			if(DoEvents != null)
				DoEvents(this, e);
		}

		public void Run()
		{
			if(running) throw new InvalidOperationException();
			ev = new AutoResetEvent(true);
			urbs = new Dictionary<Usb.Urb, Vhci.ProcessUrbWork>();
			hcd.WorkEnqueued += WorkEnqueuedHandler;
			running = true;
			try
			{
				while(true)
				{
					OnDoEvents(null);
					if(shutdown) goto shutdown;
					ev.WaitOne(100, false);
					bool cont = true;
					while(cont)
					{
						OnDoEvents(null);
						if(shutdown) goto shutdown;
						Usb.Urb urb = dev.ReapAnyAsyncUrb(0);
						cont = urb != null;
						if(cont) OnGivebackUrb(urb);
					}
					cont = true;
					while(cont)
					{
						OnDoEvents(null);
						if(shutdown) goto shutdown;
						Vhci.Work w;
						cont = hcd.NextWork(out w);
						if(w == null) break;
						byte port = w.Port;
						if(port == 1)
						{
							if(w is Vhci.PortStatWork)
								OnGotPortStatWork((Vhci.PortStatWork)w);
							else if(w is Vhci.ProcessUrbWork)
								OnGotProcessUrbWork((Vhci.ProcessUrbWork)w);
							else if(w is Vhci.CancelUrbWork)
								OnGotCancelUrbWork((Vhci.CancelUrbWork)w);
						}
					}
				}
			}
			finally
			{
				running = false;
				shutdown = false;
				urbs = null;
			}
		shutdown:
			Debug.WriteLine("Shutting down...");
			try { Thread.Sleep(100); } catch(ThreadInterruptedException) { }
			Debug.WriteLine("Shutdown complete");
		}

		protected Dictionary<Usb.Urb, Vhci.ProcessUrbWork> Urbs
		{
			get { return urbs; }
		}

		protected virtual void OnGotPortStatWork(Vhci.PortStatWork w)
		{
			if(w.TriggersPowerOff)
				Debug.WriteLine("port 1 is powered off.");
			if(w.TriggersPowerOn)
			{
				Debug.WriteLine("port 1 is powered on.");
				Debug.WriteLine("CONNECTING PORT 1...");
				hcd.PortConnect(1, dev.Device, dataRate);
			}
			if(w.TriggersReset)
			{
				Debug.WriteLine("port 1 is resetting.");
				if(hcd.GetPortStat(1).Connection)
				{
					Debug.WriteLine("COMPLETING RESET ON " +
					                "PORT 1 AND ENABLING..."
					                );
					hcd.PortResetDone(1);
				}
			}
			if(w.TriggersResuming)
			{
				Debug.WriteLine("port 1 is resuming.");
				if(hcd.GetPortStat(1).Connection)
				{
					Debug.WriteLine("COMPLETING RESUME ON "
					                + "PORT 1...");
					hcd.PortResumed(1);
				}
			}
			if(w.TriggersSuspend)
				Debug.WriteLine("port 1 is suspended.");
			if(w.TriggersDisable)
				Debug.WriteLine("port 1 is disabled.");
			hcd.FinishWork(w);
		}

		protected virtual void OnGotProcessUrbWork(Vhci.ProcessUrbWork w)
		{
			Usb.Urb urb = w.Urb;
			if(OnProcessUrb(urb))
			{
				urbs.Add(urb, w);
				dev.AsyncSubmitUrb(urb, ev);
			}
			else
			{
				hcd.FinishWork(w);
			}
		}

		protected virtual void OnGotCancelUrbWork(Vhci.CancelUrbWork w)
		{
			foreach(KeyValuePair<Usb.Urb, Vhci.ProcessUrbWork> kvp in urbs)
			{
				if(kvp.Key.Handle == w.Handle)
				{
					OnCancelUrb(kvp.Key);
					break;
				}
			}
		}

		protected virtual bool OnProcessUrb(Usb.Urb urb)
		{
			return true;
		}

		protected virtual void OnCancelUrb(Usb.Urb urb)
		{
			dev.CancelAsyncUrb(urb);
		}

		protected virtual void OnGivebackUrb(Usb.Urb urb)
		{
			Vhci.ProcessUrbWork w = urbs[urb];
			urbs.Remove(urb);
			hcd.FinishWork(w);
		}
	}
}
