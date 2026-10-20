// TouchInput.cs
// ---------------------------------------------------------------------------
// The bridge between the on-screen touch controls and the game. The game only
// reads the keyboard (TypingController is the ONLY input), so a touch key does
// exactly what the real key does: it is sent into Unity's Input System as if a
// keyboard sent it. Nothing in the gameplay code knows about touch.
//
//   TouchInput.Type('a');                 one typed character (like pressing a)
//   TouchInput.Press(Key.Digit3);         a key goes down and stays down...
//   TouchInput.Release(Key.Digit3);       ...and comes back up
//
// Typed characters go through Keyboard.onTextInput, the same path as real
// typing, so analytics, combo and wrong keys all work unchanged. Keys that are
// HELD (1 to aim a lure bomb, 3 / 4 to buy a weapon, the arrow keys, Escape,
// Backspace, Enter) are sent as key states, so wasPressedThisFrame,
// wasReleasedThisFrame and isPressed work too.
//
// A phone has no keyboard, so if Unity has none, one is added (it is then the
// Keyboard.current that TypingController listens to).
// TouchControls calls Update() once per frame. (Needs the Input System package,
// which this project uses: Project Settings > Player > Active Input Handling.)
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class TouchInput
{
    private static readonly List<Key> held = new List<Key>();           // keys down right now
    private static readonly Dictionary<Key, int> pressFrames = new Dictionary<Key, int>(); // frame each key went down
    private static readonly List<Key> releaseNextFrame = new List<Key>(); // let go in the same frame they went down

    // Types one character, exactly as if the player typed it on a keyboard.
    public static void Type(char character)
    {
        Keyboard keyboard = TargetKeyboard();
        InputSystem.QueueTextEvent(keyboard, character);
    }

    // A key goes down (and stays down until Release).
    public static void Press(Key key)
    {
        if (releaseNextFrame.Remove(key))
        {
            // Pressed again before its quick tap was let go: it simply stays down.
            pressFrames[key] = Time.frameCount;
            return;
        }
        if (held.Contains(key))
        {
            return;
        }
        held.Add(key);
        pressFrames[key] = Time.frameCount;
        SendKey(key, true);
    }

    // A key comes back up.
    public static void Release(Key key)
    {
        if (!held.Contains(key))
        {
            return;
        }

        // The events queued this frame are all handled together at the start of
        // the next frame. A key pressed AND released in one frame (a very quick
        // tap) would then never look pressed, so its release waits one frame.
        int pressFrame;
        if (pressFrames.TryGetValue(key, out pressFrame) && pressFrame == Time.frameCount)
        {
            if (!releaseNextFrame.Contains(key))
            {
                releaseNextFrame.Add(key);
            }
            return;
        }

        held.Remove(key);
        SendKey(key, false);
    }

    // Lets go of every key (for example when the touch controls are hidden).
    public static void ReleaseAll()
    {
        releaseNextFrame.Clear();
        foreach (Key key in held)
        {
            SendKey(key, false);
        }
        held.Clear();
    }

    // Called once per frame by TouchControls: sends the releases that had to wait.
    public static void Update()
    {
        for (int i = releaseNextFrame.Count - 1; i >= 0; i--)
        {
            Key key = releaseNextFrame[i];
            int pressFrame;
            if (pressFrames.TryGetValue(key, out pressFrame) && pressFrame < Time.frameCount)
            {
                releaseNextFrame.RemoveAt(i);
                Release(key);
            }
        }
    }

    // A key state event lists EVERY key that is down. So it starts from the keys
    // that are down now (a real keyboard's too, e.g. on a tablet with one) and
    // changes only this key, or the other keys would be let go. Events sent this
    // frame are only handled next frame, so a second change in the same frame
    // builds on the first one.
    private static KeyboardState queuedState;
    private static int queuedFrame = -1;

    private static void SendKey(Key key, bool down)
    {
        Keyboard keyboard = TargetKeyboard();
        KeyboardState state;
        if (queuedFrame == Time.frameCount)
        {
            state = queuedState;
        }
        else
        {
            keyboard.CopyState(out state);
        }
        state.Set(key, down);
        InputSystem.QueueStateEvent(keyboard, state);
        queuedState = state;
        queuedFrame = Time.frameCount;
    }

    // The keyboard the game listens to (TypingController uses Keyboard.current).
    // A phone has none, so one is added the first time it is needed.
    private static Keyboard TargetKeyboard()
    {
        if (Keyboard.current == null)
        {
            Keyboard added = InputSystem.AddDevice<Keyboard>("Touch Keyboard");
            added.MakeCurrent(); // the keyboard TypingController listens to from now on
        }
        return Keyboard.current;
    }
}
