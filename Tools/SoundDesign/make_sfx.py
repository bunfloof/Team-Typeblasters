"""
make_sfx.py - the game's typing, shot, kill and multi-kill sounds, made from
scratch (no recordings, no sample packs): noise, sine and saw oscillators,
filters, envelopes and a synthetic reverb.

    python3 Tools/SoundDesign/make_sfx.py

writes 16-bit mono WAVs to Assets/Resources/Sfx/Generated/ (KillSounds.cs
plays them). Every file is the same each time (fixed random seeds).

    key_0..key_5   a mechanical key: the click, the thock as it bottoms out,
                   a little low punch (the "gun" weight) and the key coming up
    shot           the last key of a word: a heavier keystroke with a short,
                   suppressed gunshot under it (crack, body, thump, room)
    kill_1..kill_5 the hit confirm: a short glassy "tink", one step higher for
                   every quick kill after the last (it is over in ~0.25 s)
    multi_2        two kills in one blast: impact and a bright stab
    multi_3        three or more: the big one - sub drop, a wide chord, a
                   rising sparkle, a whoosh, a long hall
    multi_5        five or more: the same, bigger, with a second hit
"""
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Sfx", "Generated")


# ---------------------------------------------------------------- building blocks

def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def silence(seconds):
    return np.zeros(int(seconds * SR))


def place(buffer, sound, at):
    start = int(at * SR)
    end = min(len(buffer), start + len(sound))
    buffer[start:end] += sound[: end - start]
    return buffer


def mix(*parts):
    """Adds sounds of different lengths (all starting together)."""
    out = np.zeros(max(len(p) for p in parts))
    for p in parts:
        out[: len(p)] += p
    return out


def env(seconds, attack, decay):
    """Fast linear attack, exponential decay (decay = seconds to fall to 1/e)."""
    t = t_axis(seconds)
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-t / decay)


def noise(seconds, seed):
    return np.random.default_rng(seed).uniform(-1, 1, int(seconds * SR))


