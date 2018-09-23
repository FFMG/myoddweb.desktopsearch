//This file is part of Myoddweb.DesktopSearch.
//
//    Myoddweb.DesktopSearch is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    Myoddweb.DesktopSearch is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with Myoddweb.DesktopSearch.  If not, see<https://www.gnu.org/licenses/gpl-3.0.en.html>.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using myoddweb.desktopsearch.interfaces.Configs;
using myoddweb.desktopsearch.interfaces.Enums;
using Newtonsoft.Json;
using ILogger = myoddweb.desktopsearch.interfaces.Configs.ILogger;
// ReSharper disable SuggestBaseTypeForParameter

namespace myoddweb.desktopsearch.service.Configs
{
  internal class Config : IConfig
  {
    /// <inheritdoc />
    [DefaultValue(255)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int MaxNumCharactersPerWords { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(25)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public int MaxNumCharactersPerParts { get; protected set; }

    /// <inheritdoc />
    [JsonProperty(Required = Required.Always)]
    public IPaths Paths { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public ITimers Timers { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(null)]
    public IPerformance Performance { get; protected set; }

    /// <inheritdoc />
    [JsonProperty(Required = Required.Always)]
    public IList<ILogger> Loggers { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public IProcessors Processors { get; protected set; }

    /// <inheritdoc />
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public IWebServer WebServer { get; protected set; }

    /// <inheritdoc />
    [JsonProperty(Required = Required.Always)]
    public IDatabase Database { get; protected set; }

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
      if (null == Performance)
      {
        Performance = JsonConvert.DeserializeObject<ConfigPerformance>("{}");
      }

      if (null == Timers)
      {
        // set the default values.
        Timers = JsonConvert.DeserializeObject<ConfigTimers>("{}");
      }

      if (null == Processors)
      {
        // set the default values.
        Processors = JsonConvert.DeserializeObject<ConfigProcessor>("{}");
      }

      if (null == WebServer)
      {
        // set the default values.
        WebServer = JsonConvert.DeserializeObject<ConfigWebServer>("{}");
      }
    }

    /// <summary>
    /// By using this constructor here we allowing JSon to convert from ConfigPaths > IPaths
    /// </summary>
    /// <param name="maxNumCharactersPerWords"></param>
    /// <param name="maxNumCharactersPerParts"></param>
    /// <param name="paths"></param>
    /// <param name="timers"></param>
    /// <param name="performance"></param>
    /// <param name="processors"></param>
    /// <param name="webserver"></param>
    /// <param name="database"></param>
    /// <param name="loggers"></param>
    public Config(
      int maxNumCharactersPerWords,
      int maxNumCharactersPerParts,
      ConfigPaths paths, 
      ConfigTimers timers, 
      ConfigPerformance performance,
      ConfigProcessor processors, 
      ConfigWebServer webserver,
      ConfigSqliteDatabase database,
      IEnumerable<ConfigLogger> loggers )
    {
      Paths = paths;
      Timers = timers;
      Loggers = RecreateLoggers(loggers);
      Processors = processors;
      WebServer = webserver;
      Database = database;
      Performance = performance;

      if (maxNumCharactersPerWords <= 0)
      {
        throw new ArgumentException("The max number number of characters per words cannot be -ve or zero");
      }
      MaxNumCharactersPerWords = maxNumCharactersPerWords;

      if (maxNumCharactersPerParts <= 0)
      {
        throw new ArgumentException("The max number number of characters per parts cannot be -ve or zero");
      }
      MaxNumCharactersPerParts = maxNumCharactersPerParts;
    }

    /// <summary>
    /// Given the various loggers from json re-create the configs.
    /// </summary>
    /// <param name="givenLoggers"></param>
    /// <returns></returns>
    private static List<ILogger> RecreateLoggers(IEnumerable<ConfigLogger> givenLoggers)
    { 
      var loggers = new List<ILogger>();
      foreach (var logger in givenLoggers)
      {
        var ll = LogLevel.None;
        foreach (var logLevel in logger.LogLevels)
        {
          ll |= logLevel;
        }
        switch (logger.Type)
        {
          case "Console":
            loggers.Add(new ConfigConsoleLogger(ll));
            break;

          case "File":
            loggers.Add(new ConfigFileLogger(logger.Path, ll));
            break;

          default:
            throw new ArgumentException( $"The Logger type '{logger.Type}' is unknown!");
        }
      }

      // we must have at least one.
      if (!loggers.Any())
      {
        throw new ArgumentException("You must have at least one logger!");
      }
      return loggers;
    }
  }
}
