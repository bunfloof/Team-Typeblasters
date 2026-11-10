// What the game logs: every analytics event and every field, in plain words.
// Safe to import anywhere (no server code). The game side is
// Assets/Scripts/Analytics*.cs in the Game Jam repo; the log server stores
// each event as one row of the `events` table (common fields as columns,
// the rest in the JSON column `data`).

export type FieldDoc = Record<string, string>;

export type EventDoc = {
  title: string;
  group: "session" | "run" | "level" | "typing" | "zombies" | "combat" | "powers" | "boss" | "ui";
  when: string;
  fields: FieldDoc;
  playerState?: boolean; // also carries PLAYER_STATE_FIELDS
};

// Columns of the `events` table (also inside `data`, except id/request_id/received_at).
export const EVENT_COLUMNS: FieldDoc = {
  id: "Row number in the events table.",
  request_id: "The HTTP request (batch) it arrived in: join to requests.id for IP, browser, headers.",
  position_in_batch: "Its position inside that batch.",
  received_at: "When the server received it (UTC). The top bar's time range filters on it (e.g. only the class playtest).",
  event: "Event type (see the catalog).",
  event_id: "session_id + '-' + seq. Unique per event; a retried batch stores the same event_id again, so dedupe by it.",
  batch_id: "The batch it was sent in (a new id per send).",
  install_id: "Random id kept in the browser (like a cookie). 'qa-...' = the team's test plays (Editor, development build, or ?qa=1).",
  session_id: "New for every page load.",
  run_id: "New for every press of Start. Empty on the Start panel.",
  seq: "1, 2, 3... per session, in order. Gaps after dedupe = lost batches.",
  build: "Game build: version + Unity build GUID ('editor' in the Editor).",
  level: "Scene name: Normal, Hard or Endless (older builds: Prototype = Normal).",
  wave: "Wave number (0 before the first ride; the last one is the boss).",
  t_play: "Seconds of play in this run when it happened (pauses and tip boxes not counted). The timeline x-axis.",
  t_real: "Seconds since the page was loaded (real time, pauses included).",
};

// Fields every event carries inside `data` (besides the columns above).
export const ENVELOPE_FIELDS: FieldDoc = {
  state: "Game state: Start, Playing, Paused, Tip, Won, Lost.",
  phase: "ride (on the rails), fight (a zombie wave), boss, or none.",
};

// "Player state" fields added to many events (as they were at that moment).
export const PLAYER_STATE_FIELDS: FieldDoc = {
  health: "Player health (0-100). On a damage event: BEFORE the hit.",
  combo: "Combo (kills in a row without a mistake; multiplies the points. Older builds: also bought weapons).",
  skill_energy: "Skill energy (0-20): every power costs it (lure 6, freeze 8, FRENZY 12, RPG 18); +1 per kill, energy crates. Missing = an older build.",
  score: "Score so far.",
  lure_charges: "Free lure uses kept (a wave reward; older builds: charges from crates) (key 1).",
  freeze_charges: "Free freezes kept (a wave reward; older builds: charges from crates) (key 2).",
  frenzy_kept: "Free FRENZY uses kept (a wave reward; older builds: bought and kept) (key 3).",
  rpg_kept: "Free RPG uses kept (a wave reward; older builds: bought and kept) (key 4).",
  rpg_loaded: "An RPG is loaded: the next finished word fires a rocket.",
  can_use_frenzy: "Key 3 would start a FRENZY right now (enough energy or a free use, none running).",
  can_use_rpg: "Key 4 would load an RPG right now (enough energy or a free use, none loaded).",
  can_buy_frenzy: "Older builds: a full hold of 3 would buy a FRENZY right now (always false since the energy update).",
  can_buy_rpg: "Older builds: a full hold of 4 would buy an RPG right now (always false since the energy update).",
  frenzy_on: "A FRENZY is running (every enemy word is short).",
  freeze_on: "A freeze is running.",
  shield_on: "The shield is up (Tab): no damage, enemies slowed.",
  shield_charges: "Shields left this wave (one per wave).",
  shield_word_open: "Older builds: the shield's word was showing.",
  aiming_lure: "Holding 1 to aim a lure bomb.",
  zombies_alive: "Zombies on the field (on a bite: not counting the biter).",
  typing_target: "What the player is typing: zombie_<kind>, barrel, crate_<kind>, orb, boss_head/arm/leg, quiz_answer, or none.",
  typing_word_length: "Length of the word being typed.",
  typing_letters_done: "Letters of it typed so far (a waiting word picked up again counts its earlier letters; older builds restarted from 0).",
  player_x: "Player position, metres east (map x).",
  player_z: "Player position, metres north (map z).",
};

