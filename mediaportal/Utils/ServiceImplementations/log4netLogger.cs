﻿using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using log4net;
using MediaPortal.Configuration;
using MediaPortal.Profile;
using MediaPortal.Services;

namespace MediaPortal.ServiceImplementations
{
  public class Log4netLogger : MediaPortal.Services.ILog
  {
    #region Variables
    private Level _minLevel;
    private bool _configuration;
    #endregion

    #region Constructors/Destructors
    public Log4netLogger()
    {
      string logPath = Config.GetFolder(Config.Dir.Log);
      using (Settings xmlreader = new MPSettings())
      {
        _minLevel = (Level)Enum.Parse(typeof(Level), xmlreader.GetValueAsString("general", "loglevel", "2"));
      }
      _configuration = false;
      string appPath = Path.GetDirectoryName(Application.ExecutablePath);
      
      XmlDocument xmlDoc = new XmlDocument();
      FileStream fs = new FileStream(Path.Combine(appPath, "log4net.config"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      xmlDoc.Load(fs);
      fs.Close();
      
      XmlNodeList nodeList = xmlDoc.SelectNodes("configuration/log4net/appender/file");
      foreach (XmlNode node in nodeList)
      {
        if (node.Attributes != null)
        {
          foreach (XmlAttribute attribute in node.Attributes)
          {
            if (attribute.Name.Equals("value"))
            {
              attribute.Value = Path.Combine(logPath, Path.GetFileName(attribute.Value));
              break;
            }
          }
        }
      }
      MemoryStream mStream = new MemoryStream();
      xmlDoc.Save(mStream);
      mStream.Seek(0, SeekOrigin.Begin);
      log4net.Config.XmlConfigurator.Configure(mStream);
    }
    #endregion

    private string GetLogTypeName(LogType type)
    {
      if (_configuration) return "Config";
      switch (type)
      {
        case LogType.Recorder: return ("Rec");
        case LogType.Error: return ("Error");
        case LogType.EPG: return ("EPG");
        case LogType.VMR9:return ("VMR9");
        case LogType.MusicShareWatcher:return ("MusicSh");
        case LogType.WebEPG:return ("WebEPG");
        default: return ("Log");
      }     
    }


    #region Implementation of ILog
    public void BackupLogFiles() {}
    public void BackupLogFile(LogType logType) {}

    
    public void Info(string format, params object[] args)
    {
      Info(LogType.Log, format, args);
    }

    public void Info(LogType type, string format, params object[] args)
    {
      if (Level.Information <= _minLevel)
      {
        if (args.Length > 0)
        {
          LogManager.GetLogger(GetLogTypeName(type)).InfoFormat(format, args);
        }
        else
        {
          LogManager.GetLogger(GetLogTypeName(type)).Info(format);
        }
      }
    }

    public void Warn(string format, params object[] args)
    {
      Warn(LogType.Log, format, args);
    }

    public void Warn(LogType type, string format, params object[] args)
    {
      if (Level.Warning <= _minLevel)
      {
        if (args.Length > 0)
        {
          LogManager.GetLogger(GetLogTypeName(type)).WarnFormat(format, args);
        }
        else
        {
          LogManager.GetLogger(GetLogTypeName(type)).Warn(format);
        }
      }
    }

    public void Debug(string format, params object[] args)
    {
      Debug(LogType.Log, format, args);
    }

    public void Debug(LogType type, string format, params object[] args)
    {
      if (Level.Debug <= _minLevel)
      {
        if (args.Length > 0)
        {
          LogManager.GetLogger(GetLogTypeName(type)).DebugFormat(format, args);
        }
        else
        {
          LogManager.GetLogger(GetLogTypeName(type)).Debug(format);
        }
      }
    }

    public void Error(string format, params object[] args)
    {
      Error(LogType.Error, format, args);
    }

    public void Error(LogType type, string format, params object[] args)
    {
      if (args.Length > 0)
      {
        LogManager.GetLogger(GetLogTypeName(type)).ErrorFormat(format, args);
      }
      else
      {
        LogManager.GetLogger(GetLogTypeName(type)).Error(format);
      }
    }

    public void Error(Exception ex)
    {
      LogManager.GetLogger(LogType.Log.GetType()).Error("", ex);
    }

    public void SetConfigurationMode()
    {
      _configuration = true;
    }

    public void SetLogLevel(Level logLevel)
    {
      _minLevel = logLevel;
    }

    #endregion
  }
}
