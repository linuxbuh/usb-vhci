/*
 * MainForm.cs -- VHCI GUI
 *
 * Copyright (C) 2010-2015 Michael Singer <michael@a-singer.de>
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
using System.Windows.Forms;
using System.Collections.Generic;

namespace VhciGui
{
	public partial class MainForm : Form
	{
		private TreeNode localRootNode;
		private TreeNode localVirtualNode;
		private TreeNode localPhysicalNode;

		private Usb.Enumeration.StatefullEnumerator enm;

		public MainForm()
		{
			InitializeComponent();
			Usb.UsbFS.Enumeration.Enumerator statelessEnum =
				new Usb.UsbFS.Enumeration.Enumerator();
			enm = new Usb.Enumeration.StatefullEnumerator(statelessEnum);
			enm.DeviceAdded += enm_DeviceAdded;
			enm.DeviceRemoved += enm_DeviceRemoved;
			localVirtualNode = new TreeNode("virtual devices");
			localPhysicalNode = new TreeNode("physical devices");
			localRootNode = new TreeNode("local",
			                             new TreeNode[] { localVirtualNode,
			                                              localPhysicalNode });
			localRootNode.ExpandAll();
			tree.BeginUpdate();
			tree.Nodes.Add(localRootNode);
			ScanBus();
			tree.EndUpdate();
		}

		private void closeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void rescanToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ScanBus();
		}

		private void ShowError(string action, string error, Exception e)
		{
			string title = "VhciGui - " + action;
			string text = error;
			if(e != null)
			{
				text += "\n\nDo you want to see the exception?" +
					"\n\n(If you choose 'Yes', I will " +
					"also copy it to your clipboard.)";
				DialogResult r = MessageBox.Show(this,
				                                 text,
				                                 title,
				                                 MessageBoxButtons.YesNo,
				                                 MessageBoxIcon.Question);
				if(r == DialogResult.Yes)
				{
					Clipboard.SetText(e.ToString(), TextDataFormat.Text);
					MessageBox.Show(this,
					                e.ToString(),
					                title,
					                MessageBoxButtons.OK,
					                MessageBoxIcon.Error);
				}
			}
			else
			{
				MessageBox.Show(this,
				                text,
				                title,
				                MessageBoxButtons.OK,
				                MessageBoxIcon.Error);
			}
		}

		private void acquireToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = (TreeNode)deviceMenu.Tag;
			DeviceNode dev = (DeviceNode)node.Tag;
			if(acquireToolStripMenuItem.Checked)
			{
				dev.ReleaseAcquiree();
			}
			else
			{
				try
				{
					dev.Acquire();
				}
				catch(System.IO.IOException ex)
				{
					ShowError("Acquire",
					          "Failed. Maybe you are not allowed " +
					          "to acquire this device.",
					          ex);
					return;
				}
			}
			System.Drawing.Font font = node.NodeFont;
			System.Drawing.FontStyle style =
				font.Style & ~System.Drawing.FontStyle.Bold;
			if(dev.IsAcquired) style |= System.Drawing.FontStyle.Bold;
			node.NodeFont = new System.Drawing.Font(font, style);
			tree.Refresh();
		}

		private void probeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TreeNode node = (TreeNode)deviceMenu.Tag;
			DeviceNode dev = (DeviceNode)node.Tag;
			try
			{
				dev.DriversProbe();
			}
			catch(System.IO.IOException ex)
			{
				ShowError("Trigger Driver Probe",
				          "Failed.",
				          ex);
			}
		}

		private void tree_AfterSelect(object sender, TreeViewEventArgs e)
		{
			object tag = e.Node.Tag;
			ViewNode(tag as Node);
			if(tag is Exception)
			{
				view.BeginUpdate();
				view.Clear();
				view.Columns.Add("hint");
				view.Items.Add("double click on the treenode item to show " +
				               "the exception");
				view.AutoResizeColumns(ColumnHeaderAutoResizeStyle.
				                       ColumnContent);
				view.EndUpdate();
			}
		}

		private void tree_NodeMouseClick(object sender,
		                                 TreeNodeMouseClickEventArgs e)
		{
			if(e.Node.ContextMenuStrip != null)
				e.Node.ContextMenuStrip.Tag = e.Node;
			DeviceNode dev = e.Node.Tag as DeviceNode;
			if(dev != null)
			{
				acquireToolStripMenuItem.Enabled = dev.IsAcquireable;
				acquireToolStripMenuItem.Checked = dev.IsAcquired;
				probeToolStripMenuItem.Enabled = !dev.IsAcquired;
			}
		}

		private void tree_NodeMouseDoubleClick(object sender,
		                                       TreeNodeMouseClickEventArgs e)
		{
			object tag = e.Node.Tag;
			if(tag is Exception)
			{
				Exception ex = (Exception)tag;
				ShowError("Bus Enumeration",
				          "Failed.",
				          ex);
			}
		}

		private void
			enm_DeviceAdded(object sender,
			                Usb.CollectionModifiedEventArgs
			                <Usb.Enumeration.IDevice> e)
		{
			Usb.Enumeration.IDevice dev = e.Item;
			Usb.Enumeration.IHub hub = dev as Usb.Enumeration.IHub;
			Usb.DeviceDescriptor d = dev.GetDescriptor();
			TreeNode guiNode;
			if(hub != null)
			{
				if(hub.IsRoot)
					guiNode = new TreeNode("root hub of bus# " +
					                       hub.Bus.ToString());
				else
					guiNode = new TreeNode(hub.ToString() + ": hub");
				guiNode.Expand();
			}
			else
			{
				Usb.ClassCodeTriple cct = d.ClassCodeTriple;
				if(cct == Usb.ClassCodeTriple.Null &&
				   d.IsUsingConfigurationIndex(1) &&
				   d[1].IsUsingInterfaceIndex(0) &&
				   d[1][0].IsUsingAlternateSettingIndex(0))
					cct = d[1][0][0].ClassCodeTriple;
				string cs = "unknown";
				if(cct != Usb.ClassCodeTriple.Null)
					cs = cct.BaseClassString();
				guiNode = new TreeNode(dev.ToString() +
				                       ": " + cs + " (address: " +
				                       dev.Address.ToString() +
				                       ")");
			}
			guiNode.ContextMenuStrip = deviceMenu;
			DeviceNode n = new DeviceNode(guiNode,
			                              dev,
			                              d);
			dev.Tag = n;
			guiNode.Tag = n;
			GenDescriptorNodes(n);
			TreeNode p;
			int index;
			if(hub != null && hub.IsRoot)
			{
				p = localPhysicalNode;
				List<Usb.Enumeration.IHub> l =
					new List<Usb.Enumeration.IHub>(enm.State);
				index = l.IndexOf(hub);
			}
			else
			{
				Usb.Enumeration.IHub ph = dev.Parent;
				p = ((DeviceNode)ph.Tag).GuiNode;
				index = ph.IndexOf(dev) + 1; // index 0 is "Descriptors"
				// If the hub was not probed by the kernel before,
				// then it may have had zero ports. This is a special
				// case and we allow the port count to be increased.
				if(p.Nodes.Count == 1) // "Descriptors" is always there
				{
					int cnt = ph.Count;
					for(int i = 1; i <= cnt; i++)
						p.Nodes.Add(Usb.Enumeration.Device.ToString(ph, i) +
						            ": [unconnected port]");
				}
				p.Nodes.RemoveAt(index);
			}
			if(hub != null)
			{
				int cnt = hub.Count;
				for(int i = 1; i <= cnt; i++)
					guiNode.Nodes.Add(Usb.Enumeration.Device.ToString(hub, i) +
					                  ": [unconnected port]");
			}
			p.Nodes.Insert(index, guiNode);
		}

		private void
			enm_DeviceRemoved(object sender,
			                  Usb.CollectionModifiedEventArgs
			                  <Usb.Enumeration.IDevice> e)
		{
			Usb.Enumeration.IDevice dev = e.Item;
			Usb.Enumeration.IHub hub = dev as Usb.Enumeration.IHub;
			DeviceNode node = (DeviceNode)dev.Tag;
			int index = node.GuiNode.Index;
			node.GuiNode.Remove();
			if(hub == null || !hub.IsRoot)
			{
				TreeNode parent = ((DeviceNode)dev.Parent.Tag).GuiNode;
				parent.Nodes.Insert(index,
				                    dev.ToString() +
				                    ": [unconnected port]");
			}
		}

		private void ScanBus()
		{
			tree.BeginUpdate();
			// remove error msg, if it exists
			if(localPhysicalNode.Nodes.Count == 1)
			{
				TreeNode err = localPhysicalNode.Nodes[0];
				if(err.Tag is Exception) localPhysicalNode.Nodes.Clear();
			}
			try
			{
				enm.Scan();
			}
			catch(Exception e)
			{
				enm.Reset();
				localPhysicalNode.Nodes.Clear();
				TreeNode err = new TreeNode("- failed to enumerate bus -");
				err.Tag = e;
				localPhysicalNode.Nodes.Add(err);
			}
			finally
			{
				localPhysicalNode.Expand();
				tree.EndUpdate();
			}
		}

		private static void GenDescriptorNodes(DeviceNode node)
		{
			TreeNode top = new TreeNode("Descriptors");
			TreeNode devTNode = new TreeNode("Device");
			DescriptorNode devDNode = new DescriptorNode(devTNode,
			                                             node,
			                                             node.Descriptor);
			devTNode.Tag = devDNode;
			GenCustomDescriptorNodes(devDNode);
			foreach(KeyValuePair<byte, Usb.ConfigurationDescriptor> kvp in
			        node.Descriptor)
			{
				Usb.ConfigurationDescriptor cd = kvp.Value;
				TreeNode cTNode = new TreeNode("Configuration# " +
				                               kvp.Key.ToString());
				DescriptorNode cDNode = new DescriptorNode(cTNode,
				                                           node,
				                                           cd);
				cTNode.Tag = cDNode;
				GenCustomDescriptorNodes(cDNode);
				foreach(KeyValuePair<byte, Usb.Interface> kvpi in cd)
				{
					Usb.Interface ifc = kvpi.Value;
					TreeNode iTNode = new TreeNode("Interface# " +
					                               kvpi.Key.ToString());
					if(ifc.AlternateSettingsCount == 1)
					{
						Usb.AlternateSetting aifc = ifc.ActiveAlternateSetting;
						DescriptorNode aDNode = new DescriptorNode(iTNode,
						                                           node,
						                                           aifc);
						iTNode.Tag = aDNode;
						GenCustomDescriptorNodes(aDNode);
						GenEndpointDescriptorNodes(aDNode);
					}
					else
					{
						foreach(KeyValuePair<byte, Usb.AlternateSetting> kvpa in
						        ifc)
						{
							Usb.AlternateSetting aifc = kvpa.Value;
							TreeNode aTNode =
								new TreeNode("AlternateSetting# " +
								             kvpa.Key.ToString());
							DescriptorNode aDNode = new DescriptorNode(aTNode,
							                                           node,
							                                           aifc);
							aTNode.Tag = aDNode;
							GenCustomDescriptorNodes(aDNode);
							GenEndpointDescriptorNodes(aDNode);
							iTNode.Nodes.Add(aTNode);
						}
					}
					cTNode.Nodes.Add(iTNode);
				}
				devTNode.Nodes.Add(cTNode);
			}
			top.Nodes.Add(devTNode);
			node.GuiNode.Nodes.Insert(0, top);
		}

		private static void GenEndpointDescriptorNodes(DescriptorNode node)
		{
			DeviceNode devNode = node.Device;
			TreeNode parent = node.GuiNode;
			Usb.AlternateSetting aifc = node.Descriptor as Usb.AlternateSetting;
			foreach(KeyValuePair<byte, Usb.Endpoint> kvp in aifc)
			{
				Usb.Endpoint ep = kvp.Value;
				TreeNode eTNode = new TreeNode("Endpoint# 0x" +
				                               kvp.Key.ToString("x2"));
				DescriptorNode eDNode = new DescriptorNode(eTNode,
				                                           devNode,
				                                           ep);
				eTNode.Tag = eDNode;
				GenCustomDescriptorNodes(eDNode);
				parent.Nodes.Add(eTNode);
			}
		}

		private static void GenCustomDescriptorNodes(DescriptorNode node)
		{
			DeviceNode devNode = node.Device;
			TreeNode parent = node.GuiNode;
			Usb.RegularDescriptor desc =
				node.Descriptor as Usb.RegularDescriptor;
			foreach(Usb.CustomDescriptor cd in desc.CustomDescriptors)
			{
				TreeNode cdTNode = new TreeNode("Custom");
				DescriptorNode cdDNode = new DescriptorNode(cdTNode,
				                                            devNode,
				                                            cd);
				cdTNode.Tag = cdDNode;
				parent.Nodes.Add(cdTNode);
			}
		}

		private static string ToHex(IEnumerable<byte> arr)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach(byte b in arr)
				sb.AppendFormat("{0:x2} ", b);
			return sb.ToString().Trim();
		}

		private static string StrEsc(string str)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append('"');
			foreach(char c in str)
			{
				if(c < 32 || c == '"')
				{
					switch(c)
					{
					case '\t': sb.Append("\\t");  break;
					case '\r': sb.Append("\\r");  break;
					case '\n': sb.Append("\\n");  break;
					case '"':  sb.Append("\\\""); break;
					default:
						sb.AppendFormat("\\x{0:x2}", (int)c);
						break;
					}
				}
				else
				{
					sb.Append(c);
				}
			}
			sb.Append('"');
			return sb.ToString();
		}

		private void ViewNode(Node node)
		{
			view.BeginUpdate();
			view.Clear();
			if(node is DeviceNode)
			{
				DeviceNode dn = (DeviceNode)node;
				Usb.Enumeration.IDevice dev = dn.Device;
				view.Columns.Add("Property");
				view.Columns.Add("Value");
				view.Items.Add(new ListViewItem(new string[] {
					"Address", dev.Address.ToString() }));
				view.Items.Add(new ListViewItem(new string[] {
					"Bus", dev.Bus.ToString() }));
				view.Items.Add(new ListViewItem(new string[] {
					"Tier", dev.Tier.ToString() }));
				view.Items.Add(new ListViewItem(new string[] {
					"Branch",
					Usb.Enumeration.Device.BranchArrayToString(dev.Branch) }));
				view.Items.Add(new ListViewItem(new string[] {
					"Speed", DataRateToString(dev.DataRate) }));
				view.Items.Add(new ListViewItem(new string[] {
					"Is Acquireable", dev.IsAcquireable.ToString() }));
			}
			else if(node is DescriptorNode)
			{
				DescriptorNode dn = (DescriptorNode)node;
				Usb.DeviceDescriptor.TransferDescriptorActiveState
					(dn.Device.Device.GetDescriptor(), dn.Device.Descriptor);
				Usb.Descriptor d = dn.Descriptor;
				view.Columns.Add("Field");
				view.Columns.Add("Value");
				if(d is Usb.DeviceDescriptor)
				{
					Usb.DeviceDescriptor desc = (Usb.DeviceDescriptor)d;
					bool qualifier = desc.IsQualifier;
					view.Items.Add(new ListViewItem(new string[] {
						"bLength", desc.LocalSize.ToString() }));
					byte dt = qualifier ?
						Usb.Descriptor.DeviceQualifierDescriptorType :
						Usb.Descriptor.DeviceDescriptorType;
					string dts = qualifier ?
						" (DEVICE_QUALIFIER)" :
						" (DEVICE)";
					view.Items.Add(new ListViewItem(new string[] {
						"bDescriptorType", dt.ToString() + dts }));
					view.Items.Add(new ListViewItem(new string[] {
						"bcdUSB", "0x" + desc.BcdUsb.ToString("x4") }));
					string cstr =
						" (" + desc.ClassCodeTriple.BaseClassString() + ")";
					if(desc.DeviceClass == 0)
						cstr = " (per interface)";
					view.Items.Add(new ListViewItem(new string[] {
						"bDeviceClass",
						"0x" + desc.DeviceClass.ToString("x2") + cstr }));
					view.Items.Add(new ListViewItem(new string[] {
						"bDeviceSubClass",
						"0x" + desc.DeviceSubClass.ToString("x2") }));
					string pstr =
						" (" + desc.ClassCodeTriple.ProtocolString() + ")";
					if(desc.DeviceProtocol == 0)
						pstr = " (per interface)";
					view.Items.Add(new ListViewItem(new string[] {
						"bDeviceProtocol",
						"0x" + desc.DeviceProtocol.ToString("x2") + pstr }));
					view.Items.Add(new ListViewItem(new string[] {
						"bMaxPacketSize0",
						desc.EndpointZero.MaxPacketSize.ToString() }));
					if(!qualifier)
					{
						view.Items.Add(new ListViewItem(new string[] {
							"idVendor",
							"0x" + desc.VendorID.ToString("x4") }));
						view.Items.Add(new ListViewItem(new string[] {
							"idProduct",
							"0x" + desc.ProductID.ToString("x4") }));
						view.Items.Add(new ListViewItem(new string[] {
							"bcdDevice",
							"0x" + desc.BcdDevice.ToString("x4") }));
						view.Items.Add(new ListViewItem(new string[] {
							"iManufacturer",
							desc.ManufacturerStringIndex.ToString() }));
						view.Items.Add(new ListViewItem(new string[] {
							"iProduct",
							desc.ProductStringIndex.ToString() }));
						view.Items.Add(new ListViewItem(new string[] {
							"iSerialNumber",
							desc.SerialNumberStringIndex.ToString() }));
					}
					view.Items.Add(new ListViewItem(new string[] {
						"bNumConfigurations",
						desc.ConfigurationsCount.ToString() }));
					int aidxi = desc.ActiveConfigurationIndex;
					string aidx = aidxi.ToString();
					if(aidxi == 0) aidx += " (none)";
					view.Items.Add(new ListViewItem(new string[] {
						"ActiveConfigurationIndex",
						aidx }));
				}
				else if(d is Usb.ConfigurationDescriptor)
				{
					Usb.ConfigurationDescriptor desc =
						(Usb.ConfigurationDescriptor)d;
					bool otherSpeed = desc.IsOtherSpeed;
					view.Items.Add(new ListViewItem(new string[] {
						"bLength", desc.LocalSize.ToString() }));
					byte dt = otherSpeed ?
						Usb.Descriptor.OtherSpeedConfigurationDescriptorType :
						Usb.Descriptor.ConfigurationDescriptorType;
					string dts = otherSpeed ?
						" (OTHER_SPEED_CONFIGURATION)" :
						" (CONFIGURATION)";
					view.Items.Add(new ListViewItem(new string[] {
						"bDescriptorType", dt.ToString() + dts }));
					view.Items.Add(new ListViewItem(new string[] {
						"wTotalLength", desc.Size.ToString() }));
					view.Items.Add(new ListViewItem(new string[] {
						"bNumInterfaces", desc.InterfacesCount.ToString() }));
					view.Items.Add(new ListViewItem(new string[] {
						"bConfigurationValue", desc.Index.ToString() }));
					view.Items.Add(new ListViewItem(new string[] {
						"iConfiguration",
						desc.StringDescriptorIndex.ToString() }));
					string attr = desc.SelfPowered ? "SelfPowered" : "";
					if(desc.RemoteWakeup)
					{
						if(attr != "") attr += " | RemoteWakeup";
						else           attr += "RemoteWakeup";
					}
					if(attr == "") attr = "0";
					view.Items.Add(new ListViewItem(new string[] {
						"bmAttributes", attr }));
					int mp = desc.MaxPower;
					view.Items.Add(new ListViewItem(new string[] {
						"bMaxPower",
						mp.ToString() + " (" + (mp * 2).ToString() + " mA)"}));
					view.Items.Add(new ListViewItem(new string[] {
						"IsActive",
						desc.IsActive.ToString() }));
				}
				else if(d is Usb.AlternateSetting)
				{
					Usb.AlternateSetting desc = (Usb.AlternateSetting)d;
					view.Items.Add(new ListViewItem(new string[] {
						"bLength", desc.LocalSize.ToString() }));
					byte dt = Usb.Descriptor.InterfaceDescriptorType;
					string dts = " (INTERFACE)";
					view.Items.Add(new ListViewItem(new string[] {
						"bDescriptorType", dt.ToString() + dts }));
					view.Items.Add(new ListViewItem(new string[] {
						"bInterfaceNumber", desc.Interface.Index.ToString() }));
					view.Items.Add(new ListViewItem(new string[] {
						"bAlternateSetting", desc.Index.ToString() }));
					view.Items.Add(new ListViewItem(new string[] {
						"bNumEndpoints", desc.EndpointCount.ToString() }));
					string cstr =
						" (" + desc.ClassCodeTriple.BaseClassString() + ")";
					view.Items.Add(new ListViewItem(new string[] {
						"bInterfaceClass",
						"0x" + desc.InterfaceClass.ToString("x2") + cstr }));
					view.Items.Add(new ListViewItem(new string[] {
						"bInterfaceSubClass",
						"0x" + desc.InterfaceSubClass.ToString("x2") }));
					string pstr =
						" (" + desc.ClassCodeTriple.ProtocolString() + ")";
					view.Items.Add(new ListViewItem(new string[] {
						"bInterfaceProtocol",
						"0x" + desc.InterfaceProtocol.ToString("x2") + pstr }));
					view.Items.Add(new ListViewItem(new string[] {
						"iInterface",
						desc.StringDescriptorIndex.ToString() }));
					string owner = desc.Interface.Owner;
					if(owner == null) owner = "null (none)";
					else owner = "\"" + owner + "\"";
					view.Items.Add(new ListViewItem(new string[] {
						"Driver",
						owner }));
					view.Items.Add(new ListViewItem(new string[] {
						"IsActive",
						desc.IsActive.ToString() }));
				}
				else if(d is Usb.Endpoint)
				{
					Usb.Endpoint desc = (Usb.Endpoint)d;
					Usb.IsochronousEndpoint iso =
						desc as Usb.IsochronousEndpoint;
					view.Items.Add(new ListViewItem(new string[] {
						"bLength", desc.LocalSize.ToString() }));
					byte dt = Usb.Descriptor.EndpointDescriptorType;
					string dts = " (ENDPOINT)";
					view.Items.Add(new ListViewItem(new string[] {
						"bDescriptorType", dt.ToString() + dts }));
					view.Items.Add(new ListViewItem(new string[] {
						"bEndpointAddress",
						"0x" + desc.BaseAddress.ToString("x2") }));
					string attr = "?";
					int interval = 0;
					string istr = "";
					Usb.DataRate rate = dn.Device.Device.DataRate;
					bool otherSpeed = ((Usb.AlternateSetting)desc.Parent).
						Interface.Configuration.IsOtherSpeed;
					if(desc is Usb.ControlEndpoint)
					{
						attr = "Control";
						interval = ((Usb.ControlEndpoint)desc).MaxNakRate;
						if(rate == Usb.DataRate.High && !otherSpeed ||
						   rate != Usb.DataRate.High && otherSpeed)
							istr = " (" + interval.ToString() + " microframes)";
					}
					else if(desc is Usb.IsochronousEndpoint)
					{
						attr = "Isochronous";
						switch(iso.SynchronizationType)
						{
						case Usb.SynchronizationType.NoSynchronization:
							attr += " | NoSynchronization";
							break;
						case Usb.SynchronizationType.Asynchronous:
							attr += " | Asynchronous";
							break;
						case Usb.SynchronizationType.Adaptive:
							attr += " | Adaptive";
							break;
						case Usb.SynchronizationType.Synchronous:
							attr += " | Synchronous";
							break;
						}
						switch(iso.UsageType)
						{
						case Usb.UsageType.DataEndpoint:
							attr += " | DataEndpoint";
							break;
						case Usb.UsageType.FeedbackEndpoint:
							attr += " | FeedbackEndpoint";
							break;
						case Usb.UsageType.ImplicitFeedbackDataEndpoint:
							attr += " | ImplicitFeedbackDataEndpoint";
							break;
						}
						interval = ((Usb.IsochronousEndpoint)desc).Interval;
						if(interval >= 1 && interval <= 16)
							istr = " (" +
								((int)Math.Pow(2.0, interval - 1)).ToString() +
								(rate == Usb.DataRate.High && !otherSpeed ||
								 rate != Usb.DataRate.High && otherSpeed ?
								 " microframes)" : " frames)");
					}
					else if(desc is Usb.BulkEndpoint)
					{
						attr = "Bulk";
						interval = ((Usb.BulkEndpoint)desc).MaxNakRate;
						if(rate == Usb.DataRate.High && !otherSpeed ||
						   rate != Usb.DataRate.High && otherSpeed)
							istr = " (" + interval.ToString() + " microframes)";
					}
					else if(desc is Usb.InterruptEndpoint)
					{
						attr = "Interrupt";
						interval = ((Usb.InterruptEndpoint)desc).Interval;
						if(rate == Usb.DataRate.High && !otherSpeed ||
						   rate != Usb.DataRate.High && otherSpeed)
						{
							if(interval >= 1 && interval <= 16)
								istr = " (" +
									((int)Math.Pow(2.0, interval - 1)).
									ToString() + " microframes)";
						}
						else
						{
							if(interval != 0)
								istr = " (" + interval.ToString() + " frames)";
						}
					}
					view.Items.Add(new ListViewItem(new string[] {
						"bmAttributes", attr }));
					int mps = desc.MaxPacketSize;
					view.Items.Add(new ListViewItem(new string[] {
						"wMaxPacketSize",
						"0x" + mps.ToString("x4") + " (" +
							(mps & 0x07ff).ToString() + " bytes)" }));
					view.Items.Add(new ListViewItem(new string[] {
						"bInterval", interval.ToString() + istr }));
				}
				else if(d is Usb.CustomDescriptor)
				{
					Usb.CustomDescriptor desc = (Usb.CustomDescriptor)d;
					view.Items.Add(new ListViewItem(new string[] {
						"bLength", desc.Size.ToString() }));
					view.Items.Add(new ListViewItem(new string[] {
						"bDescriptorType", desc.Type.ToString() }));
					view.Items.Add(new ListViewItem(new string[] {
						"Content", ToHex(desc.Data) }));
				}
				else if(d is Usb.StringDescriptor)
				{
					Usb.StringDescriptor desc = (Usb.StringDescriptor)d;
					view.Items.Add(new ListViewItem(new string[] {
						"String", StrEsc(desc.String) }));
				}

				if(d is Usb.RegularDescriptor)
				{
					Usb.RegularDescriptor desc = (Usb.RegularDescriptor)d;
					if(desc.Tail.Count > 0)
						view.Items.Add(new ListViewItem(new string[] {
							"Tail", ToHex(desc.Tail) }));
				}
			}
			view.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
			view.EndUpdate();
		}

		private static string DataRateToString(Usb.DataRate rate)
		{
			switch(rate)
			{
			case Usb.DataRate.Low:   return "1.5 Mbit/s";
			case Usb.DataRate.High:  return "480 Mbit/s";
			case Usb.DataRate.Super: return "5 Gbit/s";
			default:                 return "12 Mbit/s";
			}
		}
	}
}
