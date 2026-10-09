# Thief & the Necklace

## P7 — Scoring, Progression & Meta-Game Specification

**Status:** Design and system architecture  
**Parent:** P0 → P1 → P2 → P3 → P4 → P5 → P6  
**Primary goal:** Make deduction, bluffing, match competition, and long-term progression fair, strategic, and replayable.

P0 established role values and the initial reward model: correct Thief voters earn their own role value; an escaping Thief earns the combined values of the innocent roles. P7 separates this round score from match score and profile progression.

## 1. Three score lifetimes

| Layer | Lifetime | Purpose |
|---|---|---|
| Round score | One mystery round | Reward outcome and correct deduction. |
| Match score | Current five-round match | Determines match ranking and winner. |
| Profile progression | Across matches | Records stats, XP, level, and cosmetic unlocks. |

Match score determines the winner. XP determines profile level. Cosmetics personalize presentation. Statistics describe play. These systems must not grant competitive power.

## 2. MVP role values

| Role | Base value | Status |
|---|---:|---|
| King | 8 | Example value from source GDD |
| Queen | 7 | Example value from source GDD |
| Minister | 6 | P7 proposal |
| Soldier | 5 | Example value from source GDD |
| Servant | 4 | P7 proposal |
| Guard / configured sixth role | 5 | P7 proposal |

Minister, Servant, and Guard values are balancing proposals, not source-GDD requirements. P7 sets Guard to 5 for the current config, superseding P1's provisional Guard value of 3. Keep all values in data/configuration, not scattered through gameplay code.

Role value changes personal reward, never voting power. Each player has one vote. The Thief is a hidden objective attached to a normal role and has no separate base-role bonus.

## 3. Round scoring rules

Let `V(p)` be a player's role value and let the final deciding ballot be the initial ballot unless a runoff occurs. When a runoff occurs, it is the deciding ballot; initial votes do not score.

### Thief caught

- Each player who voted for the Thief in the final deciding ballot earns their own role value.
- Incorrect voters earn 0.
- The Thief earns 0.

Example: King, Queen, and Soldier correctly vote for the Thief; they earn +8, +7, and +5. Incorrect voters and the Thief earn 0.

### Thief escapes

- The Thief earns the sum of the role values of all innocent players in that round.
- Innocents earn 0, including any innocent who correctly suspected the Thief if the final vote did not catch them.
- No extra Thief role-value bonus is stacked on top of the escape reward.

For a six-player match with values King 8, Queen 7, Minister 6, Soldier 5, Servant 4, Guard 5, if one is Thief, the maximum escape reward is the sum of the other five role values. The precise total depends on which role is Thief.

There are no negative points, suspicion bonuses, message-count bonuses, speaking-time rewards, or hidden score modifiers in MVP. Every awarded point has a visible reason.

## 4. Match score and winner

The MVP match lasts five rounds. Match score is the sum of a player's round-score deltas. Scores reset when a new match starts and persist between rounds of the current match. A score-target mode (for example, first to 40 at a round boundary) is future scope.

At match end, rank by final match score. If players tie:

1. More correct Thief detections ranks higher.
2. If still tied, more successful escapes while assigned Thief ranks higher.
3. If still tied, share the placement.

This P7 tiebreaker supersedes P1's earlier shared-win default. A tiebreaker never changes match score.

## 5. Scoreboard and result UX

During discussion, show only a compact match score element; it must not dominate the investigation table. During results, show a full scoreboard and an individual breakdown:

```text
THIEF: MINISTER
RESULT: CAUGHT
YOUR ROUND SCORE: +8 (Correct Thief vote)
YOUR MATCH SCORE: 26
```

For an escape, explain the Thief's summed reward and show why other players scored 0. Avoid “You lost!” messaging; present the winner and the player's own score neutrally. The underlying score is committed by authority before or independently of animation. P5's score count-up animates the committed delta and total; it never calculates authoritative values.

## 6. Score transactions and idempotency

Each applied score transaction contains:

- `matchId`
- `roundId`
- `playerId`
- `reason`
- `delta`
- `newTotal`

Example reason enum:

```csharp
public enum ScoreReason
{
    CorrectThiefVote,
    ThiefEscape
}
```