export const ZOMBIE_KINDS = ["normal", "runner", "armored", "explosive", "shifter"] as const;
export type ZombieKind = (typeof ZOMBIE_KINDS)[number];

export const EVENTS: Record<string, EventDoc> = {
  session_start: {
    title: "Page loaded",
    group: "session",
    when: "Once per page load (session).",
    fields: {
      install_new: "First time this browser ran the game.",
      storage_ok: "The browser kept the install id (false = private mode or blocked storage).",
      runs_before: "Runs started in this browser before this page load.",
      test_play: "Editor, development build or ?qa=1 (install_id starts with qa-).",
      page_url: "The address the game was opened from.",
      platform: "Unity platform (WebGLPlayer in the browser).",
      unity_version: "Unity version.",
      app_version: "Game version.",
      os: "Operating system and browser as Unity reports them.",
      device_model: "Device model as Unity reports it.",
      gpu: "Graphics card (WebGL renderer string).",
      cpu_count: "Logical CPU cores.",
      memory_mb: "System memory (MB).",
      screen_width: "Canvas width (px).",
      screen_height: "Canvas height (px).",
      screen_dpi: "Screen DPI.",
      language: "System language.",
    },
  },
  run_start: {
    title: "Run started",
    group: "run",
    when: "Start pressed.",
    fields: {
      run_number: "Runs ever started in this browser, this one included (1 = first try).",
      runs_this_session: "Runs since the page was loaded.",
      tips_enabled: "'Show tips' was on.",
      hard_level: "Playing the Hard level.",
      from_checkpoint_wave: "More than 0: Restart after a death went on from the checkpoint at this wave (the Insights funnel leaves such runs out). 0 = from the start.",
      dda_on: "Adaptive difficulty is on (Settings).",
      dda_calibrated: "Adaptive difficulty has measured the player's typing speed.",
      dda_wpm: "Typing speed it measured (WPM).",
      dda_level: "How much it adjusts the pressure (0 = the level's own tuning).",
    },
  },
  run_end: {
    title: "Run ended",
    group: "run",
    when: "Won, died, or left mid-run (Restart, or MENU from the pause panel). A closed tab has no run_end (see page_hidden).",
    playerState: true,
    fields: {
      outcome: "won, lost or restart (left mid-run: see next_scene).",
      next_scene: "restart only: where the player went. The level's own name = Restart; Menu = back to the main menu. Missing in older builds (both were 'restart').",
      run_number: "As in run_start.",
      kills: "Kills this run (boss words, orbs and quiz answers count too).",
      best_combo: "Highest combo.",
      biggest_multi_kill: "Most kills from one blast.",
      correct_keys: "Correct keys typed.",
      wrong_keys: "Wrong keys typed.",
      accuracy: "correct / (correct + wrong) x 100.",
      words_per_minute: "(correct keys / 5) per minute of play, rides included (so lower than typing-test WPM).",
    },
  },
  ride_start: { title: "Ride started", group: "level", when: "The player starts riding to the next fight.", fields: { boss_next: "The next fight is the boss." } },
  ride_end: {
    title: "Ride ended",
    group: "level",
    when: "The player arrives at the fight.",
    playerState: true,
    fields: {
      seconds: "Ride duration (play seconds).",
      ambushers_placed: "Ambush zombies hidden along the road.",
      ambushers_woken: "...of which the player saw (they stepped out).",
      health_at_start: "Health when the ride began.",
    },
  },
  wave_start: {
    title: "Fight started",
    group: "level",
    when: "After the Wave N banner (zombie wave) or when the boss appears.",
    playerState: true,
    fields: {
      boss_fight: "This fight is the boss.",
      guided: "Normal's guided first wave (the game teaches one thing at a time; no planned_ fields). Older builds logged no wave_start for it (the Insights funnel counts the end of the ride to it instead).",
      zombies_planned: "Zombies this wave will send.",
      planned_normal: "...normal ones (Hard: some become shifters at spawn).",
      planned_runner: "...runners.",
      planned_armored: "...armored.",
      planned_explosive: "...red (explosive).",
      planned_shifter: "...shifters.",
      chain_pairs: "WORD CHAIN pairs (hunt + hunter).",
      zombie_speed: "Zombie speed (m/s).",
      spawn_interval: "Seconds between packs.",
      group_size: "Zombies per pack.",
      map_view: "Top-down map camera in this fight.",
      barrels: "Barrels with a word in this area.",
      crates: "Supply crates in this area.",
      boss_health: "Boss health at the start (boss fight).",
      dda_on: "Adaptive difficulty is on.",
      dda_wpm: "Typing speed it measured (WPM).",
      dda_level: "How much it adjusts the pressure.",
      dda_pace_factor: "Multiplier on zombie_speed from adaptive difficulty.",
      dda_spawn_factor: "Multiplier on spawn_interval from adaptive difficulty.",
      dda_pack_bonus: "Zombies added to group_size by adaptive difficulty.",
    },
  },
  wave_reward: {
    title: "Wave reward picked",
    group: "level",
    when: "Endless: after a wave, the player picks a reward.",
    playerState: true,
    fields: {
      wave_number: "The wave just cleared.",
      after_boss: "The wave was a boss fight (a bigger reward).",
      choice: "health, energy, free_weapon or skip (skip gives score instead).",
      health_gained: "Health it gave.",
      energy_gained: "Skill energy it gave (capped at 20).",
      score_gained: "Score it gave.",
      weapon: "free_weapon: frenzy or rpg (a free use, see power action reward).",
    },
  },
  wave_end: {
    title: "Fight won",
    group: "level",
    when: "Every zombie of the wave is gone (or the boss is dead). A death ends the run instead.",
    playerState: true,
    fields: {
      boss_fight: "This was the boss.",
      seconds: "Fight duration (play seconds).",
      kills: "Kills during the fight.",
      correct_keys: "Correct keys during the fight.",
      wrong_keys: "Wrong keys during the fight.",
      health_at_start: "Health when the fight began.",
    },
  },
  zombie_spawn: {
    title: "Zombie appeared",
    group: "zombies",
    when: "A zombie becomes active (an ambusher: when the player first sees it).",
    fields: {
      zombie_id: "Number of this zombie within the run (joins zombie, damage, word, wrong_key, blast).",
      kind: "normal, runner, armored, explosive (red) or shifter.",
      word: "Its word.",
      word_length: "Letters in the word.",
      ambusher: "Met on a ride between fights.",
      chain_linked: "Half of a WORD CHAIN pair.",
      frenzy_word: "Born during a FRENZY (short word).",
      blast_radius: "Red zombies: ring radius in metres (0.75 x word length).",
      x: "Map position x (m).",
      z: "Map position z (m).",
      distance_to_player: "Metres from the player.",
    },
  },
  zombie: {
    title: "Zombie gone",
    group: "zombies",
    when: "A zombie leaves the game, or the run ends while it is alive.",
    fields: {
      zombie_id: "As in zombie_spawn.",
      outcome: "shot, blast, bite (reached the player), rode_past, never_seen (ambusher never woken), lost, boss_died, run_over (alive when the run ended).",
      kind: "Zombie kind.",
      ambusher: "Met on a ride.",
      chain_linked: "Half of a WORD CHAIN pair.",
      word: "Its word at the end (armored: the second word; shifter: the last).",
      word_length: "Letters.",
      frenzy_word: "Carried a FRENZY word at the end.",
      letters_typed: "Letters of its word typed at the end.",
      was_targeted: "The player typed at least one letter of it.",
      typing_starts: "Times the player started typing it (2+ = dropped and restarted, or an armored zombie's second word).",
      seconds_alive: "Play seconds since it appeared (ambushers: since placed).",
      seconds_since_wake: "Ambushers: seconds since the player saw it (-1 = never).",
      word_shifts: "Shifters: times its word changed.",
      blast_radius: "Red zombies: ring radius (m).",
      in_ring_at_first_letter: "Red: other zombies inside the ring when its word was first started.",
      in_ring_at_last_start: "Red: ...when its word was started the last time.",
      in_ring_max_while_typed: "Red: most zombies in the ring while being typed.",
      in_ring_max: "Red: most zombies in the ring during its life.",
      x: "Map position x (m) at the end.",
      z: "Map position z (m) at the end.",
      distance_to_player: "Metres from the player at the end.",
    },
  },
  damage: {
    title: "Player hurt",
    group: "combat",
    when: "A zombie bite or a projectile hit: a boss orb, a spitter glob or a sniper bullet (player state = BEFORE the hit).",
    playerState: true,
    fields: {
      source: "zombie_<kind> or orb (any projectile: see projectile_kind).",
      projectile_kind: "orb hits: boss, spitter or sniper.",
      ambusher: "The biter was met on a ride (costs 1 combo, not all).",
      attacker_id: "zombie_id of the biter.",
      attacker_word: "The attacker's word.",
      attacker_word_length: "Its length.",
      attacker_letters_typed: "Letters of it typed (more than 0 = the player was typing this very attacker).",
      attacker_seconds_alive: "Seconds the attacker was alive.",
      attacker_seconds_since_wake: "Ambusher: seconds since the player saw it.",
      amount: "Damage (10 bite, 15 orb).",
      health_after: "Health after the hit.",
      combo_after: "Combo after the hit.",
      fatal: "This hit killed the player.",
    },
  },
  word: {
    title: "Word typed",
    group: "typing",
    when: "A typing attempt ends: from the first letter of a word to finished / dropped / target gone / run over.",
    fields: {
      outcome: "finished, dropped (Backspace), dropped_by_frenzy, target_gone (blown up, bit, ...) or run_ended.",
      target: "What was typed: zombie_<kind>, barrel, crate_<kind>, orb, boss_head/arm/leg, quiz_answer.",
      target_id: "zombie_id if a zombie was typed (else -1).",
      word: "The word when its first letter was typed.",
      word_length: "Letters.",
      word_style: "plain (case-insensitive), or for case-sensitive words: lower, title, mixed, symbols, or phrase (a brute's words with spaces; older builds logged phrases as symbols).",
      case_sensitive: "Every character had to match exactly (Hard level, orbs).",
      candidates: "Targets the first letter matched (2+ = a word family like hunt/hunter).",
      words_finished: "Words finished in this attempt (2+ = a WORD CHAIN).",
      correct_keys: "Correct keys in this attempt.",
      wrong_keys: "Wrong keys during it.",
      rockets: "RPG rockets fired by it.",
      typing_seconds: "From the first to the last correct key (n keys = n-1 gaps).",
      start_t_play: "t_play of the first key (the bar starts here on the timeline).",
      key_ms: "Milliseconds of every correct key after the first, comma separated ('0,180,320,...').",
      frenzy_on: "A FRENZY was on when it started.",
    },
  },
  wrong_key: {
    title: "Wrong key",
    group: "typing",
    when: "Every wrong key.",
    fields: {
      typed: "The key typed.",
      expected: "The key that was needed (if any).",
      stray: "No word was being typed and the key started none.",
      case_only: "Right letter, wrong case (Hard level / orbs).",
      target: "What was being typed (or what a stray key would have started ignoring case).",
      target_id: "zombie_id of that target (else -1).",
      letter_index: "Letters already typed (0 = the first letter). A resumed waiting word counts its earlier letters (older builds restarted from 0).",
      word_length: "Length of the word being typed.",
    },
  },
  blast: {
    title: "Explosion",
    group: "combat",
    when: "A chain reaction is over (its kills counted).",
    playerState: true,
    fields: {
      source: "red (a red zombie's word), barrel (shot), lure (lure bomb) or rpg (rocket).",
      zombie_id: "Red: the zombie that blew up.",
      word_length: "Red: its word length.",
      frenzy_word: "Red: it carried a FRENZY word.",
      blast_radius: "Red: ring radius (m).",
      in_ring_now: "Red: other zombies inside the ring when it blew up.",
      in_ring_at_last_start: "Red: other zombies in the ring when the player started the word that blew it up.",
      in_ring_at_first_letter: "Red: ...when its word was first started.",
      in_ring_max_while_typed: "Red: most in the ring while typed.",
      in_ring_max: "Red: most in the ring during its life.",
      typing_starts: "Red: times its word was started.",
      typing_seconds: "Red: from the last start to the blast.",
      distance_to_player: "Red: metres from the player.",
      lured_now: "Lure: zombies walking to it when it blew.",
      target: "RPG: what the rocket was fired at.",
      kills: "Zombies killed by the whole chain (a red zombie counts itself).",
      kills_normal: "...normal.",
      kills_runner: "...runners.",
      kills_armored: "...armored.",
      kills_explosive: "...red (chained, plus the source).",
      kills_shifter: "...shifters.",
      explosions: "Explosions in the chain.",
      barrels: "Barrels that went off in it.",
      boss_damage: "Damage dealt to the boss.",
      scored: "Its kills were scored (false after a death).",
      points: "Points it gave.",
      x: "Where it started, map x (m).",
      z: "Where it started, map z (m).",
    },
  },
  power: {
    title: "Power / weapon",
    group: "powers",
    when: "Anything done with a lure, freeze, FRENZY, RPG or the shield (player state = AFTER it).",
    playerState: true,
    fields: {
      action:
        "throw (lure), use, use_refused, aim_cancelled, fire (RPG rocket), frenzy_end, reward (a free use from a wave reward); shield: use, block, end, empty. Older builds also: buy, buy_refused, tap_empty, hold_released.",
      item: "lure, freeze, frenzy, rpg or shield.",
      reason:
        "use_refused: not_enough_energy, already_active (also a second freeze while frozen), not_enough_energy_or_not_playing (lure, when the key came up). Older builds: buy_refused slots_full / one_per_wave / not_enough_combo; use_refused none_kept / already_loaded / frenzy_on.",
      energy_before: "throw / use: skill energy before.",
      energy_spent: "throw / use: energy it cost (0 = a free use).",
      energy_after: "throw / use: energy after.",
      free_reward: "throw / use: a free use kept from a wave reward (no energy spent).",
      could_buy: "Older builds, tap_empty / hold_released: a full hold would have bought it.",
      held_seconds: "Older builds, hold_released: how long the key was held (0.6 buys).",
      combo_before: "Older builds, buy: combo before paying.",
      via: "use / use_refused: key or click (a phone's button); reward: wave_reward.",
      aim_seconds: "throw: seconds aiming (under 0.3 = a quick tap).",
      turned_degrees: "throw: steered left/right.",
      distance: "throw: metres wanted.",
      while_frozen: "Older builds only (Oct 12 to Nov 2): freeze use while a freeze was already on. Now a press during a freeze is use_refused already_active.",
      kills_during: "frenzy_end: kills during the FRENZY.",
    },
  },
  crate: {
    title: "Supply crate opened",
    group: "powers",
    when: "Its word was typed.",
    playerState: true,
    fields: {
      kind: "health or energy (older builds: health, lure or freeze).",
      word: "Its word.",
      health_before: "Health before (health crates heal 30, capped at 100).",
      energy_gained: "Energy crates: skill energy added (0 = energy was already full).",
      power_gained: "Older builds, lure/freeze: a charge was added (false = already full).",
    },
  },
  prop: {
    title: "Barrel / crate armed",
    group: "level",
    when: "A fight starts: each barrel and crate of the area gets a word.",
    fields: {
      kind: "barrel, crate_health or crate_energy (older builds: crate_lure, crate_freeze).",
      word: "Its word.",
      blast_radius: "Barrel: blast radius (m).",
      x: "Map x (m).",
      z: "Map z (m).",
    },
  },
  sample: {
    title: "Heartbeat",
    group: "run",
    when: "Every second of play (pauses excluded): where the player is and how things stand.",
    playerState: true,
    fields: {
      yaw: "Direction the player faces (degrees).",
      nearest_zombie_m: "Metres to the nearest zombie (-1 = none).",
      nearest_zombie_id: "Its zombie_id.",
      kills: "Kills so far.",
      correct_keys: "Correct keys so far.",
      wrong_keys: "Wrong keys so far.",
      fps: "Frames per second over the last sample.",
    },
  },
  guided_intro: {
    title: "Guided intro step",
    group: "ui",
    when: "Normal's guided first wave: it starts, is skipped, a lesson is skipped, or it is complete.",
    fields: {
      step: "start, skipped, guided_wave, lesson_skipped:<lesson>, complete. (Older builds sent it as 'phase', which the game's own phase overwrote, so it was lost.)",
    },
  },
  tutorial_step: {
    title: "Tutorial step (older builds)",
    group: "ui",
    when: "The removed Easy level's tutorial moved on.",
    fields: { step: "Lesson number.", phase: "Its phase." },
  },
  dda_calibrated: {
    title: "Typing speed measured",
    group: "run",
    when: "Adaptive difficulty finished measuring the player (about 10 s of active typing).",
    fields: {
      dda_wpm: "Typing speed (WPM).",
      dda_accuracy: "Accuracy it measured.",
      dda_target_level: "Adjustment it aims for.",
      dda_level: "Adjustment now.",
      active_seconds: "Seconds of active typing measured.",
      characters: "Characters measured.",
      on: "Adaptive difficulty is on.",
    },
  },
  dda_applied: {
    title: "Difficulty adjusted",
    group: "run",
    when: "Wave 1 is over: adaptive difficulty applies what it measured.",
    fields: { dda_wpm: "Typing speed (WPM).", dda_level: "Adjustment applied.", characters: "Characters measured.", on: "Adaptive difficulty is on." },
  },
  dda_toggle: {
    title: "Adaptive difficulty switched",
    group: "ui",
    when: "Switched in Settings.",
    fields: { on: "On.", dda_wpm: "Typing speed measured so far." },
  },
  tip: { title: "Tip box closed", group: "ui", when: "A first-time tip box is closed.", fields: { key: "Which tip.", seconds_open: "Seconds it was open (real time)." } },
  hint: { title: "Hint shown", group: "ui", when: "A one-line tutorial hint appears.", fields: { key: "Which hint." } },
  tips_toggle: { title: "Tips switched", group: "ui", when: "The 'Show tips' toggle is clicked.", fields: { on: "Tips on." } },
  pause: {
    title: "Paused",
    group: "ui",
    when: "Esc, or the browser tab was hidden.",
    playerState: true,
    fields: { reason: "esc_key, tab_hidden or button.", during_countdown: "Paused again during the 3-2-1 countdown." },
  },
  resume: { title: "Resumed", group: "ui", when: "The 3-2-1 countdown finished.", fields: { paused_seconds: "Seconds paused (countdown included, real time)." } },
  page_hidden: { title: "Tab hidden", group: "ui", when: "The player switched tabs, minimised, or closed the tab.", playerState: true, fields: {} },
  page_visible: { title: "Tab visible", group: "ui", when: "The player came back to the tab.", fields: { hidden_seconds: "Seconds away." } },
  boss_attack: {
    title: "Boss attack",
    group: "boss",
    when: "The boss starts an attack.",
    playerState: true,
    fields: { attack: "throw (one orb), volley (3 orbs, when angry) or stomp (summons 2 zombies).", angry: "Below half health.", boss_health: "Boss health." },
  },
  boss_hit: {
    title: "Boss hurt",
    group: "boss",
    when: "The boss takes damage.",
    fields: { cause: "head_word, limb_word, limb_off, blast or quiz_right.", damage: "Damage.", boss_health: "Health after (0 = killed).", angry: "Angry before the hit." },
  },
  boss_heal: { title: "Boss healed", group: "boss", when: "A wrong quiz answer.", fields: { amount: "Health restored.", boss_health: "Health after." } },
  boss_word: { title: "Boss word", group: "boss", when: "A boss body part gets a word (start, regrowth, after a hit).", fields: { part: "head, arm or leg.", word: "The word." } },
  quiz: {
    title: "Boss quiz",
    group: "boss",
    when: "A quiz question ends.",
    playerState: true,
    fields: {
      question_index: "Question number in QuizBank.",
      question: "Question text.",
      answers: "Answers in screen order (left | middle | right).",
      correct_position: "Position of the right answer (0 = left).",
      chosen_position: "Position chosen (-1 = too slow).",
      result: "right, wrong or timeout.",
      seconds: "Seconds to answer (timer stops while frozen).",
      boss_health: "Boss health at the verdict.",
    },
  },
  orb: {
    title: "Projectile gone",
    group: "boss",
    when: "A boss orb, spitter glob or sniper bullet is shot, hits the player, is blocked by the shield, or fizzles when the boss dies.",
    fields: {
      outcome: "shot, hit_player, shield_blocked or boss_died.",
      projectile_kind: "Who fired it: boss, spitter or sniper (missing in older builds).",
      word: "Its case-sensitive word.",
      word_length: "Letters.",
      letters_typed: "Letters typed.",
      seconds_alive: "Seconds since it was thrown.",
    },
  },
};

// Every field an event of this type can have in `data` (with its doc).
export function fieldsOf(eventType: string): FieldDoc {
  const doc = EVENTS[eventType];
  if (!doc) {
    return {};
  }
  return doc.playerState ? { ...doc.fields, ...PLAYER_STATE_FIELDS } : { ...doc.fields };
}

// The doc for a field name, wherever it appears.
export function describeField(name: string, eventType?: string): string | undefined {
  if (eventType && EVENTS[eventType]?.fields[name]) {
    return EVENTS[eventType].fields[name];
  }
  return EVENT_COLUMNS[name] || ENVELOPE_FIELDS[name] || PLAYER_STATE_FIELDS[name];
}

export const LEVEL_NAMES: Record<string, string> = {
  Prototype: "Normal",
  Normal: "Normal",
  Hard: "Hard",
  Endless: "Endless",
};

export function levelName(scene: string | null | undefined): string {
  if (!scene) {
    return "-";
  }
  return LEVEL_NAMES[scene] || scene;
}
