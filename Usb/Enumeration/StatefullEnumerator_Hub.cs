/*
 * StatefullEnumerator_Hub.cs -- USB device enumeration interfaces
 *
 * Copyright (C) 2010 Michael Singer <michael@a-singer.de>
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

namespace Usb.Enumeration
{
	partial class StatefullEnumerator
	{
		protected partial class Hub : Device, IHub
		{
			private Device[] ports;

			public Hub(StatefullEnumerator enm, Hub parent, IHub inner) :
				base(enm, parent, inner)
			{
				int c = inner.Count;
				ports = new Device[c];
				for(int i = 0; i < c; i++)
				{
					IDevice idev = inner[i];
					if(idev != null)
					{
						IHub ihub = idev as IHub;
						if(ihub != null)
							ports[i] = new Hub(enm, this, ihub);
						else
							ports[i] = new Device(enm, this, idev);
					}
				}
			}

			public virtual bool IsRoot
			{
				get { return false; }
			}

			protected override void OnRescanInnerDevice()
			{
				IHub ihub = InnerDevice as IHub;
				if(ihub == null) throw new InvalidOperationException();
				int c = ports.Length;
				if(c != ihub.Count)
				{
					if(c != 0)
						throw new InvalidOperationException();
					// If the hub was not probed by the kernel before,
					// then it may have had zero ports. This is a special
					// case and we allow the port count to be increased.
					c = ihub.Count;
					ports = new Device[c];
				}

				for(int i = 0; i < c; i++)
				{
					if(ports[i] == null && ihub[i] == null)
						continue;
					if(ports[i] == null)
					{
						IHub shub = ihub[i] as IHub;
						if(shub != null)
							ports[i] = new Hub(Enm, this, shub);
						else
							ports[i] = new Device(Enm, this, ihub[i]);
						Enm.OnDeviceAdded
							(new CollectionModifiedEventArgs<IDevice>
							 (ports[i]));
					}
					else if(ihub[i] == null)
					{
						CollectionModifiedEventArgs<IDevice> cmea =
							new CollectionModifiedEventArgs<IDevice>(ports[i]);
						ports[i] = null;
						Enm.OnDeviceRemoved(cmea);
					}
					else
					{
						// is it still the same device?
						if(DeviceComparer.Default.Compare(ports[i],
						                                  ihub[i]) == 0)
						{
							ports[i].InnerDevice = ihub[i];
						}
						else
						{
							CollectionModifiedEventArgs<IDevice> cmea =
								new CollectionModifiedEventArgs<IDevice>
									(ports[i]);
							ports[i] = null;
							Enm.OnDeviceRemoved(cmea);

							IHub shub = ihub[i] as IHub;
							if(shub != null)
								ports[i] = new Hub(Enm, this, shub);
							else
								ports[i] = new Device(Enm, this, ihub[i]);
							Enm.OnDeviceAdded
								(new CollectionModifiedEventArgs<IDevice>
								 (ports[i]));
						}
					}
				}
			}
		}
	}
}
