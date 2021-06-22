using System;
using Usb;
using Usb.Enumeration;

public static class Program
{
	public static void Main()
	{
		Usb.UsbFS.Enumeration.Enumerator statelessEnum =
			new Usb.UsbFS.Enumeration.Enumerator();
		StatefullEnumerator statefullEnum =
			new StatefullEnumerator(statelessEnum);

		statefullEnum.DeviceAdded +=
			delegate(object sender, CollectionModifiedEventArgs<IDevice> e)
		{
			Console.WriteLine("ADD    {0}", e.Item);
		};

		statefullEnum.DeviceRemoved +=
			delegate(object sender, CollectionModifiedEventArgs<IDevice> e)
		{
			Console.WriteLine("REMOVE {0}", e.Item);
		};

		string input;
		do
		{
			Console.WriteLine("Enter q to quit or s to scan the bus:");
			Console.Write("> ");
			input = Console.ReadLine();
			if(input == "s")
			{
				statefullEnum.Scan();
			}
		} while(input != "q");
	}
}
