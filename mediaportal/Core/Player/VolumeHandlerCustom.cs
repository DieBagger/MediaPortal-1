#region Copyright (C) 2005-2007 Team MediaPortal

/* 
 *	Copyright (C) 2005-2007 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#endregion

using System;
using System.Collections;
using MediaPortal.Util;
using MediaPortal.Configuration;

namespace MediaPortal.Player
{
	internal class VolumeHandlerCustom : VolumeHandler
	{
		#region Constructors

		public VolumeHandlerCustom()
		{
      using (MediaPortal.Profile.Settings reader = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
			{
				string text = reader.GetValueAsString("volume", "table", string.Empty);

				if(text == string.Empty)
					return;

				ArrayList array = new ArrayList();

				try
				{
					foreach(string volume in text.Split(new char[] { ',', ';' }))
					{
						if(volume == string.Empty)
							continue;

						array.Add(Math.Max(this.Minimum, Math.Min(this.Maximum, int.Parse(volume))));
					}

					array.Sort();

					this.Table = (int[])array.ToArray(typeof(int));
				}
				catch
				{
					// heh, its undocumented remember, no fancy logging going on here
				}
			}
		}

		#endregion Constructors
	}
}
