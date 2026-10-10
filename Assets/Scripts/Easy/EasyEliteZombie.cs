// EasyEliteZombie.cs
// ---------------------------------------------------------------------------
// EASY SCENE ONLY.
//
// In the elite wave, turns the Nth eligible normal zombie into an ELITE.
// It is part of the existing wave, not an extra spawn, so WaveSpawner's
// wave count remains correct. The script gives it a long word, enlarges
// its body, and displays an "ELITE!" announcement and one-time hint.
//
// Uses public members of WaveSpawner, Zombie, WordBank, and Tutorial.
// Runs at execution order -40, before WaveSpawner lays out word labels.
// EasyWordListSwapper should run earlier (e.g. -50), so this script's
// elite word overrides the normal Easy word.
//
// Known limitation: the word label stays at the zombie's original head
// height, so it may sit slightly low after the body is enlarged.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-40)]
public class EasyEliteZombie : MonoBehaviour
{
    [Header("Which zombie becomes the elite")]
    [SerializeField] private int eliteWave = 3;                // the wave (1 = first) that has the elite
    [SerializeField] private int eliteNormalZombieNumber = 7;  // the Nth NORMAL (grey) zombie of that wave; red / runner / armored ones are not counted.
                                                               // If the wave has fewer normal zombies than this, no elite appears.

    [Header("Look")]
    [SerializeField] private float bodyScale = 1.6f;           // how many times bigger than a normal zombie
    [SerializeField] private Color announceColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color eliteBodyColor = new Color(0.71f, 0.42f, 0.88f);
    [SerializeField] private float eliteSpeedMultiplier = 0.85f;

    [Header("References")]
    [SerializeField] private WaveSpawner spawner;              // left empty: found automatically

    private readonly HashSet<Zombie> seen = new HashSet<Zombie>(); // zombies of the elite wave already looked at
    private int trackedWave = -1;
    private int normalCount;   // normal zombies of the elite wave spawned so far
    private bool eliteDone;

    private void Awake()
    {
        if (spawner == null)
        {
            spawner = FindFirstObjectByType<WaveSpawner>();
        }
    }

    private void LateUpdate()
    {
        if (spawner == null)
        {
            return;
        }

        // A new wave: start counting again.
        if (spawner.CurrentWave != trackedWave)
        {
            trackedWave = spawner.CurrentWave;
            normalCount = 0;
            eliteDone = false;
            seen.Clear();
        }
        if (trackedWave != eliteWave || eliteDone)
        {
            return;
        }

        List<Zombie> alive = spawner.AliveZombies;
        for (int i = 0; i < alive.Count; i++)
        {
            Zombie zombie = alive[i];
            if (zombie == null || !seen.Add(zombie))
            {
                continue; // gone, or already looked at
            }

            // Ambush zombies on the ride, chain pairs and the special kinds are not candidates.
            if (zombie.IsAmbusher || zombie.IsChainLinked || zombie.Kind != ZombieKind.Normal)
            {
                continue;
            }

            normalCount += 1;
            if (normalCount == eliteNormalZombieNumber)
            {
                MakeElite(zombie);
                eliteDone = true;
                return;
            }
        }
    }

    private void MakeElite(Zombie zombie)
    {
        // A long word whose first letter nothing else on screen uses (its own old
        // first letter is freed first). The explosive kind asks for the long list.
        List<char> used = spawner.UsedFirstLetters();
        used.Remove(char.ToUpperInvariant(zombie.Word[0]));
        string[] eliteWords =
        {
            "GASTROENTERITIS",
            "HYPERCHOLESTEROLEMIA",
            "THROMBOCYTOPENIA",
            "RHABDOMYOLYSIS",
            "IMMUNODEFICIENCY",
            "ELECTROENCEPHALOGRAM"
        };

        string eliteWord = eliteWords[Random.Range(0, eliteWords.Length)];
        zombie.Wake(eliteWord);

        // Bigger body (the capsule is 2 m tall at scale 1; its centre moves up so the feet stay on the ground).
        Transform body = zombie.transform.Find("Body");
        if (body != null)
        {
            Vector3 size = body.localScale * bodyScale;
            body.localScale = size;
            body.localPosition = new Vector3(0f, size.y, 0f);

            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMaterial = Palette.Lit(eliteBodyColor);
            }
        }
        
        zombie.ApplySpeedMultiplier(eliteSpeedMultiplier);

        spawner.Hud.ShowFloatingText(zombie.LabelAnchor, "ELITE!", announceColor);
        Tutorial.Once("elite", "ELITE ZOMBIE!", 7f);
    }
}
