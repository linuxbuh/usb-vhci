using System;
using Usb;
using Usb.UsbFS;
using Vhci;
using Vhci.Forwarders;

public static class Program
{
	public static void Main(string[] args)
	{
		MyHcd hcd = new MyHcd();
		UsbDevice dev = new UsbDevice(args[0]);
		SingleDeviceForwarder fwd = new SingleDeviceForwarder(hcd, dev);

		fwd.Run();
	}
}

public class MyHcd : Vhci.Linux.LocalHcd
{
	public MyHcd() : base(1) { }

	protected override void FinishingWork(Work work)
	{
		if(work is ProcessUrbWork)
		{
			ProcessUrbWork puw = (ProcessUrbWork)work;

			if(puw.Urb is InterruptUrb && puw.Urb.IsIn)
			{
				puw.Urb.TransferBuffer[1] = (byte)-(sbyte)puw.Urb.TransferBuffer[1];
				puw.Urb.TransferBuffer[2] = (byte)-(sbyte)puw.Urb.TransferBuffer[2];
				puw.Urb.TransferBuffer[3] = (byte)-(sbyte)puw.Urb.TransferBuffer[3];
			}
		}

		base.FinishingWork(work);
	}
}

