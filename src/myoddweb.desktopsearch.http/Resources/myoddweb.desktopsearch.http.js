// some const values
SEARCH_BOX_ID = "#search-box";
SEARCH_RESULT_CONTAINER = ".live-search-result";

// The class that manages the query and so on
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
  this._query = function () {

    // before we do anything we need to reset the results
    $(SEARCH_RESULT_CONTAINER).empty();

    $.post("/Search", JSON.stringify({ what: this._querystr, count: this.MAX_NUMBER_ITEMS }), function (objs) {
      $.each(objs, function (i, item) {
        $(SEARCH_RESULT_CONTAINER).append(
          "<div class='live-search-result-element'>" + 
            "<div class='live-search-result-element-top'>" + item.Name + "</div>"+
            "<div class='live-search-result-element-bottom'>" +
              "<strong>Full Path:</strong>" + item.FullName + "<br />"+
              "<strong>Path:</strong>" + item.Directory  + "<br />"+
              "<strong>Word:</strong>" + item.Word +
              "</div>" +
          "</div>"
        );
      });
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
    $(SEARCH_BOX_ID).keyup(

      // get the string in the message box.
      function () {
        _myoddweb.query( $(this).val() );
    });
  }
);