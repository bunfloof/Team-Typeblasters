// Explosion.cs
// ---------------------------------------------------------------------------
// Everything that blows up goes through Explosion.Detonate: red (explosive)
// zombies, barrels and lure bombs.
//
//   Explosion.Detonate(position, 4f, null, 150f, report);
//
// DAMAGE (at once, only while the game is being played):
//   1. every alive zombie within the radius dies (Zombie.KillByBlast). The
//      radius is measured flat on the ground, exactly like the blast rings
//      are drawn. A RED zombie caught in the blast explodes too, ChainDelay
//      seconds later, with its own blast radius (a chain reaction);
//   2. every active barrel within the radius explodes too (Barrel.Detonate);
//   3. the boss, if its body is within radius + 3.5 m, takes bossDamage.
//   All kills of one chain reaction are scored together at the end, as one
//   multi-kill (see ExplosionChain).
//
// THE LOOK (kept simple on purpose): a flat orange sphere that grows to the
// size of the blast and shrinks away, a ring on the ground that grows out,
// and a camera shake.
//
// An "Explosion" GameObject is created for every blast; it plays the look and
// the delayed chain explosions, then destroys itself.
// ---------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    // Seconds between one explosion and the next one it sets off (chain reaction).
    public const float ChainDelay = 0.15f;

    private const float BarrelReach = 0.3f;      // a barrel counts as hit this far outside the radius (its own width)
    private const float BossReach = 3.5f;        // the boss is big: it counts as hit this far outside the radius
    private const float SourceTolerance = 0.05f; // a red zombie standing exactly on the centre IS the one exploding

    private const float GrowSeconds = 0.12f;     // the sphere swells fast...
    private const float ShrinkSeconds = 0.3f;    // ...then shrinks away
    private const float SphereSize = 1.2f;       // sphere diameter = radius x this

    private static WaveSpawner spawner; // found once, see FindSpawner

    private Vector3 center;
    private float radius;
    private int delayedExplosions; // chain explosions this object is still waiting to set off

    // Blows up at center (a point on the ground) with the given radius. See the
    // comment at the top of the file. chain: the chain reaction this explosion
    // belongs to, or null to start a new one. report: the "blast" analytics
    // event of a NEW chain, filled in by whatever set it off (see ExplosionChain).
    public static void Detonate(Vector3 center, float radius, ExplosionChain chain, float bossDamage,
        AnalyticsEvent report)
    {
        // Every Detonate ends with exactly one chain.End(), so a new chain
        // needs its matching Begin() here.
        if (chain == null)
        {
            chain = new ExplosionChain();
            chain.Begin();
            chain.Report = report;
        }
        chain.NoteExplosion(center); // the chain's points pop up where it started

        GameObject explosionObject = new GameObject("Explosion");
        explosionObject.transform.position = center;
        Explosion explosion = explosionObject.AddComponent<Explosion>();
        explosion.center = center;
        explosion.radius = Mathf.Max(0.5f, radius);

        explosion.DealDamage(chain, bossDamage);
        explosion.StartCoroutine(explosion.PlayLook());
    }


    // ---- Damage ----

    private void DealDamage(ExplosionChain chain, float bossDamage)
    {
        // The show plays anyway, but nothing is hurt once the game is over
        // (or before it starts).
        bool playing = GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
        WaveSpawner waveSpawner = FindSpawner();

        if (playing && waveSpawner != null)
        {
            HitZombies(waveSpawner, chain);
            HitBarrels(waveSpawner, chain);
            HitBoss(waveSpawner, chain, bossDamage);
        }

        chain.End();
    }

    // 1. Zombies.
    private void HitZombies(WaveSpawner waveSpawner, ExplosionChain chain)
    {
        // A COPY of the list: killed zombies take themselves out of the
        // spawner's list while we loop.
        List<Zombie> zombies = new List<Zombie>(waveSpawner.AliveZombies);
        foreach (Zombie zombie in zombies)
        {
            if (zombie == null || !zombie.IsAlive)
            {
                continue;
            }

            float distance = FlatDistance(zombie.Position, center);
            if (distance > radius)
            {
                continue;
            }

            // Remember what we need BEFORE killing it (it is removed right away).
            bool wasExplosive = zombie.IsExplosive;
            Vector3 zombiePosition = zombie.Position;
            float zombieBlastRadius = zombie.BlastRadius;

            zombie.KillByBlast(center, "blast");
            chain.NoteKill(zombie);

            // A red zombie caught in the blast goes off too, a moment later.
            // (Not the red zombie this explosion comes from: it stands on the centre.)
            if (wasExplosive && distance > SourceTolerance)
            {
                chain.Begin();
                StartCoroutine(DetonateLater(zombiePosition, zombieBlastRadius, chain));
            }
        }
    }

    // 2. Barrels: each one calls chain.Begin() itself (Barrel.Detonate).
    private void HitBarrels(WaveSpawner waveSpawner, ExplosionChain chain)
    {
        List<Barrel> barrels = new List<Barrel>(waveSpawner.ActiveBarrels);
        foreach (Barrel barrel in barrels)
        {
            if (barrel == null || barrel.IsExploded)
            {
                continue;
            }

            if (FlatDistance(barrel.Position, center) <= radius + BarrelReach)
            {
                barrel.Detonate(chain);
            }
        }
    }

    // 3. The boss: barrels, lure bombs and red zombies (the boss summons them) hurt it.
    private void HitBoss(WaveSpawner waveSpawner, ExplosionChain chain, float bossDamage)
    {
        Boss boss = waveSpawner.CurrentBoss;
        if (boss == null || !boss.IsAlive || bossDamage <= 0f)
        {
            return;
        }

        if (FlatDistance(boss.BodyCenter, center) <= radius + BossReach)
        {
            boss.TakeBlastDamage(bossDamage);
            chain.BossDamage += bossDamage;
        }
    }

    // A red zombie caught in this blast explodes ChainDelay seconds later.
    // WaitForSeconds uses game time, so the pause holds the chain too.
    private IEnumerator DetonateLater(Vector3 position, float blastRadius, ExplosionChain chain)
    {
        delayedExplosions += 1;
        yield return new WaitForSeconds(ChainDelay);
        delayedExplosions -= 1;
        Detonate(position, blastRadius, chain, 0f, null);
    }

    // Distance measured on the ground (the height difference is ignored), like the rings.
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // The WaveSpawner is found once and remembered ("== null" is true again
    // after a scene reload, so it is then found again).
    private static WaveSpawner FindSpawner()
    {
        if (spawner == null)
        {
            spawner = Object.FindFirstObjectByType<WaveSpawner>();
        }
        return spawner;
    }

    // ---- The look ----

    private IEnumerator PlayLook()
    {
        // Shake the camera: stronger for a bigger blast, weaker far away.
        Camera view = Camera.main;
        if (view != null)
        {
            float distance = Vector3.Distance(CameraDirector.PlayerEye, center);
            float strength = Mathf.Clamp01(radius / 6f) * Mathf.Clamp01(1f - distance / 45f);
            CameraDirector.Shake(0.2f + 0.5f * strength);
        }

        // A ring on the ground that grows out and disappears (BlastRing's own effect).
        BlastRing ring = BlastRing.Create(transform, radius, Palette.BlastOrange);
        ring.PlayBlastEffect();

        // A flat orange sphere: swells to the blast size, then shrinks away.
        Transform sphere = Shapes.Block(PrimitiveType.Sphere, "Blast", transform, Vector3.zero,
            Vector3.zero, Palette.Lit(Palette.BlastOrange)).transform;
        float fullSize = radius * SphereSize;
        for (float time = 0f; time < GrowSeconds + ShrinkSeconds; time += Time.deltaTime)
        {
            float size;
            if (time < GrowSeconds)
            {
                size = Mathf.Lerp(0.2f, fullSize, time / GrowSeconds);
            }
            else
            {
                size = Mathf.Lerp(fullSize, 0f, (time - GrowSeconds) / ShrinkSeconds);
            }
            sphere.localScale = Vector3.one * size;
            yield return null;
        }
        Destroy(sphere.gameObject);

        // Stay until the chain explosions this blast set off have gone off.
        while (delayedExplosions > 0)
        {
            yield return null;
        }
        Destroy(gameObject);
    }
}
