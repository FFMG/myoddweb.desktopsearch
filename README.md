# MyOddWeb.com Desktop Search [![Release](https://img.shields.io/badge/release-v0.0.0.1-brightgreen.png?style=flat)](https://github.com/FFMG/myoddweb.desktopsearch/) [![Build Status](https://travis-ci.org/FFMG/myoddweb.desktopsearch.svg?branch=master)](https://travis-ci.org/FFMG/myoddweb.desktopsearch)

myoddweb.desktopsearch, (MDS), is a powerful indexer files indexer. It makes is fast and easy to find indexed files on your desktop.

## Why use it?

Because it is much faster than the default windows search feature and it is more customisable as well.

## Components

The components are the various 'parsers' that indexes **your** own folders, you can create your own components to fit your own needs.

- TextParser: Is one of the default parser, it indexes all the text files on your system.

### Create your own
Simply create your own component with one or more IFileParser class, when a file is indexed, your components will return a list of words to add to the database.
And that's all really, you can add a list of supported extensions, (but you could still return `false` when calling `Supported( ... )` if you feel that it is not a file you support). 

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