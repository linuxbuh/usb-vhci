using System;
using Usb;
using Usb.Emulation;
using Vhci;
using Vhci.Forwarders;

public static class Program
{
	public static void Main()
	{
		LocalHcd hcd = LocalHcd.Create(1);
		MyUsbDevice dev = new MyUsbDevice();
		SingleDeviceForwarder fwd = new SingleDeviceForwarder(hcd, dev);

		fwd.DoEvents += delegate(object sender, EventArgs e)
		{
			dev.ProcAsyncUrbs();
		};

		fwd.Run();
	}
}

public class MyUsbDevice : UsbDeviceBase
{
	public MyUsbDevice() : base(null, DataRate.Full)
	{
		Device.AddConfiguration(1, new ConfigurationDescriptor());
		Device[1].AddInterface(0, new Interface());
		Device[1][0].AddAlternateSetting(0, new AlternateSetting());
		Device.ProductStringIndex = 1;
		Device.VendorID = unchecked((short)0xdeadu);
		Device.ProductID = unchecked((short)0xbeefu);
		Device.BcdDevice = 0x1138;
	}

	protected override bool OnProcGetStringDescriptor(Usb.ControlUrb urb, byte index)
	{
		ushort wLength = (ushort)urb.SetupPacketLength;
		switch(index)
		{
		case 1:
			byte[] b = (new Usb.StringDescriptor("Hello World!")).GetDescriptor();
			int l = Math.Min(b.Length, wLength);
			Array.Copy(b, urb.TransferBuffer, l);
			urb.BufferActual = l;
			urb.Ack();
			return true;
		default:
			return base.OnProcGetStringDescriptor(urb, index);
		}
	}
}