def band(x, low, high, order=2):
    sos = signal.butter(order, [low, high], btype="bandpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, cutoff, order=2):
    sos = signal.butter(order, cutoff, btype="lowpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def highpass(x, cutoff, order=2):
    sos = signal.butter(order, cutoff, btype="highpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def sine(freq, seconds, phase=0.0):
    return np.sin(2 * np.pi * freq * t_axis(seconds) + phase)


def sweep(f0, f1, seconds, curve=6.0):
    """A sine falling (or rising) from f0 to f1, fast at first (a drum's pitch drop)."""
    t = t_axis(seconds)
    k = np.exp(-curve * t / seconds)
    f = f1 + (f0 - f1) * k
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def saw(freq, seconds, seed=0):
    rng = np.random.default_rng(seed)
    t = t_axis(seconds)
    phase = (freq * t + rng.uniform(0, 1)) % 1.0
    return 2 * phase - 1


def supersaw(freq, seconds, voices=7, cents=14, seed=0):
    out = np.zeros(int(seconds * SR))
    for i in range(voices):
        detune = (i - (voices - 1) / 2) / ((voices - 1) / 2) * cents
        out += saw(freq * 2 ** (detune / 1200), seconds, seed + i)
    return out / voices


def reverb(x, seconds, wet, seed=7, brightness=6000, predelay=0.012):
    """Convolution with a synthetic hall: decaying, darkening noise."""
    t = t_axis(seconds)
    ir = noise(seconds, seed) * np.exp(-t * 6.9 / seconds)  # -60 dB at the end
    ir = lowpass(ir, brightness)
    ir = np.concatenate([np.zeros(int(predelay * SR)), ir])
    ir /= np.sqrt(np.sum(ir ** 2))
    tail = signal.fftconvolve(x, ir)[: len(x) + len(ir)]
    out = np.zeros(len(tail))
    out[: len(x)] += x
    return out * (1 - wet) + tail * wet * 1.6


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive)


def fade_out(x, seconds=0.01):
    n = min(len(x), int(seconds * SR))
    x[-n:] *= np.linspace(1, 0, n)
    return x


def trim(x, floor_db=-60):
    """Cut the silent end."""
    level = np.abs(x)
    limit = level.max() * 10 ** (floor_db / 20)
    last = np.nonzero(level > limit)[0]
    return x[: last[-1] + 1] if len(last) else x


def save(name, x, peak_db=-1.0):
    x = trim(x)
    x = fade_out(x.copy())
    x = x / np.max(np.abs(x)) * 10 ** (peak_db / 20)
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print("%-10s %5.0f ms" % (name, len(x) / SR * 1000))


# ---------------------------------------------------------------- the sounds

def keystroke(seed, weight=1.0):
    """One mechanical key: down (click + thock + a little punch), then up."""
    rng = np.random.default_rng(seed)
    pitch = rng.uniform(0.9, 1.12)          # every key a hair different
    x = silence(0.16)

    click = band(noise(0.01, seed), 1800, 6000) * env(0.01, 0.0003, 0.0010)
    place(x, click * 1.1, 0.0)

    thock_noise = band(noise(0.06, seed + 1), 260 * pitch, 1200 * pitch) * env(0.06, 0.0008, 0.011)
    thock_tone = sweep(230 * pitch, 175 * pitch, 0.08, 4) * env(0.08, 0.0015, 0.016)
    case = sine(1250 * pitch, 0.03) * env(0.03, 0.0005, 0.005)
    place(x, mix(thock_noise * 3.2, thock_tone * 0.32 * weight, case * 0.25), 0.002)

    punch = sweep(110, 52, 0.07, 5) * env(0.07, 0.002, 0.018)   # the weight under it
    place(x, punch * 0.16 * weight, 0.002)

    up = band(noise(0.012, seed + 2), 1400 * pitch, 4200) * env(0.012, 0.0003, 0.0018)
    place(x, up * 0.9, rng.uniform(0.055, 0.075))
    return reverb(x, 0.12, 0.06, seed=seed + 3, brightness=5000)


def shot():
    """The last key of a word: a heavy keystroke and a suppressed shot under it."""
    x = silence(0.7)
    place(x, keystroke(101, weight=1.4) * 0.8, 0.0)

    crack = highpass(noise(0.03, 11), 2600, 4) * env(0.03, 0.0002, 0.0024)
    body = lowpass(noise(0.2, 12), 2400) * env(0.2, 0.0006, 0.028)
    body = body + band(noise(0.2, 13), 450, 1300) * env(0.2, 0.0006, 0.02) * 0.6
    thump = sweep(140, 42, 0.18, 6) * env(0.18, 0.001, 0.05)
    shot = mix(crack * 2.2, body * 1.6, thump * 0.55)
    shot = soft_clip(shot, 1.6)
    place(x, shot, 0.004)

    # the action cycling, a beat later: two small metal ticks
    for at, f in ((0.058, 3300), (0.083, 2700)):
        tick = band(noise(0.015, int(f)), 1800, 7000) * env(0.015, 0.0002, 0.002)
        ping = sine(f, 0.03) * env(0.03, 0.0005, 0.006)
        place(x, mix(tick * 0.18, ping * 0.05), at)

    return reverb(x, 0.55, 0.16, seed=14, brightness=3500, predelay=0.018)


# D major pentatonic, up from D6: each quick kill climbs one step.
KILL_NOTES = [1174.66, 1318.51, 1479.98, 1760.00, 1975.53]


def kill(step):
    """A short glassy hit confirm. Over fast: it never sits on the next word."""
    f = KILL_NOTES[step - 1]
    x = silence(0.4)
    tone = (sine(f, 0.3) * 1.0 + sine(f * 2.0, 0.3) * 0.35 + sine(f * 3.01, 0.3) * 0.12)
    tone *= env(0.3, 0.0015, 0.05 + 0.006 * step)
    shimmer = sine(f * 1.5, 0.3) * env(0.3, 0.004, 0.035) * 0.25          # a fifth above: the "glass"
    hit = band(noise(0.02, 200 + step), 2500, 9000) * env(0.02, 0.0002, 0.0018)
    thud = sweep(180, 90, 0.05, 4) * env(0.05, 0.001, 0.012)               # the body taking it
    place(x, tone * 0.55 + shimmer, 0.0)
    place(x, mix(hit * 0.35, thud * 0.25), 0.0)
    return reverb(x, 0.35, 0.12, seed=300 + step, brightness=8000)


def chord_stab(notes, seconds, decay, cutoff_from, cutoff_to, seed):
    """A wide supersaw chord whose filter closes as it rings."""
    x = np.zeros(int(seconds * SR))
    for i, f in enumerate(notes):
        x += supersaw(f, seconds, seed=seed + 10 * i)
    x /= len(notes)
    # The filter sweep, in short blocks.
    out = np.zeros_like(x)
    block = 512
    zi = None
    for start in range(0, len(x), block):
        frac = start / len(x)
        cutoff = cutoff_to + (cutoff_from - cutoff_to) * np.exp(-frac * 5)
        sos = signal.butter(2, cutoff, btype="lowpass", fs=SR, output="sos")
        if zi is None:
            zi = signal.sosfilt_zi(sos) * 0
        out[start:start + block], zi = signal.sosfilt(sos, x[start:start + block], zi=zi)
    return out * env(seconds, 0.004, decay)


def pluck(f, seconds=0.5, decay=0.14):
    tone = sine(f, seconds) + 0.4 * sine(f * 2, seconds) + 0.15 * sine(f * 3, seconds)
    return tone * env(seconds, 0.002, decay)


def multi(level):
    """level 2: a double; 3: the big one (an "ace"); 5: bigger still."""
    big = level >= 3
    huge = level >= 5
    length = 3.2 if big else 1.6
    x = silence(length)
    dry = silence(length)     # the sub stays out of the reverb: tight and huge

    # the impact
    sub = sweep(95 if big else 120, 34, 0.9, 5) * env(0.9, 0.002, 0.32 if big else 0.18)
    place(dry, soft_clip(sub * 1.3, 2.0) * (1.0 if big else 0.7), 0.0)
    punch = lowpass(noise(0.15, 400), 3500) * env(0.15, 0.0005, 0.02)
    place(x, punch * 0.7, 0.0)

    # the chord: A major add9, wide and bright
    A = 220.0
    if big:
        notes = [A, A * 1.5, A * 2, A * 2.5, A * 3, A * 4.5]       # A3 E4 A4 C#5 E5 B5
    else:
        notes = [A * 2, A * 3, A * 2.5 * 2]                          # A4 E5 C#6
    stab = chord_stab(notes, 2.4 if big else 1.2, 0.55 if big else 0.28,
                      9000 if big else 7000, 1400, seed=500 + level)
    place(x, stab * (0.75 if big else 0.55), 0.01)

    # the sparkle running up
    run = [A * 4, A * 5, A * 6, A * 8, A * 10, A * 12] if big else [A * 4, A * 6, A * 8]
    gap = 0.045 if big else 0.055
    for i, f in enumerate(run):
        place(x, pluck(f, 0.6, 0.12 + 0.02 * i) * (0.22 if big else 0.18), 0.03 + i * gap)

    if big:
        # the whoosh: a bright noise rising into the chord, then away
        whoosh_len = 1.4
        t = t_axis(whoosh_len)
        shape = np.minimum(t / 0.18, 1.0) * np.exp(-np.maximum(t - 0.18, 0) / 0.35)
        whoosh = band(noise(whoosh_len, 600), 3500, 11000) * shape
        place(x, whoosh * 0.16, 0.0)
        # a high shimmer that trembles
        sh_len = 1.6
        trem = 0.55 + 0.45 * np.sin(2 * np.pi * 11 * t_axis(sh_len))
        shimmer = (sine(A * 16, sh_len) + 0.6 * sine(A * 20, sh_len) + 0.4 * sine(A * 24, sh_len)) * trem
        place(x, shimmer * env(sh_len, 0.06, 0.45) * 0.05, 0.12)

    if huge:
        # a second hit a beat later, an octave up
        place(dry, soft_clip(sweep(130, 45, 0.6, 5) * env(0.6, 0.002, 0.2), 2.0) * 0.7, 0.32)
        place(x, chord_stab([n * 2 for n in notes[:4]], 1.6, 0.45, 10000, 1800, seed=700) * 0.5, 0.32)
        for i, f in enumerate([A * 12, A * 16, A * 20]):
            place(x, pluck(f, 0.5, 0.1) * 0.15, 0.34 + i * 0.04)

    wet = reverb(x, 2.4 if big else 1.2, 0.38 if big else 0.28, seed=800 + level, brightness=7000, predelay=0.025)
    out = np.zeros(max(len(wet), len(dry)))
    out[: len(wet)] += wet
    out[: len(dry)] += dry
    return soft_clip(out / np.max(np.abs(out)) * 1.1, 1.3)


# ================================================================ more tools

def square(freq, seconds):
    """A square wave with its top softened (no harsh aliasing)."""
    return lowpass(np.sign(sine(freq, seconds)), min(9000, freq * 9))


def fm(carrier, ratio, index, seconds, index_decay):
    """FM synthesis: a carrier whose pitch is wobbled by a modulator (bells, metal)."""
    t = t_axis(seconds)
    mod = index * np.exp(-t / index_decay) * np.sin(2 * np.pi * carrier * ratio * t)
    return np.sin(2 * np.pi * carrier * t + mod)


def crush(x, bits=8, rate=11025):
    """Fewer bits and fewer samples: a digital crunch."""
    step = max(1, int(SR / rate))
    held = np.repeat(x[::step], step)[: len(x)]
    levels = 2 ** (bits - 1)
    return np.round(held * levels) / levels


GLASS = [1.0, 2.32, 4.25, 6.63, 9.38]        # the partials of a struck glass (not whole numbers)


def glass(f, seconds, decay):
    out = np.zeros(int(seconds * SR))
    for i, r in enumerate(GLASS):
        out += sine(f * r, seconds) * (0.6 ** i) * env(seconds, 0.001, decay / (1 + i * 0.6))
    return out


def bell(f, seconds, decay):
    """A low bell: the hum, the strike note and the inharmonic ring above."""
    out = np.zeros(int(seconds * SR))
    for r, a in ((0.5, 0.5), (1.0, 1.0), (1.19, 0.5), (1.5, 0.35), (2.0, 0.4), (2.52, 0.25), (3.01, 0.15)):
        out += sine(f * r, seconds) * a * env(seconds, 0.002, decay * (1.4 if r < 1 else 1.0 / r))
    return out


def formant_pad(notes, seconds, vowel=(700, 1150, 2600)):
    """Saws through vowel filters: a choir-like "aah"."""
    src = np.zeros(int(seconds * SR))
    for i, f in enumerate(notes):
        src += supersaw(f, seconds, voices=5, cents=10, seed=900 + i)
    out = np.zeros_like(src)
    for k, fc in enumerate(vowel):
        out += band(src, fc * 0.85, fc * 1.15) * (1.0, 0.6, 0.3)[k]
    return out


def riser(seconds, seed=0):
    """Noise rising in pitch and level into the hit."""
    t = t_axis(seconds)
    x = noise(seconds, seed)
    out = np.zeros_like(x)
    block = 512
    for start in range(0, len(x), block):
        frac = start / len(x)
        lo = 600 + 5000 * frac ** 2
        out[start:start + block] = band(x[max(0, start - 2048):start + block], lo, min(16000, lo * 3))[-len(x[start:start + block]):]
    return out * (t / seconds) ** 2.2


def reverse_swell(x):
    """A sound's reverb, played backwards: it sucks in toward the hit."""
    wet = reverb(x, 1.2, 1.0, seed=31, brightness=9000)
    return wet[::-1]


def note(name):
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    base = names[name[0]]
    sharp = 1 if "#" in name else 0
    octave = int(name[-1])
    return 440.0 * 2 ** ((base + sharp + (octave - 4) * 12) / 12)


# ================================================================ the sets
# Each set: keys(seed) -> one key, shot(), kill(step 1..5), multi(level 2/3/5).
# "thock" is the set above; five more follow.

def thock_set():
    return dict(key=keystroke, shot=shot, kill=kill, multi=multi)


# ---- 1. TACTICAL: tight, dry, military. Linear switches, a suppressed rifle,
# a sharp metallic "tink" for the kill, and for the multi-kill a sucked-in
# swell that slams into a sub drop, a choir chord and glass.

def tactical_key(seed):
    rng = np.random.default_rng(seed)
    p = rng.uniform(0.93, 1.08)
    x = silence(0.12)
    place(x, mix(band(noise(0.05, seed), 420 * p, 2200 * p) * env(0.05, 0.0005, 0.006) * 3.0,
                 sweep(320 * p, 240 * p, 0.04, 4) * env(0.04, 0.001, 0.007) * 0.35,
                 band(noise(0.01, seed + 1), 2500, 7000) * env(0.01, 0.0002, 0.0007) * 0.7), 0.0)
    place(x, band(noise(0.01, seed + 2), 1800, 5000) * env(0.01, 0.0003, 0.0012) * 0.6, rng.uniform(0.045, 0.06))
    return reverb(x, 0.08, 0.04, seed=seed + 3, brightness=6000)


def tactical_shot():
    x = silence(0.5)
    place(x, tactical_key(77) * 0.7, 0.0)
    crack = highpass(noise(0.02, 1), 3500, 4) * env(0.02, 0.0001, 0.0015)
    puff = band(noise(0.12, 2), 280, 1100) * env(0.12, 0.001, 0.03)
    thump = sweep(160, 55, 0.12, 6) * env(0.12, 0.001, 0.03)
    place(x, soft_clip(mix(crack * 2.5, puff * 2.4, thump * 0.6), 1.8), 0.003)
    for at, lo in ((0.045, 1500), (0.07, 2200)):           # the bolt: clack-clack
        place(x, band(noise(0.02, int(lo)), lo, lo * 3) * env(0.02, 0.0002, 0.0025) * 0.7, at)
    return reverb(x, 0.3, 0.1, seed=4, brightness=4000, predelay=0.01)


def tactical_kill(step):
    f = [2093.0, 2349.3, 2637.0, 2793.8, 3136.0][step - 1]      # C7 D7 E7 F7 G7
    x = silence(0.3)
    ting = (sine(f, 0.25) + sine(f * 1.012, 0.25)) * 0.5 * env(0.25, 0.0008, 0.045)
    ting += sine(f * 2.76, 0.25) * env(0.25, 0.0005, 0.015) * 0.25
    place(x, mix(ting, highpass(noise(0.01, step), 5000) * env(0.01, 0.0001, 0.001) * 0.6,
                 sweep(200, 100, 0.04, 4) * env(0.04, 0.001, 0.008) * 0.3), 0.0)
    return reverb(x, 0.3, 0.1, seed=40 + step, brightness=9000)


def tactical_multi(level):
    big, huge = level >= 3, level >= 5
    pre = 0.28 if big else 0.0                                   # the swell before the hit
    x, dry = silence(3.6), silence(3.6)
    chord = [note(n) for n in ("D3", "A3", "D4", "F#4", "A4", "E5")]
    hit = formant_pad(chord if big else chord[2:5], 2.6, (650, 1080, 2650)) * env(2.6, 0.01, 0.7 if big else 0.35)
    if big:
        swell = reverse_swell(hit[: int(0.6 * SR)] * 0.6)[-int(pre * SR):]
        place(x, swell * 0.6 + riser(pre, 5)[: len(swell)] * 0.25, 0.0)
    place(dry, soft_clip(sweep(100, 32, 1.2, 5) * env(1.2, 0.002, 0.45 if big else 0.2), 2.2) * (1 if big else 0.7), pre)
    place(x, lowpass(noise(0.2, 6), 4000) * env(0.2, 0.0004, 0.025) * 1.5, pre)
    place(x, hit * (0.8 if big else 0.6), pre)
    for i, n in enumerate(("D6", "F#6", "A6", "D7", "F#7", "A7")[: 6 if big else 3]):
        place(x, glass(note(n), 0.8, 0.35) * 0.18, pre + 0.04 + i * 0.05)
    if huge:
        place(dry, soft_clip(sweep(140, 40, 0.8, 5) * env(0.8, 0.002, 0.3), 2) * 0.8, pre + 0.35)
        place(x, formant_pad([f * 2 for f in chord[:4]], 1.8, (700, 1150, 2600)) * env(1.8, 0.01, 0.5) * 0.6, pre + 0.35)
    wet = reverb(x, 2.6 if big else 1.4, 0.4 if big else 0.3, seed=60 + level, brightness=8000, predelay=0.03)
    return soft_clip(mix(wet, dry) / np.max(np.abs(mix(wet, dry))) * 1.1, 1.3)


# ---- 2. ARCADE: clicky blue switches and punchy retro tones, made solid
# (filtered square waves, real low end): a coin-like kill and a fanfare.

def arcade_key(seed):
    rng = np.random.default_rng(seed)
    x = silence(0.1)
    for at in (0.0, 0.006):                                      # the click jacket: two snaps
        place(x, band(noise(0.008, seed + int(at * 1000)), 3000, 9000) * env(0.008, 0.0001, 0.0008) * 1.2, at)
    place(x, band(noise(0.04, seed + 9), 500, 2500) * env(0.04, 0.0005, 0.005) * 1.5, 0.008)
    place(x, square(rng.uniform(1900, 2300), 0.02) * env(0.02, 0.0005, 0.004) * 0.12, 0.0)
    return x


def arcade_shot():
    x = silence(0.4)
    place(x, arcade_key(5) * 0.7, 0.0)
    t = t_axis(0.2)
    f = 60 + 520 * np.exp(-t / 0.025)
    body = lowpass(np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)), 2500) * env(0.2, 0.001, 0.045)
    place(x, mix(body * 0.8, highpass(noise(0.05, 3), 2000) * env(0.05, 0.0002, 0.006) * 1.2,
                 sweep(120, 45, 0.15, 5) * env(0.15, 0.001, 0.04) * 0.7), 0.004)
    return reverb(x, 0.25, 0.08, seed=8, brightness=5000)


