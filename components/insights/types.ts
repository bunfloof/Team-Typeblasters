// Response shape of GET /api/insights (computed in lib/insightsServer.ts).
// Safe to import anywhere (types only).

// URL / API filter on run_start's run_number: "" (all runs), "first" (= 1,
// the player's first try in this browser) or "repeat" (> 1).
export type RunNumberScope = "" | "first" | "repeat";

// The four powers (keys 1-4).
export type PowerItem = "lure" | "freeze" | "frenzy" | "rpg";

// One step of a progress funnel (ride 1, wave 1, ride 2 ... boss, won).
export type FunnelStep = {
  level: string; // scene name
  level_name: string; // Normal / Hard
  order: number;
  step: string;
  runs: number; // runs that reached this step
  share: number; // runs / all runs of that level (0..1)
  level_runs: number; // all runs of that level
  lost_since_previous: number | null; // runs that stopped between the previous step and this one
  resumed_runs: number; // runs of that level that went on from a checkpoint: left out of the funnel
};

export type InsightsResponse = {
  scope: {
    runNumber: RunNumberScope;
    build: string;
    builds: string[]; // every build in the data (for the build picker)
    levels: string[]; // levels present in the selected runs
    runs: number;
    players: number; // distinct install_id
    runsWithoutNumber: number; // runs whose run_start / run_end was not received (left out of first / repeat)
  };

  // Q1: where and why do players die?
  deaths: {
    tiles: {
      runs: number;
      deaths: number;
      deathRate: number | null; // deaths / runs
      medianWave: number | null; // highest wave reached, median over runs
      typingAttacker: number | null; // share of fatal hits where the player had typed letters of the attacker
      typingAttackerN: number; // fatal hits that carry attacker_letters_typed
    };
    funnel: FunnelStep[];
    byStage: { stage: string; order: number; killer: string; deaths: number }[];
    typingAtDeath: { target: string; deaths: number; typing_attacker: number }[];
    healthPoints: { run_id: string; fight: string; order: number; result: "cleared" | "died"; health: number }[];
    healthSummary: { fight: string; order: number; result: "cleared" | "died"; runs: number; mean: number; median: number }[];
    damageSources: { source: string; hits: number; damage: number; fatal: number }[];
    deathPositions: { run_id: string; level: string; x: number; z: number; killer: string; stage: string }[];
  };

  // Q2: do players use the red-zombie blast ring well?
  red: {
    redZombies: number;
    outcomes: { outcome: string; zombies: number; share: number }[];
    blasts: {
      run_id: string;
      at_start: number; // zombies in the ring when the word that blew it up was started
      at_blast: number; // zombies in the ring when it blew up
      kills: number;
      run_number: number | null;
      typing_seconds: number | null;
    }[];
    redBlasts: number; // every red blast (also those without ring counts)
    killsPerRedBlast: number[]; // one value per red blast
    byRunNumber: { run_number: string; blasts: number; mean_kills: number; ci95: number | null }[];
    bySource: { source: string; blasts: number; mean_kills: number; total_kills: number; ci95: number | null }[];
  };

  // Q3: unused powers and the power funnels
  powers: {
    deaths: number; // fatal hits with player state
    energyRuns: number; // runs of current builds (every power costs skill energy)
    legacyRuns: number; // runs of older builds (crate charges, FRENZY / RPG bought with combo)
    unusedAtDeath: { item: string; deaths: number; share: number }[];
    // Current builds, per power: could afford -> pressed its key -> used -> got kills (share of energyRuns).
    powerFunnel: { item: PowerItem; order: number; step: string; runs: number; share: number }[];
    energyUse: {
      item: PowerItem;
      uses: number;
      free_uses: number; // free uses from a wave reward
      energy_spent: number;
      runs_used: number;
      paid_per_run: number;
      free_per_run: number;
    }[];
    energyIn: { crates: number; crate_energy: number; crates_when_full: number; rewards: number; reward_energy: number };
    // Older builds:
    gainedUsed: {
      item: "lure" | "freeze";
      runs: number;
      gained: number;
      used: number;
      gained_per_run: number;
      used_per_run: number;
      runs_gained: number;
      runs_used: number;
      crates_when_full: number; // crate opened while the charges were already full
    }[];
    weaponFunnel: { weapon: "frenzy" | "rpg"; order: number; step: string; runs: number; share: number }[];
    refusals: { item: string; reason: string; count: number }[];
    gainToUse: { item: string; run_id: string; seconds: number | null }[]; // null = gained, never used
  };

  typing: {
    runs: { run_id: string; run_number: number | null; wpm: number | null; accuracy: number | null }[];
    attempts: { outcome: string; count: number }[];
    speedByLength: { word_length: number; case: "plain" | "case-sensitive"; words: number; mean: number; median: number; ci95: number | null }[];
    wrongKeys: { total: number; stray: number; midWord: number; caseOnly: number };
    wrongPairs: { expected: string; typed: string; count: number; share: number; case_only: boolean }[];
    correctKeys: number; // from run_end, for "wrong keys per 100"
    wrongKeysRunEnd: number;
  };

  more: {
    quiz: {
      question_index: number | null;
      question: string;
      asked: number;
      right: number;
      wrong: number;
      timeout: number;
      share_right: number;
      mean_seconds: number | null;
    }[];
    quizPositions: { correct_position: number; chosen_position: number; count: number }[];
    tips: { key: string; closed: number; median_seconds: number; share_within_2s: number }[];
    tipPoints: { key: string; seconds: number }[];
    pauses: { reason: string; pauses: number; median_paused_seconds: number | null; with_resume: number }[];
    hiddenSeconds: number[];
  };
};
