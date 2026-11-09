// ScreenFade.cs
// ---------------------------------------------------------------------------
// A black screen that fades in, loads a scene, and fades out again:
//
//   ScreenFade.LoadSceneWithFade("Normal", 0.5f);
//
// 1. A full-screen black overlay is created at once (transparent, but it
//    catches every click, so nothing else can be pressed meanwhile).
// 2. After delaySeconds (the button's pulse in MainMenu) it fades to black.
// 3. The scene loads. The overlay survives the load (DontDestroyOnLoad).
// 4. Once the new scene has had a couple of frames to build its UI, the
//    black fades away and the overlay removes itself.
// Everything uses unscaled time. Nothing needs to be placed in a scene.
// ---------------------------------------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScreenFade : MonoBehaviour
{
    private const float FadeOutSeconds = 0.5f;
    private const float FadeInSeconds = 0.6f;
    private const int SortingOrder = 32000;   // above every canvas in the game
    private const float MaxFrameStep = 0.05f; // a slow frame while the level builds must not skip the fade

    private static bool running;
    private Image black;

    public static void LoadSceneWithFade(string sceneName, float delaySeconds)
    {
        if (running)
        {
            return;
        }
        running = true;

        GameObject root = new GameObject("Screen Fade", typeof(Canvas), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        DontDestroyOnLoad(root);

        GameObject blackObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)blackObject.transform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        ScreenFade fade = root.AddComponent<ScreenFade>();
        fade.black = blackObject.GetComponent<Image>();
        fade.black.color = new Color(0f, 0f, 0f, 0f);
        fade.black.raycastTarget = true; // blocks clicks right away
        fade.StartCoroutine(fade.Run(sceneName, delaySeconds));
    }

    private IEnumerator Run(string sceneName, float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        yield return Fade(0f, 1f, FadeOutSeconds);

        SceneManager.LoadScene(sceneName);
        // The load is under way, so the next scene may already ask for its own
        // fade (Esc on its Start panel, a quick Enter in the menu) while this
        // overlay is still black or fading away. Clearing the flag later would
        // drop that request, and the scene's own guard (GameManager.loadingLevel,
        // MainMenu.loading) would then block every later Restart or Menu.
        // The old scene cannot load twice: those same guards are already set.
        running = false;
        while (SceneManager.GetActiveScene().name != sceneName)
        {
            yield return null;
        }
        yield return null; // the new scene builds its UI in its first frames
        yield return null;

        black.raycastTarget = false; // the player may already press Enter while it fades in
        yield return Fade(1f, 0f, FadeInSeconds);

        Destroy(gameObject); // (a newer fade may have started meanwhile: it has its own overlay)
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        for (float time = 0f; time < seconds; time += Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep))
        {
            SetAlpha(Mathf.SmoothStep(from, to, time / seconds));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        black.color = new Color(0f, 0f, 0f, alpha);
    }
}
