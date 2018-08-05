function myoddweb() {

  // pseudo-const variable
  this.MIN_QUERY_LENGTH = 3;

  // the number of items we would like to return.
  this.MAX_NUMBER_ITEMS = 10;

  // the last query we did
  this._querystr = "";

  this.query = function (what) {
    what = $.trim(what);

    // have we done this already?
    if (this._querystr === what) {
      return;
    }

    // is it less than the max size?
    if (what.length < this.MIN_QUERY_LENGTH) {
      return;
    }

    // save this so we don't do it again
    this._querystr = what;

    // we can now do the query with no check.
    this._query();
  }

  // function to do the actual query.
  // we will do no validation here.
  this._query = function() {
    $.post("/Search", JSON.stringify({ what: this._querystr, count: this.MAX_NUMBER_ITEMS }), function(data) {

    }, "json");
  }
}

// our one and only instance of our class.
_myoddweb = new myoddweb();

// when the document is ready.
$(document).ready(
  // load this function as soon as the page is ready.
  function () {

    // When the user finishes typing
    $("#search-box").keyup(

      // get the string in the message box.
      function () {
        _myoddweb.query( $(this).val() );
    });
  }
);