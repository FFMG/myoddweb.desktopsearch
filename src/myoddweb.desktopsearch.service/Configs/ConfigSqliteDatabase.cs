﻿//This file is part of Myoddweb.DesktopSearch.
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
using System.IO;
using myoddweb.desktopsearch.interfaces.Configs;
using Newtonsoft.Json;

namespace myoddweb.desktopsearch.service.Configs
{
  internal class ConfigSqliteDatabase : IDatabase
  {
    /// <summary>
    /// The cache size.
    /// https://www.sqlite.org/pragma.html#pragma_cache_size
    /// </summary>
    [DefaultValue(10000)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public long CacheSize { get; protected set; }

    /// <summary>
    /// Auto checkpoint
    /// https://www.sqlite.org/wal.html#automatic_checkpoint
    /// https://www.sqlite.org/wal.html#checkpointing
    /// The default size is 16x1024 ... or ~75Mb
    /// </summary>
    [DefaultValue(16384)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public long AutoCheckpoint { get; protected set; }

    /// <summary>
    /// The expanded dabase source.
    /// </summary>
    private string _source;

    /// <summary>
    /// The source to the database.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string Source {
      get => _source;
      protected set
      {
        if (value == null)
        {
          _source = null;
          return;
        }
        // set the database source
        _source = Environment.ExpandEnvironmentVariables(value);

        // if not null, make sure that the path is set.
        var path = Path.GetDirectoryName(_source);
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
          Directory.CreateDirectory(path );
        }
      }
    }

    /// <inheritdoc />
    [DefaultValue(null)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public IList<string> IgnoredPaths { get; protected set; }
  }
}