def arcade_kill(step):
    a = [note(n) for n in ("B5", "C#6", "D#6", "F#6", "G#6")][step - 1]
    x = silence(0.25)
    place(x, square(a, 0.05) * env(0.05, 0.001, 0.03) * 0.5, 0.0)
    place(x, square(a * 4 / 3, 0.15) * env(0.15, 0.001, 0.05) * 0.5, 0.05)
    return reverb(x, 0.2, 0.08, seed=70 + step, brightness=7000)


def arcade_multi(level):
    big, huge = level >= 3, level >= 5
    x, dry = silence(2.6), silence(2.6)
    run = ["C5", "E5", "G5", "C6", "E6", "G6", "C7"][: 7 if big else 4]
    for i, n in enumerate(run):
        place(x, mix(square(note(n), 0.12) * env(0.12, 0.001, 0.06), square(note(n) * 1.005, 0.12) * env(0.12, 0.001, 0.06)) * 0.3, i * 0.055)
    end = len(run) * 0.055
    for n in (["C5", "E5", "G5", "C6"] if big else ["C6", "G6"]):
        place(x, mix(square(note(n), 1.2), square(note(n) * 1.004, 1.2)) * env(1.2, 0.005, 0.4 if big else 0.2) * 0.2, end)
    place(x, highpass(noise(1.2, 9), 4000) * env(1.2, 0.002, 0.25 if big else 0.12) * 0.5, end)   # the crash
    place(dry, soft_clip(sweep(110, 40, 0.6, 5) * env(0.6, 0.002, 0.2), 2) * (1 if big else 0.6), 0.0)
    if huge:
        place(dry, soft_clip(sweep(110, 40, 0.6, 5) * env(0.6, 0.002, 0.2), 2) * 0.9, end)
        for i, n in enumerate(["C7", "E7", "G7", "C8"]):
            place(x, square(note(n), 0.1) * env(0.1, 0.001, 0.05) * 0.18, end + 0.1 + i * 0.045)
    wet = reverb(x, 1.4, 0.25, seed=90 + level, brightness=7000)
    return soft_clip(mix(wet, dry) / np.max(np.abs(mix(wet, dry))) * 1.1, 1.3)


