/*
 * Program.cs -- VHCI GUI
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

namespace VhciGui
{
	public static class Program
	{
		private const string me = "VhciGui.exe";
		private const string me2 = "VhciGui";
		private readonly static string ver =
			System.Reflection.Assembly.GetExecutingAssembly().
				GetName().Version.ToString();

		[STAThread]
		public static int Main(string[] args)
		{
			for(int i = 0; i < args.Length; i++)
			{
				if(args[i].Length > 0 && args[i][0] == '-')
				{
					switch(args[i])
					{
					case "--help":
						Copyright();
						Console.WriteLine();
						Help();
						return 0;
					case "--version":
					case "-V":
						Copyright();
						return 0;
					default:
						Help();
						return 1;
					}
				}
				else
				{
					Help();
					return 1;
				}
			}
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
			return 0;
		}

		private static void Copyright()
		{
			Console.WriteLine(me2 + " " + ver);
			Console.WriteLine("Copyright © 2010-2015 Michael Singer <michael@a-singer.de>");
			Console.WriteLine("This is free software; see the source for copying conditions.  There is NO");
			Console.WriteLine("warranty; not even for MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.");
		}

		private static void Help()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine(" " + me + " [OPTIONS]");
			Console.WriteLine();
			Console.WriteLine("OPTIONS:");
			Console.WriteLine(" --help              " +
			                  "this help");
			Console.WriteLine(" -V --version        " +
			                  "print version");
		}
	}
}
