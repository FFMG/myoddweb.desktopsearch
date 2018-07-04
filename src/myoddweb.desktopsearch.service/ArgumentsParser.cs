//This file is part of SQLiteServer.
//
//    SQLiteServer is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    SQLiteServer is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with SQLiteServer.  If not, see<https://www.gnu.org/licenses/gpl-3.0.en.html>.
using System;
using System.Collections.Generic;
using System.Linq;

namespace myoddweb.desktopsearch.service
{
  /// <summary>
  /// The argument data, indicate required values.
  /// It can also contain description as well as default values.
  /// </summary>
  public class ArgumentData
  {
    /// <summary>
    /// Check if the value is required or not.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// The default value, (null by default).
    /// </summary>
    public string DefaultValue { get; set; }
  }

  public class ArgumentsParser
  {
    private readonly string _leadingPattern;

    /// <summary>
    /// A dictionalry with key->value of all the arguments.
    /// </summary>
    private readonly Dictionary<string, string> _parsedArguments;

    /// <summary>
    /// Rules that our arguments have to follow, for example required.
    /// Or, if not required, the default value.
    /// </summary>
    private readonly Dictionary<string, ArgumentData> _argumentData;

    /// <summary>
    /// The constructor
    /// </summary>
    /// <param name="args">the given arguments.</param>
    /// <param name="argumentData">argument data telling us about required data.</param>
    /// <param name="leadingPattern">the leading pattern to delimit arguments.</param>
    public ArgumentsParser(IReadOnlyList<string> args, Dictionary<string, ArgumentData> argumentData = null, string leadingPattern = "--")
    {
      // the leading pattern
      _leadingPattern = leadingPattern;

      // set the arguments data.
      _argumentData = argumentData ?? new Dictionary<string, ArgumentData>();

      // the arguments
      _parsedArguments = new Dictionary<string, string>();

      // parse the daa
      Parse(args);

      // validate the required arguments.
      ValidateArgumentsData();
    }

    public ArgumentsParser Clone()
    {
      return new ArgumentsParser(Arguments(false), _argumentData, _leadingPattern);
    }

    /// <summary>
    /// Remove a key from the list and return the updated value.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public ArgumentsParser Remove(string key)
    {
      if (!IsSet(key))
      {
        return this;
      }

      // adjust the key value
      var adjustedKey = AdjustedKey(key);

      // remove the key
      _parsedArguments.Remove(adjustedKey);

      // return us
      return this;
    }

    //
    // Summary:
    //     Returns a string that represents the current object.
    //
    // Returns:
    //     A string that represents the current object.
    public override string ToString()
    {
      return string.Join(" ", Arguments(true));
    }

    //
    // Summary:
    //     Returns a string that represents the current object.
    //
    // Returns:
    //     A string that represents the current object.
    private string[] Arguments(bool quoteValues)
    {
      var values = new List<string>();
      foreach (var argumentAndKey in _parsedArguments)
      {
        values.Add($"{_leadingPattern}{argumentAndKey.Key}");
        if (!(argumentAndKey.Value?.Length > 0))
        {
          continue;
        }

        // do we want to quote this?
        var value = argumentAndKey.Value;
        if (quoteValues && value.Any(char.IsWhiteSpace))
        {
          value = $"\"{value}\"";
        }
        values.Add(value);
      }
      return values.ToArray();
    }

    /// <summary>
    /// Validate that we have all the required arguments, we will
    /// throw in case the required arguments are missing.
    /// </summary>
    private void ValidateArgumentsData()
    {
      foreach (var argument in _argumentData)
      {
        // adjust the key value.
        var adjustedKey = AdjustedKey(argument.Key);

        // does it exist?
        if (_parsedArguments.ContainsKey(adjustedKey))
        {
          continue;
        }

        // is it required?
        if (!argument.Value.IsRequired)
        {
          continue;
        }

        // yes, it is required and missing, so we have to throw an error.
        throw new ArgumentException("Missing required argument", adjustedKey);
      }
    }

