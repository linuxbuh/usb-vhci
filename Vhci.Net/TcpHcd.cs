/*
 * TcpHcd.cs -- VHCI network communication classes
 *
 * Copyright (C) 2008-2009 Conemis AG Karlsruhe Germany
 * Copyright (C) 2008-2016 Michael Singer <michael@a-singer.de>
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
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;

namespace Vhci.Net
{
	public class TcpHcd : Vhci.Hcd
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

		private Socket socket;
		private PortInf[] portInf;

		public TcpHcd(byte ports, IPEndPoint ep) : base(ports)
		{
			socket = new Socket(ep.AddressFamily,
			                    SocketType.Stream,
			                    ProtocolType.Tcp);
			socket.Connect(ep);
			socket.NoDelay = true;
			socket.ReceiveTimeout = 5000;
			socket.SendTimeout = 5000;
			byte[] buf = new byte[] { 0x16, ports }; // syn + #ports
			socket.Send(buf, 0, 2, SocketFlags.None);
			OnConnectionInit(socket);
			socket.Receive(buf, 0, 1, SocketFlags.None);
			if(buf[0] != 0x06) // ack
				throw new InsufficientPortsException();
			if(ports > 0)
			{
				portInf = new PortInf[ports];
				for(byte i = 0; i < ports; i++)
				{
					portInf[i].Address = 0xff;
					portInf[i].PortStat = PortStat.Zero;
				}
			}
			InitBGThread();
		}

		protected virtual void OnConnectionInit(Socket socket) { }

		protected override void Dispose(bool disposing)
		{
			JoinBGThread();
			if(socket != null && socket.Connected)
			{
				try { socket.Shutdown(SocketShutdown.Both); }
				catch(SocketException) { }
				socket.Close();
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

		private void Receive(byte[] buf, int offset, int size)
		{
			while(size > 0)
			{
				if(!IsSocketConnected(socket))
					throw new EndOfStreamException();
				int c = socket.Receive(buf, offset, size, SocketFlags.None);
				if(c == 0)
					throw new EndOfStreamException();
				size -= c;
				offset += c;
			}
		}

		private bool IsSocketConnected(Socket socket)
		{
			if(socket.Connected)
				return true;
			byte[] tmp = new byte[1];
			try
			{
				//socket.Blocking = false;
				socket.Send(tmp, 0, 0);
				return true;
			}
			catch(SocketException)
			{
				return false;
			}
			/*
			catch(SocketException e)
			{
				return e.NativeErrorCode == 10035;
			}
			finally
			{
				socket.Blocking = true;
			}
			*/
		}

		protected override sealed void BGWork()
		{
			byte[] rbuf = new byte[5];
			try { Receive(rbuf, 0, 5); }
			catch(SocketException) { return; }
			catch(EndOfStreamException)
			{
				System.Threading.Thread.Sleep(200);
				return;
			}
			if(rbuf[0] != 0x02) // stx
				throw new InvalidDataException();
			int dlen = rbuf[1] | ((int)rbuf[2] << 8) |
				((int)rbuf[3] << 16) | ((int)rbuf[4] << 24);
			rbuf = new byte[dlen];
			try { Receive(rbuf, 0, dlen); }
			catch(SocketException)
			{
				System.Threading.Thread.Sleep(200);
				return;
			}
			catch(EndOfStreamException)
			{
				System.Threading.Thread.Sleep(200);
				return;
			}
			Vhci.Linux.Ioctl.vhci_ioc_work work =
				new Vhci.Linux.Ioctl.vhci_ioc_work();
			work.type = rbuf[0];
			byte index = rbuf[1];
			byte[] buffer = null;
			Usb.IsochronousPacket[] isobuf = null;
			switch(work.type)
			{
			case 0: // PORT_STAT
				unchecked
				{
					work.work.port.status = (ushort)(uint)((int)rbuf[2] |
					                                       ((int)rbuf[3] << 8));
					work.work.port.change = (ushort)(uint)((int)rbuf[4] |
					                                       ((int)rbuf[5] << 8));
				}
				work.work.port.flags = rbuf[6];
				break;
			case 1: // PROCESS_URB
			case 2: // CANCEL_URB
				work.handle = (ulong)rbuf[2] |
				              ((ulong)rbuf[3] << 8) |
				              ((ulong)rbuf[4] << 16) |
				              ((ulong)rbuf[5] << 24) |
				              ((ulong)rbuf[6] << 32) |
				              ((ulong)rbuf[7] << 40) |
				              ((ulong)rbuf[8] << 48) |
				              ((ulong)rbuf[9] << 56);
				if(work.type == 1) // PROCESS_URB
				{
					work.work.urb.type = rbuf[10];
					work.work.urb.endpoint = rbuf[11];
					work.work.urb.flags = (ushort)((uint)rbuf[12] |
					                               ((uint)rbuf[13] << 8));
					int pos = 14;
					bool isiso = false;
					switch(work.work.urb.type)
					{
					case 0: // ISO
					case 1: // INT
						work.work.urb.interval = (int)rbuf[pos++] |
						                         ((int)rbuf[pos++] << 8) |
						                         ((int)rbuf[pos++] << 16) |
						                         ((int)rbuf[pos++] << 24);
						isiso = work.work.urb.type == 0;
						break;
					case 2: // CONTROL
						work.work.urb.setup_packet.bmRequestType = rbuf[pos++];
						work.work.urb.setup_packet.bRequest = rbuf[pos++];
						work.work.urb.setup_packet.wValue =
							(ushort)((uint)rbuf[pos++] |
							         ((uint)rbuf[pos++] << 8));
						work.work.urb.setup_packet.wIndex =
							(ushort)((uint)rbuf[pos++] |
							         ((uint)rbuf[pos++] << 8));
						work.work.urb.setup_packet.wLength =
							(ushort)((uint)rbuf[pos++] |
							         ((uint)rbuf[pos++] << 8));
						break;
					}
					work.work.urb.buffer_length = (int)rbuf[pos++] |
					                              ((int)rbuf[pos++] << 8) |
					                              ((int)rbuf[pos++] << 16) |
					                              ((int)rbuf[pos++] << 24);
					bool isin = rbuf[pos++] != 0x01;
					int[] isooffset = null;
					if(isiso)
					{
						work.work.urb.packet_count =
							work.work.urb.buffer_length;
						isobuf = new
							Usb.IsochronousPacket[work.work.urb.packet_count];
						if(!isin)
							isooffset = new int[work.work.urb.packet_count];
						work.work.urb.buffer_length = 0;
						for(int i = 0; i < work.work.urb.packet_count; i++)
						{
							int pl = (int)rbuf[pos++] |
							         ((int)rbuf[pos++] << 8) |
							         ((int)rbuf[pos++] << 16) |
							         ((int)rbuf[pos++] << 24);
							isobuf[i] = new Usb.IsochronousPacket
								(work.work.urb.buffer_length, pl);
							if(!isin)
							{
								isooffset[i] = pos;
								pos += pl;
							}
							work.work.urb.buffer_length += pl;
						}
					}
					if(work.work.urb.buffer_length > 0)
					{
						buffer = new byte[work.work.urb.buffer_length];
						if(!isin)
						{
							if(isiso)
							{
								for(int i = 0; i < work.work.urb.packet_count;
								    i++)
									Array.Copy(rbuf, isooffset[i],
									           buffer, isobuf[i].Offset,
									           isobuf[i].PacketLength);
							}
							else
								Array.Copy(rbuf, pos, buffer, 0,
								           work.work.urb.buffer_length);
						}
					}
				}
				break;
			}
			switch(work.type)
			{
			case 0: // PORT_STAT
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
				if((status & ~Vhci.Linux.Ioctl.USB_PORT_STAT_MASK) != 0)
				{
					Debug.WriteLine("PORT_STAT: invalid status flags set");
					break;
				}
				if((change & ~Vhci.Linux.Ioctl.USB_PORT_STAT_C_MASK) != 0)
				{
					Debug.WriteLine("PORT_STAT: invalid change flags set");
					break;
				}
				if((flags & ~Vhci.Linux.Ioctl.VHCI_IOC_PORT_STAT_FLAGS_RESUMING) != 0)
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
					                             portInf[index - 1].PortStat)
					              );
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
			case 1: // PROCESS_URB
				Debug.WriteLine("FETCH_WORK was successful and " +
				                "fetched PROCESS_URB.");
				byte epadr = work.work.urb.endpoint;
				ushort uflags = work.work.urb.flags;
				lock(Lock)
				{
					if(index == 0)
					{
						Debug.WriteLine("PROCESS_URB: Invalid device " +
						                "address: " + index /*+
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
					case 0: // ISO
						urbtype = Usb.EndpointType.Isochronous;
						break;
					case 3: // BULK
						urbtype = Usb.EndpointType.Bulk;
						break;
					case 1: // INT
						urbtype = Usb.EndpointType.Interrupt;
						break;
					case 2: // CONTROL
						urbtype = Usb.EndpointType.Control;
						break;
					default: throw new InvalidOperationException();
					}
					if(eptype != urbtype)
					{
						Debug.WriteLine("PROCESS_URB: Wrong endpoint type. " /*+
						                "Dumping URB..."*/);
						// TODO: Dump
						break;
					}
					Usb.Urb urb;
					switch(eptype)
					{
					case Usb.EndpointType.Isochronous:
						urb = new Usb.IsochronousUrb(unchecked((long)
						                                       work.handle),
						                             buffer, 0,
						                             Usb.UrbStatus.Pending,
						                             Usb.UrbFlags.None, epadr,
						                             (Usb.IsochronousEndpoint)
						                             ep, work.work.urb.interval,
						                             0, isobuf);
						break;
					case Usb.EndpointType.Bulk:
						bool shortNotOk =
							(uflags &
							 Vhci.Linux.Ioctl.VHCI_IOC_URB_FLAGS_SHORT_NOT_OK) != 0;
						bool zeroPacket =
							(uflags &
							 Vhci.Linux.Ioctl.VHCI_IOC_URB_FLAGS_ZERO_PACKET) != 0;
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
						urb = new Usb.InterruptUrb(unchecked((long)work.handle),
						                           buffer, 0,
						                           Usb.UrbStatus.Pending,
						                           Usb.UrbFlags.None, epadr,
						                           (Usb.InterruptEndpoint)ep,
						                           work.work.urb.interval);
						break;
					case Usb.EndpointType.Control:
						urb = unchecked(new Usb.ControlUrb
						                ((long)work.handle, buffer, 0,
						                 Usb.UrbStatus.Pending,
						                 Usb.UrbFlags.None, epadr,
						                 (Usb.ControlEndpoint)ep,
						                 work.work.urb.setup_packet.
						                 bmRequestType,
						                 work.work.urb.setup_packet.bRequest,
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
									unchecked((byte)work.work.urb.
									          setup_packet.wValue);
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
			case 2: // CANCEL_URB
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
				ulong handle = unchecked((ulong)urb.Handle);
				int status = (int)urb.Status;
				int bufact = urb.BufferActual;
				bool isin = urb.IsIn;
				bool isiso = urb is Usb.IsochronousUrb;
				Debug.WriteLine("gb.handle = 0x" + handle.ToString("x16") +
				                "\ngb.status = " + status +
				                "\ngb.bufact = " + bufact);
				byte[] buffer = null;
				int pc = 0;
				Usb.IsochronousUrb iso = null;
				Usb.IsochronousPacket[] isos = null;
				if(isiso)
				{
					iso = (Usb.IsochronousUrb)urb;
					status = iso.ErrorCount;
					pc = iso.PacketCount;
					bufact = 4 + pc * 8;
					isos = iso.IsoPackets;
					if(isin)
						for(int i = 0; i < pc; i++)
							bufact += isos[i].PacketActual;
				}
				else if(isin && bufact > 0)
					buffer = urb.TransferBuffer;
				if(buffer != null)
				{
					int len = urb.BufferLength;
					Debug.Write("gb.buffer = ");
					/*
					Debug.WriteLine("");
					for(int i = 0; i < len; i++)
						Debug.Write(" " + buffer[i].ToString("x2"));
					Debug.WriteLine("");
					*/
					Debug.WriteLine("(" + len + " bytes)");
				}
				else Debug.WriteLine("gb.buffer = null");
				int c = 18 + ((isiso || isin) ? bufact : 0);
				byte[] tbuf = new byte[c];
				if(isiso)
				{
					unchecked
					{
						tbuf[18] = (byte)(uint)pc;
						tbuf[19] = (byte)(uint)(pc >> 8);
						tbuf[20] = (byte)(uint)(pc >> 16);
						tbuf[21] = (byte)(uint)(pc >> 24);
					}
					int pos = 22;
					byte[] buf = iso.TransferBuffer;
					for(int i = 0; i < pc; i++)
					{
						int pst = (int)isos[i].Status;
						int pac = isos[i].PacketActual;
						unchecked
						{
							tbuf[pos++] = (byte)(uint)pst;
							tbuf[pos++] = (byte)(uint)(pst >> 8);
							tbuf[pos++] = (byte)(uint)(pst >> 16);
							tbuf[pos++] = (byte)(uint)(pst >> 24);
							tbuf[pos++] = (byte)(uint)pac;
							tbuf[pos++] = (byte)(uint)(pac >> 8);
							tbuf[pos++] = (byte)(uint)(pac >> 16);
							tbuf[pos++] = (byte)(uint)(pac >> 24);
						}
						if(isin && pac > 0)
						{
							Array.Copy(buf, isos[i].Offset,
							           tbuf, pos, pac);
							pos += pac;
						}
					}
				}
				else if(buffer != null)
					Array.Copy(buffer, 0, tbuf, 18, bufact);
				tbuf[0] = 0x02; // stx
				if(isiso)
					tbuf[1] = (byte)(isin ? 0x06U : 0x05U);
				else
					tbuf[1] = (byte)(isin ? 0x02U : 0x01U);
				unchecked
				{
					tbuf[2] = (byte)handle;
					tbuf[3] = (byte)(ulong)(handle >> 8);
					tbuf[4] = (byte)(ulong)(handle >> 16);
					tbuf[5] = (byte)(ulong)(handle >> 24);
					tbuf[6] = (byte)(ulong)(handle >> 32);
					tbuf[7] = (byte)(ulong)(handle >> 40);
					tbuf[8] = (byte)(ulong)(handle >> 48);
					tbuf[9] = (byte)(ulong)(handle >> 56);
					tbuf[10] = (byte)(uint)status;
					tbuf[11] = (byte)(uint)(status >> 8);
					tbuf[12] = (byte)(uint)(status >> 16);
					tbuf[13] = (byte)(uint)(status >> 24);
					tbuf[14] = (byte)(uint)bufact;
					tbuf[15] = (byte)(uint)(bufact >> 8);
					tbuf[16] = (byte)(uint)(bufact >> 16);
					tbuf[17] = (byte)(uint)(bufact >> 24);
				}
				socket.Send(tbuf, 0, c, SocketFlags.None);
			}
		}

		public override PortStat GetPortStat(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			lock(Lock) return portInf[port - 1].PortStat;
		}

		private void SendPortStat(byte index,
		                          byte type,
		                          byte other)
		{
			byte[] tbuf = new byte[18];
			tbuf[0] = 0x02; // stx
			tbuf[1] = 0x00;
			tbuf[2] = index;
			tbuf[3] = type;
			tbuf[4] = other;
			socket.Send(tbuf, 0, 18, SocketFlags.None);
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
			int c = 9;
			byte[] desc = device.GetDescriptor(Usb.Endianness.UsbSpec);
			int l = desc.Length;
			c += l;
			if(c < 18) c = 18; // very unlikely
			byte[] tbuf = new byte[c];
			tbuf[0] = 0x02; // stx
			tbuf[1] = 0x00;
			tbuf[2] = port;
			tbuf[3] = 0x00;
			bool lowspeed = dataRate == Usb.DataRate.Low;
			bool highspeed = dataRate == Usb.DataRate.High;
			tbuf[4] = (byte)(lowspeed ? 1U : (highspeed ? 2U : 0U));
			unchecked
			{
				tbuf[5] = (byte)(uint)l;
				tbuf[6] = (byte)(uint)(l >> 8);
				tbuf[7] = (byte)(uint)(l >> 16);
				tbuf[8] = (byte)(uint)(l >> 24);
			}
			Debug.WriteLine("l=" + l.ToString());
			desc.CopyTo(tbuf, 9);
			lock(Lock)
			{
				socket.Send(tbuf, 0, c, SocketFlags.None);
				portInf[port - 1].Device = device;
			}
		}

		public override void PortDisconnect(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			SendPortStat(port, 1, 0);
		}

		public override void PortDisable(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			SendPortStat(port, 2, 0);
		}

		public override void PortResumed(byte port)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			SendPortStat(port, 3, 0);
		}

		public override void PortOvercurrent(byte port, bool setOC)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			SendPortStat(port, 4, (byte)(setOC ? 1U : 0U));
		}

		public override void PortResetDone(byte port, bool enable)
		{
			if(port == 0) throw new ArgumentException("", "port");
			if(port > PortCount)
				throw new ArgumentOutOfRangeException("port", port, "");
			SendPortStat(port, 5, (byte)(enable ? 1U : 0U));
		}
	}
}
