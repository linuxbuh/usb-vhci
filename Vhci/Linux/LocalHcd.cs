/*
 * LocalHcd.cs -- VHCI related classes
 *
 * Copyright (C) 2007-2009 Conemis AG Karlsruhe Germany
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
using System.Collections.Generic;
using System.Diagnostics;

namespace Vhci.Linux
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

		private int fd, id, usbBusNum;
		private string busId;
		private PortInf[] portInf;

		public LocalHcd(byte ports) : base(ports)
		{
			fd = Ioctl.open(Ioctl.VhciCtrlFile, Ioctl.O_RDWR);
			if(fd == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno == 2) // ENOENT
					throw new System.IO.FileNotFoundException
						("", Ioctl.VhciCtrlFile);
				throw new System.IO.IOException("Couldn't open " +
				                                Ioctl.VhciCtrlFile +
				                                ". (" + errno.ToString() + ")");
			}
			Ioctl.vhci_ioc_register reg;
			byte c = PortCount;
			reg.port_count = c;
			reg.id = 0;
			reg.usb_busnum = 0;
			reg.bus_id = null;
			if(Ioctl.ioctl(fd, Ioctl.IOCREGISTER, ref reg) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				while(Ioctl.close(fd) == -1 &&
				      Marshal.GetLastWin32Error() == 4); // EINTR
				if(errno == 12) // ENOMEM
					throw new InsufficientMemoryException();
				throw new System.IO.IOException
					("Couldn't register new host controller. (" +
					 errno.ToString() + ")");
			}
			id = reg.id;
			busId = reg.bus_id;
			usbBusNum = reg.usb_busnum;
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
			if(fd != -1)
			{
				while(Ioctl.close(fd) == -1 &&
				      Marshal.GetLastWin32Error() == 4); // EINTR
				fd = -1;
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
			Ioctl.vhci_ioc_work work;
			if(Ioctl.ioctl(fd, Ioctl.IOCFETCHWORK, out work) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				if(errno != 110 && // ETIMEDOUT
				   errno != 4 &&   // EINTR
				   errno != 61)    // ENODATA
					Debug.WriteLine("FETCH_WORK failed: " + errno.ToString());
				return;
			}
			byte index;
			switch(work.type)
			{
			case Ioctl.VHCI_IOC_WORK_TYPE_PORT_STAT:
				index = work.work.port.index;
				ushort status = work.work.port.status;
				ushort change = work.work.port.change;
				byte flags = work.work.port.flags;
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
				if((status & ~Ioctl.USB_PORT_STAT_MASK) != 0)
				{
					Debug.WriteLine("PORT_STAT: invalid status flags set");
					break;
				}
				if((change & ~Ioctl.USB_PORT_STAT_C_MASK) != 0)
				{
					Debug.WriteLine("PORT_STAT: invalid change flags set");
					break;
				}
				if((flags & ~Ioctl.VHCI_IOC_PORT_STAT_FLAGS_RESUMING) != 0)
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
					// TODO: Are there any more status changes that invalidate the address?
					OnWorkEnqueued(null);
					Debug.WriteLine("PORT_STAT updated successfully.");
				}
				break;
			case Ioctl.VHCI_IOC_WORK_TYPE_PROCESS_URB:
				Debug.WriteLine("FETCH_WORK was successful and " +
				                "fetched PROCESS_URB.");
				byte address = work.work.urb.address;
				int len = work.work.urb.buffer_length;
				byte[] buffer = (len > 0) ? new byte[len] : null;
				byte epadr = work.work.urb.endpoint;
				bool isin = (epadr & 0x80) != 0x00;
				bool isiso = work.work.urb.type == Ioctl.VHCI_IOC_URB_TYPE_ISO;
				ushort uflags = work.work.urb.flags;
				int isopktc = isiso ? work.work.urb.packet_count : 0;
				Ioctl.vhci_ioc_iso_packet_data[] iso_packets = (isopktc > 0) ?
					new Ioctl.vhci_ioc_iso_packet_data[isopktc] : null;
				if((!isin && buffer != null) || (iso_packets != null))
				{
					Ioctl.vhci_ioc_urb_data udata;
					udata.handle = work.handle;
					udata.buffer_length = len;
					udata.packet_count = isopktc;
					int ret;
					unsafe
					{
						fixed(byte* bufPin = buffer)
						fixed(void* isoPin = iso_packets)
						{
							udata.buffer = new IntPtr(bufPin);
							udata.iso_packets = new IntPtr(isoPin);
							ret = Ioctl.ioctl(fd, Ioctl.IOCFETCHDATA,
							                  ref udata);
						}
					}
					if(ret == -1)
					{
						int errno = Marshal.GetLastWin32Error();
						if(errno == 125) // ECANCELED
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
					switch(work.work.urb.type)
					{
					case Ioctl.VHCI_IOC_URB_TYPE_ISO:
						urbtype = Usb.EndpointType.Isochronous;
						break;
					case Ioctl.VHCI_IOC_URB_TYPE_BULK:
						urbtype = Usb.EndpointType.Bulk;
						break;
					case Ioctl.VHCI_IOC_URB_TYPE_INT:
						urbtype = Usb.EndpointType.Interrupt;
						break;
					case Ioctl.VHCI_IOC_URB_TYPE_CONTROL:
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
						for(int i = 0; i < isopktc; i++)
							isos[i] = unchecked(new Usb.IsochronousPacket
							                    ((int)iso_packets[i].offset,
							                     (int)iso_packets[i].
							                     packet_length));
						urb = new Usb.IsochronousUrb(unchecked((long)
						                                       work.handle),
						                             buffer, 0,
						                             Usb.UrbStatus.Pending,
						                             Usb.UrbFlags.None, epadr,
						                             (Usb.IsochronousEndpoint)
						                             ep, work.work.urb.interval,
						                             0, isos);
						break;
					case Usb.EndpointType.Bulk:
						Console.Write("b");
						bool shortNotOk =
							(uflags &
							 Ioctl.VHCI_IOC_URB_FLAGS_SHORT_NOT_OK) != 0;
						bool zeroPacket =
							(uflags &
							 Ioctl.VHCI_IOC_URB_FLAGS_ZERO_PACKET) != 0;
						Usb.UrbFlags uflags2 = (shortNotOk ?
						                        Usb.UrbFlags.ShortNotOk :
						                        Usb.UrbFlags.None) |
						                       (zeroPacket ?
						                        Usb.UrbFlags.ZeroPacket :
						                        Usb.UrbFlags.None);
						urb = new Usb.BulkUrb(unchecked((long)work.handle),
						                      buffer, 0,
						                      Usb.UrbStatus.Pending,
						                      uflags2, epadr,
						                      (Usb.BulkEndpoint)ep);
						break;
					case Usb.EndpointType.Interrupt:
						Console.Write("i");
						urb = new Usb.InterruptUrb(unchecked((long)work.handle),
						                           buffer, 0,
						                           Usb.UrbStatus.Pending,
						                           Usb.UrbFlags.None, epadr,
						                           (Usb.InterruptEndpoint)ep,
						                           work.work.urb.interval);
						break;
					case Usb.EndpointType.Control:
						Console.Write("c");
						urb = unchecked(new Usb.ControlUrb
						                ((long)work.handle, buffer, 0,
						                 Usb.UrbStatus.Pending,
						                 Usb.UrbFlags.None, epadr,
						                 (Usb.ControlEndpoint)ep,
						                 work.work.urb.setup_packet.
						                 bmRequestType,
						                 work.work.urb.setup_packet.
						                 bRequest,
						                 (short)work.work.urb.
						                 setup_packet.wValue,
						                 (short)work.work.urb.
						                 setup_packet.wIndex,
						                 (short)work.work.urb.
						                 setup_packet.wLength));
						// SET_ADDRESS?
						if((epadr & 0x0f) == 0x00 &&
						   work.work.urb.setup_packet.bmRequestType == 0x00 &&
						   work.work.urb.setup_packet.bRequest == 5)
						{
							Debug.WriteLine("PROCESS_URB: port " + index +
							                " received SET_ADDRESS with " +
							                "address " +
							                work.work.urb.setup_packet.wValue);
							if(work.work.urb.setup_packet.wValue > 0x7f)
								urb.Stall();
							else
							{
								urb.Ack();
								portInf[index - 1].Address =
									unchecked((byte)work.work.urb.setup_packet.
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
			case Ioctl.VHCI_IOC_WORK_TYPE_CANCEL_URB:
				Debug.WriteLine("FETCH_WORK was successful and " +
				                "fetched CANCEL_URB.");
				CancelProcessUrbWork(unchecked((long)work.handle));
				break;
			default:
				Debug.WriteLine("FETCH_WORK returns with " +
				                "invalid type of work: " + work.type);
				break;
			}
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
				bool isIso = urb is Usb.IsochronousUrb;
				Ioctl.vhci_ioc_giveback gb;
				gb.handle = unchecked((ulong)urb.Handle);
				gb.status = Usb.Linux.errno.ToErrno(urb.Status, isIso);
				gb.buffer_actual = urb.BufferActual;
				gb.buffer = IntPtr.Zero;
				gb.iso_packets = IntPtr.Zero;
				gb.packet_count = 0;
				gb.error_count = 0;
#if DEBUG
				Debug.WriteLine("gb.handle = 0x" + gb.handle.ToString("x16") +
				                "\ngb.status = " + gb.status);
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.AppendLine("URB DUMP:");
				urb.Dump(sb, true, false);
				Debug.Write(sb.ToString());
#endif
				byte[] buf = null;
				Ioctl.vhci_ioc_iso_packet_giveback[] isobuf = null;
				if(urb.IsIn && gb.buffer_actual > 0)
					buf = urb.TransferBuffer;
				if(isIso)
				{
					Usb.IsochronousUrb iso = (Usb.IsochronousUrb)urb;
					int pc = iso.PacketCount;
					isobuf = new Ioctl.vhci_ioc_iso_packet_giveback[pc];
					gb.packet_count = pc;
					gb.error_count = iso.ErrorCount;
					Usb.IsochronousPacket[] isos = iso.IsoPackets;
					for(int i = 0; i < pc; i++)
					{
						isobuf[i].status =
							Usb.Linux.errno.ToIsoPacketsErrno(isos[i].Status);
						isobuf[i].packet_actual =
							unchecked((uint)isos[i].PacketActual);
					}
				}
				int ret;
				unsafe
				{
					fixed(byte* bufp = buf)
					fixed(void* isobufp = isobuf)
					{
						gb.buffer = new IntPtr(bufp);
						gb.iso_packets = new IntPtr(isobufp);
						ret = Ioctl.ioctl(fd, Ioctl.IOCGIVEBACK, ref gb);
					}
				}
				if(ret == -1)
				{
					int errno = Marshal.GetLastWin32Error();
					if(errno != 125) // ECANCELED
						Debug.WriteLine("Giveback failed: " + errno.ToString());
				}
			}
		}

		public override PortStat GetPortStat(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			lock(Lock) return portInf[port - 1].PortStat;
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
			Ioctl.vhci_ioc_port_stat ps;
			ps.status = Ioctl.USB_PORT_STAT_CONNECTION;
			ps.status |= lowspeed ? Ioctl.USB_PORT_STAT_LOW_SPEED : (ushort)0U;
			ps.status |= highspeed ?
				Ioctl.USB_PORT_STAT_HIGH_SPEED : (ushort)0U;
			ps.change = Ioctl.USB_PORT_STAT_C_CONNECTION;
			ps.index = port;
			ps.flags = 0;
			lock(Lock)
			{
				if(Ioctl.ioctl(fd, Ioctl.IOCPORTSTAT, ref ps) == -1)
				{
					int errno = Marshal.GetLastWin32Error();
					Debug.WriteLine("Set portstat failed: " + errno.ToString());
				}
				portInf[port - 1].Device = device;
			}
		}

		public override void PortDisconnect(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.vhci_ioc_port_stat ps;
			ps.status = 0;
			ps.change = Ioctl.USB_PORT_STAT_C_CONNECTION;
			ps.index = port;
			ps.flags = 0;
			if(Ioctl.ioctl(fd, Ioctl.IOCPORTSTAT, ref ps) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				Debug.WriteLine("Set portstat failed: " + errno.ToString());
			}
		}

		public override void PortDisable(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.vhci_ioc_port_stat ps;
			ps.status = 0;
			ps.change = Ioctl.USB_PORT_STAT_C_ENABLE;
			ps.index = port;
			ps.flags = 0;
			if(Ioctl.ioctl(fd, Ioctl.IOCPORTSTAT, ref ps) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				Debug.WriteLine("Set portstat failed: " + errno.ToString());
			}
		}

		public override void PortResumed(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.vhci_ioc_port_stat ps;
			ps.status = 0;
			ps.change = Ioctl.USB_PORT_STAT_C_SUSPEND;
			ps.index = port;
			ps.flags = 0;
			if(Ioctl.ioctl(fd, Ioctl.IOCPORTSTAT, ref ps) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				Debug.WriteLine("Set portstat failed: " + errno.ToString());
			}
		}

		public override void PortOvercurrent(byte port, bool setOC)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.vhci_ioc_port_stat ps;
			ps.status = setOC ? Ioctl.USB_PORT_STAT_OVERCURRENT : (ushort)0U;
			ps.change = Ioctl.USB_PORT_STAT_C_OVERCURRENT;
			ps.index = port;
			ps.flags = 0;
			if(Ioctl.ioctl(fd, Ioctl.IOCPORTSTAT, ref ps) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				Debug.WriteLine("Set portstat failed: " + errno.ToString());
			}
		}

		public override void PortResetDone(byte port, bool enable)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			Ioctl.vhci_ioc_port_stat ps;
			ps.status = enable ? Ioctl.USB_PORT_STAT_ENABLE : (ushort)0U;
			ps.change = (ushort)(uint)
				(Ioctl.USB_PORT_STAT_C_RESET |
				 (enable ? (ushort)0U : Ioctl.USB_PORT_STAT_C_ENABLE));
			// could also do this instead:
			//ps.status = enable ? Ioctl.USB_PORT_STAT_ENABLE : (ushort)0U;
			//ps.change = Ioctl.USB_PORT_STAT_C_RESET;
			ps.index = port;
			ps.flags = 0;
			if(Ioctl.ioctl(fd, Ioctl.IOCPORTSTAT, ref ps) == -1)
			{
				int errno = Marshal.GetLastWin32Error();
				Debug.WriteLine("Set portstat failed: " + errno.ToString());
			}
		}
	}
}
