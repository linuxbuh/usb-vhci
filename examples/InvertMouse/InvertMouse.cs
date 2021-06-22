// mcs -r:Usb.dll -r:Usb.UsbFS.dll -r:Vhci.dll -out:InvertMouse.exe InvertMouse.cs

using System;
using Usb;
using Usb.UsbFS;
using Vhci;
using Vhci.Forwarders;

public static class Program
{
	public static void Main(string[] args)
	{
		// The following line creates a new virtual USB host controller
		// with one virtual USB port in the kernel.
		LocalHcd hcd = LocalHcd.Create(1);

		// This object represents a real device connected to this machine.
		// The argument has to be a string like "/proc/bus/usb/003/002".
		// Therefore usbfs should be mounted at /proc/bus/usb.
		UsbDevice dev = new UsbDevice(args[0]);

		// The forwarder "connects" the device to the virtual host controller.
		// It forwards the requests and their answers between the two instances.
		MyForwarder fwd = new MyForwarder(hcd, dev);

		// Start the loop which does the work.
		fwd.Run();
	}
}

// The SingleDeviceForwarder is designed to forward a single device to a
// virtual host controller. This class can be completely customized by
// inheritance. So we inherit from the SingleDeviceForwarder to implement
// our mouse axis inversion.
public class MyForwarder : SingleDeviceForwarder
{
	public MyForwarder(Hcd hcd, IUsbDevice dev) : base(hcd, dev) { }

	// This method is called every time an answer from the device has to be
	// sent to the host controller.
	protected override void OnGivebackUrb(Urb urb)
	{
		// We are only interrested in incoming interrupt transfers.
		if(urb is InterruptUrb && urb.IsIn)
		{
			// Change the signs of the three axis. (X, Y and scroll)
			urb.TransferBuffer[1] = (byte)-(sbyte)urb.TransferBuffer[1];
			urb.TransferBuffer[2] = (byte)-(sbyte)urb.TransferBuffer[2];
			urb.TransferBuffer[3] = (byte)-(sbyte)urb.TransferBuffer[3];
		}

		// With this call it will actually be sent to the host controller.		
		base.OnGivebackUrb(urb);
	}
}

