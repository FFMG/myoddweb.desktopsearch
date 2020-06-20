# MyOddWeb.com Desktop Search [![Release](https://img.shields.io/badge/release-v0.2.2.0-brightgreen.png?style=flat)](https://github.com/FFMG/myoddweb.desktopsearch/) [![Build Status](https://travis-ci.org/FFMG/myoddweb.desktopsearch.svg?branch=master)](https://travis-ci.org/FFMG/myoddweb.desktopsearch)

myoddweb.desktopsearch, (MDS), is a powerful indexer files indexer. It makes is fast and easy to find indexed files on your desktop.

## How to use it ...

**NB**: Remember that in all cases it takes a bit of time to create/index the database ... (especially if you have large drives)

While the CPU usage might seem high at first, it is only for the initial indexing ... a bit like the first time you install a new version of windows or everytime your Anti-Virus feels like scanning everything.

At the bottom of the search page, (`http://localhost:9502/`), you will get a somewhat accurate percentage of indexing done.

The port number is set in the config file under `WebServer`

```json
{
  ...
  "WebServer": {
    "Port": 9502
  },
  ...
```

### As a service

- Install the server `myoddweb.desktopsearch.exe -install --config "path\to\your\config.json"`
- Using your browser go to `http://localhost:9502/` and search for something :).

### As a console app

Running as a console is significantly slower than running as a service, (more logging, checks, output and so on)

- Just start the console: `myoddweb.desktopsearch.exe --console --config "path\to\your\config.json"`
- Using your browser go to `http://localhost:9502/` and search for something :).

## Why use it?

Because it is much faster than the default windows search feature and it is more customisable as well.

## Components

The components are the various 'parsers' that indexes **your** own folders, you can create your own components to fit your own needs.

### Parsers
- C++, (.cpp, .c, .cc, .c++, .cxx, .h, .hh, .hpp, .hxx, .h++)
- C#, (.cs)
- Python (.py)
- Text files, (.txt)
- SQL, (.sql)


### Create your own
Simply create your own component with one or more `IFileParser` class, when a file is indexed, your components will return a list of words to add to the database.
And that's all really, you can add a list of supported extensions, (but you could still return `false` when calling `Supported( ... )` if you feel that it is not a file you support). 

```csharp
public interface IFileParser
{
  /// <summary>
  /// The name of the parser.
  /// </summary>
  string Name { get; }
  
  /// <summary>
  /// The list of extensions we aim to support
  /// </summary>
  string[] Extenstions { get; }
  
  /// <summary>
  /// Check if the given file is supported.
  /// Return true if we will parse it or not.
  /// </summary>
  bool Supported(FileInfo file);
  
  /// <summary>
  /// Parse a single file and return a list of words.
  /// Return null if the file is not supported.
  /// </summary>
  /// <param name="file"></param>
  /// <param name="logger"></param>
  /// <param name="token"></param>
  /// <returns></returns>
  Task<Words> ParseAsync(FileInfo file, ILogger logger, CancellationToken token);
}
```
	
# Desktop Search with [Piger](https://github.com/FFMG/myoddweb.piger)

With [Piger](https://github.com/FFMG/myoddweb.piger) you can launch the desktop seach app and automagically search for a word

- Install the servcice
- Make sure it is all up and running.
- Go to your piger root commands (normally `%appdata%\myoddweb\ActionMonitor\RootCommands`)
- Create a new `lua` file and call it something like `DesktopSearch`
- Add the code below
- Reload piger, (`Caps Lock` + `this.reload`)

```lua
--
-- Open Desktop search with selected words.
--

-- the number of words passed
local sizeOf = am_getCommandCount();

-- the query we (might) use.
local query = "";
if sizeOf == 0  then
    -- no words given ... try use the hilighted ones.
    query = am_getstring();
    if false == query then
      am_say( "Starting Desktop Search.", 400, 10 );
      query = "";
    else
      am_say( "Starting Desktop Search: <b><i>" .. query .. "</i></b>", 400, 10 );
    end
else
  local prettyQuery = "";
  for count = 1, sizeOf, 1  do
    -- the numbers are 0 based.
    -- and we ignore the first one as it is the command itself
    local word = am_getCommand( count );
    query = query .. word;
    prettyQuery = prettyQuery .. "<b><i>" .. word .. "</i></b>";
    if count < sizeOf then
      query = query .. " "
      prettyQuery = prettyQuery .. " and ";
    end
  end  
  am_say( "Starting Desktop Search: " .. prettyQuery, 400, 10 );
end

-- we can launch the desktop search.
am_execute( [[%ProgramFiles%\myoddweb\desktopsearch\myoddweb.desktopsearch.exe]], [[--query "]]..query..[[" --config "%appdata%\myoddweb\desktopsearch\desktop.json"]], false);
```

- Of course if you have a different config/locations then you can edit those.