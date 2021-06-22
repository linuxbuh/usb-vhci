/*
 * LocalHcd.cs -- VHCI related classes
 *
 * Copyright (C) 2010-2016 Michael Singer <michael@a-singer.de>
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
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;

namespace Vhci.Windows
{
	public class LocalHcd : Vhci.LocalHcd
	{
		private struct PortInf
		{
			public byte Address;
			public PortStat PortStat;
			public Usb.DeviceDescriptor Device;

			public PortInf(byte addr,
			               PortStat stat,
			               Usb.DeviceDescriptor device)
			{
				Address = addr;
				PortStat = stat;
				Device = device;
			}
		}

		private Microsoft.Win32.SafeHandles.SafeFileHandle h;
		private int id, usbBusNum;
		private string busId;
		private PortInf[] portInf;

		public LocalHcd(byte ports) : base(ports)
		{
			Guid guid = Ioctl.GUID_DEVINTERFACE_VIRTUSB_BUS;
			string devicePath;

			// Open a handle to the device interface information set of all
			// present virtual usb bus enumerator interfaces.
			IntPtr hardwareDeviceInfo = Ioctl.
				SetupDiGetClassDevs(ref guid,
				                    IntPtr.Zero,
				                    IntPtr.Zero,
				                    Ioctl.DIGCF_PRESENT |
				                    Ioctl.DIGCF_DEVICEINTERFACE);
			if(hardwareDeviceInfo == (IntPtr)(-1))
			{
				int errno = Marshal.GetLastWin32Error();
				throw new System.IO.IOException("SetupDiGetClassDevs failed: " +
				                                errno.ToString());
			}

			try
			{
				Ioctl.SP_DEVICE_INTERFACE_DATA deviceInterfaceData;
				deviceInterfaceData.cbSize =
					Ioctl.SP_DEVICE_INTERFACE_DATA_SIZE;
				deviceInterfaceData.Flags = 0;
				deviceInterfaceData.InterfaceClassGuid = Guid.Empty;
				deviceInterfaceData.Reserved = IntPtr.Zero;
				if(Ioctl.SetupDiEnumDeviceInterfaces(hardwareDeviceInfo,
				                                     IntPtr.Zero,
				                                     ref guid,
				                                     0,
				                                     ref deviceInterfaceData))
				{
					uint requiredLength;
					Ioctl.SetupDiGetDeviceInterfaceDetail
						(hardwareDeviceInfo,
						 ref deviceInterfaceData,
						 IntPtr.Zero,
						 0,
						 out requiredLength,
						 IntPtr.Zero);
					int errno = Marshal.GetLastWin32Error();
					if(errno != Ioctl.ERROR_INSUFFICIENT_BUFFER)
					{
						throw new System.IO.IOException
							("SetupDiGetDeviceInterfaceDetail #1 failed: " +
							 errno.ToString());
					}
					uint predictedLength = requiredLength;
					unsafe
					{
						fixed(void* pin = new byte[predictedLength])
						{
							Ioctl.SP_DEVICE_INTERFACE_DETAIL_DATA*
								deviceInterfaceDetailData =
									(Ioctl.SP_DEVICE_INTERFACE_DETAIL_DATA*)pin;
							deviceInterfaceDetailData->cbSize =
								Ioctl.SP_DEVICE_INTERFACE_DETAIL_DATA_SIZE;
							if(!Ioctl.SetupDiGetDeviceInterfaceDetail
							   (hardwareDeviceInfo,
							    ref deviceInterfaceData,
							    (IntPtr)deviceInterfaceDetailData,
							    predictedLength,
							    out requiredLength,
							    IntPtr.Zero))
							{
								errno = Marshal.GetLastWin32Error();
								throw new System.IO.IOException
									("SetupDiGetDeviceInterfaceDetail #2" +
									 " failed: " + errno.ToString());
							}
							// Finally, we have the device path
							devicePath = Marshal.PtrToStringAuto
								((IntPtr)deviceInterfaceDetailData->DevicePath);
						}
					}
				}
				else
				{
					int errno = Marshal.GetLastWin32Error();
					if(errno == Ioctl.ERROR_NO_MORE_ITEMS)
					{
						throw new System.IO.IOException
							("Inteface GUID_DEVINTERFACE_VIRTUSB_BUS" +
							 " is not registered");
					}
					else
					{
						throw new System.IO.IOException
							("SetupDiEnumDeviceInterfaces failed: " +
							 errno.ToString());
					}
				}
			}
			finally
			{
				Ioctl.SetupDiDestroyDeviceInfoList(hardwareDeviceInfo);
			}

			Debug.WriteLine("Opening " + devicePath);
			h = Ioctl.CreateFile(devicePath,
			                     Ioctl.GENERIC_READ |
			                     Ioctl.GENERIC_WRITE,
			                     Ioctl.FILE_SHARE_READ |
			                     Ioctl.FILE_SHARE_WRITE,
			                     IntPtr.Zero,
			                     Ioctl.OPEN_EXISTING,
			                     Ioctl.FILE_ATTRIBUTE_NORMAL |
			                     Ioctl.FILE_FLAG_OVERLAPPED,
			                     IntPtr.Zero);
			if(h.IsInvalid)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno == Ioctl.ERROR_FILE_NOT_FOUND)
					throw new System.IO.FileNotFoundException("", devicePath);
				if(errno == Ioctl.ERROR_ACCESS_DENIED)
					throw new System.UnauthorizedAccessException
						("Couldn't open " + devicePath);
				throw new System.IO.IOException("Couldn't open " +
				                                devicePath +
				                                ". (" + errno.ToString() + ")");
			}
			ManualResetEvent ev = new ManualResetEvent(false);
			NativeOverlapped ov;
			ov.InternalLow = IntPtr.Zero;
			ov.InternalHigh = IntPtr.Zero;
			ov.OffsetLow = 0;
			ov.OffsetHigh = 0;
			ov.EventHandle = ev.SafeWaitHandle.DangerousGetHandle();
			Ioctl.VIRTUSB_REGISTER reg;
			uint id, inf;
			byte c = PortCount;
			reg.Version = 1;
			reg.PortCount = c;
			unsafe
			{
				if(!Ioctl.DeviceIoControl(h,
				                          Ioctl.VIRTUSB_IOCREGISTER,
				                          &reg,
				                          Ioctl.VIRTUSB_REGISTER_SIZE,
				                          &id,
				                          sizeof(uint),
				                          &inf,
				                          &ov))
				{
					int errno = Marshal.GetLastWin32Error();
					if(errno == Ioctl.ERROR_IO_PENDING)
					{
						ev.WaitOne();
						if(Ioctl.GetOverlappedResult(h,
						                             &ov,
						                             &inf,
						                             false))
						{
							goto noErr;
						}
						errno = Marshal.GetLastWin32Error();
					}
					h.Dispose();
					ev.Close();
					throw new System.IO.IOException
						("Couldn't register new host controller. (" +
						 errno.ToString() + ")");
				 noErr:;
				}
			}
			ev.Close();
			if(inf == sizeof(uint))
				this.id = unchecked((int)id);
			else
				this.id = 0;
			busId = "<unknown>";
			usbBusNum = 0;
			if(c > 0)
			{
				portInf = new PortInf[c];
				for(byte i = 0; i < c; i++)
				{
					portInf[i].Address = 0xff;
					portInf[i].PortStat = PortStat.Zero;
				}
			}
			InitBGThread();
		}

		protected override void Dispose(bool disposing)
		{
			JoinBGThread();
			if(h != null && !h.IsInvalid)
			{
				// close device file
				h.Dispose();
			}
			base.Dispose(disposing);
		}

		// caller has Lock
		protected override byte AddressFromPort(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			return portInf[port - 1].Address;
		}

		// caller has Lock
		protected override byte PortFromAddress(byte address)
		{
			if(address > 0x7f)
				throw new ArgumentException("", "address");
			byte c = PortCount;
			for(byte i = 0; i < c; i++)
				if(portInf[i].Address == address) return (byte)(i + 1);
			return 0;
		}

		protected override sealed void BGWork()
		{
			ManualResetEvent ev = new ManualResetEvent(false);
			NativeOverlapped ov;
			ov.InternalLow = IntPtr.Zero;
			ov.InternalHigh = IntPtr.Zero;
			ov.OffsetLow = 0;
			ov.OffsetHigh = 0;
			ov.EventHandle = ev.SafeWaitHandle.DangerousGetHandle();
			uint inf;
			int errno = 0;
			bool ret;
			Ioctl.VIRTUSB_WORK work;
			unsafe
			{
				ret = Ioctl.DeviceIoControl(h,
				                            Ioctl.VIRTUSB_IOCFETCHWORK,
				                            null,
				                            0U,
				                            &work,
				                            Ioctl.VIRTUSB_WORK_SIZE,
				                            &inf,
				                            &ov);
				if(!ret)
				{
					errno = Marshal.GetLastWin32Error();
					if(errno == Ioctl.ERROR_IO_PENDING)
					{
						int timeout = 100;
					 waitAgain:
						if(ev.WaitOne(timeout, false))
						{
							ret = Ioctl.GetOverlappedResult(h,
							                                &ov,
							                                &inf,
							                                false);
							if(!ret || inf != Ioctl.VIRTUSB_WORK_SIZE)
							{
								errno = Marshal.GetLastWin32Error();
							}
						}
						else
						{
							if(!Ioctl.CancelIo(h))
							{
								errno = Marshal.GetLastWin32Error();
								Debug.WriteLine("Cancel FETCH_WORK failed: " +
								                errno.ToString());
							}
							timeout = Timeout.Infinite;
							goto waitAgain;
						}
					}
				}
			}
			if(!ret || inf != Ioctl.VIRTUSB_WORK_SIZE)
			{
				if(errno != Ioctl.ERROR_OPERATION_ABORTED)
					Debug.WriteLine("FETCH_WORK failed: " + errno.ToString());
				return;
			}
			byte index;
			switch(work.Type)
			{
			case Ioctl.VIRTUSB_WORK_TYPE_PORT_STAT:
				index = work.Work.PortStat.PortIndex;
				ushort status = work.Work.PortStat.Status;
				ushort change = work.Work.PortStat.Change;
				byte flags = work.Work.PortStat.Flags;
				Debug.WriteLine
					("FETCH_WORK was successful and fetched PORT_STAT: [index="
					 + index + "] [status=0x"
					 + status.ToString("x4") + "] [change=0x"
					 + change.ToString("x4") + "] [flags=0x"
					 + flags.ToString("x2") + "]");
				if(index == 0 || index > PortCount)
				{
					Debug.WriteLine("PORT_STAT: INDEX OUT OF RANGE!");
					break;
				}
				if((status & ~Ioctl.VIRTUSB_PORT_STAT_MASK) != 0)
				{
					Debug.WriteLine("PORT_STAT: invalid status flags set");
					break;
				}
				if((change & ~Ioctl.VIRTUSB_PORT_STAT_C_MASK) != 0)
				{
					Debug.WriteLine("PORT_STAT: invalid change flags set");
					break;
				}
				if((flags & ~Ioctl.VIRTUSB_PORT_STAT_FLAGS_RESUMING) != 0)
				{
					Debug.WriteLine("PORT_STAT: invalid flags set");
					break;
				}
				PortStat nps = new PortStat((PortStatStatusFlags)status,
				                            (PortStatChangeFlags)change,
				                            (PortStatFlags)flags);
				lock(Lock)
				{
					EnqueueWork(new PortStatWork(index, nps,
					                             portInf[index - 1].PortStat));
					portInf[index - 1].PortStat = nps;
					if(nps.ConnectionChanged)
					{
						// If the CONNECTION status of this port
						// changes, then invalidate address.
						portInf[index - 1].Address = 0xff;
						Debug.WriteLine("PORT_STAT: Invalidated address of " +
						                "port " + index +
						                " after CONNECT changed.");
						if(!nps.Connection) portInf[index - 1].Device = null;
					}
					if(nps.ResetChanged && !nps.Reset && nps.Enable)
					{
						// If RESET was successful then assign address 0.
						portInf[index - 1].Address = 0x00;
						Debug.WriteLine("PORT_STAT: Set address of port " +
						                index + " to 0x00 after RESET.");
					}
					if(nps.Reset)
					{
						// For some unknown reason, we sometimes get control requests for
						// address 0 too early, before the reset is complete. Therefore,
						// we set the address to 0 here.
						portInf[index - 1].Address = 0x00;
						Debug.WriteLine("PORT_STAT: Set address of port " +
						                index + " to 0x00 before RESET.");
					}
					// TODO: Are there any more status changes that invalidate the address?
					OnWorkEnqueued(null);
					Debug.WriteLine("PORT_STAT updated successfully.");
				}
				break;
			case Ioctl.VIRTUSB_WORK_TYPE_PROCESS_URB:
				Debug.WriteLine("FETCH_WORK was successful and " +
				                "fetched PROCESS_URB.");
				byte address = work.Work.Urb.Address;
				uint len = work.Work.Urb.BufferLength;
				byte[] buffer = (len > 0) ? new byte[len] : null;
				byte epadr = work.Work.Urb.Endpoint;
				bool isin = (epadr & 0x80) != 0x00;
				bool isiso = work.Work.Urb.Type == Ioctl.VIRTUSB_URB_TYPE_ISO;
				ushort uflags = work.Work.Urb.Flags;
				uint isopktc = isiso ? work.Work.Urb.PacketCount : 0U;
				uint udataBufferSize = Ioctl.SIZEOF_VIRTUSB_URB_DATA(isopktc);
				byte[] udataBuffer = new byte[udataBufferSize];
				if((!isin && buffer != null) || (isopktc > 0))
				{
					ov.InternalLow = IntPtr.Zero;
					ov.InternalHigh = IntPtr.Zero;
					ov.OffsetLow = 0;
					ov.OffsetHigh = 0;
					unsafe
					{
						fixed(byte* bufPin = buffer)
						fixed(void* udataPin = udataBuffer)
						{
							Ioctl.VIRTUSB_URB_DATA* udata =
								(Ioctl.VIRTUSB_URB_DATA*)udataPin;
							udata->Handle = work.Work.Urb.Handle;
							udata->PacketCount = isopktc;
							ret = Ioctl.DeviceIoControl(h,
							                            Ioctl.
							                            VIRTUSB_IOCFETCHDATA,
							                            udataPin,
							                            udataBufferSize,
							                            bufPin,
							                            len,
							                            &inf,
							                            &ov);
							if(!ret)
							{
								errno = Marshal.GetLastWin32Error();
								if(errno == Ioctl.ERROR_IO_PENDING)
								{
									ev.WaitOne();
									ret = Ioctl.GetOverlappedResult(h,
									                                &ov,
									                                &inf,
									                                false);
									if(!ret || inf != len)
									{
										errno = Marshal.GetLastWin32Error();
									}
								}
							}
						}
					}
					if(!ret || inf != len)
					{
						if(errno == Ioctl.ERROR_REQUEST_ABORTED)
						{
							Debug.WriteLine("URB was canceled between " +
							                "FETCH_WORK and FETCH_DATA.");
						}
						else
						{
							Debug.WriteLine("FETCH_DATA failed. " /*+
							                "Dumping URB..."*/);
							// TODO: Dump
						}
						break;
					}
				}
				lock(Lock)
				{
					index = PortFromAddress(address);
					Debug.WriteLine("address=" + address.ToString() +
					                " port=" + index.ToString());
					if(index == 0)
					{
						Debug.WriteLine("PROCESS_URB: Invalid device " +
						                "address: " + address /*+
						                "Dumping URB..."*/);
						// TODO: Dump
						break;
					}
					Usb.Endpoint ep =
						portInf[index - 1].Device.TryGetEndpoint(epadr);
					if(ep == null)
					{
						Debug.WriteLine("PROCESS_URB: Invalid endpoint " +
						                "address. " /*+
						                "Dumping URB..."*/);
						// TODO: Dump
						break;
					}
					Usb.EndpointType eptype = ep.Type;
					Usb.EndpointType urbtype;
					switch(work.Work.Urb.Type)
					{
					case Ioctl.VIRTUSB_URB_TYPE_ISO:
						urbtype = Usb.EndpointType.Isochronous;
						break;
					case Ioctl.VIRTUSB_URB_TYPE_BULK:
						urbtype = Usb.EndpointType.Bulk;
						break;
					case Ioctl.VIRTUSB_URB_TYPE_INT:
						urbtype = Usb.EndpointType.Interrupt;
						break;
					case Ioctl.VIRTUSB_URB_TYPE_CONTROL:
						urbtype = Usb.EndpointType.Control;
						break;
					default: throw new InvalidOperationException();
					}
					if(eptype != urbtype)
					{
						if(eptype == Usb.EndpointType.Interrupt &&
						   urbtype == Usb.EndpointType.Bulk)
						{
							/* ok. qemu does this. */
						}
						else
						{
							Debug.WriteLine("PROCESS_URB: Wrong endpoint type. " /*+
							                "Dumping URB..."*/);
							// TODO: Dump
							break;
						}
					}
					Usb.Urb urb;
					switch(eptype)
					{
					case Usb.EndpointType.Isochronous:
						Console.Write("I");
						Usb.IsochronousPacket[] isos =
							new Usb.IsochronousPacket[isopktc];
						unsafe
						{
							fixed(void* udataPin = udataBuffer)
							{
								Ioctl.VIRTUSB_URB_DATA* udata =
									(Ioctl.VIRTUSB_URB_DATA*)udataPin;
								Ioctl.VIRTUSB_ISO_PACKET_DATA* pkt =
									(Ioctl.VIRTUSB_ISO_PACKET_DATA*)
										udata->IsoPackets;
								for(int i = 0; i < isopktc; i++)
									isos[i] =
										unchecked(new Usb.IsochronousPacket
										          ((int)pkt[i].Offset,
										           (int)pkt[i].PacketLength));
							}
						}
						urb = new Usb.IsochronousUrb(unchecked((long)work.Work.
						                                       Urb.Handle),
						                             buffer, 0,
						                             Usb.UrbStatus.Pending,
						                             Usb.UrbFlags.None, epadr,
						                             (Usb.IsochronousEndpoint)
						                             ep, work.Work.Urb.Interval,
						                             0, isos);
						break;
					case Usb.EndpointType.Bulk:
						Console.Write("b");
						// TODO: is this right?
						bool shortNotOk = isin &&
							(uflags &
							 Ioctl.VIRTUSB_URB_FLAGS_SHORT_TRANSFER_OK) == 0;
						bool zeroPacket = !isin &&
							(uflags &
							 Ioctl.VIRTUSB_URB_FLAGS_SHORT_TRANSFER_OK) == 0;
						Usb.UrbFlags uflags2 = (shortNotOk ?
						                        Usb.UrbFlags.ShortNotOk :
						                        Usb.UrbFlags.None) |
						                       (zeroPacket ?
						                        Usb.UrbFlags.ZeroPacket :
						                        Usb.UrbFlags.None);
						urb = new Usb.BulkUrb(unchecked((long)work.Work.
						                                Urb.Handle),
						                      buffer, 0,
						                      Usb.UrbStatus.Pending,
						                      uflags2, epadr,
						                      (Usb.BulkEndpoint)ep);
						break;
					case Usb.EndpointType.Interrupt:
						Console.Write("i");
						urb = new Usb.InterruptUrb(unchecked((long)work.Work.
						                                     Urb.Handle),
						                           buffer, 0,
						                           Usb.UrbStatus.Pending,
						                           Usb.UrbFlags.None, epadr,
						                           (Usb.InterruptEndpoint)ep,
						                           work.Work.Urb.Interval);
						break;
					case Usb.EndpointType.Control:
						Console.Write("c");
						urb = unchecked(new Usb.ControlUrb
						                ((long)work.Work.Urb.Handle, buffer, 0,
						                 Usb.UrbStatus.Pending,
						                 Usb.UrbFlags.None, epadr,
						                 (Usb.ControlEndpoint)ep,
						                 work.Work.Urb.
						                 SetupPacket.bmRequestType,
						                 work.Work.Urb.SetupPacket.bRequest,
						                 (short)work.Work.Urb.
						                 SetupPacket.wValue,
						                 (short)work.Work.Urb.
						                 SetupPacket.wIndex,
						                 (short)work.Work.Urb.
						                 SetupPacket.wLength));
						// SET_ADDRESS?
						if((epadr & 0x0f) == 0x00 &&
						   work.Work.Urb.SetupPacket.bmRequestType == 0x00 &&
						   work.Work.Urb.SetupPacket.bRequest == 5)
						{
							Debug.WriteLine("PROCESS_URB: port " + index +
							                " received SET_ADDRESS with " +
							                "address " +
							                work.Work.Urb.SetupPacket.wValue);
							if(work.Work.Urb.SetupPacket.wValue > 0x7f)
								urb.Stall();
							else
							{
								urb.Ack();
								portInf[index - 1].Address =
									unchecked((byte)work.Work.Urb.SetupPacket.
									          wValue);
							}
						}
						break;
					default: throw new InvalidOperationException();
					}
					EnqueueWork(new ProcessUrbWork(index, urb));
					OnWorkEnqueued(null);
					Debug.WriteLine("PROCESS_URB queued.");
				}
				break;
			case Ioctl.VIRTUSB_WORK_TYPE_CANCEL_URB:
				Debug.WriteLine("FETCH_WORK was successful and " +
				                "fetched CANCEL_URB.");
				CancelProcessUrbWork(unchecked((long)work.Work.Urb.Handle));
				break;
			default:
				Debug.WriteLine("FETCH_WORK returns with " +
				                "invalid type of work: " + work.Type);
				break;
			}
			ev.Close();
		}

		public override int VhciID
		{
			get { return id; }
		}

		public override string BusID
		{
			get { return busId; }
		}

		public override int UsbBusNum
		{
			get { return usbBusNum; }
		}

		// caller has Lock
		protected override void CancelingWork(Work work, bool inProgress)
		{
			if(inProgress && (work is ProcessUrbWork))
			{
				ProcessUrbWork w = (ProcessUrbWork)work;
				Usb.Urb urb = w.Urb;
				EnqueueWork(new CancelUrbWork(w.Port, urb.Handle));
				OnWorkEnqueued(null);
				Debug.WriteLine("CANCEL_URB queued.");
			}
		}

		// caller has Lock
		protected override void FinishingWork(Work work)
		{
			if(work is ProcessUrbWork)
			{
				ProcessUrbWork w = (ProcessUrbWork)work;
				Usb.Urb urb = w.Urb;
				uint isopktc = 0U;
				Usb.IsochronousPacket[] isos = null;
				bool isIso = urb is Usb.IsochronousUrb;
				if(isIso)
				{
					Usb.IsochronousUrb iso = (Usb.IsochronousUrb)urb;
					isopktc = unchecked((uint)iso.PacketCount);
					isos = iso.IsoPackets;
				}
				uint gbBufferSize = Ioctl.SIZEOF_VIRTUSB_GIVEBACK(isopktc);
				byte[] gbBuffer = new byte[gbBufferSize];
				ManualResetEvent ev = new ManualResetEvent(false);
				NativeOverlapped ov;
				ov.InternalLow = IntPtr.Zero;
				ov.InternalHigh = IntPtr.Zero;
				ov.OffsetLow = 0;
				ov.OffsetHigh = 0;
				ov.EventHandle = ev.SafeWaitHandle.DangerousGetHandle();
				bool ret;
				uint inf;
				int errno = 0;
				unsafe
				{
					fixed(void* gbPin = gbBuffer)
					{
						Ioctl.VIRTUSB_GIVEBACK* gb =
							(Ioctl.VIRTUSB_GIVEBACK*)gbPin;
						gb->Handle = unchecked((ulong)urb.Handle);
						gb->Status = Usb.Windows.USBD_STATUS.
							ToUsbdStatus(urb.Status, isIso);
						gb->BufferActual = unchecked((uint)urb.BufferActual);
						gb->PacketCount = 0U;
						gb->ErrorCount = 0U;
#if DEBUG
						Debug.WriteLine("gb->Handle = 0x" +
						                gb->Handle.ToString("x16") +
						                "\ngb->Status = " + gb->Status);
						System.Text.StringBuilder sb =
							new System.Text.StringBuilder();
						sb.AppendLine("URB DUMP:");
						urb.Dump(sb, true, false);
						Debug.Write(sb.ToString());
#endif
						byte[] buf = null;
						Ioctl.VIRTUSB_ISO_PACKET_GIVEBACK* isobuf =
							(Ioctl.VIRTUSB_ISO_PACKET_GIVEBACK*)
								gb->IsoPackets;
						if(urb.IsIn && gb->BufferActual > 0)
							buf = urb.TransferBuffer;
						if(isIso)
						{
							Usb.IsochronousUrb iso = (Usb.IsochronousUrb)urb;
							gb->PacketCount = isopktc;
							gb->ErrorCount = unchecked((uint)iso.ErrorCount);
							for(int i = 0; i < isopktc; i++)
							{
								isobuf[i].Status = Usb.Windows.USBD_STATUS.
									ToIsoPacketsUsbdStatus(isos[i].Status);
								isobuf[i].PacketActual =
									unchecked((uint)isos[i].PacketActual);
							}
						}
						fixed(byte* bufp = buf)
						{
							ret = Ioctl.DeviceIoControl(h,
							                            Ioctl.
							                            VIRTUSB_IOCGIVEBACK,
							                            gbPin,
							                            gbBufferSize,
							                            bufp,
							                            urb.IsIn ?
							                            gb->BufferActual : 0U,
							                            &inf,
							                            &ov);
							if(!ret)
							{
								errno = Marshal.GetLastWin32Error();
								if(errno == Ioctl.ERROR_IO_PENDING)
								{
									ev.WaitOne();
									ret = Ioctl.GetOverlappedResult(h,
									                                &ov,
									                                &inf,
									                                false);
									if(!ret || inf != 0)
									{
										errno = Marshal.GetLastWin32Error();
									}
								}
							}
						}
					}
				}
				if(!ret || inf != 0)
				{
					if(errno != Ioctl.ERROR_REQUEST_ABORTED)
						Debug.WriteLine("Giveback failed: " +
						                errno.ToString());
				}
				ev.Close();
			}
		}

		public override PortStat GetPortStat(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			lock(Lock) return portInf[port - 1].PortStat;
		}

		private unsafe void CallIocPortStat([In] Ioctl.VIRTUSB_PORT_STAT* ps)
		{
			ManualResetEvent ev = new ManualResetEvent(false);
			NativeOverlapped ov;
			ov.InternalLow = IntPtr.Zero;
			ov.InternalHigh = IntPtr.Zero;
			ov.OffsetLow = 0;
			ov.OffsetHigh = 0;
			ov.EventHandle = ev.SafeWaitHandle.DangerousGetHandle();
			bool ret;
			uint inf;
			int errno = 0;
			unsafe
			{
				ret = Ioctl.DeviceIoControl(h,
				                          Ioctl.VIRTUSB_IOCPORTSTAT,
				                          ps,
				                          Ioctl.VIRTUSB_PORT_STAT_SIZE,
				                          null,
				                          0,
				                          &inf,
				                          &ov);
				if(!ret)
				{
					errno = Marshal.GetLastWin32Error();
					if(errno == Ioctl.ERROR_IO_PENDING)
					{
						ev.WaitOne();
						ret = Ioctl.GetOverlappedResult(h,
						                                &ov,
						                                &inf,
						                                false);
						if(!ret || inf != 0)
						{
							errno = Marshal.GetLastWin32Error();
						}
					}
				}
			}
			if(!ret || inf != 0)
			{
				Debug.WriteLine("Set portstat failed: " + errno.ToString());
			}
			ev.Close();
		}

		public override void PortConnect(byte port,
		                                 Usb.DeviceDescriptor device,
		                                 Usb.DataRate dataRate)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			if(device == null) throw new ArgumentNullException("device");
			if(dataRate != Usb.DataRate.Full &&
			   dataRate != Usb.DataRate.Low &&
			   dataRate != Usb.DataRate.High)
				throw new ArgumentException("", "dataRate");
			bool lowspeed = dataRate == Usb.DataRate.Low;
			bool highspeed = dataRate == Usb.DataRate.High;
			Ioctl.VIRTUSB_PORT_STAT ps;
			ps.Status = Ioctl.VIRTUSB_PORT_STAT_CONNECT;
			ps.Status |=
				lowspeed ? Ioctl.VIRTUSB_PORT_STAT_LOW_SPEED : (ushort)0U;
			ps.Status |=
				highspeed ? Ioctl.VIRTUSB_PORT_STAT_HIGH_SPEED : (ushort)0U;
			ps.Change = Ioctl.VIRTUSB_PORT_STAT_C_CONNECT;
			ps.PortIndex = port;
			ps.Flags = 0;
			ps._reserved1 = 0;
			ps._reserved2 = 0;
			lock(Lock)
			{
				unsafe { CallIocPortStat(&ps); }
				portInf[port - 1].Device = device;
			}
		}

		public override void PortDisconnect(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.VIRTUSB_PORT_STAT ps;
			ps.Status = 0;
			ps.Change = Ioctl.VIRTUSB_PORT_STAT_C_CONNECT;
			ps.PortIndex = port;
			ps.Flags = 0;
			ps._reserved1 = 0;
			ps._reserved2 = 0;
			unsafe { CallIocPortStat(&ps); }
		}

		public override void PortDisable(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.VIRTUSB_PORT_STAT ps;
			ps.Status = 0;
			ps.Change = Ioctl.VIRTUSB_PORT_STAT_C_ENABLE;
			ps.PortIndex = port;
			ps.Flags = 0;
			ps._reserved1 = 0;
			ps._reserved2 = 0;
			unsafe { CallIocPortStat(&ps); }
		}

		public override void PortResumed(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.VIRTUSB_PORT_STAT ps;
			ps.Status = 0;
			ps.Change = Ioctl.VIRTUSB_PORT_STAT_C_SUSPEND;
			ps.PortIndex = port;
			ps.Flags = 0;
			ps._reserved1 = 0;
			ps._reserved2 = 0;
			unsafe { CallIocPortStat(&ps); }
		}

		public override void PortOvercurrent(byte port, bool setOC)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.VIRTUSB_PORT_STAT ps;
			ps.Status =
				setOC ? Ioctl.VIRTUSB_PORT_STAT_OVER_CURRENT : (ushort)0U;
			ps.Change = Ioctl.VIRTUSB_PORT_STAT_C_OVER_CURRENT;
			ps.PortIndex = port;
			ps.Flags = 0;
			ps._reserved1 = 0;
			ps._reserved2 = 0;
			unsafe { CallIocPortStat(&ps); }
		}

		public override void PortResetDone(byte port, bool enable)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.VIRTUSB_PORT_STAT ps;
			ps.Status = enable ? Ioctl.VIRTUSB_PORT_STAT_ENABLE : (ushort)0U;
			ps.Change = (ushort)(uint)
				(Ioctl.VIRTUSB_PORT_STAT_C_RESET |
				 (enable ? (ushort)0U : Ioctl.VIRTUSB_PORT_STAT_C_ENABLE));
			// could also do this instead:
			//ps.Status = enable ? Ioctl.VIRTUSB_PORT_STAT_ENABLE : (ushort)0U;
			//ps.Change = Ioctl.VIRTUSB_PORT_STAT_C_RESET;
			ps.PortIndex = port;
			ps.Flags = 0;
			ps._reserved1 = 0;
			ps._reserved2 = 0;
			unsafe { CallIocPortStat(&ps); }
		}
	}
}
