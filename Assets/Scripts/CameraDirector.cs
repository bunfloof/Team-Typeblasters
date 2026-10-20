// CameraDirector.cs
// ---------------------------------------------------------------------------
// The player's "head". Lives on the Main Camera, which is a child of the
// Player rig at eye height. RailMover turns the whole rig toward where it
// rides and toward each fight; this script adds, on top of that (in the
// camera's LOCAL rotation, position and field of view):
//
//   - FOCUS: like in The Typing of the Dead, the view turns a little toward
//     the enemy whose word is being typed (clamped, so it never swings too
//     far) and glides back to straight ahead when there is nothing to type.
//     With no word started, a zombie that comes close (idleFocusDistance)
//     pulls the view gently toward it, so the player notices the threat.
//   - SHAKE:     CameraDirector.Shake(0.5f);   explosions, hits ("trauma" 0..1)
//   - KICK:      CameraDirector.Kick(1f);      every shot tips the view up a bit
//   - a running head-bob between fights, and a slow breathing sway all the time.
//   - WIDE VIEW: while the player aims a lure bomb (holds 1) or types a word,
//     the field of view widens (wideFieldOfView), so more of the fight is seen.
//   - on the Start panel and the results screens the view drifts slowly
//     left and right, so the scene behind the panel feels alive.
//   - MAP VIEW (waves 2 and 4, see WaveSpawner.mapViewWaves): once the player
//     stops at such a fight, the camera rises out of the head, pulls back and
//     tilts down until it sees the WHOLE area: every spawn point and its
//     exit, barrels, crates and every zombie. The player's green body (only
//     shown in this view) sits at the bottom centre of the screen. When the
//     ride to the next fight starts, it glides back into the eyes.
//
// Any script can call the static methods (Shake, Kick). They do nothing if
// there is no CameraDirector in the scene. Gameplay that needs "where the
// player is" uses PlayerEye / ShotOrigin, never the camera's position,
// because in the map view the camera is high above the player.
//
// Everything happens in LateUpdate, after every Update has moved the rig and
// the enemies. DefaultExecutionOrder(-100) makes it run BEFORE the other
// LateUpdates, so the words on the HUD use this frame's view.
//
// Pause: everything moves with Time.deltaTime, which is 0 while the game is
// paused, so the view holds perfectly still behind the pause panel.
// ---------------------------------------------------------------------------
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CameraDirector : MonoBehaviour
{
    [Header("Focus (turn toward the word being typed)")]
    [SerializeField] private float maxFocusYaw = 16f;         // degrees left/right, at most (before focusStrength); small, so zombies on the other side stay on screen
    [SerializeField] private float maxFocusPitchDown = 12f;   // degrees down, at most
    [SerializeField] private float maxFocusPitchUp = 22f;     // degrees up, at most (looking up at the tall boss)
    [SerializeField] private float focusStrength = 0.75f;     // 1 = put the target dead centre, 0 = never turn
    [SerializeField] private float focusSmoothTime = 0.22f;   // seconds; higher = slower, softer turns
    [SerializeField] private float focusLingerSeconds = 0.3f; // keep looking at a finished word this long (its last bullet is flying)

    [Header("Idle focus (no word started)")]
    [SerializeField] private float idleFocusDistance = 10f;   // metres: a zombie closer than this draws the eye
    [SerializeField] private float idleFocusStrength = 0.35f; // a gentle turn, not a full one

    [Header("Drift on the Start panel and results screens")]
    [SerializeField] private float idleDriftDegrees = 4f;     // how far the view wanders left and right
    [SerializeField] private float idleDriftSpeed = 0.05f;    // back-and-forth wanders per second

    [Header("Shake")]
    [SerializeField] private float maxShakeAngle = 5f;        // degrees of yaw and pitch at full trauma
    [SerializeField] private float maxShakeRoll = 3f;         // degrees of roll at full trauma
    [SerializeField] private float maxShakeOffset = 0.06f;    // metres of movement at full trauma
    [SerializeField] private float shakeFrequency = 22f;      // how fast it jitters
    [SerializeField] private float traumaDecay = 1.6f;        // trauma lost per second (at 1.6, a full shake of 1 is gone after about 0.6 s)

    [Header("Kick (every shot)")]
    [SerializeField] private float recoilReturnTime = 0.12f;  // seconds for the view to settle back after a kick
    [SerializeField] private float maxRecoil = 4f;            // degrees: fast typing never tips the view further

    [Header("Field of view")]
    [SerializeField] private float baseFieldOfView = 65f;     // degrees, top to bottom of the screen
    [SerializeField] private float wideFieldOfView = 78f;     // while aiming a lure bomb or typing a word: see more around
    [SerializeField] private float wideLingerSeconds = 1.2f;  // stay wide this long after the last word (no zoom in and out between words)
    [SerializeField] private float fieldOfViewSmoothTime = 0.35f; // seconds to widen / narrow

    [Header("Head bob (while running between fights) and breathing")]
    [SerializeField] private float bobHeight = 0.06f;         // metres up and down
    [SerializeField] private float bobFrequency = 2.8f;       // bobs per second (one per step: a running pace)
    [SerializeField] private float bobRollDegrees = 1f;       // the head tilts left, then right, every two steps
    [SerializeField] private float bobFadeSeconds = 0.5f;     // time for the bob to fade in / out when starting / stopping
    [SerializeField] private float breathingDegrees = 0.25f;  // tiny sway that never stops
    [SerializeField] private float breathingFrequency = 0.25f; // breaths per second

    [Header("Map view (waves 2 and 4)")]
    [SerializeField] private float viewSwitchSeconds = 1.6f;      // time to glide between first person and the map view
    [SerializeField] private float mapPitch = 48f;                // degrees the map view looks down (90 = straight down)
    [SerializeField] private float mapFieldOfView = 55f;
    [SerializeField] private float mapPlayerScreenHeight = 0.14f; // where the player sits on screen: 0 = bottom edge, 1 = top edge
    [SerializeField] private float mapScreenMargin = 0.06f;       // keep the area this far inside the screen edges (fraction of the screen)
    [SerializeField] private float mapMinDistance = 12f;          // metres from the camera to the player, at least...
    [SerializeField] private float mapMaxDistance = 120f;         // ...and at most
    [SerializeField] private float mapZoomSmoothTime = 0.6f;      // seconds; the zoom follows the zombies softly

    // The camera does not draw anything closer than this (metres).
    private const float NearClip = 0.1f;
    // Nothing further away than this is drawn (the whole USC block and the streets around it fit).
    private const float FarClip = 400f;

    private const float TwoPi = Mathf.PI * 2f;

    // The CameraDirector in the scene, used by the static methods (null if none).
    private static CameraDirector current;

    private Camera cam;
    private Vector3 basePosition;        // the eye position (local) set in the scene
    private TypingController typing;     // tells us which word is being typed
    private WaveSpawner spawner;         // tells us where the zombies are
    private RailMover rail;              // on the Player rig: tells us when we are riding

    // Focus: where the head points now, in degrees relative to the rig
    // (pitch > 0 = looking UP), and how fast it is turning (for SmoothDampAngle).
    private float focusYaw;
    private float focusPitch;
    private float focusYawVelocity;
    private float focusPitchVelocity;
    private Vector3 lingerPoint;         // the last target's position, looked at for a moment after it is gone
    private float lingerTimer;

    private float shakeTrauma;           // 0..1; the shake strength is trauma x trauma
    private float shakeTime;             // runs with game time, feeds the noise
    private float recoil;                // degrees the view is tipped up right now
    private float recoilVelocity;
    private float rideWeight;            // 0 = standing still, 1 = riding: scales the head-bob
    private float bobTime;
    private float breathTime;
    private float fieldOfView;           // the field of view now (glides between base and wide)
    private float fieldOfViewVelocity;
    private float wideTimer;             // seconds the view stays wide after the last word / aim

    // Map view.
    private float viewBlend;             // 0 = first person, 1 = map view; glides between them
    private float mapDistance;           // metres from the camera to the player in the map view
    private float mapDistanceVelocity;
    private bool mapDistanceValid;       // false until the first map-view frame (then no zoom glide)
    private GameObject avatar;           // the player's body, only shown in the map view
    private readonly System.Collections.Generic.List<Vector3> framePoints = new System.Collections.Generic.List<Vector3>();

    // How far the camera is into the map view: 0 = first person, 1 = map view
    // (WaveSpawner.LayoutLabels draws the words smaller and spreads them out).
    public static float MapBlend
    {
        get { return current != null ? SmoothStep(current.viewBlend) : 0f; }
    }

    // Where the player's eyes are, whatever the camera is doing. Gameplay aims
    // here (energy orbs fly at it, blasts shake by distance to it).
    public static Vector3 PlayerEye
    {
        get
        {
            if (current != null)
            {
                return current.EyePosition();
            }
            Camera view = Camera.main;
            return view != null ? view.transform.position : Vector3.zero;
        }
    }

    // Where a shot or a throw starts: cameraLocalOffset from the camera in first
    // person (just below the view), sliding to the player's chest in the map view.
    public static Vector3 ShotOrigin(Vector3 cameraLocalOffset)
    {
        if (current == null)
        {
            return Camera.main.transform.TransformPoint(cameraLocalOffset);
        }
        Vector3 fromCamera = current.transform.TransformPoint(cameraLocalOffset);
        if (current.viewBlend <= 0f)
        {
            return fromCamera;
        }
        return Vector3.Lerp(fromCamera, current.ChestPosition(), SmoothStep(current.viewBlend));
    }

    // Adds screen shake. trauma: 0..1 (0.2 = a bump, 0.6 = a close explosion, 1 = huge).
    public static void Shake(float trauma)
    {
        if (current == null)
        {
            return;
        }
        current.shakeTrauma = Mathf.Clamp01(current.shakeTrauma + Mathf.Max(0f, trauma));
    }

    // A shot's kick: tips the view up by this many degrees, then it settles back.
    public static void Kick(float degrees)
    {
        if (current == null)
        {
            return;
        }
        current.recoil = Mathf.Clamp(current.recoil + degrees, -current.maxRecoil, current.maxRecoil);
    }

    private void Awake()
    {
        current = this;

        cam = GetComponent<Camera>();
        cam.nearClipPlane = NearClip;
        cam.farClipPlane = FarClip;
        cam.fieldOfView = baseFieldOfView;
        fieldOfView = baseFieldOfView;

        basePosition = transform.localPosition; // eye height from the scene (0, 1.7, 0)
        rail = GetComponentInParent<RailMover>();
    }

    private void Start()
    {
        // Found once: both live in the scene from the start.
        typing = Object.FindFirstObjectByType<TypingController>();
        spawner = Object.FindFirstObjectByType<WaveSpawner>();
        BuildAvatar();
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime; // 0 while paused: the view holds still
        bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;

        // ---- 1. Focus: where should the head point? ----
        float wantedYaw;
        float wantedPitch;
        if (playing)
        {
            ChooseFocus(deltaTime, out wantedYaw, out wantedPitch);
        }
        else
        {
            // Start panel, pause, results: straight ahead, slowly looking around.
            // Real time (unscaled) so the drift never jumps, whatever the time scale.
            float wave = Time.unscaledTime * idleDriftSpeed * TwoPi;
            wantedYaw = Mathf.Sin(wave) * idleDriftDegrees;
            wantedPitch = Mathf.Sin(wave * 0.7f + 1.3f) * idleDriftDegrees * 0.3f;
            lingerTimer = 0f;
        }

        // Glide toward it, never snap. (SmoothDampAngle must not run with a frame
        // time of 0, it would divide by zero, so it is skipped while paused.)
        if (deltaTime > 0f)
        {
            focusYaw = Mathf.SmoothDampAngle(focusYaw, wantedYaw, ref focusYawVelocity, focusSmoothTime, Mathf.Infinity, deltaTime);
            focusPitch = Mathf.SmoothDampAngle(focusPitch, wantedPitch, ref focusPitchVelocity, focusSmoothTime, Mathf.Infinity, deltaTime);
        }

        // ---- 2. Shake and kick fade out ----
        shakeTrauma = Mathf.Max(0f, shakeTrauma - traumaDecay * deltaTime);
        shakeTime += deltaTime;
        recoil = SpringToZero(recoil, ref recoilVelocity, recoilReturnTime, deltaTime);

        // Shake strength grows with trauma squared: small bumps stay subtle,
        // big blasts really rattle. Perlin noise gives a smooth random jitter.
        float shake = shakeTrauma * shakeTrauma;
        float shakeYaw = 0f;
        float shakePitch = 0f;
        float shakeRoll = 0f;
        Vector3 shakeOffset = Vector3.zero;
        if (shake > 0f)
        {
            shakeYaw = Noise(1.7f) * maxShakeAngle * shake;
            shakePitch = Noise(23.3f) * maxShakeAngle * shake;
            shakeRoll = Noise(47.9f) * maxShakeRoll * shake;
            shakeOffset = new Vector3(Noise(71.3f), Noise(93.1f), 0f) * (maxShakeOffset * shake);
        }

        // ---- 3. Head bob while riding (fades in and out), breathing always ----
        bool riding = playing && rail != null && rail.IsRiding;
        float fadeSpeed = 1f / Mathf.Max(0.01f, bobFadeSeconds);
        rideWeight = Mathf.MoveTowards(rideWeight, riding ? 1f : 0f, fadeSpeed * deltaTime);
        if (rideWeight > 0f)
        {
            bobTime += deltaTime;
        }
        float bobHeightNow = Mathf.Sin(bobTime * bobFrequency * TwoPi) * bobHeight * rideWeight;
        float bobRoll = Mathf.Sin(bobTime * bobFrequency * Mathf.PI) * bobRollDegrees * rideWeight; // half as fast: left step, right step

        breathTime += deltaTime;
        float breathPitch = Mathf.Sin(breathTime * breathingFrequency * TwoPi) * breathingDegrees;
        float breathYaw = Mathf.Sin(breathTime * breathingFrequency * Mathf.PI + 1f) * breathingDegrees * 0.6f;

        // ---- 4. Put it all together ----
        // Unity's X rotation looks DOWN when positive, so the "up" angles are negated.
        float yaw = focusYaw + breathYaw + shakeYaw;
        float pitchUp = focusPitch + recoil + breathPitch + shakePitch;
        float roll = bobRoll + shakeRoll;
        transform.localRotation = Quaternion.Euler(-pitchUp, yaw, roll);
        transform.localPosition = basePosition + Vector3.up * bobHeightNow + transform.localRotation * shakeOffset;
        // (on a phone the view above the on-screen keyboard is very wide: see TouchControls)
        cam.fieldOfView = TouchControls.FitFieldOfView(UpdateFieldOfView(deltaTime, playing), cam.aspect);

        // ---- 5. Map view (waves 2 and 4): blend the first-person view above with it ----
        UpdateMapView(deltaTime, shakeYaw, shakePitch, shakeRoll);
    }

    // ---- Map view ----

    private void UpdateMapView(float deltaTime, float shakeYaw, float shakePitch, float shakeRoll)
    {
        // Up in a map-view wave once the player has stopped at the fight; back
        // into the eyes as soon as the ride to the next fight starts. Glides
        // (and holds still while paused).
        bool riding = rail != null && rail.IsRiding;
        bool showMap = spawner != null && spawner.IsMapViewWave && !riding;
        float speed = 1f / Mathf.Max(0.01f, viewSwitchSeconds);
        viewBlend = Mathf.MoveTowards(viewBlend, showMap ? 1f : 0f, speed * deltaTime);

        // The body only shows once the camera has left the head (it would block the first-person view).
        if (avatar != null)
        {
            avatar.SetActive(viewBlend > 0.15f);
        }

        if (viewBlend <= 0f)
        {
            mapDistanceValid = false; // next time, start the zoom where it belongs
            return;                   // pure first person: keep what LateUpdate set
        }

        // The first-person pose, as LateUpdate just set it, in world space.
        Vector3 eyePosition = transform.position;
        Quaternion eyeRotation = transform.rotation;

        Vector3 mapPosition;
        Quaternion mapRotation;
        MapPose(deltaTime, out mapPosition, out mapRotation);
        mapRotation = mapRotation * Quaternion.Euler(-shakePitch, shakeYaw, shakeRoll); // blasts still shake the map

        // Height leads the move: the camera first RISES straight out of the
        // head, then pulls back and tilts down over the whole area (and on the
        // way back it stays high until the end, then drops into the eyes).
        float t = SmoothStep(viewBlend);
        float rise = 1f - (1f - t) * (1f - t);
        Vector3 position = Vector3.Lerp(eyePosition, mapPosition, t);
        position.y = Mathf.Lerp(eyePosition.y, mapPosition.y, rise);

        transform.SetPositionAndRotation(position, Quaternion.Slerp(eyeRotation, mapRotation, t));
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, TouchControls.FitFieldOfView(mapFieldOfView, cam.aspect), t);
    }

    // The map view's camera pose: behind and above the player, facing the way
    // the player faces (toward the fight), looking down mapPitch degrees. The
    // player sits at mapPlayerScreenHeight on screen, and the camera stands just
    // far enough back to fit the whole area of the current encounter.
    private void MapPose(float deltaTime, out Vector3 position, out Quaternion rotation)
    {
        Transform rig = transform.parent != null ? transform.parent : transform;
        Vector3 anchor = rig.position + Vector3.up; // the middle of the player's body
        Vector3 facing = rig.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = Vector3.forward;
        }
        Quaternion heading = Quaternion.LookRotation(facing.normalized);
        rotation = heading * Quaternion.Euler(mapPitch, 0f, 0f);

        // The player is BELOW the centre of the view: the ray to them points
        // "below" degrees further down than the view itself.
        float tanHalf = Mathf.Tan(TouchControls.FitFieldOfView(mapFieldOfView, cam.aspect) * 0.5f * Mathf.Deg2Rad);
        float below = Mathf.Atan((0.5f - mapPlayerScreenHeight) * 2f * tanHalf) * Mathf.Rad2Deg;
        Vector3 towardPlayer = heading * Quaternion.Euler(mapPitch + below, 0f, 0f) * Vector3.forward;

        CollectFramePoints(anchor);
        float wanted = FitDistance(anchor, rotation, towardPlayer, tanHalf);
        if (!mapDistanceValid)
        {
            mapDistance = wanted;
            mapDistanceVelocity = 0f;
            mapDistanceValid = true;
        }
        else if (deltaTime > 0f)
        {
            mapDistance = Mathf.SmoothDamp(mapDistance, wanted, ref mapDistanceVelocity, mapZoomSmoothTime, Mathf.Infinity, deltaTime);
        }

        position = anchor - towardPlayer * mapDistance;
    }

    // Everything the map view must show: the player, the fight's stop point,
    // every spawn point and its exit (where enemies come from), the barrels,
    // the crates, and every zombie.
    private void CollectFramePoints(Vector3 anchor)
    {
        framePoints.Clear();
        framePoints.Add(anchor);
        if (spawner == null)
        {
            return;
        }

        Encounter encounter = spawner.CurrentEncounter;
        if (encounter != null)
        {
            if (encounter.Route.Count > 0)
            {
                framePoints.Add(encounter.Route[encounter.Route.Count - 1]);
            }
            foreach (SpawnPoint point in encounter.SpawnPoints)
            {
                if (point != null)
                {
                    framePoints.Add(point.Position);
                    framePoints.Add(point.Exit);
                }
            }
            foreach (Barrel barrel in encounter.Barrels)
            {
                if (barrel != null)
                {
                    framePoints.Add(barrel.transform.position);
                }
            }
            foreach (SupplyCrate crate in encounter.Crates)
            {
                if (crate != null)
                {
                    framePoints.Add(crate.transform.position);
                }
            }
        }

        foreach (Zombie zombie in spawner.AliveZombies)
        {
            if (zombie != null && zombie.IsAlive)
            {
                framePoints.Add(zombie.HitPoint);
            }
        }
    }

    // The shortest camera-to-player distance (between mapMinDistance and
    // mapMaxDistance) at which every frame point is on screen, inside the margin.
    // Moving back only brings points closer to the player on screen, so a
    // binary search finds it.
    private float FitDistance(Vector3 anchor, Quaternion rotation, Vector3 towardPlayer, float tanHalf)
    {
        if (!FitsAt(mapMaxDistance, anchor, rotation, towardPlayer, tanHalf))
        {
            return mapMaxDistance; // too big to fit: show as much as we can
        }

        float low = mapMinDistance;
        float high = mapMaxDistance;
        for (int step = 0; step < 20; step++)
        {
            float middle = (low + high) * 0.5f;
            if (FitsAt(middle, anchor, rotation, towardPlayer, tanHalf))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }
        return high;
    }

    // Would every frame point be on screen with the camera 'distance' metres from the player?
    private bool FitsAt(float distance, Vector3 anchor, Quaternion rotation, Vector3 towardPlayer, float tanHalf)
    {
        Vector3 cameraPosition = anchor - towardPlayer * distance;
        Quaternion toCamera = Quaternion.Inverse(rotation);
        float limit = 1f - 2f * mapScreenMargin; // -1..1 is the whole screen
        float aspect = cam.aspect;

        foreach (Vector3 point in framePoints)
        {
            Vector3 local = toCamera * (point - cameraPosition); // x right, y up, z forward
            if (local.z < 0.5f)
            {
                return false; // behind (or right at) the camera
            }
            float x = local.x / (local.z * tanHalf * aspect);
            float y = local.y / (local.z * tanHalf);
            if (Mathf.Abs(x) > limit || Mathf.Abs(y) > limit)
            {
                return false;
            }
        }
        return true;
    }

    // The player's body, only shown in the map view: a green capsule the same
    // shape and size as a zombie's (so it reads as "a person"), a nose that
    // points where the player faces, and a flat green disc on the ground.
    // Green: no enemy or prop uses it.
    private void BuildAvatar()
    {
        if (transform.parent == null)
        {
            return;
        }

        avatar = new GameObject("PlayerAvatar");
        avatar.transform.SetParent(transform.parent, false);
        Material body = Palette.Lit(PlayerGreen);
        Shapes.Block(PrimitiveType.Capsule, "Body", avatar.transform, new Vector3(0f, 1f, 0f), Vector3.one, body);
        Shapes.Block(PrimitiveType.Cube, "Nose", avatar.transform, new Vector3(0f, 1.4f, 0.55f), new Vector3(0.3f, 0.3f, 0.5f), body);
        GameObject disc = Shapes.Block(PrimitiveType.Cylinder, "Disc", avatar.transform, new Vector3(0f, 0.03f, 0f),
            new Vector3(2.2f, 0.01f, 2.2f), Palette.Unlit(PlayerGreen));
        disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        avatar.SetActive(false);
    }

    private static readonly Color PlayerGreen = new Color(0.20f, 0.95f, 0.45f);

    // The player's chest, in front of the body: where map-view shots start.
    private Vector3 ChestPosition()
    {
        Transform rig = transform.parent != null ? transform.parent : transform;
        return rig.position + Vector3.up * 1.3f + rig.forward * 0.5f;
    }

    // 0..1 -> 0..1, starting and ending gently.
    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    // ---- Wide view ----

    // The field of view for this frame: wide while a lure bomb is being aimed or
    // a word is being typed (and wideLingerSeconds after), normal otherwise,
    // gliding between the two. Holds still while paused.
    private float UpdateFieldOfView(float deltaTime, bool playing)
    {
        bool busy = false;
        if (playing)
        {
            bool aimingLure = GameManager.Instance.Powers != null && GameManager.Instance.Powers.IsAiming;
            bool typingWord = typing != null && typing.CurrentTarget != null;
            busy = aimingLure || typingWord;
        }

        if (busy)
        {
            wideTimer = wideLingerSeconds;
        }
        else
        {
            wideTimer = Mathf.Max(0f, wideTimer - deltaTime);
        }

        float wanted = wideTimer > 0f ? wideFieldOfView : baseFieldOfView;
        if (deltaTime > 0f)
        {
            fieldOfView = Mathf.SmoothDamp(fieldOfView, wanted, ref fieldOfViewVelocity, fieldOfViewSmoothTime, Mathf.Infinity, deltaTime);
        }
        return fieldOfView;
    }

    // ---- Focus ----

    // While playing: the angles (degrees, pitch > 0 = up) the head should turn to.
    private void ChooseFocus(float deltaTime, out float wantedYaw, out float wantedPitch)
    {
        wantedYaw = 0f;   // nothing to look at: straight ahead
        wantedPitch = 0f;

        // A word is being typed: look toward its enemy.
        ITypingTarget target = null;
        if (typing != null)
        {
            target = typing.CurrentTarget;
        }
        if (target != null && target.IsAlive)
        {
            lingerPoint = target.HitPoint;
            lingerTimer = focusLingerSeconds;
            AnglesToward(lingerPoint, focusStrength, out wantedYaw, out wantedPitch);
            return;
        }

        // The word was just finished (or dropped): keep looking there a moment,
        // so the view does not leave before the final bullet lands.
        if (lingerTimer > 0f)
        {
            lingerTimer -= deltaTime;
            AnglesToward(lingerPoint, focusStrength, out wantedYaw, out wantedPitch);
            return;
        }

        // No word started: a close zombie draws the eye a little.
        Zombie closest = ClosestZombie();
        if (closest != null)
        {
            AnglesToward(closest.HitPoint, idleFocusStrength, out wantedYaw, out wantedPitch);
        }
    }

    // The yaw and pitch (degrees, pitch > 0 = up) that turn the head toward
    // worldPoint, measured from the way the Player rig faces, clamped to the
    // limits and scaled by strength (0..1).
    private void AnglesToward(Vector3 worldPoint, float strength, out float yaw, out float pitch)
    {
        // The direction to the point, turned into the rig's own space
        // (x = right, y = up, z = the way the rig faces).
        Vector3 direction = Quaternion.Inverse(ParentRotation()) * (worldPoint - EyePosition());
        float flatDistance = new Vector2(direction.x, direction.z).magnitude;

        yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        pitch = Mathf.Atan2(direction.y, flatDistance) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, -maxFocusYaw, maxFocusYaw) * strength;
        pitch = Mathf.Clamp(pitch, -maxFocusPitchDown, maxFocusPitchUp) * strength;
    }

    // The alive zombie closest to the eye within idleFocusDistance, or null.
    private Zombie ClosestZombie()
    {
        if (spawner == null)
        {
            return null;
        }

        Vector3 eye = EyePosition();
        Zombie closest = null;
        float closestDistance = idleFocusDistance;
        foreach (Zombie zombie in spawner.AliveZombies)
        {
            if (zombie == null || !zombie.IsAlive)
            {
                continue;
            }
            float distance = Vector3.Distance(eye, zombie.HitPoint);
            if (distance < closestDistance)
            {
                closest = zombie;
                closestDistance = distance;
            }
        }
        return closest;
    }

    // ---- Small helpers ----

    // The rotation of the Player rig (the camera's parent), or none.
    private Quaternion ParentRotation()
    {
        if (transform.parent != null)
        {
            return transform.parent.rotation;
        }
        return Quaternion.identity;
    }

    // Where the eye is in the world, without bob or shake.
    private Vector3 EyePosition()
    {
        if (transform.parent != null)
        {
            return transform.parent.TransformPoint(basePosition);
        }
        return basePosition;
    }

    // Smooth random number from -1 to 1. Each seed gives a different wobble.
    private float Noise(float seed)
    {
        return Mathf.PerlinNoise(shakeTime * shakeFrequency, seed) * 2f - 1f;
    }

    // Moves value back to 0 like a stiff spring that does not bounce
    // ("critically damped"): it is almost back after 'seconds'. velocity keeps
    // the speed between frames. Holds still while paused (deltaTime 0).
    private static float SpringToZero(float value, ref float velocity, float seconds, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return value;
        }
        // SmoothDamp's smoothTime is about half the time it needs to settle.
        return Mathf.SmoothDamp(value, 0f, ref velocity, seconds * 0.5f, Mathf.Infinity, deltaTime);
    }
}
