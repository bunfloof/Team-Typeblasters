// AnalyticsSender.cs
// ---------------------------------------------------------------------------
// Sends the events queued by Analytics to the log server, a batch at a time:
//
//   POST https://typeblasterslogs.joey.ma/v1/batch      (Content-Type: text/plain)
//   {"format":1,"batch_id":"...","install_id":"...","session_id":"...",
//    "build":"...","events":[{...},{...}]}
//
// text/plain (not application/json) keeps it a "simple" request for the
// browser: no extra permission check (CORS preflight) has to go first.
//
// WHEN: every sendEverySeconds, as soon as maxEventsPerBatch are waiting, and
// right away at checkpoints (Analytics.FlushSoon: wave end, death, pause...).
// One request at a time. A batch that fails (no reply, server error) is kept
// and sent again later, unchanged, waiting longer after each failure. The game
// never waits for the server: if it is down, the events simply wait here.
//
// IN THE WEB BUILD the request is made by the browser (AnalyticsWeb.jslib),
// with "keepalive", so a batch still arrives if the tab is closed right after.
// The browser also tells us when the tab is HIDDEN (switched away, minimised,
// closed): the game pauses (GameManager.PauseForHiddenTab) and everything
// queued is sent at once, because a closing tab gives no second chance.
// In the Editor UnityWebRequest is used instead (no tab to hide there).
//
// Lives on the "Analytics" object (prefab Assets/Prefabs/Analytics) in the
// FIRST scene of the build (Prototype). It survives scene loads and Restarts
// (DontDestroyOnLoad), so nothing queued is lost when a scene loads; a copy in
// a reloaded scene removes itself. If a scene without it is the first one
// loaded (Hard opened directly, a future menu scene), CreateIfMissing makes
// one with the default settings below.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class AnalyticsSender : MonoBehaviour
{
    [Header("Server")]
    [SerializeField] private string url = "https://typeblasterslogs.joey.ma/v1/batch";
    [SerializeField] private bool sendFromEditor = true;  // Editor plays are marked as test data ("qa-")
    [SerializeField] private bool logToConsole = false;   // prints every batch result (for testing)

    [Header("Batches")]
    [SerializeField] private float sendEverySeconds = 5f;
    [SerializeField] private int maxEventsPerBatch = 100;

    [Header("Heartbeat")]
    [SerializeField] private float sampleEverySeconds = 1f; // a "sample" event per second of play (0 = off)

    // The browser allows 64 KB for all keepalive requests in flight together;
    // a batch stays under half of that, so two can be in flight when the tab hides.
    private const int MaxBatchBytes = 30000;
    private const float MaxRetrySeconds = 30f;
    private const float RequestTimeoutSeconds = 15f;

    public static AnalyticsSender Instance { get; private set; }

    // Batches (complete JSON bodies) built but not sent yet, oldest first.
    // A failed batch goes back to the front.
    private readonly List<string> readyBatches = new List<string>();

    // Batches on their way to the server, by request number.
    private readonly Dictionary<int, string> inFlight = new Dictionary<int, string>();
    private int inFlightBytes;
    private int nextRequestId = 1;

    private float nextSendTime;   // realtime: the timer's next send
    private float retryTime;      // realtime: no new attempt before this (after a failure)
    private int failuresInARow;
    private float hiddenSince = -1f;

    private float sampleTimer;        // play seconds since the last "sample"
    private int framesSinceSample;    // for its fps
    private float sampleWindowStart;  // realtime the current fps window began

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void AnalyticsWeb_ListenForPageVisibility(string objectName);

    [DllImport("__Internal")]
    private static extern void AnalyticsWeb_Post(string url, string body, int requestId, string objectName, int timeoutMs);
#endif

    // Runs once after the first scene has loaded (Awake of its objects done):
    // makes sure there is always a sender.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {
        if (Instance == null)
        {
            new GameObject("Analytics").AddComponent<AnalyticsSender>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // the one from before the Restart keeps working
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Analytics.Begin(); // the IDs, and "session_start"
        nextSendTime = Time.realtimeSinceStartup + sendEverySeconds;

#if UNITY_WEBGL && !UNITY_EDITOR
        AnalyticsWeb_ListenForPageVisibility(gameObject.name);
#endif
    }

    private void Update()
    {
        if (Instance != this)
        {
            return; // a copy that is about to remove itself (see Awake)
        }
        if (!SendingAllowed())
        {
            Analytics.Queue.Clear(); // nothing is sent from here; do not let it pile up
            Analytics.FlushRequested = false;
            return;
        }

        float now = Time.realtimeSinceStartup;
        UpdateHeartbeat(now);
        bool timerDue = now >= nextSendTime;
        bool queueFull = Analytics.Queue.Count >= maxEventsPerBatch;
        if ((timerDue || queueFull || Analytics.FlushRequested) && Analytics.Queue.Count > 0)
        {
            BuildBatches();
            Analytics.FlushRequested = false;
        }
        if (timerDue)
        {
            nextSendTime = now + sendEverySeconds;
        }

        // One request at a time, and not too soon after a failure.
        if (inFlight.Count == 0 && readyBatches.Count > 0 && now >= retryTime)
        {
            SendNextBatch();
        }
    }

    // One "sample" event per sampleEverySeconds of play (pauses, tip boxes
    // and menus do not count), with the frame rate since the last one.
    private void UpdateHeartbeat(float now)
    {
        GameManager game = GameManager.Instance;
        if (sampleEverySeconds <= 0f || game == null || game.State != GameState.Playing)
        {
            framesSinceSample = 0;
            sampleWindowStart = now;
            return;
        }
        framesSinceSample += 1;
        sampleTimer += Time.deltaTime;
        if (sampleTimer < sampleEverySeconds)
        {
            return;
        }
        float elapsed = now - sampleWindowStart;
        Analytics.LogSample(elapsed > 0f ? framesSinceSample / elapsed : 0f);
        sampleTimer = 0f;
        framesSinceSample = 0;
        sampleWindowStart = now;
    }

    private bool SendingAllowed()
    {
        return !Application.isEditor || sendFromEditor;
    }

    // Moves everything queued into batches of at most maxEventsPerBatch events
    // and MaxBatchBytes bytes.
    private void BuildBatches()
    {
        StringBuilder events = new StringBuilder();
        int count = 0;
        int bytes = 0;
        foreach (string json in Analytics.Queue)
        {
            int size = Encoding.UTF8.GetByteCount(json) + 1;
            if (count > 0 && (count >= maxEventsPerBatch || bytes + size > MaxBatchBytes - 400))
            {
                readyBatches.Add(WrapBatch(events.ToString()));
                events.Length = 0;
                count = 0;
                bytes = 0;
            }
            if (count > 0)
            {
                events.Append(',');
            }
            events.Append(json);
            count += 1;
            bytes += size;
        }
        if (count > 0)
        {
            readyBatches.Add(WrapBatch(events.ToString()));
        }
        Analytics.Queue.Clear();
    }

    // The JSON body around a list of events (see the top of the file).
    private static string WrapBatch(string eventsJson)
    {
        StringBuilder body = new StringBuilder();
        body.Append("{\"format\":").Append(Analytics.Format);
        body.Append(",\"batch_id\":").Append(AnalyticsEvent.Quote(System.Guid.NewGuid().ToString("N")));
        body.Append(",\"install_id\":").Append(AnalyticsEvent.Quote(Analytics.InstallId));
        body.Append(",\"session_id\":").Append(AnalyticsEvent.Quote(Analytics.SessionId));
        body.Append(",\"build\":").Append(AnalyticsEvent.Quote(Analytics.Build));
        body.Append(",\"events\":[").Append(eventsJson).Append("]}");
        return body.ToString();
    }

    private void SendNextBatch()
    {
        string body = readyBatches[0];
        readyBatches.RemoveAt(0);
        Post(body);
    }

    private void Post(string body)
    {
        int requestId = nextRequestId;
        nextRequestId += 1;
        inFlight[requestId] = body;
        inFlightBytes += Encoding.UTF8.GetByteCount(body);

#if UNITY_WEBGL && !UNITY_EDITOR
        AnalyticsWeb_Post(url, body, requestId, gameObject.name, (int)(RequestTimeoutSeconds * 1000f));
#else
        StartCoroutine(PostWithUnityWebRequest(body, requestId));
#endif
    }

    // Editor (and any non-Web build): the same request with UnityWebRequest.
    private IEnumerator PostWithUnityWebRequest(string body, int requestId)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.uploadHandler.contentType = "text/plain";
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = (int)RequestTimeoutSeconds;
            yield return request.SendWebRequest();

            long status = 0; // 0 = no reply at all (network down, timeout...)
            if (request.result == UnityWebRequest.Result.Success || request.result == UnityWebRequest.Result.ProtocolError)
            {
                status = request.responseCode;
            }
            FinishRequest(requestId, (int)status);
        }
    }

    // Called by AnalyticsWeb.jslib (through SendMessage) when the browser's
    // request is done: "requestId status", e.g. "12 200" (status 0 = no reply).
    public void OnBatchResult(string result)
    {
        string[] parts = result.Split(' ');
        int requestId;
        int status;
        if (parts.Length == 2 && int.TryParse(parts[0], out requestId) && int.TryParse(parts[1], out status))
        {
            FinishRequest(requestId, status);
        }
    }

    // A request is done. 2xx: stored. 4xx: the server will never accept this
    // body (it keeps a copy anyway), so it is dropped; 408 and 429 mean "try
    // again later". Anything else (0 = no reply, 5xx): sent again later.
    private void FinishRequest(int requestId, int status)
    {
        string body;
        if (!inFlight.TryGetValue(requestId, out body))
        {
            return;
        }
        inFlight.Remove(requestId);
        inFlightBytes -= Encoding.UTF8.GetByteCount(body);

        bool stored = status >= 200 && status < 300;
        bool refused = status >= 400 && status < 500 && status != 408 && status != 429;
        if (logToConsole)
        {
            Debug.Log("Analytics: batch " + requestId + " -> " + status + (stored ? " (stored)" : refused ? " (refused, dropped)" : " (will retry)"));
        }
        if (stored || refused)
        {
            failuresInARow = 0;
            if (hiddenSince >= 0f)
            {
                // The tab is still hidden, so Update does not run: send what
                // is left from here (only after a success, so a network
                // outage cannot start an endless loop).
                SendEverythingNow();
            }
            return;
        }

        readyBatches.Insert(0, body);
        failuresInARow += 1;
        retryTime = Time.realtimeSinceStartup + Mathf.Min(MaxRetrySeconds, Mathf.Pow(2f, failuresInARow));
    }

    // ---- The browser tab (Web build only; called by AnalyticsWeb.jslib) ----

    // "hidden": the player switched tabs, minimised the window or is closing
    // the tab. "visible": they came back.
    public void OnPageVisibility(string state)
    {
        if (state == "hidden")
        {
            hiddenSince = Time.realtimeSinceStartup;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PauseForHiddenTab(); // logs "pause" if it paused
            }
            AnalyticsEvent e = Analytics.NewEvent("page_hidden");
            Analytics.AddPlayerState(e);
            Analytics.Send(e);
            SendEverythingNow();
        }
        else if (state == "visible")
        {
            AnalyticsEvent e = Analytics.NewEvent("page_visible");
            if (hiddenSince >= 0f)
            {
                e.Add("hidden_seconds", Time.realtimeSinceStartup - hiddenSince);
            }
            Analytics.Send(e);
            hiddenSince = -1f;
            Analytics.FlushSoon();
        }
    }

    // The tab may be closing: send every waiting batch now, without waiting
    // for the one in flight (as many as the browser's keepalive limit allows;
    // while the tab stays hidden, every successful reply sends the next ones,
    // see FinishRequest; after a failure they wait until the tab comes back).
    private void SendEverythingNow()
    {
        if (!SendingAllowed())
        {
            return;
        }
        if (Analytics.Queue.Count > 0)
        {
            BuildBatches();
        }
        while (readyBatches.Count > 0
            && inFlightBytes + Encoding.UTF8.GetByteCount(readyBatches[0]) <= 2 * MaxBatchBytes)
        {
            SendNextBatch();
        }
    }
}