MVP activates only these two reasons. Match bonuses and achievement bonuses are future additions and must not silently enter the core formula.

The authoritative score service resolves and commits the round once. It stores resolved round IDs / transaction IDs and returns the previous result on a duplicate request rather than applying the delta again. The client cannot submit “I earned +8”; it can only display the authority's transaction.

Suggested data shapes:

```csharp
[Serializable]
public class RoundScoreEntry
{
    public string playerId;
    public int delta;
    public ScoreReason reason;
}

[Serializable]
public class RoundScoreResult
{
    public string matchId;
    public string roundId;
    public List<RoundScoreEntry> entries;
}
```

## 7. Role value and balance monitoring

The basic formula may create a larger Thief score swing than an innocent's single correct-vote reward. Keep the formula unchanged for the first playtest; measure before tuning.

Track for balance analysis:

- Average and median score per round.
- Score variance and maximum swing.
- Average Thief escape reward and escape rate.
- Correct-vote rate and vote concentration.
- Role assignment count, detection rate, and average score by role.
- First-place frequency and match win rate.
- Thief assignment frequency by player across many rounds.

Do not force Thief rotation in a match just to make every round feel fair; seeded randomness should be unbiased. Use statistics to identify a randomization or balance problem rather than “correcting” normal variance invisibly.

## 8. Profile statistics

Initial profile stats may include:

- Matches played and won.
- Rounds completed.
- Correct Thief votes.
- Incorrect votes.
- Times assigned Thief.
- Successful Thief escapes and times caught as Thief.
- Total points and average round/match score.
- Detection rate, with numerator/denominator visible or clearly defined.

Define denominators consistently. For example, `DetectionRate = correct Thief votes / submitted votes when the player was Innocent`; do not include rounds as Thief or abstentions. Profile summaries are descriptive, not gameplay classes or buffs.

## 9. XP and level progression

XP is separate from match score: a player may gain XP while losing the match. XP must not change the current match winner or role/vote/clue mechanics.

Initial reward examples for tuning:

| Event | Example XP |
|---|---:|
| Complete a round | +10 |
| Correct Thief detection | +20 |
| Successful Thief escape | +20 |
| Complete a match | +25 |

These are starting targets, not locked economy values. Define stacking clearly: MVP may grant completion XP plus the applicable outcome XP, and match-completion XP only after the five-round match is finalized. A disconnect/abort must not grant match-completion XP; ordinary network failures should not receive harsh penalties.

Use data-driven level thresholds rather than hard-coding a formula prematurely. Initial example: level thresholds at 0, 100, 250, 450, 700 XP. XP and level are profile-only and never increase vote power, clue quantity, information access, or scoring multipliers.

## 10. MVP persistence and future profile features

MVP profile scope: local profile persistence for XP, level, basic stats, and earned cosmetic IDs. Match score remains session/match data and resets for a new match. Profile progression should be updated only from verified completed match results. Local persistence is appropriate for an early trusted prototype; it is not cheat-resistant.

Future online profile architecture should accept validated match results at a trusted service boundary. Never trust a client-submitted level, XP balance, achievement state, or leaderboard score. Global leaderboards should wait for authentication, moderation, persistence, and anti-cheat to mature.

## 11. Cosmetic and achievement progression (future)

Future unlocks may include card backs, avatar/profile frames, table themes, titles, badges, and emotes. Cosmetic choices must not reveal the player's role or guilt, and hidden-role card backs remain visually identical until reveal. Scenario unlocks grant content access, not private information or competitive advantage; public matchmaking must ensure all players can access the selected scenario.

Achievements may track first match, correct detections, successful escapes, or a clean match. Rewards are XP, titles, badges, or cosmetics only. No extra votes, clues, or gameplay power. Ranked tiers, seasonal challenges, global leaderboard, and paid economy are outside MVP. The game needs no coins, gems, energy, lives, or loot boxes for the core loop.

## 12. Anti-farming and integrity

Do not award progression for message count, speaking time, accusation volume, or mere room presence. Require a valid completed round/match result. Define quick-quit and disconnect handling conservatively; do not punish routine network failure harshly in MVP. Future online progression must prevent repeated duplicate results, AFK farming, boosting, and fabricated achievements.

## 13. Configuration and service boundaries

