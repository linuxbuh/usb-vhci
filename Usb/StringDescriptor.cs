/*
 * StringDescriptor.cs -- USB related classes
 *
 * Copyright (C) 2009-2015 Michael Singer <michael@a-singer.de>
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
using System.Runtime.InteropServices;

namespace Usb
{
	public class StringDescriptor : Descriptor
	{
		public const char EnglishUnitedStatesLangID = (char)0x0409U;

		public static readonly StringDescriptor NullString =
			new StringDescriptor();
		public static readonly StringDescriptor EnglishUnitedStatesLangIDTable =
			new StringDescriptor(new char[] { EnglishUnitedStatesLangID });

		private char[] chars;

		public StringDescriptor() : base()
		{
		}

		public StringDescriptor(char[] chars) : base()
		{
			this.chars = chars;
		}

		public StringDescriptor(string str) : base()
		{
			if(!string.IsNullOrEmpty(str))
				chars = str.ToCharArray();
		}

		private void Init(byte[] desc, ref int pos)
		{
			int al = desc.Length;
			if(pos < 0 || pos >= al)
				throw new ArgumentOutOfRangeException("pos", pos, "");
			int l = desc[pos];
			if(l == 0)
				throw new ArgumentException("desc len is 0", "desc");
			if(al < pos++ + l)
				throw new InvalidOperationException();
			if((l & 1) != 0 || l < 2)
				throw new ArgumentException("len invalid", "desc");
			l -= 2;
			if(desc[pos++] != 3)
				throw new ArgumentException("type invalid", "desc");
			l = checked(l / 2);
			if(l != 0) chars = new char[l];
			for(int i = 0; i < l; i++)
			{
				chars[i] = unchecked((char)((ushort)(desc[(i + 1) * 2 + pos]) |
				                            (ushort)(desc[(i + 1) * 2 + 1 +
				                                          pos]) << 8));
			}
			pos += l * 2;
		}

		public StringDescriptor(byte[] desc, ref int pos) : base()
		{
			if(desc == null)
				throw new ArgumentNullException("desc");
			int p = pos;
			Init(desc, ref p);
			pos = p; // pos gets updated only if there was no exception thrown
		}

		public StringDescriptor(byte[] desc) : base()
		{
			if(desc != null)
			{
				int l = desc.Length;
				if(l == 0 || desc[0] != l)
					throw new ArgumentException("len invalid", "desc");
				int p = 0;
				Init(desc, ref p);
			}
		}

		public override bool Validate(ValidationMode mode)
		{
			if(chars == null) return true;
			return chars.Length < 127;
		}

		public string String
		{
			get { return new string(chars); }
			set
			{
				if(string.IsNullOrEmpty(value))
					chars = null;
				else
					chars = value.ToCharArray();
			}
		}

		public override int GetDescriptor([In, Out] byte[] desc, int index)
		{
			int dl = chars == null ? 2 : checked(2 + chars.Length * 2);
			int evmax = (byte.MaxValue | 1) ^ 1;
			if(dl > evmax)
				throw new InvalidOperationException();
			if(desc == null)
				return dl;
			int al = desc.Length;
			if(index < 0 || index >= al)
				throw new ArgumentOutOfRangeException("index", index, "");
			if(al < dl + index)
				throw new InvalidOperationException();
			desc[index] = (byte)dl;
			desc[index + 1] = Descriptor.StringDescriptorType;
			if(chars == null) return dl;
			int l = chars.Length;
			int lmax = (dl - 2) / 2;
			if(l > lmax) l = lmax;
			for(int i = 0; i < l; i++)
			{
				desc[index + (i + 1) * 2] = unchecked((byte)chars[i]);
				desc[index + (i + 1) * 2 + 1] =
					unchecked((byte)((ushort)chars[i] >> 8));
			}
			return dl;
		}

		public override void Dump(System.IO.TextWriter stm, string prefix)
		{
			if(stm == null) throw new ArgumentNullException("stm");
			if(prefix == null) throw new ArgumentNullException("prefix");
			int l = chars == null ? 0 : chars.Length;
			string s = Escape(new string(chars));
			stm.WriteLine(prefix + "Length:         " + l.ToString());
			stm.WriteLine(prefix + "String:         \"" + s + "\"");
		}

		public static string Escape(string str)
		{
			if(str == null) return null;
			int l = str.Length;
			System.Text.StringBuilder sb = new System.Text.StringBuilder(l);
			foreach(char c in str)
			{
				if(c == '\\')
				{
					sb.Append(@"\\");
				}
				else if(c < 32)
				{
					switch(c)
					{
					case '\n':
						sb.Append(@"\n");
						break;
					case '\r':
						sb.Append(@"\r");
						break;
					case '\t':
						sb.Append(@"\t");
						break;
					default:
						sb.AppendFormat(@"\u{0:x4}", unchecked((ushort)c));
						break;
					}
				}
				else
				{
					sb.Append(c);
				}
			}
			return sb.ToString();
		}
	}
}