# ---- 3. CINEMATIC: heavy, deep, trailer-sized. Thick low keys, a big
# distant boom of a shot, a dark bell for the kill, and a "BRAAAM".

def cinematic_key(seed):
    rng = np.random.default_rng(seed)
    p = rng.uniform(0.92, 1.08)
    x = silence(0.16)
    place(x, mix(lowpass(noise(0.08, seed), 900 * p) * env(0.08, 0.001, 0.012) * 3.0,
                 sweep(170 * p, 120 * p, 0.1, 4) * env(0.1, 0.002, 0.022) * 0.5,
                 band(noise(0.01, seed + 1), 1500, 4000) * env(0.01, 0.0003, 0.0012) * 0.4), 0.0)
    return reverb(x, 0.2, 0.08, seed=seed + 2, brightness=3000)


def cinematic_shot():
    x, dry = silence(1.2), silence(1.2)
    place(x, cinematic_key(3) * 0.7, 0.0)
    place(dry, soft_clip(sweep(120, 36, 0.5, 6) * env(0.5, 0.001, 0.1), 2.5) * 0.9, 0.004)
    place(x, mix(highpass(noise(0.03, 4), 2000) * env(0.03, 0.0002, 0.003) * 1.5,
                 lowpass(noise(0.4, 5), 1200) * env(0.4, 0.002, 0.06) * 1.6), 0.004)
    wet = reverb(x, 1.0, 0.3, seed=12, brightness=2500, predelay=0.03)
    return mix(wet, dry)


def cinematic_kill(step):
    f = [note(n) for n in ("D4", "E4", "F#4", "A4", "B4")][step - 1]
    x = silence(0.5)
    place(x, mix(bell(f, 0.45, 0.09) * 0.5, lowpass(noise(0.03, step), 2500) * env(0.03, 0.0003, 0.004) * 0.8,
                 sweep(150, 70, 0.08, 4) * env(0.08, 0.001, 0.02) * 0.5), 0.0)
    return reverb(x, 0.5, 0.15, seed=110 + step, brightness=4000)


def braam(notes, seconds, decay):
    src = np.zeros(int(seconds * SR))
    for i, f in enumerate(notes):
        src += supersaw(f, seconds, voices=5, cents=18, seed=120 + i)
    src = soft_clip(lowpass(src, 1400) * 1.8, 2.5)
    return src * env(seconds, 0.025, decay)


def cinematic_multi(level):
    big, huge = level >= 3, level >= 5
    x, dry = silence(3.8), silence(3.8)
    notes = [note(n) for n in ("D1", "D2", "A2", "D3", "F3")] if big else [note(n) for n in ("D2", "A2", "D3")]
    place(x, braam(notes, 2.8 if big else 1.4, 0.8 if big else 0.35) * 0.8, 0.0)
    place(dry, soft_clip(sweep(80, 28, 1.4, 4) * env(1.4, 0.002, 0.55 if big else 0.25), 2.5), 0.0)
    place(x, lowpass(noise(0.4, 13), 1500) * env(0.4, 0.001, 0.06) * 1.6, 0.0)       # the taiko-like skin
    place(x, bell(note("D5"), 2.0, 0.6) * (0.25 if big else 0.15), 0.02)
    if huge:
        place(x, braam([n * 2 for n in notes[1:4]], 1.8, 0.5) * 0.6, 0.45)
        place(dry, soft_clip(sweep(90, 30, 0.9, 4) * env(0.9, 0.002, 0.35), 2.5) * 0.85, 0.45)
    wet = reverb(x, 3.0 if big else 1.6, 0.4, seed=130 + level, brightness=3500, predelay=0.04)
    return soft_clip(mix(wet, dry) / np.max(np.abs(mix(wet, dry))) * 1.1, 1.4)