Keep tunable values in configuration assets/data:

```text
MetaConfig
├── RoleValues
├── MatchLength
├── ThiefEscapeRule
├── XPRewards
├── LevelThresholds
├── AchievementRules (future)
└── CosmeticCatalog (future)
```

The authoritative scoring service belongs to the P3/P4 authority layer. The profile repository owns persistent profile data. A future progression pipeline is:

```text
VERIFIED MATCH RESULT → XP/STAT RESOLUTION → PROFILE COMMIT → UNLOCKS
```

Separate lifetimes:

- Round data resets after the round.
- Match score resets after the match.
- Profile stats and XP persist between matches.

## 14. Suggested data model

```csharp
[Serializable]
public class PlayerProfile
{
    public string playerId;
    public int level;
    public int experience;
    public int matchesPlayed;
    public int matchesWon;
    public int correctThiefVotes;
    public int incorrectVotes;
    public int timesAssignedThief;
    public int successfulThiefEscapes;
    public int timesCaughtAsThief;
}

[Serializable]
public class MatchScore
{
    public string playerId;
    public int totalScore;
    public int correctDetections;
    public int successfulThiefEscapes;
}
```

Future `AchievementProgress` and `UnlockState` should be versioned, idempotent, and data-driven. Cosmetic catalog entries must not be embedded in scoring logic.

## 15. Unity folder structure

```text
Scripts/
├── Scoring/       ScoreService.cs, ScoreConfig.cs,
│                  RoundScoreResult.cs, ScoreReason.cs
├── Progression/   XPService.cs, LevelService.cs,
│                  AchievementService.cs (future), UnlockService (future)
├── Profile/       PlayerProfile.cs, ProfileRepository.cs, PlayerStats.cs
└── Leaderboards/  LeaderboardService.cs (future)
```

## 16. Score and progression validation cases

- Thief caught: correct final-ballot voters receive their role value; wrong voters and Thief receive 0.
- Thief escapes: Thief receives the sum of innocent role values; all innocents receive 0.
- Runoff: only final deciding ballot determines correct-voter rewards.
- No negative points; no suspicion/talking bonuses; no Thief base-role bonus.
- Duplicate result does not duplicate score or XP.
- Match scores accumulate over five rounds and reset on a new match.
- Final ranking and each tiebreak rule resolve correctly.
- XP remains separate from match score; configured thresholds produce expected levels.
- Disconnect/aborted match rules do not grant completion XP.
- Profile persistence survives app restart; client cannot alter authoritative online results.

These are specification cases for future implementation/testing; this document creation does not implement or run them.

## 17. MVP and future scope

### MVP

- Role values and P1 round scoring.
- No negative points or hidden modifiers.
- Cumulative score over five rounds.
- Final ranking with P7 tiebreaks.
- Basic stats, XP, level, and local profile persistence, once the profile work is scheduled.
- Authority-committed, idempotent score transactions.

### Future

- Achievements, cosmetics, titles, card backs, table themes, emotes.
- Scenario unlock catalog.
- Global leaderboards and backend profile.
- Ranked mode, seasons, daily challenges, competitive rating.
- Expanded analytics dashboards.

## 18. P7 definition of done

P7 is ready when the scoring formula, five-round match accumulation, final ranking/ties, profile lifetime, XP separation, and authority/idempotency boundary are specified and the system can be implemented without ambiguous rewards. Core scoring must have deterministic expected results for caught, escaped, runoff, duplicate-result, and match-tie cases. Cosmetics/progression must be unable to affect deduction power.

## 19. Design lock and P8 handoff

```text
MATCH SCORE → Who wins this match?
XP          → How far has the profile progressed?
COSMETICS   → How does the player personalize the game?
STATS       → What can the player learn about their play?
```

Progression can reward continued play, but it must never reward power over another player. Levels grant no extra information, votes, or scoring advantage.

P8 — QA, Balancing, Playtesting & Validation should define the combined test framework: 4/5/6-player gameplay matrix, scenario generation checks, deduction balance, networking/privacy/fault tests, UI and animation regression, performance, score/progression verification, exploit checks, seed reproducibility, playtest methodology, telemetry metrics, bug severity, release checklist, and production Definition of Done.
