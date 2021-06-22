/*
 * TcpHcdServer.cs -- VHCI network communication classes
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
using System.Collections.Generic;
using System.Threading;

namespace Vhci.Net
{
	public class TcpHcdServer
	{
		protected readonly object Lock = new object();
		private Vhci.Hcd hcd;
		private Socket listener;
		private bool running;
		private volatile bool shutdown;

		private class Client
		{
			public byte[] PortMap;
			public Socket Socket;
			public Thread Thread;
			public Dictionary<ulong, Vhci.ProcessUrbWork> Urbs;
			public readonly object UrbsLock = new object();
		}
		private List<Client> client;
		private KeyValuePair<Client, byte>[] portMap;
		private byte portCount;

		public event EventHandler DoEvents;

		public TcpHcdServer(Vhci.Hcd hcd, IPEndPoint ep)
		{
			if(hcd == null) throw new ArgumentNullException("hcd");
			if(ep == null) throw new ArgumentNullException("ep");
			this.hcd = hcd;
			portCount = hcd.PortCount;
			listener = new Socket(AddressFamily.InterNetwork,
			                      SocketType.Stream,
			                      ProtocolType.Tcp);
			listener.Bind(ep);
			listener.Blocking = false;
			portMap = new KeyValuePair<Client, byte>[portCount];
			client = new List<Client>();
		}

		public byte FreePorts
		{
			get
			{
				byte n = 0;
				foreach(KeyValuePair<Client, byte> kvp in portMap)
					if(kvp.Key == null) n++;
				return n;
			}
		}

		private AutoResetEvent ev;
		private void WorkEnqueuedHandler(object sender, EventArgs e)
		{
			if(sender == hcd) ev.Set();
		}

		private bool GetClientPort(byte serverPort,
		                           out Client client,
		                           out byte clientPort)
		{
			if(serverPort == 0) throw new ArgumentException("", "serverPort");
			if(serverPort > portCount)
				throw new ArgumentOutOfRangeException("serverPort",
				                                      serverPort, "");
			lock(Lock)
			{
				client = portMap[serverPort - 1].Key;
				clientPort = (byte)(uint)(portMap[serverPort - 1].Value + 1);
			}
			return client != null;
		}

		private byte GetHcdPort(Client client, byte clientPort)
		{
			if(client == null) throw new ArgumentNullException("client");
			if(clientPort == 0) throw new ArgumentException("", "clientPort");
			if(clientPort > client.PortMap.Length)
				throw new ArgumentOutOfRangeException("clientPort",
				                                      clientPort, "");
			return (byte)(uint)(client.PortMap[clientPort - 1] + 1);
		}

		private void VhciThread()
		{
			ev = new AutoResetEvent(true);
			hcd.WorkEnqueued += WorkEnqueuedHandler;
			byte[] tbuf = new byte[32];
			tbuf[0] = 0x02; // stx
			try
			{
				while(!shutdown)
				{
					ev.WaitOne(100, false);
					bool cont = true;
					while(cont && !shutdown)
					{
						//Debug.WriteLine("Fetching work...");
						Vhci.Work w;
						cont = hcd.NextWork(out w);
						if(w == null) break;
						byte port = w.Port;
						Debug.WriteLine("got work for port " + port);
						Client client;
						byte cport;
						if(GetClientPort(port, out client, out cport))
						{
							Debug.WriteLine("for cport " + cport);
							if(w is Vhci.PortStatWork)
							{
								Console.Write(":");
								Vhci.PortStatWork psw = (Vhci.PortStatWork)w;
								Debug.WriteLine("forwarding port_stat update");
								tbuf[1] = 0x07; // len=7
								tbuf[2] = 0x00; //   "
								tbuf[3] = 0x00; //   "
								tbuf[4] = 0x00; //   "
								tbuf[5] = 0x00; // type=PORT_STAT
								tbuf[6] = cport;
								unchecked
								{
									uint tmp = (uint)psw.PortStat.Status;
									tbuf[7] = (byte)tmp;
									tbuf[8] = (byte)(tmp >> 8);
									tmp = (uint)psw.PortStat.Change;
									tbuf[9] = (byte)tmp;
									tbuf[10] = (byte)(tmp >> 8);
									tmp = (uint)psw.PortStat.Flags;
									tbuf[11] = (byte)tmp;
								}
								client.Socket.Send(tbuf, 0, 12,
								                   SocketFlags.None);
								hcd.FinishWork(w);
							}
							else if(w is Vhci.ProcessUrbWork)
							{
								Console.Write(".");
								Vhci.ProcessUrbWork puw =
									(Vhci.ProcessUrbWork)w;
								Usb.Urb urb = puw.Urb;
								ulong handle = unchecked((ulong)urb.Handle);
								lock(client.UrbsLock)
									client.Urbs.Add(handle, puw);
								bool isin = urb.IsIn;
								Usb.EndpointType t = urb.Type;
								int blen = urb.BufferLength;
								int c = 19;
								Usb.IsochronousUrb iso =
									urb as Usb.IsochronousUrb;
								bool isiso = iso != null;
								switch(t)
								{
								case Usb.EndpointType.Isochronous:
								case Usb.EndpointType.Interrupt: c += 4; break;
								case Usb.EndpointType.Control:   c += 8; break;
								}
								int csum = (isin || isiso) ? c : (c + blen);
								int isoc = 0;
								Usb.IsochronousPacket[] isos = null;
								int isol = 0;
								if(isiso)
								{
									isoc = iso.PacketCount;
									isos = iso.IsoPackets;
									isol += 4 * isoc;
									if(!isin)
										for(int i = 0; i < isoc; i++)
											isol += isos[i].PacketLength;
									csum += isol;
								}
								unchecked
								{
									tbuf[1] = (byte)(uint)csum;
									tbuf[2] = (byte)(uint)(csum >> 8);
									tbuf[3] = (byte)(uint)(csum >> 16);
									tbuf[4] = (byte)(uint)(csum >> 24);
								}
								tbuf[5] = 0x01; // type=PROCESS_URB
								tbuf[6] = cport;
								unchecked
								{
									tbuf[7] = (byte)handle;
									tbuf[8] = (byte)(handle >> 8);
									tbuf[9] = (byte)(handle >> 16);
									tbuf[10] = (byte)(handle >> 24);
									tbuf[11] = (byte)(handle >> 32);
									tbuf[12] = (byte)(handle >> 40);
									tbuf[13] = (byte)(handle >> 48);
									tbuf[14] = (byte)(handle >> 56);
								}
								switch(t)
								{
								case Usb.EndpointType.Control:
									tbuf[15] =
										Vhci.Linux.Ioctl.VHCI_IOC_URB_TYPE_CONTROL;
									break;
								case Usb.EndpointType.Bulk:
									tbuf[15] =
										Vhci.Linux.Ioctl.VHCI_IOC_URB_TYPE_BULK;
									break;
								case Usb.EndpointType.Interrupt:
									tbuf[15] =
										Vhci.Linux.Ioctl.VHCI_IOC_URB_TYPE_INT;
									break;
								case Usb.EndpointType.Isochronous:
									tbuf[15] =
										Vhci.Linux.Ioctl.VHCI_IOC_URB_TYPE_ISO;
									break;
								}
								tbuf[16] = urb.EndpointAddress;
								unchecked
								{
									uint tmp = (uint)urb.Flags;
									tbuf[17] = (byte)tmp;
									tbuf[18] = (byte)(tmp >> 8);
								}
								int pos = 19;
								switch(t)
								{
								case Usb.EndpointType.Isochronous:
								case Usb.EndpointType.Interrupt:
									unchecked
									{
										uint tmp;
										if(t == Usb.EndpointType.Interrupt)
											tmp = (uint)((Usb.InterruptUrb)urb).
												Interval;
										else tmp = (uint)iso.Interval;
										tbuf[pos++] = (byte)tmp;
										tbuf[pos++] = (byte)(tmp >> 8);
										tbuf[pos++] = (byte)(tmp >> 16);
										tbuf[pos++] = (byte)(tmp >> 24);
									}
									break;
								case Usb.EndpointType.Control:
									{
										Usb.ControlUrb curb =
											(Usb.ControlUrb)urb;
										tbuf[pos++] =
											curb.SetupPacketRequestType;
										tbuf[pos++] =
											curb.SetupPacketRequest;
										unchecked
										{
											uint tmp = (uint)(ushort)curb.
											           SetupPacketValue;
											tbuf[pos++] = (byte)tmp;
											tbuf[pos++] = (byte)(tmp >> 8);
											tmp = (uint)(ushort)curb.
											      SetupPacketIndex;
											tbuf[pos++] = (byte)tmp;
											tbuf[pos++] = (byte)(tmp >> 8);
											tmp = (uint)(ushort)curb.
											      SetupPacketLength;
											tbuf[pos++] = (byte)tmp;
											tbuf[pos++] = (byte)(tmp >> 8);
										}
									}
									break;
								}
								int cval = isiso ? isoc : blen;
								unchecked
								{
									tbuf[pos++] = (byte)(uint)cval;
									tbuf[pos++] = (byte)(uint)(cval >> 8);
									tbuf[pos++] = (byte)(uint)(cval >> 16);
									tbuf[pos++] = (byte)(uint)(cval >> 24);
								}
								tbuf[pos++] = isin ? (byte)0x00U : (byte)0x01U;
#if DEBUG
								if(pos != c + 5)
									throw new Exception("something went wrong");
#endif
								// TODO: This is blocking. We have to rework this
								//       sometime.
								if(isiso)
								{
									byte[] isob = new byte[isol];
									int isop = 0;
									for(int i = 0; i < isoc; i++)
									{
										int v = isos[i].PacketLength;
										unchecked
										{
											isob[isop++] = (byte)(uint)v;
											isob[isop++] = (byte)(uint)(v >> 8);
											isob[isop++] =
												(byte)(uint)(v >> 16);
											isob[isop++] =
												(byte)(uint)(v >> 24);
										}
										if(!isin)
										{
											Array.Copy(urb.TransferBuffer,
											           isos[i].Offset,
											           isob, isop, v);
											isop += v;
										}
									}
									client.Socket.Send(tbuf, 0, pos,
									                   SocketFlags.None);
									client.Socket.Send(isob, 0, isol,
									                   SocketFlags.None);
								}
								else if(!isin)
								// THANKS MONO: NotImplementedException
								/*
									client.Socket.Send(new ArraySegment<byte>[]
									{
										new ArraySegment<byte>(tbuf, 0, pos),
										new ArraySegment<byte>
											(urb.TransferBuffer, 0, blen)
									});
								*/
								{
									client.Socket.Send(tbuf, 0, pos,
									                   SocketFlags.None);
									if(blen != 0)
										client.Socket.Send(urb.TransferBuffer,
										                   0, blen,
										                   SocketFlags.None);
#if DEBUG
									if(pos + blen != csum + 5)
										throw new Exception("something went wrong");
#endif
								}
								else
									client.Socket.Send(tbuf, 0, pos,
									                   SocketFlags.None);
							}
							else if(w is Vhci.CancelUrbWork)
							{
								Console.Write("!");
								Vhci.CancelUrbWork cuw = (Vhci.CancelUrbWork)w;
								ulong handle = unchecked((ulong)cuw.Handle);
								tbuf[1] = 0x0a; // len=10
								tbuf[2] = 0x00; //   "
								tbuf[3] = 0x00; //   "
								tbuf[4] = 0x00; //   "
								tbuf[5] = 0x02; // type=CANCEL_URB
								tbuf[6] = cport;
								unchecked
								{
									tbuf[7] = (byte)handle;
									tbuf[8] = (byte)(handle >> 8);
									tbuf[9] = (byte)(handle >> 16);
									tbuf[10] = (byte)(handle >> 24);
									tbuf[11] = (byte)(handle >> 32);
									tbuf[12] = (byte)(handle >> 40);
									tbuf[13] = (byte)(handle >> 48);
									tbuf[14] = (byte)(handle >> 56);
								}
								client.Socket.Send(tbuf, 0, 15,
								                   SocketFlags.None);
							}
						}
					}
				}
			}
			finally
			{
				hcd.WorkEnqueued -= WorkEnqueuedHandler;
			}
		}

		private void Receive(Socket socket, byte[] buf, int offset, int size)
		{
			while(size > 0)
			{
				if(shutdown || !IsSocketConnected(socket))
					throw new EndOfStreamException();
				int c = socket.Receive(buf, offset, size, SocketFlags.None);
				if(c == 0)
					throw new EndOfStreamException();
				size -= c;
				offset += c;
			}
		}

		private static void TrySocketShutdown(Socket socket)
		{
			try { socket.Shutdown(SocketShutdown.Both); }
			catch(SocketException) { }
		}

		public void Run()
		{
			if(running) throw new InvalidOperationException();
			Thread worker = null;
			Debug.WriteLine("Start listening");
			listener.Listen(10);
			running = true;
			try
			{
				worker = new Thread(VhciThread);
				worker.Name = "TcpHcdServer_VhciWorker";
				worker.Priority = ThreadPriority.Normal;
				worker.IsBackground = true;
				worker.Start();
				while(true)
				{
					Thread.Sleep(200);
					OnDoEvents(null);
					if(shutdown) break;
					Socket socket = null;
					try
					{
						socket = listener.Accept();
					}
					catch(SocketException) { }
					if(socket != null)
					{
						Debug.WriteLine("Connection pending");
						socket.Blocking = true;
						socket.NoDelay = true;
						socket.ReceiveTimeout = 5000;
						socket.SendTimeout = 5000;
						byte[] rbuf = new byte[2];
						try { Receive(socket, rbuf, 0, 2); }
						catch(SocketException)
						{
							Debug.WriteLine("Connection timeout");
							TrySocketShutdown(socket);
							socket.Close();
							continue;
						}
						catch(EndOfStreamException)
						{
							Debug.WriteLine("Closing connection");
							TrySocketShutdown(socket);
							socket.Close();
							continue;
						}
						if(rbuf[0] != 0x16) // syn
						{
							Debug.WriteLine("Ignoring connection");
							TrySocketShutdown(socket);
							socket.Close();
						}
						else
						{
							if(FreePorts < rbuf[1])
							{
								Debug.WriteLine("Refusing connection");
								rbuf[0] = 0x15; // nak
								try
								{
									socket.Send(rbuf, 0, 1, SocketFlags.None);
									socket.Shutdown(SocketShutdown.Both);
								}
								catch(SocketException) { }
								socket.Close();
							}
							else
							{
								if(!OnConnectionInit(socket))
								{
									Debug.WriteLine("Closing connection " +
									                "(custom protocol error)");
									TrySocketShutdown(socket);
									socket.Close();
									continue;
								}
								Debug.WriteLine("Accepting connection");
								Client c = new Client();
								byte n = rbuf[1];
								c.PortMap = new byte[n];
								c.Socket = socket;
								c.Urbs =
									new Dictionary<ulong, Vhci.ProcessUrbWork>
										();
								c.Thread = new Thread(ClientThread);
								c.Thread.Name = "TcpHcdServer_ClientWorker";
								c.Thread.Priority = ThreadPriority.Normal;
								c.Thread.IsBackground = true;
								lock(Lock)
								{
									for(byte i = 0; i < portCount; i++)
									{
										if(portMap[i].Key == null)
										{
											if(unchecked(n--) == 0) break;
											Debug.WriteLine("Mapping hcd[" +
											                i.ToString() +
											                "] to client[" +
											                n.ToString() + "]");
											portMap[i] =
												new KeyValuePair<Client, byte>
													(c, n);
											Debug.WriteLine("Mapping client[" +
											                n.ToString() +
											                "] to hcd[" +
											                i.ToString() + "]");
											c.PortMap[n] = i;
										}
									}
									client.Add(c);
									c.Thread.Start(c);
								}
								rbuf[0] = 0x06; // ack
								socket.Send(rbuf, 0, 1, SocketFlags.None);
								// TODO: This is so ugly. We have to think about
								//       a better soulution here.
								Debug.WriteLine("HACK: triggering port_connect");
								for(int i = 1; i <= c.PortMap.Length; i++)
								{
									byte[] tbuf = new byte[12];
									tbuf[0] = 0x02; // stx
									tbuf[1] = 0x07; // len=7
									tbuf[2] = 0x00; //   "
									tbuf[3] = 0x00; //   "
									tbuf[4] = 0x00; //   "
									tbuf[5] = 0x00; // type=PORT_STAT
									tbuf[6] = (byte)(uint)i;
									tbuf[7] = 0;
									tbuf[8] = 1;
									tbuf[9] = 0;
									tbuf[10] = 0;
									tbuf[11] = 0;
									socket.Send(tbuf, 0, 12,
									            SocketFlags.None);
								}
							}
						}
					}
				}
			}
			finally
			{
				Debug.WriteLine("Stop listening");
				listener.Close();
				shutdown = true;
				if(!object.ReferenceEquals(worker, null) && worker.IsAlive)
					worker.Join();
				while(true)
				{
					lock(Lock)
						if(client.Count == 0) break;
					Thread.Sleep(10);
				}
				running = false;
				shutdown = false;
			}
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

		public void Stop()
		{
			if(!running) throw new InvalidOperationException();
			shutdown = true;
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

		private void ClientThread(object parameter)
		{
			Client client = (Client)parameter;
			byte[] rbuf = new byte[18];
			try
			{
				while(!shutdown && IsSocketConnected(client.Socket))
				{
					try { Receive(client.Socket, rbuf, 0, 18); }
					catch(SocketException) { continue; }
					catch(EndOfStreamException)
					{
						Debug.WriteLine("Socket closed");
						break;
					}
					if(rbuf[0] != 0x02) // stx
					{
						Debug.WriteLine("Protocol error: packet not starting with 0x02");
						throw new InvalidDataException();
					}
					if(rbuf[1] == 1 || rbuf[1] == 2 ||
						rbuf[1] == 5 || rbuf[1] == 6) // GIVEBACK
					{
						ulong handle;
						int status, bufact;
						handle = (ulong)rbuf[2] |
						         ((ulong)rbuf[3] << 8) |
						         ((ulong)rbuf[4] << 16) |
						         ((ulong)rbuf[5] << 24) |
						         ((ulong)rbuf[6] << 32) |
						         ((ulong)rbuf[7] << 40) |
						         ((ulong)rbuf[8] << 48) |
						         ((ulong)rbuf[9] << 56);
						unchecked
						{
							status = (int)((uint)rbuf[10] |
							               ((uint)rbuf[11] << 8) |
							               ((uint)rbuf[12] << 16) |
							               ((uint)rbuf[13] << 24));
							bufact = (int)((uint)rbuf[14] |
							               ((uint)rbuf[15] << 8) |
							               ((uint)rbuf[16] << 16) |
							               ((uint)rbuf[17] << 24));
						}
						Vhci.ProcessUrbWork puw = null;
						lock(client.UrbsLock)
							if(client.Urbs.TryGetValue(handle, out puw))
								client.Urbs.Remove(handle);
						if(puw != null)
						{
							Usb.Urb urb = puw.Urb;
							bool isin = urb.IsIn;
							if(rbuf[1] == 5 || rbuf[1] == 6)
							{
								Usb.IsochronousUrb iso =
									(Usb.IsochronousUrb)urb;
								if(rbuf[1] == 6 && !isin)
								{
									Debug.WriteLine("Protocol error: iso urb direction mismatch");
									throw new ProtocolViolationException();
								}
								byte[] isobuf = new byte[bufact];
								Receive(client.Socket, isobuf, 0, bufact);
								iso.ErrorCount = status;
								int pc = iso.PacketCount;
								int pc2;
								unchecked
								{
									pc2 = (int)((uint)isobuf[0] |
									            ((uint)isobuf[1] << 8) |
									            ((uint)isobuf[2] << 16) |
									            ((uint)isobuf[3] << 24));
								}
								if(pc != pc2)
								{
									Debug.WriteLine("Protocol error: iso urb packet count mismatch");
									throw new ProtocolViolationException();
								}
								Usb.IsochronousPacket[] isos = iso.IsoPackets;
								int pos = 4;
								for(int i = 0; i < pc; i++)
								{
									unchecked
									{
										status = (int)((uint)isobuf[pos++] |
										           ((uint)isobuf[pos++] << 8) |
										           ((uint)isobuf[pos++] << 16) |
										           ((uint)isobuf[pos++] << 24));
										bufact = (int)((uint)isobuf[pos++] |
										           ((uint)isobuf[pos++] << 8) |
										           ((uint)isobuf[pos++] << 16) |
										           ((uint)isobuf[pos++] << 24));
									}
									Usb.IsochronousPacket p = isos[i];
									isos[i] = new Usb.IsochronousPacket
										(p.Offset, p.PacketLength,
										 bufact, (Usb.UrbStatus)status);
									if(isin)
									{
										Array.Copy(isobuf, pos,
										           iso.TransferBuffer, p.Offset,
										           bufact);
										pos += bufact;
									}
								}
								if(isin) iso.BufferActual = iso.BufferLength;
							}
							else
							{
								if(rbuf[1] == 2 && !isin)
								{
									Debug.WriteLine("Protocol error: urb direction mismatch");
									throw new ProtocolViolationException();
								}
								urb.Status = (Usb.UrbStatus)status;
								urb.BufferActual = bufact;
								if(isin && bufact != 0)
									Receive(client.Socket,
									        urb.TransferBuffer,
									        0, bufact);
							}
							Console.Write("+");
							hcd.FinishWork(puw);
						}
						else
						{
							Debug.WriteLine("nix gibts");
							if((rbuf[1] == 2 || rbuf[1] == 5 || rbuf[1] == 6) &&
								bufact != 0)
								Receive(client.Socket,
								        new byte[bufact],
								        0, bufact);
						}
					}
					else if(rbuf[1] == 0) // PORT_STAT
					{
						byte port = GetHcdPort(client, rbuf[2]);
						switch(rbuf[3])
						{
						case 0: // connect
							{
								bool lowspeed = (rbuf[4] & 0x01) != 0x00;
								bool highspeed = (rbuf[4] & 0x02) != 0x00;
								Usb.DataRate dr = Usb.DataRate.Full;
								if(lowspeed) dr = Usb.DataRate.Low;
								else if(highspeed) dr = Usb.DataRate.High;
								int l = unchecked((int)(uint)rbuf[5] |
								                  ((int)(uint)rbuf[6] << 8) |
								                  ((int)(uint)rbuf[7] << 16) |
								                  ((int)(uint)rbuf[8] << 24));
								// If l is too big, then we are fucked here:
								byte[] desc = new byte[l];
								Array.Copy(rbuf, 9, desc, 0, (l < 9) ? l : 9);
								Debug.WriteLine("l=" + l.ToString());
								if(l > 9)
								{
									int r = l - 9;
									Debug.WriteLine("r=" + r.ToString());
									Receive(client.Socket, desc, 9, r);
								}
								// We trust that the client is providing valid data
								Usb.DeviceDescriptor dev =
									new Usb.DeviceDescriptor(desc,
									                         Usb.Endianness.
									                         UsbSpec);
								// ugly:
								dev.ActiveConfigurationIndex = 1;
								foreach(Usb.Interface ifc in dev[1].Interfaces)
								{
									ifc.IsActive = true;
								}
								dev.Dump(Console.Out, "> ");
								hcd.PortConnect(port, dev, dr);
							}
							break;
						case 1: // disconnect
							hcd.PortDisconnect(port);
							break;
						case 2: // disable
							hcd.PortDisable(port);
							break;
						case 3: // resumed
							hcd.PortResumed(port);
							break;
						case 4: // overcurrent
							hcd.PortOvercurrent(port, rbuf[4] != 0);
							break;
						case 5: // reset_done
							hcd.PortResetDone(port, rbuf[4] != 0);
							break;
						}
					}
					else
						throw new InvalidDataException();
				}
				Debug.WriteLine("client.Socket.Connected = " + client.Socket.Connected.ToString());
			}
			finally
			{
				CleanupConnection(client);
			}
		}

		private void CleanupConnection(Client client)
		{
			lock(Lock)
			{
				Debug.WriteLine("Closing connection");
				TrySocketShutdown(client.Socket);
				client.Socket.Close();
				for(byte i = 0; i < portCount; i++)
				{
					if(portMap[i].Key == client)
					{
						hcd.PortDisconnect((byte)(i + 1));
						portMap[i] =
							new KeyValuePair<Client, byte>(null, 0);
					}
				}
				this.client.Remove(client);
			}
		}

		protected virtual bool OnConnectionInit(Socket socket)
		{
			return true;
		}
	}
}
