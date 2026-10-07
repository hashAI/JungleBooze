# First code review (static; nothing compiled or run in the cloud)

Source: code-reviewer report, 2026-10-07. Verdict: no compile errors spotted, no unguarded freeze or softlock in the core
run, pause, continue, game-over, restart and vine flows. Determinism is clean. Per-frame code is mostly allocation-free.

## Must fix before the next play-test
1. Tutorial death is rescued one frame late, so the full death sound and dead pose play on a first-run player's first mistake. RunDriver.cs:374-382 checks TryTakeRescue before DispatchEvents sets it (TutorialDirector.cs:176-180). Fix: rescue at death time from session.Runner.Current.IsDead/DeathCause/DeathArchetype and drop the Died event.
2. The runner event buffer is 64, not the 512 the track spec wants (TrackRunWorld.cs:141, RunnerDesignValues.cs:36, TrackDesignValues.cs:33). Overflow overwrites the oldest events and can lose Died/Revived/VineReleased/Landed/Stumbled, leaving views stuck. Fix: use max(runner, track capacity) or raise the runner default to 256.
3. Reduce Motion in Settings does almost nothing (RunSceneBootstrap.cs:231, FollowCameraView:58/94, PowerUpView:96 read the config asset, not the save), and the Haptics toggle has no consumer. Fix: feed save.ReduceMotion into the presentation config and subscribe to SettingsChanged; implement or hide haptics.
4. Swipes slower than 250 ms are silently lost (SwipeRecognizer.cs:64-68). Fix: rolling window that re-bases the origin.
5. A force-quit while paused or running loses the run's coins and best score (RunResultRecorder.cs:46-72, RunDriver.cs:420-432). Fix: bank or record the run on backgrounding, keeping the exactly-once rule.

## Should fix soon
6. A touch on a UI button also feeds game input (PlayerInputAdapter.FeedPointer): ignore touches over UI or reset input from buttons.
7. Continue offer only counts the wallet, not current-run coins (RunDriver.cs:199-213). Design decision, recommended: count session coins.
8. ObstacleView toggles many SetActive per frame because pieces are keyed by running index (ObstacleView.cs:208-230): key a piece to the obstacle id.
9. Gateway blends re-upload sky vertex colours every frame (SkyView.ApplyTheme): use a shader uniform or a lower rate.
10. Unconditional logs (RunConfigLoader.LoadWorlds, WorldThemeView.cs:222-225) and a synchronous fsync save on the Game Over frame (FileSaveStorage.cs:74): gate logs, defer the save a frame.
11. Continue offer timer burns while the app is backgrounded (RunDriver.cs:373): clamp the real delta (about 0.1 s).
12. A rescued tutorial death still updates the session best (GameSession.cs:466-479).
14. Pause then Restart records the run as abandoned (intended): say so in the UI strings.

## Later
Run clip must have Loop Time in the importer; FitToHeight reads skinned bounds before the first evaluate; sky dome radius 150 vs far plane 160; Math.Sin/Cos in SpawnVineBonusCoins is not bit-identical across platforms; RunAudioCues hard-codes event byte codes (add a test); HazardView strike column drop takes 6 ticks while the hitbox is lethal immediately; PowerUpView.RenderShield calls HasGround every frame; tests are stale (RunSceneBootstrapTests view count 4 vs 18, RunDriverLifecycleTests assume no tutorial) and there are no tests for meta services, SaveCodec compatibility, tutorial rescue ordering, exactly-once recording, ContinueRun, or Revive-over-gap.

## Checked and OK
All 18 views rebind per run; pause/resume/countdown and double Continue are safe; saves are atomic with a .bak fallback; rewards are granted once; GenerateAhead and FindGroundAhead are bounded.

## Open questions for the owner
1. Count current-run coins toward affording a Continue? Recommended: yes.
2. Should Reduce Motion also disable vine slow-mo, camera tilt/shake and shards, with haptics implemented or hidden? Recommended: yes.
3. Swipe window: rolling instead of a hard 250 ms limit? Recommended: rolling.
