/*
 * UsbDevice.cs -- USB-FS related classes
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
using System.Threading;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Usb.UsbFS
{
	public class UsbDevice : Usb.IUsbDevice, IDisposable
	{
		internal const uint ControlTimeout = 5000;
		private const uint BulkTimeout = 5000;

		private struct NativeUrb
		{
			public byte[] Urb;
			public byte[] Buffer;
			public GCHandle UrbPin;
			public GCHandle BufferPin;
		}

		private enum SubmissionType : byte
		{
			Async = 0,
			SyncWrapCtrl,
			SyncWrapBulk,
			IgnoreNative
		}

		private class AsyncSubmission
		{
			public Usb.Urb ManagedUrb;
			public EventWaitHandle Signal;
			public NativeUrb[] NativeUrb;
			public uint PendingCount; // For splitted URBs, this gets decremented by
			                          // the background thread. So we will know when the
			                          // last sub-packet is reaped and we can move the asub
			                          // to the reaped collection.
			public bool Zombie; // true, when the URB should be dropped/ignored, regardless
			                    // if it got already processed or not.
			public SubmissionType Type;
		}

		private Thread bgThread;
		private Thread pollThread;
		private volatile bool threadShutdown;
		private object threadSync;
		private volatile bool disconnected;
		private bool disposed;

		private DeviceFile usbfs;

		private Dictionary<IntPtr, AsyncSubmission> pending;
		private Dictionary<Usb.Urb, AsyncSubmission> pendingWrapped;
		private OrderedDictionary reaped;
		private AutoResetEvent reapAny;

		private Usb.DeviceDescriptor dev;

		private byte adr;

		private Usb.DeviceState state;

		private UsbDevice()
		{
			threadSync = new object();
			pending = new Dictionary<IntPtr, AsyncSubmission>();
			pendingWrapped = new Dictionary<Usb.Urb, AsyncSubmission>();
			reaped = new OrderedDictionary();
			reapAny = new AutoResetEvent(false);
			adr = 0x00;
		}

		private void Init()
		{
			// read descriptors
			dev = new Usb.DeviceDescriptor(usbfs.GetDescriptors(),
			                               Usb.Endianness.System);
			usbfs.UpdateDescriptorActiveState(dev, true);
		}

		public UsbDevice(string path) : this()
		{
			usbfs = new DeviceFile(path);
			usbfs.Disconnected += usbfs_Disconnected;
			try
			{
				Init();
				InitBGThread();
			}
			catch
			{
				usbfs.Dispose();
				throw;
			}
		}

		protected UsbDevice(DeviceFile file) : this()
		{
			if(file == null) throw new ArgumentNullException("file");
			usbfs = file;
			usbfs.Disconnected += usbfs_Disconnected;
			Init();
		}

		~UsbDevice()
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
			{
				JoinBGThread();
				if(usbfs != null) usbfs.Dispose();
				disconnected = true;
			}
			disposed = true;
		}

		protected void InitBGThread()
		{
			lock(threadSync)
			{
				if(bgThread != null)
					throw new InvalidOperationException("bgThread exists");
				if(pollThread != null)
					throw new InvalidOperationException("pollThread exists");
				Thread t = new Thread(PollThreadStart);
				t.Name = "UsbFSDevice_pollThread";
				t.Priority = ThreadPriority.Lowest;
				t.IsBackground = true;
				t.Start();
				pollThread = t;
				t = new Thread(BGThreadStart);
				t.Name = "UsbFSDevice_bgThread";
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
				if(bgThread != null && bgThread.IsAlive)
				{
					threadShutdown = true;
					while(!bgThread.Join(100))
						bgThread.Abort();
					threadShutdown = false;
					bgThread = null;
				}
				if(pollThread != null && pollThread.IsAlive)
				{
					threadShutdown = true;
					pollThread.Join();
					threadShutdown = false;
					pollThread = null;
				}
			}
		}

		private void BGThreadStart()
		{
			try { while(!threadShutdown) BGWork(); }
			catch(ThreadAbortException) { return; }
			catch(ThreadInterruptedException) { }
		}

		private void PollThreadStart()
		{
			while(!threadShutdown) PollDiscon();
		}

		private void PollDiscon()
		{
			if(usbfs.PollDiscon(100))
			{
				Debug.WriteLine("Device disconnected");
				disconnected = true;
			}
		}

		protected virtual void BGWork()
		{
			IntPtr ptr;
			try
			{
				ptr = usbfs.ReapUrb();
			}
			catch(Usb.DeviceDisconnectedException)
			{
				Debug.WriteLine("Device disconnected");
				disconnected = true;
				Thread.Sleep(100);
				return;
			}
			if(ptr == IntPtr.Zero) return;
			AsyncSubmission asub;
			lock(pending)
			{
				if(!pending.TryGetValue(ptr, out asub))
				{
#if DEBUG
					Debug.WriteLine("Native Urb Pointer " + ptr.ToString() +
					                " is not in list!");
#endif
					return;
				}
				pending.Remove(ptr);
				// now we can release the pin pointers
				int len = asub.NativeUrb.Length;
				for(int i = 0; i < len; i++)
				{
					// only release the ones of the currently finished native URB
					if(asub.NativeUrb[i].UrbPin.IsAllocated &&
						asub.NativeUrb[i].UrbPin.AddrOfPinnedObject() == ptr)
					{
						asub.NativeUrb[i].UrbPin.Free();
						if(asub.NativeUrb[i].BufferPin.IsAllocated)
							asub.NativeUrb[i].BufferPin.Free();
					}
				}
				if(asub.Zombie)
				{
					// TODO: Find out if the zombie got successfully processed
					//       already and print this information too.
#if DEBUG
					Debug.WriteLine("ZOMBIE dropped");
#endif
					return;
				}
				if(asub.Type == SubmissionType.Async)
				{
#if DEBUG
					Debug.WriteLine("PendingCount: " + asub.PendingCount);
#endif
					// decrement, and if it was the last sub-packet...
					if(checked(--asub.PendingCount) == 0)
					{
						// ...add it to the list of finished URBs
						lock(reaped) reaped.Add(asub.ManagedUrb, asub);
						// send a signal to the submitter
						if(asub.Signal != null) asub.Signal.Set();
						// and wake up ReapAnyAsyncUrb
						reapAny.Set();
					}
				}
				else // was it a synchronous URB?
					asub.Signal.Set();
			}
		}

		private void usbfs_Disconnected(object sender, EventArgs e)
		{
			disconnected = true;
		}

		public Usb.DeviceDescriptor Device
		{
			get
			{
				if(disposed) throw new ObjectDisposedException(usbfs.Path);
				return dev;
			}
		}
		
		public byte Address
		{
			get
			{
				if(disposed) throw new ObjectDisposedException(usbfs.Path);
				return adr;
			}
		}

		public byte RealAddress
		{
			get
			{
				if(disposed) throw new ObjectDisposedException(usbfs.Path);
				byte devnum; bool lowspeed;
				usbfs.ConnectInfo(out devnum, out lowspeed);
				return devnum;
			}
		}

		public Usb.DataRate DataRate
		{
			get
			{
				if(disposed) throw new ObjectDisposedException(usbfs.Path);
				// TODO: ask Usb.UsbFS.Enumeration instead of calling UsbFS crap
				byte devnum; bool lowspeed;
				usbfs.ConnectInfo(out devnum, out lowspeed);
				if(lowspeed) return Usb.DataRate.Low;
				// This sucks...
				if(dev != null && dev.EndpointZero.MaxPacketSize == 64)
					return Usb.DataRate.High;
				return Usb.DataRate.Full;
			}
		}

		public Usb.DeviceState State
		{
			get { return state; }
		}

		private static readonly int SizeOfNativeUrb;
		private static readonly int SizeOfNativeIso;
		private static readonly int OffsetOfUrbType;
		private static readonly int OffsetOfUrbEndpoint;
		private static readonly int OffsetOfUrbStatus;
		private static readonly int OffsetOfUrbFlags;
		private static readonly int OffsetOfUrbBuffer;
		private static readonly int OffsetOfUrbBufferLength;
		private static readonly int OffsetOfUrbActualLength;
		//private static readonly int OffsetOfUrbStartFrame;
		private static readonly int OffsetOfUrbNumberOfPackets;
		private static readonly int OffsetOfUrbErrorCount;
		//private static readonly int OffsetOfUrbSigNr;
		//private static readonly int OffsetOfUrbUserContext;
		private static readonly int OffsetOfIsoLength;
		private static readonly int OffsetOfIsoActualLength;
		private static readonly int OffsetOfIsoStatus;

		static UsbDevice()
		{
			SizeOfNativeUrb =
				Marshal.SizeOf(typeof(Ioctl.usbdevfs_urb));
			SizeOfNativeIso =
				Marshal.SizeOf(typeof(Ioctl.usbdevfs_iso_packet_desc));
			OffsetOfUrbType =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "type").ToInt32();
			OffsetOfUrbEndpoint =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "endpoint").ToInt32();
			OffsetOfUrbStatus =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "status").ToInt32();
			OffsetOfUrbFlags =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "flags").ToInt32();
			OffsetOfUrbBuffer =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "buffer").ToInt32();
			OffsetOfUrbBufferLength =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "buffer_length").ToInt32();
			OffsetOfUrbActualLength =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "actual_length").ToInt32();
			//OffsetOfUrbStartFrame =
			//	Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
			//	                 "start_frame").ToInt32();
			OffsetOfUrbNumberOfPackets =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "number_of_packets").ToInt32();
			OffsetOfUrbErrorCount =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
				                 "error_count").ToInt32();
			//OffsetOfUrbSigNr =
			//	Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
			//	                 "signr").ToInt32();
			//OffsetOfUrbUserContext =
			//	Marshal.OffsetOf(typeof(Ioctl.usbdevfs_urb),
			//	                 "usercontext").ToInt32();
			OffsetOfIsoLength =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_iso_packet_desc),
				                 "length").ToInt32();
			OffsetOfIsoActualLength =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_iso_packet_desc),
				                 "actual_length").ToInt32();
			OffsetOfIsoStatus =
				Marshal.OffsetOf(typeof(Ioctl.usbdevfs_iso_packet_desc),
				                 "status").ToInt32();
		}

		public void CancelAsyncUrb(Usb.Urb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(usbfs.Path);
			AsyncSubmission found = null;
			lock(pending)
			{
				foreach(AsyncSubmission asub in pending.Values)
				{
					if(asub.ManagedUrb == urb)
					{
						int c = asub.NativeUrb.Length;
						for(int i = 0; i < c; i++)
						{
							if(asub.NativeUrb[i].UrbPin.IsAllocated)
							{
								pending.Remove(asub.NativeUrb[i].UrbPin.
								               AddrOfPinnedObject());
							}
						}
						found = asub;
						break;
					}
				}
				if(found != null)
				{
					int c = found.NativeUrb.Length;
					for(int i = 0; i < c; i++)
					{
						if(found.NativeUrb[i].UrbPin.IsAllocated)
						{
							usbfs.DiscardUrb(found.NativeUrb[i].UrbPin.
							                 AddrOfPinnedObject());
							found.NativeUrb[i].UrbPin.Free();
							if(found.NativeUrb[i].BufferPin.IsAllocated)
								found.NativeUrb[i].BufferPin.Free();
						}
					}
					found.PendingCount = 0;
					Debug.WriteLine("Urb canceled.");
					if(!found.Zombie)
					{
						lock(reaped) reaped.Add(found.ManagedUrb, found);
						if(found.Signal != null) found.Signal.Set();
						reapAny.Set();
					}
					return;
				}
			}
			lock(pendingWrapped)
			{
				AsyncSubmission asub;
				if(pendingWrapped.TryGetValue(urb, out asub))
				{
					if(!asub.Zombie)
						return;
					throw new InvalidOperationException
						("Urb was not submitted");
				}
			}
			lock(reaped)
			{
				if(reaped.Contains(urb))
					return;
			}
			throw new InvalidOperationException("Urb was not submitted");
		}

		public void ForgetAsyncUrb(Usb.Urb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(usbfs.Path);
			lock(pending)
			{
				foreach(AsyncSubmission asub in pending.Values)
				{
					if(asub.ManagedUrb == urb)
					{
						if(!asub.Zombie)
						{
							asub.Zombie = true;
							Debug.WriteLine("Urb zombiefyed");
						}
						return;
					}
				}
			}
			lock(pendingWrapped)
			{
				AsyncSubmission asub;
				if(pendingWrapped.TryGetValue(urb, out asub))
				{
					if(!asub.Zombie)
					{
						asub.Zombie = true;
						Debug.WriteLine("Synchronous wrapped asynchronous " +
						                "urb zombiefyed");
					}
					return;
				}
			}
			lock(reaped)
			{
				if(reaped.Contains(urb))
				{
					// TODO: Find out if the URB got successfully processed
					//       and print this information too.
					reaped.Remove(urb);
					Debug.WriteLine("Dropped processed urb");
					return;
				}
			}
			throw new InvalidOperationException("Urb was not submitted");
		}
		
		private void AsyncWrapperEP0Thread(object state)
		{
			AsyncSubmission asuba = (AsyncSubmission)state;
			// first check if it got zombiefied
			if(asuba.Zombie)
			{
				lock(pendingWrapped)
					pendingWrapped.Remove(asuba.ManagedUrb);
				Debug.WriteLine("Unprocessed zombie dropped");
				return;
			}
			// send out
			ProcControlUrbEP0((ControlUrb)asuba.ManagedUrb);
			// remove from pending list
			lock(pendingWrapped)
			{
				pendingWrapped.Remove(asuba.ManagedUrb);
				if(asuba.Zombie)
				{
					string res = (asuba.ManagedUrb.Status ==
					              UrbStatus.Success) ?
						"Successfully" : "Unsuccessfully";
					Debug.WriteLine(res +
					                " processed zombie dropped");
					return;
				}
				// add to list of finished URBs
				lock(reaped) reaped.Add(asuba.ManagedUrb, asuba);
			}
			// send a signal to the submitter
			if(asuba.Signal != null) asuba.Signal.Set();
			// wake up ReapAnyAsyncUrb
			reapAny.Set();
		}
		
		public void AsyncSubmitUrb(Usb.Urb urb, EventWaitHandle ewh)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(usbfs.Path);
			if(disconnected) throw new Usb.DeviceDisconnectedException();

			if(urb is Usb.ControlUrb)
			{
				// Use the synchronous USB-FS functions for EP0 CONTROL URBs,
				// because commands like SET_INTERFACE won't work otherwise.
				Usb.ControlUrb curb = (ControlUrb)urb;
				if((curb.EndpointAddress & 0x0f) == 0x00)
				{
					AsyncSubmission asubw = new AsyncSubmission();
					// Tell the NativeToManaged method that it should ignore
					// this URB because it does not have a native representation.
					asubw.Type = SubmissionType.IgnoreNative;
					asubw.ManagedUrb = urb;
					asubw.Signal = ewh;
					lock(pendingWrapped) pendingWrapped.Add(urb, asubw);
					ThreadPool.QueueUserWorkItem(AsyncWrapperEP0Thread, asubw);
					return;
				}
			}

			KeyValuePair<byte[], byte[]>[] native = ManagedToNative(urb);
			int len = native.Length;
			AsyncSubmission asub = new AsyncSubmission();
			asub.ManagedUrb = urb;
			asub.Signal = ewh;
			asub.PendingCount = (uint)len;
			asub.NativeUrb = new NativeUrb[len];
			for(int i = 0; i < len; i++)
			{
				asub.NativeUrb[i].Urb = native[i].Key;
				asub.NativeUrb[i].Buffer = native[i].Value;
			}
			PinAndGo(asub);
		}

		private void PinAndGo(AsyncSubmission asub)
		{
			int len = asub.NativeUrb.Length;
			if(len > 1)
				Console.Write("#"); // a big fish
			try
			{
				// pin everything
				for(int i = 0; i < len; i++)
				{
					asub.NativeUrb[i].UrbPin =
						GCHandle.Alloc(asub.NativeUrb[i].Urb,
						               GCHandleType.Pinned);
					asub.NativeUrb[i].BufferPin =
						GCHandle.Alloc(asub.NativeUrb[i].Buffer,
						               GCHandleType.Pinned);
					SetPtrInNativeUrb
						(asub.NativeUrb[i].Urb,
						 asub.NativeUrb[i].BufferPin.AddrOfPinnedObject());
				}
				lock(pending)
				{
					try
					{
						for(int i = 0; i < len; i++)
						{
							pending.Add(asub.NativeUrb[i].UrbPin.
							            AddrOfPinnedObject(),
							            asub);
						}
						for(int i = 0; i < len; i++)
						{
							/*
							unsafe
							{
								int l =
									Marshal.SizeOf(typeof(Ioctl.usbdevfs_urb));
								byte* p = (byte*)asub.NativeUrb[i].UrbPin.
									AddrOfPinnedObject().ToPointer();
								Debug.Write("native_urb = ");
								Debug.WriteLine("");
								for(int j = 0; j < l; j++)
									Debug.Write(" " + p[j].ToString("x2"));
								Debug.WriteLine("");
								Debug.WriteLine("(" + l + " bytes)");
							}
							*/

							try
							{
								usbfs.SubmitUrb
									(asub.NativeUrb[i].UrbPin.
									 AddrOfPinnedObject());
							}
							catch
							{
								// cancel the already submitted URBs
								for(int j = 0; j < i; j++)
								{
									try
									{
										usbfs.DiscardUrb(asub.NativeUrb[j].
										                 UrbPin.
										                 AddrOfPinnedObject());
									}
									catch(Usb.DeviceDisconnectedException)
									{
										throw;
									}
									catch
									{
									}
								}
								throw;
							}
						}
					}
					catch
					{
						for(int i = 0; i < len; i++)
						{
							pending.Remove(asub.NativeUrb[i].UrbPin.
							               AddrOfPinnedObject());
						}
						throw;
					}
				}
			}
			catch
			{
				// release pin pointers
				for(int i = 0; i < len; i++)
				{
					if(asub.NativeUrb[i].UrbPin.IsAllocated)
						asub.NativeUrb[i].UrbPin.Free();
					if(asub.NativeUrb[i].BufferPin.IsAllocated)
						asub.NativeUrb[i].BufferPin.Free();
				}
				throw;
			}
		}

		// Creates one or more byte array pairs from a URB. These pairs
		// represent the native URBs that are used by USB-FS. Normally the
		// result is just one pair. But if the data buffer of the URB is larger
		// than 16k, it needs to be splitted up.
		// The primary element of the pair contains the native URB structure,
		// and the secondary element contains its data buffer. The pointer to
		// the data buffer in the URB structure needs to be set later, after
		// the buffer got fixated on the heap by a pin pointer. For this
		// purpose the SetPtrInNativeUrb(byte[], IntPtr) method may be used.
		private static
			KeyValuePair<byte[], byte[]>[] ManagedToNative(Usb.Urb urb)
		{
			int c = urb.BufferLength;
			bool isin = urb.IsIn;
			int cCur;
			int pos = 0;
			Usb.EndpointType type = urb.Type;
			KeyValuePair<byte[], byte[]>[] result;
			byte[] nativeUrb;
			byte[] nativeBuffer;
			switch(type)
			{
			case Usb.EndpointType.Isochronous:
				Usb.IsochronousUrb iso = (Usb.IsochronousUrb)urb;
				Usb.IsochronousPacket[] isos = iso.IsoPackets;
				int pktc = isos.Length;
				List<KeyValuePair<int, int>> pkt =
					new List<KeyValuePair<int, int>>(pktc + 1);
				pkt.Add(new KeyValuePair<int, int>(0, 0));
				// Determine how many sub-packets we need. For every sub-packet
				// we store the number of the contained ISO (sub) packets and
				// the needed size of the data buffer.
				for(int i = 0; i < pktc; i++)
				{
					int clen = isos[i].PacketLength;
					KeyValuePair<int, int> cp = pkt[pos];
					if(cp.Value + clen <= Ioctl.MAX_USBFS_BUFFER_SIZE)
					//if(cp.Key == 0)
					{
						pkt[pos] = new KeyValuePair<int, int>(cp.Key + 1,
						                                      cp.Value + clen);
					}
					else
					{
						if(clen > Ioctl.MAX_USBFS_BUFFER_SIZE)
							throw new NotSupportedException();
						pkt.Add(new KeyValuePair<int, int>(1, clen));
						pos++;
					}
				}
				cCur = pos + 1;
#if DEBUG
				Debug.Assert(cCur == pkt.Count);
#endif
				pos = 0;
				result = new KeyValuePair<byte[], byte[]>[cCur];
				for(int i = 0; i < cCur; i++)
				{
					KeyValuePair<int, int> cp = pkt[i];
					nativeUrb = new byte[SizeOfNativeUrb +
					                     cp.Key * SizeOfNativeIso];
					nativeBuffer = new byte[cp.Value];
					int dst_offset = 0;
					for(int j = 0; j < cp.Key; j++)
					{
						int offset = SizeOfNativeUrb + j * SizeOfNativeIso;
						Usb.IsochronousPacket ip = isos[pos++];
						int clen = ip.PacketLength;
						BitConverter.GetBytes(Usb.Linux.errno.EINPROGRESS).
							CopyTo(nativeUrb, offset + OffsetOfIsoStatus);
						BitConverter.GetBytes(clen).
							CopyTo(nativeUrb, offset + OffsetOfIsoLength);
						if(!isin)
						{
							Array.Copy(urb.TransferBuffer, ip.Offset,
							           nativeBuffer, dst_offset, clen);
							dst_offset += clen;
						}
					}
					nativeUrb[OffsetOfUrbType] =
						Ioctl.USBDEVFS_URB_TYPE_ISO;
					nativeUrb[OffsetOfUrbEndpoint] = urb.EndpointAddress;
					BitConverter.GetBytes(Usb.Linux.errno.EINPROGRESS).
						CopyTo(nativeUrb, OffsetOfUrbStatus);
					BitConverter.GetBytes(Ioctl.URB_ISO_ASAP).
						CopyTo(nativeUrb, OffsetOfUrbFlags);
					BitConverter.GetBytes(cp.Value).
						CopyTo(nativeUrb, OffsetOfUrbBufferLength);
					BitConverter.GetBytes(cp.Key).
						CopyTo(nativeUrb, OffsetOfUrbNumberOfPackets);
					result[i] = new KeyValuePair<byte[], byte[]>(nativeUrb,
					                                             nativeBuffer);
				}
#if DEBUG
				Debug.Assert(pos == iso.PacketCount);
#endif
				break;
			case Usb.EndpointType.Bulk:
				// determine how many sub-packets we need
				int modulo = c % Ioctl.MAX_USBFS_BUFFER_SIZE;
				bool passt = (modulo == 0);
				int splits = c / Ioctl.MAX_USBFS_BUFFER_SIZE +
					((passt && c != 0) ? 0 : 1);
				result = new KeyValuePair<byte[], byte[]>[splits];
				for(int i = 0; i < splits; i++)
				{
					// determine the buffer size for the sub packet
					cCur = (splits == 1) ? c :
						(((i == splits - 1) && !passt) ? 
						modulo : Ioctl.MAX_USBFS_BUFFER_SIZE);
					nativeUrb = new byte[SizeOfNativeUrb];
					// UsbFS always wants a buffer,
					// even if buffer_length==0
					if(cCur <= 4) nativeBuffer = new byte[4];
					else          nativeBuffer = new byte[cCur];
					// copy buffer
					if(!isin && cCur > 0)
						Array.Copy(urb.TransferBuffer, pos,
						           nativeBuffer, 0, cCur);
					nativeUrb[OffsetOfUrbType] =
						Ioctl.USBDEVFS_URB_TYPE_BULK;
					nativeUrb[OffsetOfUrbEndpoint] = urb.EndpointAddress;
					BitConverter.GetBytes(Usb.Linux.errno.EINPROGRESS).
						CopyTo(nativeUrb, OffsetOfUrbStatus);
					uint flags = 0U;
					if(urb.IsShortNotOk) flags |= Ioctl.URB_SHORT_NOT_OK;
					if(urb.IsZeroPacket) flags |= Ioctl.URB_ZERO_PACKET;
					BitConverter.GetBytes(flags).
						CopyTo(nativeUrb, OffsetOfUrbFlags);
					BitConverter.GetBytes(cCur).
						CopyTo(nativeUrb, OffsetOfUrbBufferLength);
					result[i] = new KeyValuePair<byte[], byte[]>(nativeUrb,
					                                             nativeBuffer);
					pos += Ioctl.MAX_USBFS_BUFFER_SIZE;
				}
				break;
			default:
				// For CONTROL transfers UsbFS allows 8 more bytes for the
				// SETUP packet (thats why we check c, not c+8)
				if(c > Ioctl.MAX_USBFS_BUFFER_SIZE)
					throw new NotSupportedException();
				result = new KeyValuePair<byte[], byte[]>[1];
				nativeUrb = new byte[SizeOfNativeUrb];
				bool isc = type == Usb.EndpointType.Control;
				cCur = isc ? (c + 8) : c;
				// UsbFS always wants a buffer,
				// even if buffer_length==0
				if(cCur <= 4) nativeBuffer = new byte[4];
				else          nativeBuffer = new byte[cCur];
				if(isc)
				{
					pos = 8;
					Usb.ControlUrb curb = (Usb.ControlUrb)urb;
					ushort wValue = unchecked((ushort)curb.SetupPacketValue);
					ushort wIndex = unchecked((ushort)curb.SetupPacketIndex);
					ushort wLength = unchecked((ushort)curb.SetupPacketLength);
					nativeBuffer[0] = curb.SetupPacketRequestType;
					nativeBuffer[1] = curb.SetupPacketRequest;
					unchecked
					{
						nativeBuffer[2] = (byte)wValue;
						nativeBuffer[3] = (byte)(uint)(wValue >> 8);
						nativeBuffer[4] = (byte)wIndex;
						nativeBuffer[5] = (byte)(uint)(wIndex >> 8);
						nativeBuffer[6] = (byte)wLength;
						nativeBuffer[7] = (byte)(uint)(wLength >> 8);
					}
				}
				// copy buffer
				if(!isin && c > 0)
					Array.Copy(urb.TransferBuffer, 0,
					           nativeBuffer, pos, c);
				nativeUrb[OffsetOfUrbType] = isc ?
					Ioctl.USBDEVFS_URB_TYPE_CONTROL :
					Ioctl.USBDEVFS_URB_TYPE_INTERRUPT;
				nativeUrb[OffsetOfUrbEndpoint] = urb.EndpointAddress;
				BitConverter.GetBytes(Usb.Linux.errno.EINPROGRESS).
					CopyTo(nativeUrb, OffsetOfUrbStatus);
				BitConverter.GetBytes(cCur).
					CopyTo(nativeUrb, OffsetOfUrbBufferLength);
				result[0] = new KeyValuePair<byte[], byte[]>(nativeUrb,
				                                             nativeBuffer);
				break;
			}
			return result;
		}

		private unsafe static void SetPtrInNativeUrb(byte[] urb, IntPtr ptr)
		{
			// Why isn't there any BitConverter method for IntPtr or void*?
			//BitConverter.GetBytes(ptr).
			//	CopyTo(urb, OffsetOfUrbBuffer);
			fixed(byte* uPin = urb)
			{
				byte* u = uPin + OffsetOfUrbBuffer;
				int ptrSize = IntPtr.Size;
				for(byte* p = (byte*)&ptr; p < (byte*)&ptr + ptrSize; p++, u++)
					*u = *p;
			}
		}

		public Usb.Urb ReapAnyAsyncUrb(int millisecondsTimeout)
		{
			if(disposed) throw new ObjectDisposedException(usbfs.Path);
			AsyncSubmission asub;
			if(millisecondsTimeout == 0)
			{
				// We are very impatient here. Only if we get the lock immediately
				// we will do something...
				if(Monitor.TryEnter(reaped, 0))
				{
					try
					{
						if(reaped.Count > 0)
						{
							asub = (AsyncSubmission)reaped[0];
							reaped.RemoveAt(0);
							goto gotIt;
						}
					}
					finally { Monitor.Exit(reaped); }
				}
			}
			else if(millisecondsTimeout < 0)
			{
				if(millisecondsTimeout != Timeout.Infinite) throw new
					ArgumentOutOfRangeException("millisecondsTimeout",
				                                millisecondsTimeout, "");
				while(true)
				{
					lock(reaped)
					{
						if(reaped.Count > 0)
						{
							asub = (AsyncSubmission)reaped[0];
							reaped.RemoveAt(0);
							goto gotIt;
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
					lock(reaped)
					{
						if(reaped.Count > 0)
						{
							asub = (AsyncSubmission)reaped[0];
							reaped.RemoveAt(0);
							goto gotIt;
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
		gotIt:
			//Console.WriteLine("Reap: " + System.Environment.TickCount);
			NativeToManaged(asub);
			return asub.ManagedUrb;
		}

		// Converts the results of a reaped native URB back to its managed
		// representation.
		// Should asub still be in the reaped collection, then the caller
		// must own the lock for that collection.
		private static void NativeToManaged(AsyncSubmission asub)
		{
			if(asub.Type == SubmissionType.IgnoreNative) return;
			int len = asub.NativeUrb.Length;
			Usb.Urb urb = asub.ManagedUrb;
			bool isin = urb.IsIn;
			byte[] data = urb.TransferBuffer;
			int status = 0;
			int pos = 0;
			if(urb is Usb.IsochronousUrb)
			{
				Usb.IsochronousUrb iso = (Usb.IsochronousUrb)urb;
				int error = 0;
				for(int i = 0; i < len; i++)
				{
					byte[] nurb = asub.NativeUrb[i].Urb;
					byte[] ndata = asub.NativeUrb[i].Buffer;
					int npackets = BitConverter.ToInt32
						(nurb, OffsetOfUrbNumberOfPackets);
					int nerror = BitConverter.ToInt32(nurb,
					                                  OffsetOfUrbErrorCount);
					int nstatus = BitConverter.ToInt32(nurb, OffsetOfUrbStatus);
					error += nerror;
					if(status == 0) status = nstatus;
					int npos = 0;
					for(int j = 0; j < npackets; j++)
					{
						int cur = SizeOfNativeUrb + j * SizeOfNativeIso;
						int nlen = BitConverter.ToInt32
							(nurb, cur + OffsetOfIsoLength);
						int nactual = BitConverter.ToInt32
							(nurb, cur + OffsetOfIsoActualLength);
						Usb.IsochronousPacket nip = iso.IsoPackets[pos];
						nip.Status = Usb.Linux.errno.
							FromIsoPacketsErrno(BitConverter.ToInt32
							                    (nurb,
							                     cur + OffsetOfIsoStatus));
						nip.PacketActual = nactual;
						iso.IsoPackets[pos] = nip;
						if(isin)
							Array.Copy(ndata, npos,
							           data, nip.Offset,
							           nactual);
						npos += nlen;
						pos++;
					}
				}
				if(isin) urb.BufferActual = urb.BufferLength;
				urb.Status = Usb.Linux.errno.FromErrno(status, true);
				iso.ErrorCount = error;
			}
			else
			{
				bool isc = urb is Usb.ControlUrb;
				for(int i = 0; i < len; i++)
				{
					byte[] nurb = asub.NativeUrb[i].Urb;
					byte[] ndata = asub.NativeUrb[i].Buffer;
					int nstatus = BitConverter.ToInt32(nurb, OffsetOfUrbStatus);
					int nactual = BitConverter.ToInt32(nurb,
					                                   OffsetOfUrbActualLength);
					if(isin)
					{
						int npos = (isc && i == 0) ? 8 : 0;
						if(nactual > 0)
							Array.Copy(ndata, npos, data, pos, nactual);
					}
					pos += nactual;
					if(status == 0) status = nstatus;
				}
				urb.BufferActual = pos;
				urb.Status = Usb.Linux.errno.FromErrno(status, false);
			}
		}

		public Usb.Urb ReapAnyAsyncUrb()
		{
			return ReapAnyAsyncUrb(Timeout.Infinite);
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

		public void ProcControlUrb(Usb.ControlUrb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(usbfs.Path);
			if(disconnected) throw new Usb.DeviceDisconnectedException();
			if((urb.EndpointAddress & 0x0f) == 0x00)
				ProcControlUrbEP0(urb);
			else
				// UsbFS does not support specifying an endpoint address for
				// synchronous control transfers. (They are always ep0)
				// TODO: wrap asynchronously
				throw new NotSupportedException();
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

		private void ProcControlUrbEP0(Usb.ControlUrb urb)
		{
#if DEBUG
			byte epadr = urb.EndpointAddress;
			if((epadr & 0x0f) != 0x00) throw new NotSupportedException();
#endif
			ushort wValue = unchecked((ushort)urb.SetupPacketValue);
			ushort wIndex = unchecked((ushort)urb.SetupPacketIndex);
			ushort wLength = unchecked((ushort)urb.SetupPacketLength);
			byte req = urb.SetupPacketRequest;
			bool isin = urb.IsIn;
			if(urb.IsSetupTypeStandard)
			{
				switch(req)
				{
				case Usb.ControlUrb.SET_ADDRESS:
				{
					byte adr = unchecked((byte)wValue);
					if((adr & 0x80) != 0x00)
						goto stall;
					this.adr = adr;
					GuessState();
					goto ack;
				}
				case Usb.ControlUrb.SET_CONFIGURATION:
					if(!urb.IsSetupRecipientDevice || isin)
						goto stall;
					byte config = unchecked((byte)wValue);
					if(config != 0x00 && !dev.IsUsingConfigurationIndex(config))
						goto stall;
					try { SetConfiguration(config); }
					catch { goto stall; } // TODO: log exception
					goto ack;
				case Usb.ControlUrb.GET_CONFIGURATION:
					if(!urb.IsSetupRecipientDevice || !isin)
						goto stall;
					if(wLength == 0) goto ack;
					urb.TransferBuffer[0] = dev.ActiveConfigurationIndex;
					urb.BufferActual = 1;
					goto ack;
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
					try { SetInterface((byte)wIndex, (byte)wValue); }
					catch { goto stall; } // TODO: log exception
					goto ack;
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
					if(wLength == 0) goto ack;
					urb.TransferBuffer[0] =
						dev.ActiveConfiguration[(byte)wIndex].
							ActiveAlternateSettingIndex;
					urb.BufferActual = 1;
					goto ack;
				case Usb.ControlUrb.SET_DESCRIPTOR:
					if(!urb.IsSetupRecipientDevice || isin)
						goto stall;
					// allow SET_DESCRIPTOR only for string descriptors
					if((wValue & 0xff00) != 0x0300) goto stall;
					break;
				case Usb.ControlUrb.CLEAR_FEATURE:
					if(!urb.IsOut) goto stall;
					if(urb.IsSetupRecipientEndpoint)
					{
						if(wValue != 0) goto stall;
						Usb.Endpoint ep = dev.TryGetEndpoint((byte)wIndex);
						if(ep == null) goto stall;
						Usb.AlternateSetting alt =
							ep.Parent as Usb.AlternateSetting;
						if(alt != null && !alt.Interface.IsActive)
							goto stall;
						try { ClearHalt((byte)wIndex); }
						catch { goto stall; } // TODO: log exception
						goto ack;
					}
					break;
				}
			}
			Ioctl.usbdevfs_ctrltransfer_ptr curb;
			byte[] buf;
			// UsbFS always wants a buffer,
			// even if wLength==0
			if(wLength == 0) buf = dummy;
			else             buf = urb.TransferBuffer;
			curb.timeout = ControlTimeout;
			curb.bRequestType = urb.SetupPacketRequestType;
			curb.bRequest = req;
			curb.wValue = wValue;
			curb.wIndex = wIndex;
			curb.wLength = wLength;
			int ret;
			unsafe
			{
				fixed(byte* bufp = buf)
				{
					curb.data = new IntPtr(bufp);
					ret = usbfs.ControlTransfer(ref curb);
				}
			}
			if(ret < 0) goto stall;
			urb.BufferActual = ret;
		ack:   urb.Ack();   return;
		stall: urb.Stall(); return;
		}

		protected void SetConfiguration(byte cfg)
		{
			if(dev.ActiveConfigurationIndex != cfg)
			{
				// TODO: Somehow we need to prevent the submitting of new
				//       non-ep0 urbs here. And we should set an error status
				//       for already submitted but unprocessed urbs. (Maybe
				//       EPIPE or ESHUTDOWN)

				// Release interfaces (Maybe not necessary, but wouldn't hurt)
				if(dev.ActiveConfiguration != null)
				{
					foreach(KeyValuePair<byte, Interface> kvp
					        in dev.ActiveConfiguration)
					{
						if(kvp.Value.IsActive)
						{
							usbfs.ReleaseInterface(kvp.Key);
							kvp.Value.IsActive = false;
						}
					}
				}
				
				// set active configuration
				usbfs.SetConfiguration(cfg);
				dev.ActiveConfigurationIndex = cfg;
				GuessState();

				// Re-claim interfaces and query their alternate
				// settings (should be all zero after setting the
				// configuration)
				if(dev.ActiveConfiguration != null)
				{
					Ioctl.usbdevfs_ctrltransfer urb;
					urb.data = new byte[4];
					urb.timeout = ControlTimeout;
					urb.bRequestType = 0x81;
					urb.bRequest = ControlUrb.GET_INTERFACE;
					urb.wValue = 0;
					urb.wLength = 1;
					foreach(KeyValuePair<byte, Interface> kvp
					        in dev.ActiveConfiguration)
					{
						try
						{
							usbfs.Disconnect(kvp.Key);
							usbfs.ClaimInterface(kvp.Key);
							kvp.Value.IsActive = true;
						}
						catch { }
						
						urb.wIndex = kvp.Key;
						urb.data[0] = 0;
						int ret = usbfs.ControlTransfer(ref urb);
						if(ret < 0)
						{
							int errno = Marshal.GetLastWin32Error();
							// Some devices that provide only one alternate setting are
							// STALLing GET_INTERFACE requests. So EPIPE (STALL) is ok here.
							if(errno != 32) // EPIPE
								throw new System.IO.IOException("Error: " +
								                                errno.
								                                ToString());
						}
						if(ret == 1)
							kvp.Value.ActiveAlternateSettingIndex = urb.data[0];
					}
				}
			}
		}

		protected void SetInterface(byte ifc, byte altifc)
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

				// set active alternate setting
				usbfs.SetInterface(ifc, altifc);
				dev.ActiveConfiguration[ifc].ActiveAlternateSettingIndex =
					altifc;
			}
		}

		protected void ClearHalt(byte epadr)
		{
			Usb.Endpoint ep = dev.TryGetEndpoint(epadr);
			if(ep == null) throw new InvalidOperationException();
			Usb.AlternateSetting alt = ep.Parent as Usb.AlternateSetting;
			if(alt != null && !alt.Interface.IsActive)
				throw new InvalidOperationException();
			usbfs.ClearHalt(epadr);
		}

		public void ProcBulkUrb(Usb.BulkUrb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(usbfs.Path);
			if(disconnected) throw new Usb.DeviceDisconnectedException();
			// ProcBulkUrbIntern can't handle SHORT_NOT_OK and stuff,
			// so we have to wrap this asynchronously...
			if(urb.IsShortNotOk || urb.IsZeroPacket ||
			   urb.BufferLength > Ioctl.MAX_USBFS_BUFFER_SIZE)
			{
				AutoResetEvent ev = new AutoResetEvent(false);
				KeyValuePair<byte[], byte[]>[] native = ManagedToNative(urb);
				int len = native.Length;
				AsyncSubmission asub = new AsyncSubmission();
				asub.Type = SubmissionType.SyncWrapBulk;
				asub.ManagedUrb = urb;
				asub.Signal = ev;
				asub.PendingCount = (uint)len;
				asub.NativeUrb = new NativeUrb[len];
				for(int i = 0; i < len; i++)
				{
					asub.NativeUrb[i].Urb = native[i].Key;
					asub.NativeUrb[i].Buffer = native[i].Value;
				}
				PinAndGo(asub);
				ev.WaitOne((int)BulkTimeout, false);
				if(!ReapAsyncUrb(urb))
				{
					CancelAsyncUrb(urb);
					ForgetAsyncUrb(urb);
				}
			}
			else ProcBulkUrbIntern(urb);
		}

		public bool ReapAsyncUrb(Usb.Urb urb)
		{
			if(urb == null) throw new ArgumentNullException("urb");
			if(disposed) throw new ObjectDisposedException(usbfs.Path);
			lock(reaped)
			{
				if(reaped.Contains(urb))
				{
					AsyncSubmission asub = (AsyncSubmission)reaped[urb];
					reaped.Remove(urb);
					if(asub.Zombie)
					{
						// TODO: Find out if the processing of the urb was successful
						//       and print this information too.
						Debug.WriteLine("Dropped processed urb");
						return false;
					}
					NativeToManaged(asub);
					return true;
				}
			}
			return false;
		}

		private void ProcBulkUrbIntern(Usb.BulkUrb urb)
		{
#if DEBUG
			if(urb.IsShortNotOk || urb.IsZeroPacket)
				throw new NotSupportedException();
#endif
			int c = urb.BufferLength;
			Ioctl.usbdevfs_bulktransfer_ptr burb;
			byte[] buf;
			// UsbFS always wants a buffer,
			// even if buffer_length==0
			if(c == 0) buf = dummy;
			else       buf = urb.TransferBuffer;
			burb.timeout = BulkTimeout;
			burb.ep = urb.EndpointAddress;
			burb.len = (uint)c;
			int ret;
			unsafe
			{
				fixed(byte* bufp = buf)
				{
					burb.data = new IntPtr(bufp);
					ret = usbfs.BulkTransfer(ref burb);
				}
			}
			if(ret < 0) goto stall;
			urb.BufferActual = ret;
			urb.Ack();
			return;
		stall: urb.Stall(); return;
		}

		private static readonly byte[] dummy = new byte[4];
	}
}
