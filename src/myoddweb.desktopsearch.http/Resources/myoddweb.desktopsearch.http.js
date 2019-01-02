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

// some const values
SEARCH_BOX_CONTAINER = ".live-search-box";        // the class that has the search text
SEARCH_RESULT_CONTAINER = ".live-search-result";  // the class that will contain the search results.
KEY_UP_DELAY = 450;                               // how long we will wait until we send the 'word' to the service

// The class that manages the query and so on
function myoddweb() {

  // pseudo-const variable
  this.MIN_QUERY_LENGTH = 3;

  // the number of items we would like to return.
  this.MAX_NUMBER_ITEMS = 10;

  // the last query we did
  this._querystr = "";

  this.query = function (what, force ) {
    what = $.trim(what);

    // have we done this already?
    if (force === false && this._querystr === what) {
      return;
    }

    // is it less than the max size?
    if (what.length < this.MIN_QUERY_LENGTH) {

      // we are not searching, (yet), but the string is not the same anymore
      // so we will clear the content until we actually do some work.
      $(SEARCH_RESULT_CONTAINER).empty();

      return;
    }

    // save this so we don't do it again
    this._querystr = what;

    // we can now do the query with no check.
    this._query();
  }

  this._msToTime = function(s) {

    // Pad to 2 or 3 digits, default is 2
    function pad(n, z) {
      z = z || 2;
      return ('00' + n).slice(-z);
    }

    var ms = s % 1000;
    s = (s - ms) / 1000;
    var secs = s % 60;
    s = (s - secs) / 60;
    var mins = s % 60;
    var hrs = (s - mins) / 60;

    return pad(hrs) + ':' + pad(mins) + ':' + pad(secs) + '.' + pad(ms, 3);
  }

  // function to do the actual query.
  // we will do no validation here.
  this._query = function () {

    // before we do anything we need to reset the results
    $(SEARCH_RESULT_CONTAINER).empty();

    $(SEARCH_RESULT_CONTAINER).append(
      "<div class='live-search-result-element'>" +
      "<div class='live-search-result-element-top'>Wait ...</div>" +
      "</div>" +
      "</div>"
    );

    var mow = this;
    $.post("/Search", JSON.stringify({ what: this._querystr, count: this.MAX_NUMBER_ITEMS }), function (objs) {

      // we have to do this again in case we have more than one query on the go.
      $(SEARCH_RESULT_CONTAINER).empty();

      var words = objs.Words;
      $.each(words, function (i, item) {
        $(SEARCH_RESULT_CONTAINER).append(
          "<div class='live-search-result-element'>" + 
          "<div class='live-search-result-element-top'>" + item.Name + "</div>"+
          "<div class='live-search-result-element-bottom'>" +
            "<strong>Full Path:</strong> <a href=\"file:///"+ item.FullName+"\">" + item.FullName + "</a><br />"+
            "<strong>Path:</strong> " + item.Directory  + "<br />"+
            "<strong>Word:</strong> " + item.Actual +
          "</div>" +
          "</div>"
        );
      });

      var pct = (objs.Status.Files === 0 ? "0" : (((objs.Status.Files - objs.Status.PendingUpdates) / objs.Status.Files)*100).toFixed(4)) + "%";
      var ms = mow._msToTime(objs.ElapsedMilliseconds);
      $(SEARCH_RESULT_CONTAINER).append(
        "<div class='live-search-result-element'>" +
        " <div class='live-search-result-element-top'>Time Elapsed: " + ms + "</div>" +
        " <div class='live-search-result-element-top'>" + pct + " indexed</div>" +
        "</div>" +
        "</div>"
      );

    }, "json");
  }

  this.delaykeyup = function () {
    var timer = 0;
    return function (callback, ms) {
      clearTimeout(timer);
      timer = setTimeout(callback, ms);
    };
  }()
}

// our one and only instance of our class.
_myoddweb = new myoddweb();

// when the document is ready.
$(document).ready(
  // load this function as soon as the page is ready.
  function () {

    // When the user finishes typing
    $(SEARCH_BOX_CONTAINER).keyup(
      function () {
        var what = $(this).val();
        _myoddweb.delaykeyup(function() {
            // get the string in the message box.
            _myoddweb.query(what, false );
          },
          KEY_UP_DELAY);
      }
    );

    $(SEARCH_BOX_CONTAINER).keypress(function (e) {
      var what = $(this).val();
      if (e.which === 13) {
        // get the string in the message box.
        _myoddweb.query(what, true );
      }
    });
  }
);