# ---- 4. CYBER: synthwave and FM metal. Keys with a digital crunch, a
# metallic "tschk" of a shot, FM bell kills, and a gated, rising cascade.

def cyber_key(seed):
    rng = np.random.default_rng(seed)
    p = rng.uniform(0.94, 1.07)
    x = silence(0.1)
    base = mix(band(noise(0.05, seed), 400 * p, 2000 * p) * env(0.05, 0.0005, 0.006) * 2.6,
               fm(2600 * p, 1.41, 3.0, 0.03, 0.008) * env(0.03, 0.0003, 0.004) * 0.25)
    place(x, crush(base, 7, 16000), 0.0)
    return reverb(x, 0.1, 0.06, seed=seed + 1, brightness=7000)


def cyber_shot():
    x = silence(0.5)
    place(x, cyber_key(4) * 0.7, 0.0)
    place(x, mix(fm(900, 2.73, 8.0, 0.18, 0.04) * env(0.18, 0.0005, 0.035) * 0.6,
                 highpass(noise(0.06, 6), 3000) * env(0.06, 0.0002, 0.008) * 1.4,
                 sweep(150, 48, 0.14, 6) * env(0.14, 0.001, 0.035) * 0.7), 0.004)
    return reverb(x, 0.4, 0.14, seed=21, brightness=6000)


def cyber_kill(step):
    f = [note(n) for n in ("E6", "F#6", "G#6", "B6", "C#7")][step - 1]
    x = silence(0.3)
    place(x, fm(f, 3.5, 2.5, 0.25, 0.05) * env(0.25, 0.001, 0.05) * 0.6, 0.0)
    place(x, fm(f * 2, 1.0, 1.0, 0.1, 0.02) * env(0.1, 0.001, 0.02) * 0.2, 0.0)
    return reverb(x, 0.3, 0.14, seed=140 + step, brightness=9000)


def cyber_multi(level):
    big, huge = level >= 3, level >= 5
    pre = 0.3 if big else 0.0
    x, dry = silence(3.2), silence(3.2)
    if big:
        place(x, riser(pre, 7) * 0.35, 0.0)
    chord = [note(n) for n in ("E3", "B3", "E4", "G#4", "B4", "D#5")] if big else [note(n) for n in ("E4", "B4", "G#5")]
    pad = chord_stab(chord, 2.0, 0.7 if big else 0.35, 8000, 1600, seed=150)
    gate = (np.sin(2 * np.pi * 8 * t_axis(2.0)) > -0.3).astype(float)           # 16th-note gate
    gate = lowpass(gate, 60)
    place(x, pad * gate * 0.75, pre)
    place(dry, soft_clip(sweep(100, 34, 0.9, 5) * env(0.9, 0.002, 0.35 if big else 0.18), 2.2), pre)
    place(x, highpass(noise(0.1, 8), 1500) * env(0.1, 0.0003, 0.015) * 1.0, pre)
    for i, n in enumerate(("E6", "G#6", "B6", "E7", "G#7", "B7", "E8")[: 7 if big else 3]):
        place(x, fm(note(n), 3.5, 2.0, 0.5, 0.08) * env(0.5, 0.001, 0.12) * 0.2, pre + 0.03 + i * 0.045)
    if huge:
        place(dry, soft_clip(sweep(120, 38, 0.7, 5) * env(0.7, 0.002, 0.25), 2) * 0.8, pre + 0.38)
        place(x, chord_stab([c * 2 for c in chord[:4]], 1.4, 0.4, 10000, 2000, seed=160) * 0.5, pre + 0.38)
    wet = reverb(x, 2.2 if big else 1.2, 0.35, seed=170 + level, brightness=9000, predelay=0.02)
    return soft_clip(mix(wet, dry) / np.max(np.abs(mix(wet, dry))) * 1.1, 1.3)


# ---- 5. CRYSTAL: soft, quiet keys and glass. A muffled pop of a shot,
# crystal pings for kills, and a blooming cascade of glass for the multi-kill.

def crystal_key(seed):
    rng = np.random.default_rng(seed)
    p = rng.uniform(0.93, 1.08)
    x = silence(0.1)
    place(x, mix(lowpass(band(noise(0.05, seed), 250 * p, 1400 * p), 1800) * env(0.05, 0.001, 0.008) * 3.0,
                 sweep(210 * p, 160 * p, 0.05, 4) * env(0.05, 0.0015, 0.01) * 0.25), 0.0)
    return reverb(x, 0.1, 0.05, seed=seed + 1, brightness=4000)


def crystal_shot():
    x = silence(0.6)
    place(x, crystal_key(6) * 0.8, 0.0)
    place(x, mix(lowpass(noise(0.1, 2), 1600) * env(0.1, 0.001, 0.022) * 1.8,
                 sweep(130, 50, 0.12, 5) * env(0.12, 0.001, 0.03) * 0.6,
                 glass(note("A6"), 0.3, 0.06) * 0.12), 0.004)
    return reverb(x, 0.5, 0.18, seed=22, brightness=7000)


def crystal_kill(step):
    f = [note(n) for n in ("A5", "B5", "C#6", "E6", "F#6")][step - 1]
    x = silence(0.4)
    place(x, mix(glass(f, 0.35, 0.09) * 0.5, highpass(noise(0.006, step), 6000) * env(0.006, 0.0001, 0.0008) * 0.4), 0.0)
    return reverb(x, 0.4, 0.15, seed=180 + step, brightness=10000)


def crystal_multi(level):
    big, huge = level >= 3, level >= 5
    x, dry = silence(4.0), silence(4.0)
    run = ["A5", "C#6", "E6", "A6", "C#7", "E7", "A7", "C#8"][: 8 if big else 4]
    for i, n in enumerate(run):
        place(x, glass(note(n), 1.2, 0.5) * 0.22, i * 0.04)
    pad = formant_pad([note(n) for n in ("A3", "E4", "A4", "C#5")], 3.0, (800, 1200, 2800)) * env(3.0, 0.15, 0.9 if big else 0.4)
    place(x, pad * (0.5 if big else 0.3), 0.0)
    place(dry, soft_clip(sweep(90, 35, 1.0, 5) * env(1.0, 0.01, 0.35 if big else 0.18), 2) * (0.8 if big else 0.5), 0.0)
    if huge:
        for i, n in enumerate(["A7", "E7", "C#7", "A6", "E6"]):
            place(x, glass(note(n), 1.0, 0.4) * 0.18, 0.4 + i * 0.04)
    wet = reverb(x, 3.4 if big else 1.8, 0.5 if big else 0.4, seed=190 + level, brightness=10000, predelay=0.03)
    return soft_clip(mix(wet, dry) / np.max(np.abs(mix(wet, dry))) * 1.1, 1.2)


