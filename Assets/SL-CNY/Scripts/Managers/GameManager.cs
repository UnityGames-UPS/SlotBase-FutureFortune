using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] internal SocketIOManager socketManager;
    [SerializeField] internal UIManager uiManager;
    [SerializeField] internal PopupManager popupManager;
    [SerializeField] internal SlotView slotView;
    public SlotView SlotView => slotView;
    [SerializeField] internal NudgeController nudgeController;

    [Header("Spin Settings")]
    [SerializeField] private float normalSpinDuration = 3.5f;
    [SerializeField] private float turboSpinDuration = 2.0f;
    [SerializeField] private float quickSpinCycleDuration = 0.8f;

    // FF: autoplay nudge delay by speed (reference: ~1.5 s normal, ~0.7 s turbo)
    [Header("Autoplay Nudge Delay")]
    [SerializeField] private float autoNudgeDelayNormal = 1.5f;
    [SerializeField] private float autoNudgeDelayTurbo = 0.7f;

    [Header("Win Settings")]
    [SerializeField] private double bigWinMultiplierThreshold = 500.0;
    public double BigWinMultiplierThreshold => bigWinMultiplierThreshold;

    internal GameConfig gameConfig;
    internal PlayerData playerData;
    internal SpinResult lastResult;

    internal GameState currentState;
    internal SpinSpeed currentSpinSpeed;

    internal int currentBetIndex;
    internal double currentBetAmount;

    internal bool isAutoPlaying;
    internal int autoPlayTotalRounds;
    internal int autoPlayRemainingRounds;
    internal bool wasAutoPlayingBeforeFreeSpins;
    internal int savedAutoPlayRemainingRounds;
    internal int savedAutoPlayTotalRounds;

    internal bool isInFreeSpins;
    internal int freeSpinsRemaining;
    internal int freeSpinsUsed;
    internal bool waitingForFreeSpinStart;

    internal bool isInitialized;
    internal bool initializationFailed;

    private Coroutine spinCoroutine;
    private bool stopRequested;
    private bool waitingForSpecialWin;

    // FF: Nudge synchronization flags, references, and coroutines
    [SerializeField] private bool debugNudge = true; // FF: Task 3 - Debug nudge aid
    private string nudgedDirection;
    private List<int> nudgedColBefore;
    private bool nudgeAnimDone;
    private bool nudgeResponseDone;
    private int nudgedReelIndex = -1;
    private Coroutine completeNudgeCoroutine;
    private Coroutine nudgeTimeoutCoroutine;
    private Coroutine autoNudgeCoroutine;
    internal bool IsNudgePending => nudgeTimeoutCoroutine != null || completeNudgeCoroutine != null;

    #region Initialization

    private void Start()
    {
        if (nudgeController == null)
        {
            nudgeController = FindFirstObjectByType<NudgeController>();
        }

        currentState = GameState.Initializing;
        currentSpinSpeed = SpinSpeed.Normal;
        waitingForFreeSpinStart = false;
        isInitialized = false;
        initializationFailed = false;
    }

    internal void OnInitDataReceived(GameConfig config, PlayerData player, List<List<int>> initialMatrix, ServerState serverState = null)
    {
        gameConfig = config;
        playerData = player;
        currentBetIndex = playerData.currentBetIndex;
        UpdateBetAmount();

        if (initialMatrix != null && slotView != null)
        {
            slotView.SetInitialMatrix(initialMatrix);
        }

        // FF: Task 7 - Resume after refresh (initData.state)
        if (serverState != null)
        {
            // Case 1: Free games active on resume
            if (serverState.isFreeGame || serverState.freeSpinsRemaining > 0)
            {
                isInFreeSpins = true;
                freeSpinsRemaining = serverState.freeSpinsRemaining;
                isInitialized = true;
                currentState = GameState.Idle;

                if (slotView != null) slotView.SetFreeGameLayout(true);
                if (uiManager != null)
                {
                    uiManager.FreezeBalanceDisplay(playerData.balance);
                    uiManager.UpdateFreeSpinRemainingCount(freeSpinsRemaining);
                    if (serverState.totalFreeSpinsWin > 0)
                    {
                        uiManager.UpdateWinDisplay(serverState.totalFreeSpinsWin);
                    }
                    uiManager.OnGameInitialized();
                    // FF: Task 2 - One switch for free-game mode on resume
                    uiManager.SetFreeGameMode(true);
                    uiManager.OnFreeSpinsStarted(freeSpinsRemaining);
                }
                return;
            }

            // Case 2: Awaiting nudge on resume
            if (serverState.awaitingNudge)
            {
                if (isInFreeSpins)
                {
                    // FF: Task 3 - No nudge during free games
                    Debug.LogWarning("[FF] Server sent awaitingNudge during free games on resume - ignored");
                }
                else
                {
                bool parsedPending = false;
                List<List<int>> pendingMatrix = null;
                List<int> pendingAbove = null;
                List<int> pendingBelow = null;
                List<ServerNudgeOption> pendingNudgeOptions = null;

                if (serverState.pending != null && serverState.pending.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                {
                    try
                    {
                        var pObj = (Newtonsoft.Json.Linq.JObject)serverState.pending;
                        if (pObj.TryGetValue("matrix", out var mToken))
                        {
                            var rawMatrix = mToken.ToObject<List<List<int>>>();
                            if (rawMatrix != null && rawMatrix.Count > 0)
                            {
                                pendingMatrix = InitDataConverter.ConvertServerRowsToClientColumns(rawMatrix);
                            }
                        }
                        if (pObj.TryGetValue("above", out var aToken))
                        {
                            pendingAbove = aToken.ToObject<List<int>>();
                        }
                        if (pObj.TryGetValue("below", out var bToken))
                        {
                            pendingBelow = bToken.ToObject<List<int>>();
                        }
                        if (pObj.TryGetValue("nudgeOptions", out var noToken))
                        {
                            pendingNudgeOptions = noToken.ToObject<List<ServerNudgeOption>>();
                        }

                        if (pendingMatrix != null && pendingMatrix.Count == 5 && pendingAbove != null && pendingBelow != null)
                        {
                            parsedPending = true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[FF] Failed to parse pending nudge: {ex.Message}");
                        parsedPending = false;
                    }
                }

                if (parsedPending)
                {
                    if (slotView != null) slotView.SetInitialMatrix(pendingMatrix);

                    lastResult = new SpinResult
                    {
                        resultMatrix = pendingMatrix,
                        above = pendingAbove,
                        below = pendingBelow,
                        nudgeOptions = pendingNudgeOptions,
                        awaitingNudge = true,
                        playerData = playerData
                    };

                    isInitialized = true;
                    currentState = GameState.AwaitingNudge;
                    if (uiManager != null)
                    {
                        uiManager.OnGameInitialized();
                        uiManager.DisableControlsForNudge(isAutoPlaying);
                    }

                    if (nudgeController != null)
                    {
                        nudgeController.EnableNudgeMode(pendingAbove, pendingBelow, pendingNudgeOptions);
                    }
                    return;
                }
                else
                {
                    // awaitingNudge but unparsable -> server error popup (never a stuck game)
                    Debug.LogError("[FF] awaitingNudge is true but pending data is null or unparsable.");
                    isInitialized = true;
                    currentState = GameState.Idle;
                    if (uiManager != null) uiManager.OnGameInitialized();
                    if (popupManager != null)
                    {
                        popupManager.ShowServerError("Pending nudge data could not be recovered. Please reload or contact support.", false);
                    }
                    return;
                }
            }
        }
        }

        isInitialized = true;
        currentState = GameState.Idle;

        uiManager.OnGameInitialized();
    }

    #endregion

    #region Bet Management

    internal void IncreaseBet()
    {
        if (currentState != GameState.Idle || isAutoPlaying) return;
        if (gameConfig == null || gameConfig.availableBets == null || gameConfig.availableBets.Count == 0) return;

        int maxIndex = gameConfig.availableBets.Count - 1;
        int nextIndex = currentBetIndex + 1;
        if (nextIndex > maxIndex)
        {
            nextIndex = 0;
        }

        if (nextIndex == maxIndex)
        {
            AudioManager.Instance?.PlayMaxBetReached();
        }
        else
        {
            AudioManager.Instance?.PlayBetPlusMinus();
        }

        SetBetIndex(nextIndex);
    }

    internal void DecreaseBet()
    {
        if (currentState != GameState.Idle || isAutoPlaying) return;
        if (gameConfig == null || gameConfig.availableBets == null || gameConfig.availableBets.Count == 0) return;

        int maxIndex = gameConfig.availableBets.Count - 1;
        int nextIndex = currentBetIndex - 1;
        if (nextIndex < 0)
        {
            nextIndex = maxIndex;
        }

        if (nextIndex == maxIndex)
        {
            AudioManager.Instance?.PlayMaxBetReached();
        }
        else
        {
            AudioManager.Instance?.PlayBetPlusMinus();
        }

        SetBetIndex(nextIndex);
    }

    internal void SetBetIndex(int index)
    {
        currentBetIndex = index;
        UpdateBetAmount();
        uiManager.UpdateBetDisplay();
        if (slotView != null) slotView.OnBetChanged();
    }

    private void UpdateBetAmount()
    {
        currentBetAmount = gameConfig.availableBets[currentBetIndex];
    }

    #endregion

    #region Spin Control
    
    internal void RequestSpin()
    {
        if (waitingForFreeSpinStart) return;

        if (currentState != GameState.Idle) return;
        if (!socketManager.isConnected) return;

        double totalPay = GetTotalPay();
        if (!isInFreeSpins && playerData.balance < totalPay)
        {
            if (popupManager != null)
            {
                popupManager.ShowInsufficientFundsError();
            }
            return;
        }

        StartSpin();
    }

    internal void RequestStop()
    {
        if (currentState == GameState.Spinning)
        {
            if (isAutoPlaying)
            {
                StopAutoPlay();
            }
            else if (!isInFreeSpins)
            {
                stopRequested = true;
                uiManager.DisableSpinButtonDuringStop();
            }
        }
    }

    private void StartSpin()
    {
        if (lastResult != null)
        {
            ProcessSpinResult();

            // FF: SPIN pressed during the win of a free-games trigger spin: processing the result just
            // opened the award popup. Do not start a base spin under it - free spins start from the popup.
            if (waitingForFreeSpinStart) return;
        }

        lastResult = null;
        currentState = GameState.Spinning;
        stopRequested = false;

        // Deduct total pay from balance on spin start (except in free spins)
        if (!isInFreeSpins)
        {
            playerData.balance -= GetTotalPay();
            if (playerData.balance < 0) playerData.balance = 0;
        }

        // FF: Stop previous spin loops, play spin start SFX, and begin reel spin loop
        AudioManager.Instance?.StopAllLoops();
        AudioManager.Instance?.PlayFFSpinStart();
        AudioManager.Instance?.StartReelSpinLoop();

        uiManager.OnSpinStarted();

        if (slotView != null)
        {
            slotView.StartSpin();
        }

        socketManager.SendSpinRequest(currentBetIndex, isInFreeSpins);

        if (spinCoroutine != null)
            StopCoroutine(spinCoroutine);
        spinCoroutine = StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        float spinDuration = GetSpinDuration();
        float elapsed = 0f;

        while (elapsed < spinDuration && !stopRequested)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Player pressed Stop manually — hold for 0.5s so the reels keep
        // spinning briefly before snapping, giving clear visual feedback.
        if (stopRequested)
        {
            yield return new WaitForSeconds(0.5f);
        }

        while (lastResult == null)
        {
            yield return null;
        }

        currentState = GameState.Stopping;

        if (slotView != null && lastResult.resultMatrix != null)
        {
            if (currentSpinSpeed == SpinSpeed.QuickSpin || stopRequested)
            {
                slotView.QuickStop(lastResult.resultMatrix);

                // Wait for the snap animation to settle before processing result
                float quickStopWaitTime = 0.5f;
                yield return new WaitForSeconds(quickStopWaitTime);

                OnReelsStoppedComplete();
            }
            else
            {
                slotView.StopSpin(lastResult.resultMatrix, OnReelsStoppedComplete, lastResult.above, lastResult.below);
            }
        }
        else
        {
            OnReelsStoppedComplete();
        }
    }

    private void OnReelsStoppedComplete()
    {
        // FF: Stop reel spin loop when reels are fully stopped
        AudioManager.Instance?.StopReelSpinLoop();

        if (lastResult != null)
        {
            double featureDeferredWin = lastResult.GetTotalFeatureDeferredWins();
            double reelStopBalance = lastResult.playerData != null ? (lastResult.playerData.balance - featureDeferredWin) : 0;

            playerData = new PlayerData
            {
                balance = reelStopBalance,
                currentBetIndex = lastResult.playerData != null ? lastResult.playerData.currentBetIndex : currentBetIndex
            };
        }

        // --- Nudge Phase Handling ---
        if (lastResult != null && lastResult.awaitingNudge)
        {
            // FF: Task 3 - No nudge during free games
            if (isInFreeSpins)
            {
                Debug.LogWarning("[FF] Server sent awaitingNudge during free games - ignored");
            }
            else
            {
                currentState = GameState.AwaitingNudge;
                if (uiManager != null)
                {
                    uiManager.OnSpinStopping(lastResult);
                    // FF: Task 5 - Lock spin, bet, settings during nudge phase
                    uiManager.DisableControlsForNudge(isAutoPlaying);
                }

                if (nudgeController != null)
                {
                    nudgeController.EnableNudgeMode(lastResult.above, lastResult.below, lastResult.nudgeOptions);
                }

                // FF: Task 3 - Autoplay auto-nudge after 0.7s delay
                if (isAutoPlaying)
                {
                    if (autoNudgeCoroutine != null) StopCoroutine(autoNudgeCoroutine);
                    autoNudgeCoroutine = StartCoroutine(AutoNudgeRoutine());
                }

                return; // Pause here! Do NOT go to Idle, do NOT enable Spin button
            }
        }

        if (lastResult != null && lastResult.winAmount > 0 && lastResult.winLines != null && lastResult.winLines.Count > 0)
        {
            double totalPay = GetTotalPay();
            double multiplier = totalPay > 0 ? (lastResult.winAmount / totalPay) : 0;

            if (multiplier >= bigWinMultiplierThreshold)
            {
                uiManager.DisableControlsDuringWinAnimation();
                currentState = GameState.Idle;
                slotView.ShowWinLineAnimation(lastResult.winLines, OnWinAnimationComplete);
                StartCoroutine(TriggerWinPopupWithDelay(1.5f, lastResult));
            }
            else
            {
                // FF: Task 5 - Balance display updates after win animation completes
                uiManager.OnSpinStopping(lastResult);
                uiManager.EnableControlsAfterWinAnimation();
                currentState = GameState.Idle;
                slotView.ShowWinLineAnimation(lastResult.winLines, OnWinAnimationComplete);
            }
        }
        else
        {
            uiManager.OnSpinStopping(lastResult);
            currentState = GameState.Idle;
            OnWinAnimationComplete();
        }
    }

    private IEnumerator TriggerWinPopupWithDelay(float delay, SpinResult result)
    {
        double totalPay = GetTotalPay();
        double multiplier = totalPay > 0 ? (result.winAmount / totalPay) : 0;
        if (multiplier < bigWinMultiplierThreshold)
        {
            waitingForSpecialWin = false;
            yield break;
        }

        waitingForSpecialWin = true;

        yield return new WaitForSeconds(delay);

        if (lastResult == result && multiplier >= bigWinMultiplierThreshold)
        {
            uiManager.TriggerBigWinPopup(result, () =>
            {
                waitingForSpecialWin = false;
            });
        }
        else
        {
            waitingForSpecialWin = false;
        }
    }

    private void OnWinAnimationComplete()
    {
        if (lastResult != null)
        {
            double totalPay = GetTotalPay();
            double multiplier = totalPay > 0 ? (lastResult.winAmount / totalPay) : 0;

            // Only update UI here if it wasn't already updated in OnReelsStoppedComplete (multiplier < bigWinMultiplierThreshold)
            if (multiplier >= bigWinMultiplierThreshold)
            {
                uiManager.OnSpinStopping(lastResult);
            }

            // FF: Task 5 - Update balance display after win animation completes
            uiManager.UpdateBalanceDisplay();
        }

        StartCoroutine(ProcessSpecialFeaturesAfterWin());
    }

    private IEnumerator ProcessSpecialFeaturesAfterWin()
    {
        // Wait for special win popup to finish before starting special features
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        // FF: Task 8 - Jackpot win hook
        if (lastResult != null && lastResult.jackpot != null && lastResult.jackpot.triggered)
        {
            bool jpDone = false;
            if (uiManager != null)
            {
                uiManager.ShowJackpotWin(lastResult.jackpot.tier, lastResult.jackpot.amount, () =>
                {
                    jpDone = true;
                });
                yield return new WaitUntil(() => jpDone);
            }
        }

        // FF: Task 4 - Retrigger in free spins
        if (isInFreeSpins && lastResult != null && lastResult.freeSpinsAwarded > 0)
        {
            freeSpinsRemaining = lastResult.freeSpinsRemaining >= 0 ? lastResult.freeSpinsRemaining : (freeSpinsRemaining + lastResult.freeSpinsAwarded);
            if (uiManager != null)
            {
                uiManager.UpdateFreeSpinRemainingCount(freeSpinsRemaining);
            }

            bool retriggerDone = false;
            uiManager.ShowRetrigger(lastResult.freeSpinsAwarded, () =>
            {
                retriggerDone = true;
            });
            yield return new WaitUntil(() => retriggerDone);
        }

        if (lastResult != null && lastResult.freeSpinData != null && lastResult.freeSpinData.isTriggered && !isInFreeSpins)
        {
            yield return StartCoroutine(DelayScatterTriggerResult());
            yield break;
        }

        ResumeAfterSpecialFeature();
    }

    private void ResumeAfterSpecialFeature()
    {
        if (isAutoPlaying || isInFreeSpins)
        {
            StartCoroutine(DelayBeforeNextRound());
        }
        else
        {
            ProcessSpinResult();
        }
    }

    private IEnumerator DelayScatterTriggerResult()
    {
        // FF: Block SPIN during the trigger celebration. Without this the state is Idle here, a SPIN
        // press runs StartSpin -> ProcessSpinResult early and a spin starts under the award popup.
        waitingForFreeSpinStart = true;
        uiManager.DisableControlsDuringWinAnimation();

        // FF: Play Free Games trigger sound
        AudioManager.Instance?.PlayFreeGamesTrigger();

        // Start scatter animations together AFTER all reels have stopped
        // Using 4 loops to match the 6-second delay (4 * 1.5s = 6s)
        slotView.AnimateAllScatters(4);

        // Wait for scatter hit animations to play
        yield return new WaitForSeconds(3.5f);
        ProcessSpinResult();
    }

    private IEnumerator DelayBeforeNextRound()
    {
        float delayTime = currentSpinSpeed == SpinSpeed.QuickSpin ? 0.3f : 0.5f;
        yield return new WaitForSeconds(delayTime);

        // Wait for special win popup using the flag and active state
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        ProcessSpinResult();
    }

    private float GetSpinDuration()
    {
        return currentSpinSpeed switch
        {
            SpinSpeed.Normal => normalSpinDuration,
            SpinSpeed.Turbo => turboSpinDuration,
            SpinSpeed.QuickSpin => quickSpinCycleDuration,
            _ => normalSpinDuration
        };
    }

    internal void OnSpinResultReceived(SpinResult result)
    {
        lastResult = result;

        // FF: Task 4 - Update free spin counter from freeSpinsRemaining
        if (isInFreeSpins)
        {
            int remaining = result.freeSpinsRemaining >= 0 ? result.freeSpinsRemaining : result.serverSpinsRemaining;
            if (remaining >= 0)
            {
                freeSpinsRemaining = remaining;
                freeSpinsUsed = result.serverSpinsUsed;
                uiManager.UpdateFreeSpinRemainingCount(freeSpinsRemaining);
            }
        }

        if (result.winLines != null)
        {
            for (int i = 0; i < result.winLines.Count; i++)
            {
                var line = result.winLines[i];

            }
        }

        // FF: Task 2 & Task 4 - If this result is the resolution of a nudge, signal response received
        if (currentState == GameState.Stopping && !result.awaitingNudge)
        {
            if (nudgeTimeoutCoroutine != null)
            {
                StopCoroutine(nudgeTimeoutCoroutine);
                nudgeTimeoutCoroutine = null;
            }

            nudgeResponseDone = true;
        }
    }

    // FF: Task 2 - Wait until BOTH the nudge animation has completed AND the NudgeData response has arrived,
    // then redraw/snap, then OnReelsStoppedComplete(). Works in either arrival order.
    private IEnumerator CompleteNudgeRoutine()
    {
        while (!nudgeAnimDone || !nudgeResponseDone)
        {
            yield return null;
        }

        completeNudgeCoroutine = null;

        if (slotView != null && lastResult != null && lastResult.resultMatrix != null)
        {
            // Redraw nudged reel symbols and snap localPosition.y to middlePosition in the same frame
            if (nudgedReelIndex >= 0 && nudgedReelIndex < lastResult.resultMatrix.Count)
            {
                slotView.SnapNudgedReel(nudgedReelIndex, lastResult.resultMatrix[nudgedReelIndex]);
            }
            slotView.UpdateMatrixAfterNudge(lastResult.resultMatrix);
        }

        // FF: Task 3 - Debug aid: Log resolved nudge
        if (debugNudge && lastResult != null && lastResult.resultMatrix != null)
        {
            string beforeStr = nudgedColBefore != null ? string.Join(",", nudgedColBefore) : "?";
            string afterStr = (nudgedReelIndex >= 0 && nudgedReelIndex < lastResult.resultMatrix.Count)
                ? string.Join(",", lastResult.resultMatrix[nudgedReelIndex])
                : "?";

            var centerList = new List<string>();
            for (int c = 0; c < lastResult.resultMatrix.Count; c++)
            {
                centerList.Add(lastResult.resultMatrix[c].Count > 1 ? lastResult.resultMatrix[c][1].ToString() : "?");
            }
            string centerStr = string.Join(",", centerList);

            Debug.Log($"[FF Nudge] reel={nudgedReelIndex} dir={nudgedDirection} before=[{beforeStr}] after=[{afterStr}] center=[{centerStr}] win={lastResult.winAmount}");
        }

        OnReelsStoppedComplete();
    }

    // FF: Task 2, Task 4, Task 5 - Execute nudge with animation sync, safety timeout, and debounce
    public void ExecuteNudge(int reelIndex, string direction)
    {
        if (currentState != GameState.AwaitingNudge) return;

        currentState = GameState.Stopping;
        nudgeAnimDone = false;
        nudgeResponseDone = false;
        nudgedReelIndex = reelIndex;
        nudgedDirection = direction; // FF: Task 3

        // FF: Task 3 - Record column before nudge for debug log
        if (slotView != null && slotView.currentDisplayMatrix != null && reelIndex >= 0 && reelIndex < slotView.currentDisplayMatrix.Count)
        {
            nudgedColBefore = new List<int>(slotView.currentDisplayMatrix[reelIndex]);
        }
        else
        {
            nudgedColBefore = null;
        }

        // FF: Task 3 - Cancel pending auto-nudge coroutine if any
        if (autoNudgeCoroutine != null)
        {
            StopCoroutine(autoNudgeCoroutine);
            autoNudgeCoroutine = null;
        }

        // FF: Task 1 & 5 - Immediately disable buttons to debounce spam-tapping
        if (nudgeController != null)
        {
            nudgeController.DisableAllButtons();
        }

        // FF: Task 5 - Lock all controls completely during nudge execution
        if (uiManager != null)
        {
            uiManager.LockAllControlsForNudgeExecution();
        }

        // FF: Task 2 - Animate reel nudge (~0.25s total)
        if (slotView != null)
        {
            slotView.AnimateReelNudge(reelIndex, direction, () =>
            {
                nudgeAnimDone = true;
            });
        }
        else
        {
            nudgeAnimDone = true;
        }

        // FF: Send nudge request to server
        if (socketManager != null)
        {
            socketManager.SendNudgeRequest(reelIndex, direction);
        }

        // FF: Task 4 - Start 5s safety timeout
        if (nudgeTimeoutCoroutine != null)
        {
            StopCoroutine(nudgeTimeoutCoroutine);
        }
        nudgeTimeoutCoroutine = StartCoroutine(NudgeTimeoutRoutine(5.0f));

        // FF: Task 2 - Start routine waiting for BOTH animation and server response
        if (completeNudgeCoroutine != null)
        {
            StopCoroutine(completeNudgeCoroutine);
        }
        completeNudgeCoroutine = StartCoroutine(CompleteNudgeRoutine());
    }

    // FF: Task 3 - Autoplay auto-nudge routine (waits 0.7s; cancels if autoplay stopped)
    private IEnumerator AutoNudgeRoutine()
    {
        float timer = 0f;
        // FF: delay depends on spin speed
        float nudgeDelay = (currentSpinSpeed == SpinSpeed.Normal) ? autoNudgeDelayNormal : autoNudgeDelayTurbo;
        while (timer < nudgeDelay)
        {
            // If player stopped autoplay during this wait, cancel and leave waiting for manual tap
            if (!isAutoPlaying || currentState != GameState.AwaitingNudge)
            {
                autoNudgeCoroutine = null;
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        autoNudgeCoroutine = null;

        if (isAutoPlaying && currentState == GameState.AwaitingNudge)
        {
            if (nudgeController != null && nudgeController.TryGetFirstValidNudge(out int reel, out string direction))
            {
                ExecuteNudge(reel, direction);
            }
            else
            {
                ExecuteNudge(0, "lower");
            }
        }
    }

    // FF: Task 4 - 5-second timeout routine for nudge response
    private IEnumerator NudgeTimeoutRoutine(float timeoutSeconds)
    {
        yield return new WaitForSeconds(timeoutSeconds);
        HandleNudgeFailure("Nudge failed. Please try again.");
    }

    // FF: Task 4 - Safety handler for nudge timeout or socket error/disconnect
    internal void HandleNudgeFailure(string errorMessage)
    {
        if (nudgeTimeoutCoroutine != null)
        {
            StopCoroutine(nudgeTimeoutCoroutine);
            nudgeTimeoutCoroutine = null;
        }
        if (completeNudgeCoroutine != null)
        {
            StopCoroutine(completeNudgeCoroutine);
            completeNudgeCoroutine = null;
        }
        if (autoNudgeCoroutine != null)
        {
            StopCoroutine(autoNudgeCoroutine);
            autoNudgeCoroutine = null;
        }

        // Reset nudged reel position back to middlePosition if it was offset
        if (slotView != null && nudgedReelIndex >= 0)
        {
            slotView.ResetReelPosition(nudgedReelIndex);
        }

        // FF: Task 3 - Reset spin visuals on nudge failure
        slotView?.ResetSpinVisuals();

        if (nudgeController != null)
        {
            nudgeController.DisableAllButtons();
        }

        if (isAutoPlaying)
        {
            StopAutoPlay();
        }

        currentState = GameState.Idle;

        if (uiManager != null)
        {
            uiManager.EnableControlsAfterWinAnimation();
        }

        if (popupManager != null)
        {
            popupManager.ShowServerError(errorMessage, false);
        }
    }

    private void ProcessSpinResult()
    {
        // FF: guard - result can already be consumed (e.g. by StartSpin) when a delayed coroutine lands here
        if (lastResult == null)
        {
            Debug.LogWarning("[FF] ProcessSpinResult called with no pending result - skipped");
            return;
        }
        playerData = lastResult.playerData;

        uiManager.OnSpinCompleted(lastResult);

        // Extract server-authoritative values before nullifying lastResult
        int serverSpinsRemaining = (lastResult.freeSpinsRemaining >= 0) ? lastResult.freeSpinsRemaining : lastResult.serverSpinsRemaining;
        int serverSpinsUsed = lastResult.serverSpinsUsed;
        double serverTotalRoundWin = lastResult.serverTotalRoundWin > 0 ? lastResult.serverTotalRoundWin : lastResult.totalFreeSpinsWin;
        bool isRoundOver = lastResult.isRoundOver;

        // Note: freeSpinsRemaining already updated in OnSpinResultReceived
        // Keeping this for safety in case OnSpinResultReceived wasn't called
        if (isInFreeSpins && freeSpinsRemaining != serverSpinsRemaining)
        {
            freeSpinsRemaining = serverSpinsRemaining;
        }


        // Check if free spins were just triggered (initial trigger from base game)
        if (lastResult.freeSpinData != null && lastResult.freeSpinData.isTriggered && !isInFreeSpins)
        {
            StartFreeSpins(lastResult.freeSpinData.spinsAwarded);
            lastResult = null;
            return;
        }

        lastResult = null;

        if (isAutoPlaying && !isInFreeSpins)
        {
            if (autoPlayTotalRounds != -1)
            {
                autoPlayRemainingRounds--;
            }

            uiManager.UpdateAutoPlayCount();

            if (autoPlayTotalRounds != -1 && autoPlayRemainingRounds <= 0)
            {
                currentState = GameState.Idle;
                StopAutoPlay();
            }
            else
            {
                // Before requesting the next spin, verify the player can still afford it.
                // If not, stop autoplay (restores all UI) then show the popup.
                double totalPay = GetTotalPay();
                if (playerData.balance < totalPay)
                {
                    currentState = GameState.Idle;
                    StopAutoPlay();
                    if (popupManager != null) popupManager.ShowInsufficientFundsError();
                }
                else
                {
                    currentState = GameState.Idle;
                    RequestSpin();
                }
            }
        }
        else if (isInFreeSpins)
        {
            // Free spin counter already updated in OnSpinResultReceived
            // No need to update again here

            if (isRoundOver || freeSpinsRemaining <= 0)
            {
                // Always use server-authoritative spinsUsed
                EndFreeSpins(serverTotalRoundWin, serverSpinsUsed);
            }
            else
            {
                currentState = GameState.Idle;
                StartCoroutine(DelayBeforeNextFreeSpin());
            }
        }
        else
        {
            currentState = GameState.Idle;
        }
    }

    #endregion

    #region Spin Speed Control

    internal void SetSpinSpeed(SpinSpeed speed)
    {
        currentSpinSpeed = speed;
    }

    #endregion



    #region Auto Play

    internal void StartAutoPlay(int rounds)
    {
        if (currentState != GameState.Idle) return;

        // Check balance BEFORE locking any UI — if insufficient, show popup and bail.
        double totalPay = GetTotalPay();
        if (playerData.balance < totalPay)
        {
            if (popupManager != null) popupManager.ShowInsufficientFundsError();
            return;
        }

        isAutoPlaying = true;
        autoPlayTotalRounds = rounds;
        autoPlayRemainingRounds = rounds;
        wasAutoPlayingBeforeFreeSpins = false;

        uiManager.OnAutoPlayStarted();
        RequestSpin();
    }

    internal void StopAutoPlay()
    {
        isAutoPlaying = false;
        autoPlayRemainingRounds = 0;
        wasAutoPlayingBeforeFreeSpins = false;

        // FF: Task 3 - Cancel pending auto-nudge timer so it leaves game waiting for manual tap
        if (autoNudgeCoroutine != null)
        {
            StopCoroutine(autoNudgeCoroutine);
            autoNudgeCoroutine = null;
        }

        uiManager.OnAutoPlayStopped();

        // FF: If stopped while awaiting nudge, ensure manual nudge controls stay active
        if (currentState == GameState.AwaitingNudge && uiManager != null)
        {
            uiManager.DisableControlsForNudge(false);
        }
    }

    internal bool ShouldResumeAutoPlay()
    {
        return wasAutoPlayingBeforeFreeSpins && (savedAutoPlayTotalRounds == -1 || savedAutoPlayRemainingRounds > 0);
    }

    internal void ResumeAutoPlay()
    {
        if (!ShouldResumeAutoPlay()) return;

        int remaining = savedAutoPlayRemainingRounds;
        int total = savedAutoPlayTotalRounds;
        wasAutoPlayingBeforeFreeSpins = false;

        if (currentState != GameState.Idle) return;

        double totalPay = GetTotalPay();
        if (playerData.balance < totalPay)
        {
            if (popupManager != null) popupManager.ShowInsufficientFundsError();
            return;
        }

        isAutoPlaying = true;
        autoPlayTotalRounds = total;
        autoPlayRemainingRounds = remaining;

        uiManager.OnAutoPlayStarted();
        RequestSpin();
    }

    #endregion

    #region Free Spins

    private void StartFreeSpins(int spins)
    {
        isInFreeSpins = true;
        freeSpinsRemaining = spins;
        freeSpinsUsed = 0;
        waitingForFreeSpinStart = true;
        AudioManager.Instance?.PlayFreeSpinBg();
        // FF: Task 2 - Free spin music is managed in SetFreeGameMode when START is pressed

        int prevTotal = autoPlayTotalRounds;
        int prevRemaining = autoPlayRemainingRounds;

        if (isAutoPlaying)
        {
            StopAutoPlay();
            wasAutoPlayingBeforeFreeSpins = true;
            savedAutoPlayTotalRounds = prevTotal;
            savedAutoPlayRemainingRounds = (prevTotal != -1) ? (prevRemaining - 1) : -1;
        }

        uiManager.OnFreeSpinsStarted(spins);

        currentState = GameState.Idle;
    }

    internal void StartFirstFreeSpin()
    {
        waitingForFreeSpinStart = false;

        StartCoroutine(DelayBeforeFirstFreeSpin());
    }


    private IEnumerator DelayBeforeFirstFreeSpin()
    {
        yield return new WaitForSeconds(0.5f);
        RequestSpin();
    }

    private IEnumerator DelayBeforeNextFreeSpin()
    {
        yield return new WaitForSeconds(0.3f);

        // Wait for special win popup if it's still active or pending
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        RequestSpin();
    }

    private void EndFreeSpins(double totalRoundWin, int totalSpinsUsed)
    {
        isInFreeSpins = false;
        freeSpinsRemaining = 0;
        AudioManager.Instance?.PlayMainBg();
        // FF: Task 2 - Main music is restored in SetFreeGameMode when TAKE is pressed

        uiManager.OnFreeSpinsEnded(totalRoundWin, totalSpinsUsed);

        currentState = GameState.Idle;
    }

    #endregion

    #region Connection Events

    internal void OnDisconnected()
    {
        // FF: Stop all active sound loops on disconnect
        AudioManager.Instance?.StopAllLoops();

        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            spinCoroutine = null;
        }

        // FF: Task 4 - Handle disconnect while in AwaitingNudge or pending nudge
        if (currentState == GameState.AwaitingNudge || nudgeTimeoutCoroutine != null || completeNudgeCoroutine != null)
        {
            if (nudgeTimeoutCoroutine != null)
            {
                StopCoroutine(nudgeTimeoutCoroutine);
                nudgeTimeoutCoroutine = null;
            }
            if (completeNudgeCoroutine != null)
            {
                StopCoroutine(completeNudgeCoroutine);
                completeNudgeCoroutine = null;
            }
            if (autoNudgeCoroutine != null)
            {
                StopCoroutine(autoNudgeCoroutine);
                autoNudgeCoroutine = null;
            }
            if (nudgeController != null)
            {
                nudgeController.DisableAllButtons();
            }
            if (slotView != null && nudgedReelIndex >= 0)
            {
                slotView.ResetReelPosition(nudgedReelIndex);
            }
        }

        wasAutoPlayingBeforeFreeSpins = false;
        if (isAutoPlaying)
        {
            StopAutoPlay();
        }

        // FF: Task 2e - Stop SlotView win animations and hide win boxes on disconnect
        if (slotView != null)
        {
            slotView.StopAllWinAnimations();
            // FF: Task 3 - Reset spin visuals on disconnect
            slotView.ResetSpinVisuals();
        }

        currentState = GameState.Idle;
        // Note: The disconnection popup is shown by SocketIOManager.OnSocketDisconnected()
        // to avoid duplicates. GameManager only cleans up state here.
    }

    internal void ExitGame()
    {
        socketManager.CloseSocket();

    }

    #endregion

    #region Helper Methods

    internal double GetTotalPay()
    {
        double divisor = (gameConfig != null && gameConfig.creditDivisor > 0) ? gameConfig.creditDivisor : 20; // FF: Task 2d
        return currentBetAmount * divisor;
    }

    internal bool CanAffordBet()
    {
        double totalPay = GetTotalPay();
        return playerData.balance >= totalPay;
    }

    internal bool IsSpinning()
    {
        return currentState == GameState.Spinning || currentState == GameState.Stopping;
    }

    /// <summary>
    /// Returns true if at least one scatter symbol appears anywhere in the result matrix.
    /// Uses the server-configured scatterSymbolId (FF: Bonus = 4) as the reference ID.
    /// </summary>
    private bool ResultMatrixHasScatter(List<List<int>> matrix)
    {
        if (matrix == null) return false;

        int scatterId = gameConfig != null ? gameConfig.scatterSymbolId : -1;

        foreach (var col in matrix)
        {
            if (col == null) continue;
            foreach (int sym in col)
            {
                if (sym == scatterId) return true;
            }
        }

        return false;
    }

    #endregion
}