// Palette.cs
// ---------------------------------------------------------------------------
// The colours of everything created while playing (zombies, the boss,
// explosions, rings, bullets, words). The game is a GREYBOX prototype on
// purpose: no textures, no images, only flat colours on Unity's primitive
// shapes (cubes, spheres, capsules, cylinders). The map and the props placed
// in the scene (doors, gates, barrels, crates) use the material assets in
// Assets/Materials/Map and Assets/Materials/Props instead.
//
//   Palette.Lit(color)   - a matte material lit by the scene's light (almost everything).
//   Palette.Unlit(color) - a material that ignores light and always shows its
//                          exact colour (only the flat blast rings and bullets).
//
// A material is created once per colour and then shared, which is faster to
// draw than one material per object. Never change the colour of a shared
// material: every object using it would change. To recolour one object, give
// it another shared material, e.g. renderer.sharedMaterial = Palette.Lit(red).
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class Palette
{
    // ---- Zombies (colour coded) ----
    public static readonly Color ZombieGrey = new Color(0.80f, 0.82f, 0.85f);
    public static readonly Color RunnerYellow = new Color(0.90f, 0.85f, 0.30f);
    public static readonly Color ArmorSteel = new Color(0.35f, 0.42f, 0.55f);
    public static readonly Color FrozenIce = new Color(0.60f, 0.88f, 1.00f);
    public static readonly Color ExplosiveRed = new Color(0.90f, 0.25f, 0.20f);
    public static readonly Color ShifterPurple = new Color(0.65f, 0.20f, 0.90f);
    public static readonly Color SplitterTeal = new Color(0.10f, 0.60f, 0.55f);  // teal, not green: green words are supply crates
    public static readonly Color SplitlingTeal = new Color(0.40f, 0.85f, 0.78f);
    public static readonly Color GhostPale = new Color(0.90f, 0.86f, 1.00f);    // pale lavender
    public static readonly Color BruteDark = new Color(0.45f, 0.25f, 0.20f);    // dark red-brown
    public static readonly Color SpitterAcid = new Color(0.75f, 0.95f, 0.10f);  // acid yellow-green (its globs too)

    // ---- Props ----
    public static readonly Color LureCrate = new Color(1.00f, 0.50f, 0.10f);   // also the lure bomb power on the HUD
    public static readonly Color FreezeCrate = new Color(0.30f, 0.70f, 1.00f); // also the freeze power on the HUD
    public static readonly Color Bomb = new Color(0.10f, 0.10f, 0.10f);
    public static readonly Color BombBlink = new Color(0.90f, 0.10f, 0.10f);
    public static readonly Color QuizAnswer = new Color(0.40f, 0.90f, 1.00f);
    public static readonly Color BlastOrange = new Color(1.00f, 0.55f, 0.10f);
    public static readonly Color Shield = new Color(0.35f, 0.95f, 0.85f);      // the SHIELD power (Tab): HUD row, screen glow, "BLOCKED"

    // ---- Word colours (HUD text) ----
    public static readonly Color WordBarrel = new Color(1.00f, 0.60f, 0.20f);
    public static readonly Color WordCrate = new Color(0.45f, 1.00f, 0.50f);
    public static readonly Color WordQuiz = new Color(0.40f, 0.90f, 1.00f);
    public static readonly Color WordArmor = new Color(0.75f, 0.80f, 0.90f);

    // A zombie's word is a light shade of its body colour (light enough to
    // read on any background), so a word always tells which kind carries it.
    public static readonly Color WordRunner = new Color(1.00f, 0.95f, 0.55f);
    public static readonly Color WordExplosive = new Color(1.00f, 0.60f, 0.55f);
    public static readonly Color WordShifter = new Color(0.86f, 0.62f, 1.00f);
    public static readonly Color WordSplitter = new Color(0.45f, 0.95f, 0.88f);
    public static readonly Color WordSplitling = new Color(0.70f, 1.00f, 0.94f);
    public static readonly Color WordGhost = new Color(0.92f, 0.88f, 1.00f);
    public static readonly Color WordBrute = new Color(0.95f, 0.72f, 0.60f);
    public static readonly Color WordSpitter = new Color(0.88f, 1.00f, 0.45f);

    // The word colour of a zombie of this kind (an armored one with its armour on: WordArmor).
    public static Color WordFor(ZombieKind kind)
    {
        switch (kind)
        {
            case ZombieKind.Runner: return WordRunner;
            case ZombieKind.Armored: return WordArmor;
            case ZombieKind.Explosive: return WordExplosive;
            case ZombieKind.Shifter: return WordShifter;
            case ZombieKind.Splitter: return WordSplitter;
            case ZombieKind.Splitling: return WordSplitling;
            case ZombieKind.Ghost: return WordGhost;
            case ZombieKind.Brute: return WordBrute;
            case ZombieKind.Spitter: return WordSpitter;
            default: return Color.white; // normal zombies are grey
        }
    }
    public static readonly Color WordChain = new Color(0.82f, 0.52f, 1.00f); // purple: a WORD CHAIN pair (hunt / hunter) and its link line
    public static readonly Color WordOrb = new Color(1.00f, 0.55f, 0.85f);   // pink, like the boss's energy orbs (their words are case-sensitive); lighter than WordFrenzy so the two never look alike
    public static readonly Color WordOrbFlash = new Color(1.00f, 0.92f, 0.97f); // almost white: an orb's word flashes up to this with every throb
    public static readonly Color WordFrenzy = new Color(1.00f, 0.25f, 0.35f); // vivid red: the short words during a FRENZY
    public static readonly Color WeaponFrenzy = new Color(1.00f, 0.30f, 0.20f); // the FRENZY weapon: its HUD slot and status line

    private static readonly Dictionary<Color, Material> litMaterials = new Dictionary<Color, Material>();
    private static readonly Dictionary<Color, Material> unlitMaterials = new Dictionary<Color, Material>();

    // A matte material lit by the scene's light.
    public static Material Lit(Color color)
    {
        Material material;
        // "!= null" also catches a material Unity has destroyed in the meantime.
        if (litMaterials.TryGetValue(color, out material) && material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = "Lit " + color;
        material.color = color;
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0f); // matte: no shiny highlights
        }
        litMaterials[color] = material;
        return material;
    }

    // A material that ignores lighting: it always shows exactly its colour.
    public static Material Unlit(Color color)
    {
        Material material;
        if (unlitMaterials.TryGetValue(color, out material) && material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.name = "Unlit " + color;
        material.color = color;
        unlitMaterials[color] = material;
        return material;
    }
}
