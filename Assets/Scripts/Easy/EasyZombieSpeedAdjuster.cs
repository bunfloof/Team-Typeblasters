// EasyZombieSpeedAdjuster.cs
// ---------------------------------------------------------------------------
// EASY SCENE ONLY.
//
// Adjusts each zombie type's movement speed for Easy without changing
// WaveSpawner's shared speed settings. Each spawned zombie is adjusted
// once using the multipliers configured in the Inspector.
//
// Normal, Runner, Armored, and Explosive zombies can each have a separate
// multiplier. A value below 1 slows that type down; 1 leaves its speed
// unchanged; above 1 makes it faster. These multipliers apply to the
// zombie's existing speed, including the type-specific multiplier already
// applied by Zombie.Setup.
//
// Uses public members of WaveSpawner and Zombie. Requires the small public
// ApplySpeedMultiplier() method in Zombie.cs to adjust its private speed.
//
// Runs in LateUpdate so newly spawned zombies are adjusted after setup.
// EasyEliteZombie runs earlier (-40), so the elite also receives the
// appropriate type multiplier. Apply its separate elite multiplier in
// EasyEliteZombie if the elite should move at a different speed.
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-30)]
public class EasyZombieSpeedAdjuster : MonoBehaviour
{
    [Header("Speed multipliers (relative to current speed)")]
    [SerializeField] private float normalMultiplier = 0.8f;
    [SerializeField] private float runnerMultiplier = 0.75f;
    [SerializeField] private float armoredMultiplier = 0.8f;
    [SerializeField] private float explosiveMultiplier = 0.8f;

    [Header("References")]
    [SerializeField] private WaveSpawner spawner;

    private readonly HashSet<Zombie> adjusted = new HashSet<Zombie>();

    private void Awake()
    {
        if (spawner == null)
        {
            spawner = FindFirstObjectByType<WaveSpawner>();
        }
    }

    private void LateUpdate()
    {
        if (spawner == null) return;

        foreach (Zombie zombie in spawner.AliveZombies)
        {
            if (zombie == null || !adjusted.Add(zombie)) continue;

            float multiplier = zombie.Kind switch
            {
                ZombieKind.Normal => normalMultiplier,
                ZombieKind.Runner => runnerMultiplier,
                ZombieKind.Armored => armoredMultiplier,
                ZombieKind.Explosive => explosiveMultiplier,
                _ => 1f
            };

            zombie.ApplySpeedMultiplier(multiplier);
        }
    }
}