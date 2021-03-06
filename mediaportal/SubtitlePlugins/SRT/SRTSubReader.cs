#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.IO;
using System.Text;

namespace MediaPortal.Subtitle
{
  /// <summary>
  /// 
  /// </summary>
  public class SRTSubReader : ISubtitleReader
  {
    private SubTitles m_subs = new SubTitles();

    public SRTSubReader()
    {
      // 
      // TODO: Add constructor logic here
      //
    }

    public override bool SupportsFile(string strFileName)
    {
      string strExt = Path.GetExtension(strFileName).ToLower();
      if (strExt == ".srt")
      {
        return true;
      }
      return false;
    }

    public override bool ReadSubtitles(string strFileName)
    {
      try
      {
        m_subs.Clear();
        using (StreamReader oRead = new StreamReader(strFileName, Encoding.GetEncoding(1252)))
        {
          while (oRead.Peek() >= 0)
          {
            string s = oRead.ReadLine();
            string sText = "";
            string StartTime = "";
            string EndTime = "";

            if (s != null && s.Trim() != "")
            {
              if (s.Trim().IndexOf(":") > 0)
              {
                StartTime = s.Trim().Substring(0, 12);
                EndTime = s.Substring(17).Trim();

                while (s.Trim() != "")
                {
                  s = oRead.ReadLine();
                  if (s == null)
                  {
                    break;
                  }
                  if (s.Trim() != "")
                  {
                    if (sText != "")
                    {
                      sText += "\n\r";
                    }

                    sText += s.Replace("\\N", "\n\r").Replace("\\N", "\n\r");
                  }
                }


                SubTitles.Line newLine = new SubTitles.Line();
                newLine.StartTime = GetSamiTime(StartTime.Replace(":", "").Replace(",", ""));
                newLine.Text = sText;
                newLine.EndTime = GetSamiTime(EndTime.Replace(":", "").Replace(",", ""));
                m_subs.Add(newLine);
              }
            }
          }
          if (m_subs.Count > 0)
          {
            return true;
          }
          return false;
        }
      }
      catch (Exception) {}

      return false;
    }

    //*********************************************************************************************
    private Int32 GetSamiTime(string HHMMSSmmm)
    {
      //Create same Time like SAMI File
      //H = H * 60 * 60
      //M = M * 60
      //S = S
      //Total = (H + M + S * 1000) + Milliseconds

      Int32 H = 0;
      Int32 M = 0;
      Int32 S = 0;
      Int32 Mil = 0;

      if (HHMMSSmmm.Length == 7)
      {
        M = Convert.ToInt32(HHMMSSmmm.Substring(0, 2));
        S = Convert.ToInt32(HHMMSSmmm.Substring(2, 2));
        Mil = Convert.ToInt32(HHMMSSmmm.Substring(4));
      }
      else if (HHMMSSmmm.Length == 9)
      {
        H = Convert.ToInt32(HHMMSSmmm.Substring(0, 2));
        M = Convert.ToInt32(HHMMSSmmm.Substring(2, 2));
        S = Convert.ToInt32(HHMMSSmmm.Substring(4, 2));
        Mil = Convert.ToInt32(HHMMSSmmm.Substring(6));
      }

      Int32 SamiTime = (((H * 3600) + (M * 60) + S) * 1000) + Mil;
      return SamiTime;
    }


    public override SubTitles Subs
    {
      get { return m_subs; }
    }
  }
}