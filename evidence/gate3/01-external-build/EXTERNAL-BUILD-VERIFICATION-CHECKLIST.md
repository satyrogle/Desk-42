# Desk 42 External Build Verification Checklist

Run against every externally distributed build. Record results with the build archive.

## Build: __________ | Date: __________ | Tester: __________

### Installation and launch

- [ ] Installs from packaged build (no Unity Editor required)
- [ ] Launches to main menu without errors
- [ ] Resolution defaults to a playable window size
- [ ] No console errors on startup (check Player.log)

### First-time experience

- [ ] New player can understand how to start
- [ ] Tutorial/onboarding is functional
- [ ] No dead-end states in the first 5 minutes

### Core loop

- [ ] Can receive and process at least one claim
- [ ] All core mechanics are functional (dials, punch cards, etc.)
- [ ] Can complete at least one full work cycle
- [ ] Can reach a meaningful endpoint / shift completion

### Stability

- [ ] No crashes during a full session
- [ ] No progression-blocking bugs encountered
- [ ] Save/load works if applicable
- [ ] Can exit the game normally (quit to desktop)

### Presentation

- [ ] Build version number is visible
- [ ] Controls are communicated to the player
- [ ] Audio plays and levels are consistent
- [ ] No placeholder/debug text visible to player
- [ ] No obviously broken visual elements

### Feedback route

- [ ] Tester knows how to report issues
- [ ] Feedback mechanism is functional

### Result

- [ ] **PASS** — cleared for external distribution
- [ ] **FAIL** — blocking issues listed below

### Blocking issues

*(List any issues that prevent external distribution.)*
