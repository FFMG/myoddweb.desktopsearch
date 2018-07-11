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
using myoddweb.desktopsearch.interfaces.Configs;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class Config : IConfig
  {
    [JsonProperty(Required = Required.Always)]
    public IPaths Paths { get; protected set; }

    [JsonProperty(Required = Required.Always)]
    public ITimers Timers { get; protected set; }

    // ReSharper disable once SuggestBaseTypeForParameter
    /// <summary>
    /// By using this constructor here we allowing JSon to convert from ConfigPaths > IPaths
    /// </summary>
    /// <param name="paths"></param>
    /// <param name="timers"></param>
    public Config(ConfigPaths paths, ConfigTimers timers )
    {
      Paths = paths;
      Timers = timers;
    }
  }
}
