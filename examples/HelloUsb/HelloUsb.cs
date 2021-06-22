using System;
using System.Threading;
using Usb;
using Vhci;

public static class Program
{
	public static void Main()
	{
		LocalHcd hcd = LocalHcd.Create(1);

		DeviceDescriptor device = new DeviceDescriptor();
		device.AddConfiguration(1, new ConfigurationDescriptor());
		device[1].AddInterface(0, new Interface());
		device[1][0].AddAlternateSetting(0, new AlternateSetting());
		device.ProductStringIndex = 1;
		device.VendorID = unchecked((short)0xdeadu);
		device.ProductID = unchecked((short)0xbeefu);
		device.BcdDevice = 0x1138;
		StringDescriptor helloWorld = new StringDescriptor("Hello World!");

		AutoResetEvent ev = new AutoResetEvent(true);

		hcd.WorkEnqueued += delegate(object sender, EventArgs e)
		{
			ev.Set();
		};

		while(true)
		{
			ev.WaitOne(100, false);
			bool cont = true;
			while(cont)
			{
				Work w;
				cont = hcd.NextWork(out w);

				if(w is PortStatWork)
				{
					PortStatWork psw = (PortStatWork)w;

					if(psw.TriggersPowerOn)
						hcd.PortConnect(1, device, DataRate.Full);

					if(psw.TriggersReset)
						hcd.PortResetDone(1);
				}

				else if(w is ProcessUrbWork)
				{
					ProcessUrbWork puw = (ProcessUrbWork)w;

					if(puw.Urb is ControlUrb)
					{
						ControlUrb urb = (ControlUrb)puw.Urb;

						if(urb.Endpoint == device.EndpointZero)
						{
							if(urb.SetupPacketRequestType == 0x00 &&
								urb.SetupPacketRequest == ControlUrb.SET_ADDRESS)
							{
								urb.Ack();
							}

							else if(urb.SetupPacketRequestType == 0x00 &&
								urb.SetupPacketRequest == ControlUrb.SET_CONFIGURATION)
							{
								urb.Ack();
							}

							else if(urb.SetupPacketRequestType == 0x00 &&
								urb.SetupPacketRequest == ControlUrb.SET_INTERFACE)
							{
								urb.Ack();
							}

							else if(urb.SetupPacketRequestType == 0x80 &&
								urb.SetupPacketRequest == ControlUrb.GET_DESCRIPTOR)
							{
								int descType = urb.SetupPacketValue >> 8;

								if(descType == 1)
								{
									byte[] d = device.GetLocalDescriptor(Endianness.UsbSpec);
									int l = d.Length;
									if(urb.SetupPacketLength < l)
										l = urb.SetupPacketLength;
									Array.Copy(d, urb.TransferBuffer, l);
									urb.BufferActual = l;
									urb.Ack();
								}

								else if(descType == 2)
								{
									byte[] d = device[1].GetDescriptor(Endianness.UsbSpec);
									int l = d.Length;
									if(urb.SetupPacketLength < l)
										l = urb.SetupPacketLength;
									Array.Copy(d, urb.TransferBuffer, l);
									urb.BufferActual = l;
									urb.Ack();
								}

								else if(descType == 3)
								{
									int strIndex = urb.SetupPacketValue & 0xff;

									if(strIndex == 0)
									{
										byte[] langIds = StringDescriptor.EnglishUnitedStatesLangIDTable.GetDescriptor();
										langIds.CopyTo(urb.TransferBuffer, 0);
										urb.BufferActual = langIds.Length;
										urb.Ack();
									}

									else if(strIndex == 1)
									{
										byte[] hwd = helloWorld.GetDescriptor();
										hwd.CopyTo(urb.TransferBuffer, 0);
										urb.BufferActual = hwd.Length;
										urb.Ack();
									}

									else urb.Stall();
								}
							}

							else urb.Stall();
						}
						else urb.Stall();
					}
				}

				hcd.FinishWork(w);
			}
		}
	}
}

