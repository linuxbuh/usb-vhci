using System;
using Usb;
using Usb.UsbFS;
using Vhci;
using Vhci.Forwarders;

public static class Program
{
	public static void Main(string[] args)
	{
		LocalHcd hcd = LocalHcd.Create(1);
		UsbDevice dev = new UsbDevice(args[0]);
		SingleDeviceForwarder fwd = new SingleDeviceForwarder(hcd, dev);

		fwd.Run();
	}
}

