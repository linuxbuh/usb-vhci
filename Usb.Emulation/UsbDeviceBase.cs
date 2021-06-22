/*
 * UsbDeviceBase.cs -- USB emulation
 *
 * Copyright (C) 2008-2009 Conemis AG Karlsruhe Germany
 * Copyright (C) 2008-2015 Michael Singer <michael@a-singer.de>
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
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace Usb.Emulation
{
	public abstract class UsbDeviceBase : Usb.IUsbDevice, IDisposable
	{
		private class AsyncSubmission
		{
			public Usb.Urb Urb;
			public EventWaitHandle Signal;
			public bool Zombie;
		}

		private bool disposed;

		private OrderedDictionary pending;
		private OrderedDictionary done;
		private AsyncSubmission inprog;
		private AutoResetEvent reapAny;

		private Usb.DeviceDescriptor dev;
		private Usb.DataRate dataRate;

		private Usb.DeviceState state;

		private byte adr;

		protected UsbDeviceBase(Usb.DeviceDescriptor device,
		                        Usb.DataRate dataRate)
		{
			pending = new OrderedDictionary();
			done = new OrderedDictionary();
			reapAny = new AutoResetEvent(false);
			if(device != null)
				this.dev = device;
			else
				this.dev = new Usb.DeviceDescriptor();
			this.dataRate = dataRate;
			adr = 0x00;
		}

		public Usb.DeviceDescriptor Device
		{
			get
			{
				if(disposed) throw new ObjectDisposedException(ToString());
				return dev;
			}
		}

		public Usb.DataRate DataRate
		{
			get
			{
				if(disposed) throw new ObjectDisposedException(ToString());
				return dataRate;
			}
		}

		public Usb.DeviceState State
		{
			get
			{
				if(disposed) throw new ObjectDisposedException(ToString());
				return state;
			}
		}

		public byte Address
		{
			get
			{
				if(disposed) throw new ObjectDisposedException(ToString());
				return adr;
			}
		}

		public void ForgetAsyncUrb(Usb.Urb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(ToString());
			if(pending.Contains(urb))
			{
				pending.Remove(urb);
				Debug.WriteLine("Dropped unprocessed urb");
				return;
			}
			if(inprog != null)
			{
				if((object)inprog.Urb == urb)
				{
					if(!inprog.Zombie)
					{
						inprog.Zombie = true;
						Debug.WriteLine("Urb zombiefyed");
					}
					return;
				}
			}
			if(done.Contains(urb))
			{
				// TODO: Find out if the processing of the urb was successful
				//       and print this information too.
				done.Remove(urb);
				Debug.WriteLine("Dropped processed urb");
				return;
			}
			throw new InvalidOperationException("Urb was not submitted");
		}

		public void CancelAsyncUrb(Usb.Urb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(ToString());
			if(pending.Contains(urb))
			{
				AsyncSubmission asub = (AsyncSubmission)pending[urb];
				pending.Remove(urb);
				Debug.WriteLine("Urb canceled.");
				if(!asub.Zombie)
				{
					done.Add(urb, asub);
					if(asub.Signal != null) asub.Signal.Set();
					reapAny.Set();
				}
				return;
			}
			if(inprog != null)
			{
				if((object)inprog.Urb == urb)
					return;
			}
			if(done.Contains(urb))
				return;
			throw new InvalidOperationException("Urb was not submitted");
		}

		public void AsyncSubmitUrb(Usb.Urb urb, EventWaitHandle ewh)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(ToString());
			AsyncSubmission asub = new AsyncSubmission();
			asub.Urb = urb;
			asub.Signal = ewh;
			pending.Add(urb, asub);
		}

		public Usb.Urb ReapAnyAsyncUrb(int millisecondsTimeout)
		{
			if(disposed) throw new ObjectDisposedException(ToString());
			AsyncSubmission asub;
			if(millisecondsTimeout == 0)
			{
				// We are very impatient here. Only if we get the lock immediately
				// we will do something...
				if(Monitor.TryEnter(done, 0))
				{
					try
					{
						if(done.Count > 0)
						{
							asub = (AsyncSubmission)done[0];
							done.RemoveAt(0);
							return asub.Urb;
						}
					}
					finally { Monitor.Exit(done); }
				}
			}
			else if(millisecondsTimeout < 0)
			{
				if(millisecondsTimeout != Timeout.Infinite) throw new
					ArgumentOutOfRangeException("millisecondsTimeout",
				                                millisecondsTimeout, "");
				while(true)
				{
					lock(done)
					{
						if(done.Count > 0)
						{
							asub = (AsyncSubmission)done[0];
							done.RemoveAt(0);
							return asub.Urb;
						}
					}
					reapAny.WaitOne(1000, false);
				}
			}
			else
			{
				// TODO: Is there any faster way of measuring time?
				DateTime start = DateTime.Now;
				do
				{
					lock(done)
					{
						if(done.Count > 0)
						{
							asub = (AsyncSubmission)done[0];
							done.RemoveAt(0);
							return asub.Urb;
						}
					}
					int remain = millisecondsTimeout -
						(int)(DateTime.Now - start).TotalMilliseconds;
					if(remain > 0)
						reapAny.WaitOne(remain > 1000 ? 1000 : remain, false);
				} while((int)(DateTime.Now - start).TotalMilliseconds <
				        millisecondsTimeout);
			}
			return null;
		}

		public Usb.Urb ReapAnyAsyncUrb()
		{
			return ReapAnyAsyncUrb(-1);
		}

		public Usb.Urb ReapAnyAsyncUrb(TimeSpan timeout)
		{
			double d = timeout.TotalMilliseconds;
			if(d > (double)int.MaxValue)
				throw new ArgumentOutOfRangeException("timeout", timeout, "");
			int t;
			if(d < 0.0)
			{
				if(d != (double)Timeout.Infinite)
					throw new ArgumentOutOfRangeException("timeout",
					                                      timeout, "");
				t = Timeout.Infinite;
			}
			else t = (int)d;
			return ReapAnyAsyncUrb(t);
		}

		public bool ReapAsyncUrb(Usb.Urb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(ToString());
			if(done.Contains(urb))
			{
				AsyncSubmission asub = (AsyncSubmission)done[urb];
				done.Remove(urb);
				if(asub.Zombie)
				{
					// TODO: Find out if the processing of the urb was successful
					//       and print this information too.
					Debug.WriteLine("Dropped processed urb");
					return false;
				}
				return true;
			}
			return false;
		}

		public void ProcControlUrb(Usb.ControlUrb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(ToString());
			if(!OnProcControlUrb(urb))
				urb.Stall();
		}

		public void ProcBulkUrb(Usb.BulkUrb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(ToString());
			if(!OnProcBulkUrb(urb))
				urb.Stall();
		}

		~UsbDeviceBase()
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
			disposed = true;
		}

		public void ProcAsyncUrbs()
		{
			if(inprog != null) throw new InvalidOperationException();
			int c = pending.Count;
			if(c == 0) return;
			AsyncSubmission[] arr = new AsyncSubmission[c];
			pending.Values.CopyTo(arr, 0);
			foreach(AsyncSubmission asub in arr)
			{
				pending.Remove(asub.Urb);
				inprog = asub;
				bool isDone = OnProcUrb(asub.Urb);
				inprog = null;
				
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				asub.Urb.Dump(sb);
				Console.WriteLine(sb.ToString());
				
				if(asub.Zombie)
					continue;
				if(isDone)
				{
					done.Add(asub.Urb, asub);
					if(asub.Signal != null) asub.Signal.Set();
					reapAny.Set();
				}
				else
					pending.Add(asub.Urb, asub);
			}
		}

		protected virtual bool OnProcUrb(Usb.Urb urb)
		{
			if(urb is Usb.ControlUrb)
				return OnProcControlUrb((Usb.ControlUrb)urb);
			else if(urb is Usb.BulkUrb)
				return OnProcBulkUrb((Usb.BulkUrb)urb);
			else if(urb is Usb.InterruptUrb)
				return OnProcInterruptUrb((Usb.InterruptUrb)urb);
			else if(urb is Usb.IsochronousUrb)
				return OnProcIsochronousUrb((Usb.IsochronousUrb)urb);
			else throw new ArgumentException("", "urb");
		}

		protected virtual bool OnProcControlUrb(Usb.ControlUrb urb)
		{
			if(urb.Endpoint == dev.EndpointZero)
			{
				if(urb.IsSetupTypeStandard)
				{
					ushort wValue = (ushort)urb.SetupPacketValue;
					ushort wIndex = (ushort)urb.SetupPacketIndex;
					byte req = urb.SetupPacketRequest;
					bool isin = urb.IsIn;
					switch(req)
					{
					case Usb.ControlUrb.SET_ADDRESS:
						byte adr = unchecked((byte)wValue);
						if((adr & 0x80) != 0x00)
							goto stall;
						return OnProcSetAddress(urb, adr);
					case Usb.ControlUrb.SET_CONFIGURATION:
						if(!urb.IsSetupRecipientDevice || isin)
							goto stall;
						byte config = unchecked((byte)wValue);
						if(config != 0x00 &&
						   !dev.IsUsingConfigurationIndex(config))
							goto stall;
						return OnProcSetConfiguration(urb, config);
					case Usb.ControlUrb.GET_CONFIGURATION:
						if(!urb.IsSetupRecipientDevice || !isin)
							goto stall;
						return OnProcGetConfiguration(urb);
					case Usb.ControlUrb.SET_INTERFACE:
						if(dev.ActiveConfiguration == null) goto stall;
						if(!urb.IsSetupRecipientInterface || isin)
							goto stall;
						if(wIndex > byte.MaxValue ||
						   wValue > byte.MaxValue)
							goto stall;
						if(!dev.ActiveConfiguration.
						   IsUsingInterfaceIndex((byte)wIndex))
							goto stall;
						if(!dev.ActiveConfiguration[(byte)wIndex].IsActive)
							goto stall;
						return OnProcSetInterface(urb,
						                          (byte)wIndex,
						                          (byte)wValue);
					case Usb.ControlUrb.GET_INTERFACE:
						if(dev.ActiveConfiguration == null) goto stall;
						if(!urb.IsSetupRecipientInterface || !isin)
							goto stall;
						if(wIndex > byte.MaxValue)
							goto stall;
						if(!dev.ActiveConfiguration.
						   IsUsingInterfaceIndex((byte)wIndex))
							goto stall;
						if(!dev.ActiveConfiguration[(byte)wIndex].IsActive)
							goto stall;
						return OnProcGetInterface(urb);
					case Usb.ControlUrb.SET_DESCRIPTOR:
						if(!urb.IsSetupRecipientDevice || isin)
							goto stall;
						// allow SET_DESCRIPTOR only for string descriptors
						if((wValue & 0xff00) != 0x0300) goto stall;
						return OnProcSetDescriptor(urb);
					case Usb.ControlUrb.CLEAR_FEATURE:
						if(!urb.IsOut) goto stall;
						return OnProcClearFeature(urb);
					case Usb.ControlUrb.GET_STATUS_REQ:
						if(urb.IsOut) goto stall;
						return OnProcGetStatus(urb);
					case Usb.ControlUrb.GET_DESCRIPTOR:
						if(!urb.IsSetupRecipientDevice || !isin)
							goto stall;
						return OnProcGetDescriptor(urb);
					}
				}
			}
		stall:
			urb.Stall();
			return true;
		}

		protected virtual bool OnProcBulkUrb(Usb.BulkUrb urb)
		{
			urb.Stall();
			return true;
		}

		protected virtual bool OnProcInterruptUrb(Usb.InterruptUrb urb)
		{
			urb.Stall();
			return true;
		}

		protected virtual bool OnProcIsochronousUrb(Usb.IsochronousUrb urb)
		{
			urb.Stall();
			return true;
		}

		private void GuessState()
		{
			if(adr != 0x00)
			{
				if(dev.ActiveConfiguration == null)
					state = Usb.DeviceState.Address;
				else
					state = Usb.DeviceState.Configured;
			}
			else
				state = Usb.DeviceState.Default;
		}

		protected virtual bool OnProcSetAddress(Usb.ControlUrb urb, byte adr)
		{
			if((adr & 0x80) != 0x00)
				throw new ArgumentOutOfRangeException("adr", adr, "");
			this.adr = adr;
			GuessState();
			urb.Ack();
			return true;
		}

		protected virtual bool OnProcSetConfiguration(Usb.ControlUrb urb,
		                                              byte config)
		{
			dev.ActiveConfigurationIndex = config;
			GuessState();
			urb.Ack();
			return true;
		}

		protected virtual bool OnProcGetConfiguration(Usb.ControlUrb urb)
		{
			ushort wLength = (ushort)urb.SetupPacketLength;
			if(wLength == 0) goto ack;
			urb.TransferBuffer[0] = dev.ActiveConfigurationIndex;
			urb.BufferActual = 1;
		ack:
			urb.Ack();
			return true;
		}

		protected virtual bool OnProcSetInterface(Usb.ControlUrb urb,
		                                          byte ifc,
		                                          byte altifc)
		{
			if(dev.ActiveConfiguration == null)
				throw new InvalidOperationException();
			if(dev.ActiveConfiguration[ifc].ActiveAlternateSettingIndex !=
			   altifc)
			{
				if(!dev.ActiveConfiguration[ifc].IsActive)
					throw new InvalidOperationException();

				// TODO: Maybe we should return all the urbs that are related to
				//       endpoints of the previous interface with an error status.
				
				dev.ActiveConfiguration[ifc].ActiveAlternateSettingIndex =
					altifc;
			}
			urb.Ack();
			return true;
		}

		protected virtual bool OnProcGetInterface(Usb.ControlUrb urb)
		{
			ushort wLength = (ushort)urb.SetupPacketLength;
			ushort wIndex = (ushort)urb.SetupPacketIndex;
			if(wLength == 0) goto ack;
			urb.TransferBuffer[0] =
				dev.ActiveConfiguration[(byte)wIndex].
					ActiveAlternateSettingIndex;
			urb.BufferActual = 1;
		ack:
			urb.Ack();
			return true;
		}

		protected virtual bool OnProcSetDescriptor(Usb.ControlUrb urb)
		{
			urb.Stall();
			return true;
		}

		protected virtual bool OnProcClearFeature(Usb.ControlUrb urb)
		{
			urb.Stall();
			return true;
		}

		protected virtual bool OnProcGetStatus(Usb.ControlUrb urb)
		{
			ushort wLength = (ushort)urb.SetupPacketLength;
			if(wLength < 2)
			{
				urb.Stall();
				return true;
			}
			urb.TransferBuffer[0] = 0;
			urb.TransferBuffer[1] = 0;
			urb.BufferActual = 2;
			urb.Ack();
			return true;
		}

		protected virtual bool OnProcGetDescriptor(Usb.ControlUrb urb)
		{
			ushort wLength = (ushort)urb.SetupPacketLength;
			ushort wValue = (ushort)urb.SetupPacketValue;
			byte t = unchecked((byte)(uint)(wValue >> 8));
			byte i = unchecked((byte)(uint)wValue);
			switch(t)
			{
			case 1:
			{
				if(i != 0) goto stall;
				byte[] b = dev.GetLocalDescriptor(Usb.Endianness.UsbSpec);
				int l = Math.Min(b.Length, wLength);
				Array.Copy(b, urb.TransferBuffer, l);
				urb.BufferActual = l;
				goto ack;
			}
			case 2:
			{
				Usb.ConfigurationDescriptor conf = null;
				IEnumerator<Usb.ConfigurationDescriptor> en =
					dev.Configurations.GetEnumerator();
				i++;
				do
				{
					i--;
					if(!en.MoveNext()) goto stall;
					conf = en.Current;
				} while(i > 0);
				byte[] b = conf.GetDescriptor(Usb.Endianness.UsbSpec);
				int l = Math.Min(b.Length, wLength);
				Array.Copy(b, urb.TransferBuffer, l);
				urb.BufferActual = l;
				goto ack;
			}
			case 3:
			{
				if(i == 0)
				{
					byte[] b = Usb.StringDescriptor.
						EnglishUnitedStatesLangIDTable.GetDescriptor();
					int l = Math.Min(b.Length, wLength);
					Array.Copy(b, urb.TransferBuffer, l);
					urb.BufferActual = l;
					goto ack;
				}
				return OnProcGetStringDescriptor(urb, i);
			}
			default:
				goto stall;
			}
		ack:
			urb.Ack();
			return true;
		stall:
			urb.Stall();
			return true;
		}

		protected virtual bool OnProcGetStringDescriptor(Usb.ControlUrb urb,
		                                                 byte index)
		{
			urb.Stall();
			return true;
		}
	}
}
