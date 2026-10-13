// ShieldScreenEffect.cs
// ---------------------------------------------------------------------------
// Shared shield visual effect used by all scenes.
//
// Creates a full-screen UI overlay with a procedurally generated blue
// vignette: the edges are tinted while the center remains transparent.
// No external sprite or material is required.
//
// SetShieldActive(true/false) fades the vignette in or out as the shield
// becomes active or is consumed. Pulse() adds a short, subtle pulse when
// the shield blocks an attack. The pulse is optional and can be removed
// if it distracts from typing.
//
// Uses an overlay Canvas and Image, and animates with unscaled time so
// the effect can fade while the game is paused. It does not implement
// shield logic; EasyShield controls when the effect is shown.
// ---------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShieldScreenEffect : MonoBehaviour
{
    [Header("Vignette")]
    [SerializeField, Range(0f, 1f)]
    private float activeAlpha = 0.22f;

    [SerializeField, Range(0f, 1f)]
    private float pulseStrength = 0.10f;

    [SerializeField, Min(0.01f)]
    private float fadeSpeed = 5f;

    [Header("Block Pulse")]
    [SerializeField, Min(0.01f)]
    private float pulseDuration = 0.16f;

    private Canvas overlayCanvas;
    private Image vignetteImage;
    private Texture2D vignetteTexture;
    private Sprite vignetteSprite;

    private bool shieldActive;
    private float currentAlpha;
    private float pulseTimer;

    private void Awake()
    {
        CreateOverlay();
    }

    private void Update()
    {
        if (vignetteImage == null) return;

        float pulseAlpha = 0f;

        if (pulseTimer > 0f)
        {
            pulseTimer -= Time.unscaledDeltaTime;

            float progress = 1f -
                Mathf.Clamp01(pulseTimer / pulseDuration);

            // Smooth pulse: rises and then fades away.
            pulseAlpha = pulseStrength *
                Mathf.Sin(progress * Mathf.PI);
        }

        float targetAlpha =
            (shieldActive ? activeAlpha : 0f) + pulseAlpha;

        targetAlpha = Mathf.Clamp01(targetAlpha);

        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            targetAlpha,
            fadeSpeed * Time.unscaledDeltaTime
        );

        Color imageColor = vignetteImage.color;
        imageColor.a = currentAlpha;
        vignetteImage.color = imageColor;
    }

    public void SetShieldActive(bool active)
    {
        shieldActive = active;
    }

    public void Pulse()
    {
        pulseTimer = pulseDuration;
    }

    private void CreateOverlay()
    {
        GameObject canvasObject = new GameObject(
            "Shield Screen Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );

        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 1000;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject(
            "Blue Vignette",
            typeof(RectTransform),
            typeof(Image)
        );

        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect =
            imageObject.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        vignetteImage = imageObject.GetComponent<Image>();
        vignetteImage.raycastTarget = false;
        vignetteImage.color = new Color(1f, 1f, 1f, 0f);

        CreateVignetteSprite();
        vignetteImage.sprite = vignetteSprite;
        vignetteImage.type = Image.Type.Simple;
        vignetteImage.preserveAspect = false;
    }

    private void CreateVignetteSprite()
    {
        const int size = 256;

        vignetteTexture = new Texture2D(
            size, size, TextureFormat.RGBA32, false
        );

        vignetteTexture.name = "Generated Blue Vignette";
        vignetteTexture.wrapMode = TextureWrapMode.Clamp;
        vignetteTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;

                float dx = (u - 0.5f) * 2f;
                float dy = (v - 0.5f) * 2f;

                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // Transparent center, soft blue edges.
                float t = Mathf.InverseLerp(
                    0.48f, 1.15f, distance
                );

                t = Mathf.Clamp01(t);
                t = t * t * (3f - 2f * t);

                Color blue = new Color(
                    0.12f, 0.42f, 1f, t
                );

                pixels[y * size + x] = blue;
            }
        }

        vignetteTexture.SetPixels(pixels);
        vignetteTexture.Apply();

        vignetteSprite = Sprite.Create(
            vignetteTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f)
        );

        vignetteSprite.name = "Generated Blue Vignette Sprite";
    }

    private void OnDestroy()
    {
        if (vignetteSprite != null)
            Destroy(vignetteSprite);

        if (vignetteTexture != null)
            Destroy(vignetteTexture);
    }
}