    /// <summary>
    /// Get a value directly, with no default value.
    /// </summary>
    /// <param name="key">The key we are looking for.</param>
    /// <returns></returns>
    public string this[string key] => Get(key);

    /// <summary>
    /// Check if the value is a valid key
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    private bool IsKey(string str)
    {
      // check for the leading pattern
      return str.StartsWith(_leadingPattern);
    }

    private static string AdjustedKey(string key)
    {
      if (null == key)
      {
        // the key cannot be null.
        throw new ArgumentNullException(nameof(key));
      }
      return key.ToLower();
    }

    /// <summary>
    /// Get a value given a template.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns></returns>
    public T Get<T>(string key)
    {
      var adjustedKey = AdjustedKey(key);
      var s = Get(adjustedKey);
      if (s == null)
      {
        return default(T);
      }

      try
      {
        return (T)Convert.ChangeType(s, typeof(T));
      }
      catch (FormatException)
      {
        return default(T);
      }
    }

    /// <summary>
    /// Get a value, and if it does not exist we will return the default.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public T Get<T>(string key, T defaultValue)
    {
      var adjustedKey = AdjustedKey(key);
      var s = Get(adjustedKey);
      if (s == null)
      {
        return defaultValue;
      }

      try
      {
        return (T)Convert.ChangeType(s, typeof(T));
      }
      catch (FormatException)
      {
        return default(T);
      }
    }

    /// <summary>
    /// Get a value with a valid key
    /// </summary>
    /// <param name="key">the key we are looking for</param>
    /// <param name="defaultValue">if the value does not exist, this is what we will return.</param>
    /// <returns></returns>
    public string Get(string key, string defaultValue)
    {
      // adjust the key value
      var adjustedKey = AdjustedKey(key);

      // return it if we have it, the default otherwise.
      return !_parsedArguments.ContainsKey(adjustedKey) ? defaultValue : _parsedArguments[adjustedKey];
    }

    /// <summary>
    /// Get a value with a valid key
    /// </summary>
    /// <param name="key">the key we are looking for</param>
    /// <returns></returns>
    public string Get(string key)
    {
      // adjust the key value
      var adjustedKey = AdjustedKey(key);

      // look for that key in our default arguments.
      var defaultValue = _argumentData.ContainsKey(adjustedKey) ? _argumentData[adjustedKey].DefaultValue : null;

      // return it if we have it, the default otherwise.
      return !_parsedArguments.ContainsKey(adjustedKey) ? defaultValue : _parsedArguments[adjustedKey];
    }

    /// <summary>
    /// Check if a value exists or not.
    /// </summary>
    /// <param name="key">the key we are looking for</param>
    /// <returns></returns>
    public bool IsSet(string key)
    {
      // adjust the key value
      var adjustedKey = AdjustedKey(key);

      // look for that key in our default arguments.
      return _parsedArguments.ContainsKey(adjustedKey);
    }


    /// <summary>
    /// Parse the given arguments.
    /// </summary>
    /// <param name="args"></param>
    private void Parse(IReadOnlyList<string> args)
    {
      if (args == null)
      {
        // if we have null, it is fine, this implies we have no data at all.
        return;
      }

      for (var i = 0; i < args.Count; i++)
      {
        if (args[i] == null)
        {
          continue;
        }

        string key = null;
        string val = null;

        if (IsKey(args[i]))
        {
          key = args[i].Substring(_leadingPattern.Length);

          if (i + 1 < args.Count && !IsKey(args[i + 1]))
          {
            val = args[i + 1];
            i++;
          }
        }
        else
        {
          val = args[i];
        }

        // adjustment
        if (key == null)
        {
          key = val;
          val = null;
        }
        if (key != null)
        {
          _parsedArguments[AdjustedKey(key)] = val;
        }
      }
    }
  }
}