# ================================================================ premium keyboards
# A real key is a few damped ringing modes (the plate, the case, the keycap's
# hollow), set off by a sharp contact, then a softer second contact as the key
# comes back up. Each take nudges the modes a little, like different keys.

def modes(freqs, decays, amps, seconds, jitter, rng):
    out = np.zeros(int(seconds * SR))
    t = t_axis(seconds)
    for f, d, a in zip(freqs, decays, amps):
        f *= rng.uniform(1 - jitter, 1 + jitter)
        a *= rng.uniform(0.85, 1.15)
        out += np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / d) * a
    return out * np.clip(t / 0.0004, 0, 1)


def switch_key(seed, freqs, decays, amps, contact, up_at, up_level, room):
    rng = np.random.default_rng(seed)
    x = silence(0.16)
    lo, hi, tau = contact
    place(x, band(noise(0.01, seed), lo, hi) * env(0.01, 0.0001, tau) * 1.2, 0.0)
    place(x, modes(freqs, decays, amps, 0.1, 0.04, rng), 0.0005)
    at = rng.uniform(*up_at)
    place(x, band(noise(0.008, seed + 1), lo * 1.2, hi) * env(0.008, 0.0001, tau * 0.7) * up_level * 1.2, at)
    place(x, modes([f * 1.07 for f in freqs], [d * 0.7 for d in decays], amps, 0.06, 0.05, rng) * up_level, at + 0.0005)
    return reverb(x, 0.09, room, seed=seed + 2, brightness=7000, predelay=0.004)


def red_key(seed):
    """Keychron-style linear reds: poppy, clacky, a hollow case under it."""
    return switch_key(seed, [390, 1150, 1900, 2750, 3900], [0.012, 0.008, 0.007, 0.0055, 0.0035],
                      [0.45, 0.7, 1.0, 0.6, 0.28], (1000, 8000, 0.0009), (0.055, 0.08), 0.38, 0.07)


def flow_key(seed):
    """Low-profile, smooth and marbly (Flow-style): short travel, a rounded "tok"."""
    return switch_key(seed, [640, 1350, 2250, 3300], [0.0075, 0.0055, 0.004, 0.003],
                      [0.85, 1.0, 0.5, 0.22], (900, 6000, 0.0007), (0.032, 0.048), 0.26, 0.05)


def deep_key(seed):
    """Lubed linears in a gasket-mounted board: deep and creamy."""
    return switch_key(seed, [215, 345, 830, 1500, 2600], [0.02, 0.016, 0.009, 0.006, 0.003],
                      [0.75, 1.0, 0.45, 0.22, 0.08], (600, 4500, 0.0011), (0.06, 0.085), 0.22, 0.08)


# ================================================================ heavy boards
# Modelled the way a real board makes its sound: a sharp contact (keycap on
# housing) excites a bank of RESONATORS - the keycap's hollow, the plate, the
# case cavity - each a tuned band of noise that rings and dies (more natural
# than pure tones). Under it the desk/case "thud". Then the key comes back up:
# a second, lighter contact through the same resonators.

def resonate(excitation, bank):
    out = np.zeros(len(excitation))
    for freq, q, gain in bank:
        b, a = signal.iirpeak(freq, q, fs=SR)
        out += signal.lfilter(b, a, excitation) * gain
    return out


def heavy_key(seed, bank, thud_hz, thud_decay, thud_gain, contact_lo, contact_hi, up_at, up_level, room, top):
    rng = np.random.default_rng(seed)
    p = rng.uniform(0.96, 1.04)                                 # every key slightly different
    bank = [(f * p * rng.uniform(0.98, 1.02), q, g * rng.uniform(0.85, 1.15)) for f, q, g in bank]
    x = silence(0.2)

    def stroke(level, length, tone_shift):
        hit = band(noise(length, int(rng.integers(1 << 30))), contact_lo, contact_hi) * env(length, 0.0001, length * 0.25)
        body = resonate(np.concatenate([hit, np.zeros(int(0.09 * SR))]), [(f * tone_shift, q, g) for f, q, g in bank])
        body = body / (np.max(np.abs(body)) + 1e-9)
        hit = hit / (np.max(np.abs(hit)) + 1e-9)
        click = np.concatenate([hit * 0.18, np.zeros(len(body) - len(hit))])
        return (body + click) * level

    down = stroke(1.0, 0.0025, 1.0)
    thud = sweep(thud_hz * 1.4, thud_hz, 0.08, 5) * env(0.08, 0.001, thud_decay) * thud_gain
    place(x, mix(down, thud), 0.0)
    place(x, stroke(up_level, 0.0018, 1.06), rng.uniform(*up_at))
    x = lowpass(x, top)                                        # thick PBT caps: no fizzy top
    return reverb(x, 0.1, room, seed=seed + 2, brightness=5000, predelay=0.004)


def alpaca_key(seed):
    """Alpaca-style smooth linears, lubed, in a heavy board: deep, creamy, marbly thock."""
    return heavy_key(seed, [(240, 7, 0.7), (430, 9, 1.0), (760, 10, 0.55), (1250, 12, 0.3), (2100, 14, 0.12)],
                     150, 0.014, 0.2, 600, 5000, (0.06, 0.085), 0.3, 0.06, 5500)


def black_key(seed):
    """MX Black-style heavy linears: a harder bottom-out, a deeper clack, more weight."""
    return heavy_key(seed, [(190, 6, 0.6), (360, 8, 1.0), (980, 11, 0.8), (1700, 13, 0.5), (2900, 16, 0.2)],
                     120, 0.016, 0.25, 800, 7000, (0.065, 0.09), 0.35, 0.07, 7000)


def redheavy_key(seed):
    """Red-style linears, thick caps, foamed case: poppy on top, full underneath."""
    return heavy_key(seed, [(300, 8, 0.8), (620, 10, 0.9), (1300, 12, 1.0), (2300, 15, 0.55), (3600, 18, 0.2)],
                     160, 0.012, 0.14, 1000, 8000, (0.055, 0.08), 0.38, 0.06, 8000)


# ================================================================ cinematic sets
# The cinematic direction, three more ways. Each takes the keyboard it is played with.

