# Desk 42 — Session Data Schema

Record one entry per play session per tester.

## Fields

```
Tester ID:          D42-Txxx
Session ID:         D42-Txxx-Sxx
Build version:      v0.x
Session start:      YYYY-MM-DD HH:MM (UTC)
Session end:        YYYY-MM-DD HH:MM (UTC)
Playtime (min):     XX
Progress reached:   CP-XX (see PROGRESSION-CHECKPOINTS.md)
Completed:          Yes / No
Exit point:         (where in the game they stopped)
Critical interactions: (notable choices, mechanic engagement)
Errors/crashes:     (description or "none")
```

## Tester register fields

```
Tester ID:          D42-Txxx
Source:             Reddit / Discord / Personal referral / Game-dev community / Steam / Showcase / Other
Date recruited:     YYYY-MM-DD
Build:              v0.x
Sessions:           X
Total playtime:     XX min
Completed:          Yes / No
Survey completed:   Yes / No
Bugs reported:      X
Cohort:             Pilot / A / B
```

## Anonymisation rules

- Tester IDs are assigned sequentially: D42-T001, D42-T002, etc.
- No real names, emails, or identifying information in any file committed to Git.
- The mapping from tester ID to real identity is stored outside this repository.
- Recruitment source is kept at category level (e.g., "Reddit"), not specific usernames or posts.
