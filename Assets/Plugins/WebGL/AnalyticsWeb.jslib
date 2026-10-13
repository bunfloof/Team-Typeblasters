// AnalyticsWeb.jslib
// ---------------------------------------------------------------------------
// The browser side of AnalyticsSender (only used in the Web build).
// Unity copies these functions into the build; C# calls them through
// [DllImport("__Internal")], and they answer through SendMessage, which calls
// a public method on a GameObject (by its name) right away.
// ---------------------------------------------------------------------------
mergeInto(LibraryManager.library, {

  // Tells C# whenever the tab is hidden (switched away, minimised, closing)
  // or visible again: calls OnPageVisibility("hidden" / "visible").
  AnalyticsWeb_ListenForPageVisibility: function (objectNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    document.addEventListener("visibilitychange", function () {
      SendMessage(objectName, "OnPageVisibility", document.visibilityState);
    });
  },

  // POSTs one batch. A text body is sent as "text/plain", so no CORS
  // preflight is needed. "keepalive" lets the request finish even if the tab
  // is closed right after. When it is done, calls OnBatchResult with
  // "requestId status" (status 0 = no reply: network down or timed out).
  AnalyticsWeb_Post: function (urlPtr, bodyPtr, requestId, objectNamePtr, timeoutMs) {
    var url = UTF8ToString(urlPtr);
    var body = UTF8ToString(bodyPtr);
    var objectName = UTF8ToString(objectNamePtr);
    var finished = false;

    function report(status) {
      if (finished) {
        return;
      }
      finished = true;
      try {
        SendMessage(objectName, "OnBatchResult", requestId + " " + status);
      } catch (error) {
        // the page is closing: nothing left to tell
      }
    }

    var controller = typeof AbortController !== "undefined" ? new AbortController() : null;
    var timer = setTimeout(function () {
      if (controller) {
        controller.abort();
      }
      report(0);
    }, timeoutMs);

    try {
      fetch(url, {
        method: "POST",
        body: body,
        keepalive: true,
        signal: controller ? controller.signal : undefined
      }).then(function (response) {
        clearTimeout(timer);
        report(response.status);
      }).catch(function () {
        clearTimeout(timer);
        report(0);
      });
    } catch (error) {
      clearTimeout(timer);
      report(0);
    }
  }
});