def heavy_shot(key, boom=1.0, crack=1.0, tail=0.3, seed=0):
    x, dry = silence(1.3), silence(1.3)
    k = key(900 + seed)
    place(x, k / np.max(np.abs(k)) * 0.75, 0.0)
    place(dry, soft_clip(sweep(125, 34, 0.55, 6) * env(0.55, 0.001, 0.11), 2.5) * 0.9 * boom, 0.004)
    place(x, mix(highpass(noise(0.03, 4 + seed), 2200) * env(0.03, 0.0002, 0.003) * 1.6 * crack,
                 lowpass(noise(0.45, 5 + seed), 1300) * env(0.45, 0.002, 0.065) * 1.5), 0.004)
    wet = reverb(x, 1.1, tail, seed=12 + seed, brightness=2800, predelay=0.03)
    return mix(wet, dry)


# --- TITAN: lower, heavier, metal. Anvil kills, a brass-like BRAAM with taiko.
def titan_kill(step):
    f = [note(n) for n in ("A3", "B3", "C#4", "E4", "F#4")][step - 1]
    x = silence(0.5)
    anvil = mix(fm(f * 2, 1.414, 4.0, 0.4, 0.03) * env(0.4, 0.0005, 0.06) * 0.35,
                bell(f, 0.4, 0.07) * 0.35)
    place(x, mix(anvil, lowpass(noise(0.03, step), 3000) * env(0.03, 0.0002, 0.003),
                 sweep(160, 60, 0.1, 5) * env(0.1, 0.001, 0.025) * 0.6), 0.0)
    return reverb(x, 0.5, 0.16, seed=210 + step, brightness=3500)


def titan_multi(level):
    big, huge = level >= 3, level >= 5
    x, dry = silence(4.2), silence(4.2)
    notes = [note(n) for n in ("A0", "A1", "E2", "A2", "C3")] if big else [note(n) for n in ("A1", "E2", "A2")]
    horn = formant_pad(notes, 3.0 if big else 1.5, (500, 900, 2300))
    horn = soft_clip(horn * 3.0, 2.0) * env(3.0 if big else 1.5, 0.03, 0.9 if big else 0.4)
    place(x, horn * 0.8, 0.0)
    place(x, braam(notes[1:], 2.6 if big else 1.3, 0.7 if big else 0.3) * 0.45, 0.0)
    for k, at in enumerate((0.0, 0.18, 0.3) if big else (0.0,)):           # taiko: dum, da-dum
        place(dry, soft_clip(sweep(95, 40, 0.6, 6) * env(0.6, 0.002, 0.16), 2.4) * (1.0 - 0.15 * k), at)
        place(x, lowpass(noise(0.3, 30 + k), 1800) * env(0.3, 0.001, 0.05) * 1.3, at)
    if huge:
        place(x, horn[: int(1.6 * SR)] * 0.6, 0.55)
        place(dry, soft_clip(sweep(80, 30, 1.0, 5) * env(1.0, 0.002, 0.4), 2.5), 0.55)
    wet = reverb(x, 3.2 if big else 1.8, 0.42, seed=230 + level, brightness=3000, predelay=0.05)
    return soft_clip(mix(wet, dry) / np.max(np.abs(mix(wet, dry))) * 1.1, 1.4)


# --- HERO: an epic lift. Kills ring like a deep bell; the multi-kill hits,
# then a swell of strings opens into a big major chord with choir on top.
def strings(notes, seconds, attack, decay):
    out = np.zeros(int(seconds * SR))
    t = t_axis(seconds)
    for i, f in enumerate(notes):
        vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * t + i)
        phase = np.cumsum(f * vib) / SR
        voice = np.zeros_like(t)
        for h in range(1, 9):
            voice += np.sin(2 * np.pi * h * phase) / h
        out += voice
    out = lowpass(out / len(notes), 3500)
    a = np.clip(t / attack, 0, 1) ** 2
    return out * a * np.exp(-np.maximum(t - attack, 0) / decay)


def hero_kill(step):
    f = [note(n) for n in ("D4", "E4", "F#4", "A4", "B4")][step - 1]
    x = silence(0.5)
    place(x, mix(bell(f, 0.45, 0.08) * 0.45, sine(f * 2, 0.3) * env(0.3, 0.001, 0.05) * 0.2,
                 lowpass(noise(0.02, step), 3500) * env(0.02, 0.0002, 0.003) * 0.8,
                 sweep(150, 70, 0.08, 4) * env(0.08, 0.001, 0.02) * 0.5), 0.0)
    return reverb(x, 0.55, 0.18, seed=250 + step, brightness=5000)


def hero_multi(level):
    big, huge = level >= 3, level >= 5
    x, dry = silence(4.5), silence(4.5)
    place(dry, soft_clip(sweep(85, 30, 1.2, 5) * env(1.2, 0.002, 0.45 if big else 0.22), 2.5), 0.0)
    place(x, lowpass(noise(0.4, 41), 2000) * env(0.4, 0.001, 0.05) * 1.5, 0.0)
    chord = [note(n) for n in ("D2", "A2", "D3", "F#3", "A3", "D4", "F#4")] if big else [note(n) for n in ("D3", "A3", "F#4")]
    place(x, strings(chord, 3.4 if big else 1.6, 0.35 if big else 0.2, 1.0 if big else 0.4) * 0.9, 0.05)
    if big:
        place(x, formant_pad([note(n) for n in ("D4", "A4", "D5", "F#5")], 3.0, (750, 1150, 2700))
              * env(3.0, 0.4, 1.0) * 0.35, 0.15)
        place(x, bell(note("D5"), 2.5, 0.8) * 0.2, 0.02)
    if huge:
        place(dry, soft_clip(sweep(100, 32, 1.0, 5) * env(1.0, 0.002, 0.35), 2.5) * 0.85, 0.6)
        place(x, strings([f * 2 for f in chord[2:]], 2.4, 0.15, 0.8) * 0.6, 0.6)
    wet = reverb(x, 3.4 if big else 1.8, 0.45, seed=270 + level, brightness=4500, predelay=0.04)
    return soft_clip(mix(wet, dry) / np.max(np.abs(mix(wet, dry))) * 1.1, 1.35)


# --- PULSE: dark and modern. The hit is sucked in by a reversed swell, then
# slams, then pulses: distorted low hits like a heartbeat, fading out.
def pulse_kill(step):
    f = [note(n) for n in ("E3", "F#3", "G#3", "B3", "C#4")][step - 1]
    x = silence(0.45)
    tom = sweep(f * 2.2, f, 0.25, 5) * env(0.25, 0.001, 0.05)
    place(x, mix(soft_clip(tom * 1.5, 2) * 0.7, highpass(noise(0.02, step), 3000) * env(0.02, 0.0001, 0.002) * 0.6,
                 sine(f * 4, 0.25) * env(0.25, 0.001, 0.04) * 0.2), 0.0)
    return reverb(x, 0.45, 0.16, seed=290 + step, brightness=4000)


