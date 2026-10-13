// Bullet.cs
// ---------------------------------------------------------------------------
// Every correct letter the player types fires a Bullet (Bullet.Fire) from a
// point just below the camera at the target. The bullet homes in on the target, so it always
// hits even though zombies keep walking.
//   - A normal shot (any letter but the last) makes a zombie stagger (Flinch).
//   - An RPG rocket (combo weapon, Powers.ConsumeRocket) replaces a final shot:
//     bigger and slower, and it EXPLODES where it hits (Explosion).
//   - The FINAL shot (the last letter) is bigger, and ON IMPACT it calls
//     target.CompleteWord(): only then does the zombie die / explode, the
//     barrel blow up, the boss part break...
//
// If the target is already gone when the bullet gets there (caught in another
// blast, or it reached the player), the bullet simply disappears.
// A bullet is a small flat-coloured sphere with a short trail.
// ---------------------------------------------------------------------------
using UnityEngine;

public class Bullet : MonoBehaviour
{
    private const float Speed = 70f;          // metres per second
    private const float Size = 0.08f;         // sphere diameter in metres (normal shot)
    private const float FinalSize = 0.16f;    // the killing shot is bigger
    private const float TrailSeconds = 0.06f; // how long the tracer trail is
    private const float MaxLifetime = 3f;     // safety net: never fly forever

    // The RPG rocket (a combo weapon, see Powers): bigger, slower, a long trail,
    // and it explodes where it hits.
    private const float RocketSpeed = 40f;
    private const float RocketSize = 0.35f;
    private const float RocketTrailSeconds = 0.3f;
    public const float RocketBlastRadius = 3.5f;
    public const float RocketBossDamage = 60f;    // weaker than a barrel (150) on the boss, so the RPG is no boss killer
    private static readonly Color RocketColor = new Color(1f, 0.35f, 0.1f);

    // Where shots start, relative to the camera (right, up, forward), in metres.
    private static readonly Vector3 StartOffset = new Vector3(0f, -0.4f, 0.6f);

    private static readonly Color BulletColor = new Color(1f, 0.85f, 0.3f);

    private ITypingTarget target;
    private bool finalShot;
    private bool rocket;
    private Vector3 lastAim;     // where the target was last seen (a rocket still flies there if it dies)
    private Vector3 flyDirection;
    private float age;

    // Fires a bullet from just below the camera at the target. finalShot = the last letter of the word.
    // rocket = the RPG (only for a final shot): it explodes where it hits.
    public static void Fire(ITypingTarget target, bool finalShot, bool rocket = false)
    {
        rocket = rocket && finalShot;
        Vector3 aim = target.HitPoint;
        // Just below the camera in first person; from the player's chest in the map view.
        Vector3 start = CameraDirector.ShotOrigin(StartOffset);
        if (rocket)
        {
            CameraDirector.Kick(3f); // a hard kick of the view
            CameraDirector.Shake(0.25f);
        }
        else
        {
            CameraDirector.Kick(finalShot ? 1f : 0.3f); // a small kick of the view
        }
        float size = rocket ? RocketSize : (finalShot ? FinalSize : Size);
        Color color = rocket ? RocketColor : BulletColor;

        // Shapes.Block with no parent: a sphere without a collider (it hits by distance).
        GameObject bulletObject = Shapes.Block(PrimitiveType.Sphere, rocket ? "Rocket" : "Bullet", null, start,
            Vector3.one * size, Palette.Unlit(color));

        TrailRenderer trail = bulletObject.AddComponent<TrailRenderer>();
        trail.time = rocket ? RocketTrailSeconds : TrailSeconds;
        trail.startWidth = size;
        trail.endWidth = 0f;
        trail.sharedMaterial = Palette.Unlit(color);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Bullet bullet = bulletObject.AddComponent<Bullet>();
        bullet.target = target;
        bullet.finalShot = finalShot;
        bullet.rocket = rocket;
        bullet.lastAim = aim;
        bullet.flyDirection = (aim - start).normalized;
    }

    private void Update()
    {
        // The target is gone (another blast got it first, or it reached the
        // player): a bullet just disappears, a rocket flies on to where the
        // target was and explodes there.
        if (!target.IsAlive && !rocket)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (age > MaxLifetime)
        {
            if (rocket)
            {
                Explode();
            }
            Destroy(gameObject);
            return;
        }

        // Fly straight at where the target is NOW.
        if (target.IsAlive)
        {
            lastAim = target.HitPoint;
        }
        Vector3 aim = lastAim;
        float step = (rocket ? RocketSpeed : Speed) * Time.deltaTime;
        if (Vector3.Distance(transform.position, aim) <= step)
        {
            transform.position = aim;
            Hit();
            return;
        }
        flyDirection = (aim - transform.position).normalized;
        transform.position = Vector3.MoveTowards(transform.position, aim, step);
    }

    // The RPG rocket's blast: kills zombies, sets off barrels and hurts the boss
    // around where it is (the same Explosion as barrels and lure bombs).
    private void Explode()
    {
        if (GameManager.Instance.State == GameState.Playing)
        {
            AnalyticsEvent report = Analytics.NewEvent("blast");
            report.Add("source", "rpg");
            report.Add("target", Analytics.TargetType(target)); // the word the rocket was fired at
            Explosion.Detonate(transform.position, RocketBlastRadius, null, RocketBossDamage, report);
        }
    }

    private void Hit()
    {
        // Only count the hit while the game is on (not after the player died).
        if (GameManager.Instance.State == GameState.Playing)
        {
            if (rocket)
            {
                if (target.IsAlive)
                {
                    target.CompleteWord(); // the word's own kill first, then the blast
                }
                Explode();
            }
            else if (finalShot)
            {
                target.CompleteWord();
            }
            else
            {
                // A zombie staggers; other targets just take the hit.
                Zombie zombie = target as Zombie;
                if (zombie != null)
                {
                    zombie.Flinch(flyDirection);
                }
            }
        }
        Destroy(gameObject);
    }
}