def pulse_multi(level):
    big, huge = level >= 3, level >= 5
    pre = 0.32 if big else 0.0
    x, dry = silence(4.2), silence(4.2)
    if big:
        hit = mix(lowpass(noise(0.6, 50), 3000) * env(0.6, 0.001, 0.12), braam([note("E1"), note("E2"), note("B2")], 0.8, 0.3))
        swell = reverse_swell(hit * 0.8)[-int(pre * SR):]
        place(x, swell * 0.9, 0.0)
    place(dry, soft_clip(sweep(90, 28, 1.3, 5) * env(1.3, 0.002, 0.5 if big else 0.25), 2.8), pre)
    place(x, braam([note(n) for n in ("E1", "E2", "B2", "E3")], 2.4 if big else 1.2, 0.6 if big else 0.3) * 0.7, pre)
    beats = 4 if big else 2
    for k in range(beats):                                              # the heartbeat pulses
        at = pre + 0.45 + k * 0.42
        place(dry, soft_clip(sweep(70, 35, 0.35, 5) * env(0.35, 0.002, 0.09), 3.0) * 0.7 * (0.8 ** k), at)
    for i, n in enumerate(("E5", "B5", "E6")[: 3 if big else 1]):
        place(x, glass(note(n), 1.2, 0.4) * 0.12, pre + 0.05 + i * 0.07)
    if huge:
        place(dry, soft_clip(sweep(110, 30, 1.0, 5) * env(1.0, 0.002, 0.35), 2.8) * 0.9, pre + 0.3)
    wet = reverb(x, 3.0 if big else 1.6, 0.4, seed=310 + level, brightness=3500, predelay=0.03)
    return soft_clip(mix(wet, dry) / np.max(np.abs(mix(wet, dry))) * 1.1, 1.4)


def cine(kill_fn, multi_fn, key, seed):
    return dict(key=key, shot=lambda: heavy_shot(key, seed=seed), kill=kill_fn, multi=multi_fn)


SETS = {
    "thock": thock_set(),
    "tactical": dict(key=tactical_key, shot=tactical_shot, kill=tactical_kill, multi=tactical_multi),
    "arcade": dict(key=arcade_key, shot=arcade_shot, kill=arcade_kill, multi=arcade_multi),
    "cinematic": dict(key=cinematic_key, shot=cinematic_shot, kill=cinematic_kill, multi=cinematic_multi),
    "cyber": dict(key=cyber_key, shot=cyber_shot, kill=cyber_kill, multi=cyber_multi),
    "crystal": dict(key=crystal_key, shot=crystal_shot, kill=crystal_kill, multi=crystal_multi),
    # the cinematic direction, with the premium keyboards
    "cinematic_red": dict(key=red_key, shot=lambda: heavy_shot(red_key), kill=cinematic_kill, multi=cinematic_multi),
    "titan_red": cine(titan_kill, titan_multi, red_key, 1),
    "hero_deep": cine(hero_kill, hero_multi, deep_key, 2),
    "pulse_flow": cine(pulse_kill, pulse_multi, flow_key, 3),
}
KEYBOARDS = {"red": red_key, "flow": flow_key, "deep": deep_key,
             "alpaca": alpaca_key, "black": black_key, "redheavy": redheavy_key}
for _name in ("alpaca", "black", "redheavy"):
    SETS["cinematic_" + _name] = dict(key=KEYBOARDS[_name], shot=(lambda k=KEYBOARDS[_name]: heavy_shot(k)),
                                      kill=cinematic_kill, multi=cinematic_multi)
ACTIVE_SET = "cinematic_alpaca"   # the set the game uses


def build(name, folder=None):
    global OUT
    previous = OUT
    if folder:
        OUT = folder
    kit = SETS[name]
    for i in range(6):
        save("key_%d" % i, kit["key"](10 + i * 7))
    save("shot", kit["shot"]())
    for step in range(1, 6):
        save("kill_%d" % step, kit["kill"](step))
    for level in (2, 3, 5):
        save("multi_%d" % level, kit["multi"](level))
    OUT = previous


def preview(name, folder):
    """One file: three words typed and killed (the kill climbs), then a blast of three, then one of five."""
    kit = SETS[name]
    rng = np.random.default_rng(3)
    out = np.zeros(SR * 14)
    keys = [kit["key"](10 + i * 7) for i in range(6)]
    t, last = 0.4, -1
    for word, step in (("zombie", 1), ("energy", 2), ("runner", 3)):
        for i in range(len(word)):
            if i == len(word) - 1:
                place(out, kit["shot"]() * 0.30, t)
                place(out, kit["kill"](step) * 0.24, t + 0.12)
            else:
                k = int(rng.choice([j for j in range(6) if j != last]))
                last = k
                place(out, keys[k] / np.max(np.abs(keys[k])) * 0.22, t)
            t += rng.uniform(0.09, 0.14)
        t += 0.55
    for level in (3, 5):
        for i in range(6):
            if i == 5:
                place(out, kit["shot"]() * 0.30, t)
                place(out, kit["multi"](level) * 0.55, t + 0.1)
            else:
                place(out, keys[i] / np.max(np.abs(keys[i])) * 0.22, t)
            t += 0.11
        t += 3.0
    out = trim(out)
    out = out / np.max(np.abs(out)) * 0.9
    path = os.path.join(folder, "preview_%s.wav" % name)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((out * 32767).astype(np.int16).tobytes())
    print("preview", path)


def keyboard_preview(name, key, folder):
    """A sentence typed at a steady 90 WPM, then a fast burst: just the keyboard."""
    rng = np.random.default_rng(1)
    takes = [key(10 + i * 7) for i in range(6)]
    takes = [k / np.max(np.abs(k)) for k in takes]
    out = np.zeros(SR * 9)
    t, last = 0.3, -1
    text = "the quick zombie jumps over the lazy barrel"
    for ch in text:
        if ch == " ":
            t += 0.07
        k = int(rng.choice([j for j in range(6) if j != last]))
        last = k
        place(out, takes[k] * rng.uniform(0.75, 1.0), t)
        t += rng.uniform(0.09, 0.15)
    t += 0.5
    for _ in range(14):
        k = int(rng.choice([j for j in range(6) if j != last]))
        last = k
        place(out, takes[k] * rng.uniform(0.8, 1.0), t)
        t += rng.uniform(0.055, 0.08)
    out = trim(out) / np.max(np.abs(out)) * 0.9
    path = os.path.join(folder, "keys_%s.wav" % name)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((out * 32767).astype(np.int16).tobytes())
    print("keys", path)


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 2 and sys.argv[1] == "--preview":
        for name in (sys.argv[3:] or SETS):
            preview(name, sys.argv[2])
    elif len(sys.argv) > 2 and sys.argv[1] == "--keys":
        for name, key in KEYBOARDS.items():
            if len(sys.argv) == 3 or name in sys.argv[3:]:
                keyboard_preview(name, key, sys.argv[2])
    else:
        build(sys.argv[1] if len(sys.argv) > 1 else ACTIVE_SET)
