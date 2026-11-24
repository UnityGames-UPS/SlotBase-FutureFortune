using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 6-Button Interactive Nudge Controller for Future Fortune (SL-FF)
/// Manages 3 Raise (Up) and 3 Lower (Down) buttons across active reels.
/// </summary>
public class NudgeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SlotView slotView; // FF: Direct or cached reference to SlotView

    [Header("6 Nudge Buttons (3 Top / 3 Bottom)")]
    [Tooltip("The 3 Raise buttons positioned above the reels (e.g., above Reel 0, 1, 2)")]
    [SerializeField] private Button[] raiseButtons = new Button[3];

    [Tooltip("Target reel index for each Raise button (Default: 0, 1, 2)")]
    [SerializeField] private int[] raiseReelIndices = new int[] { 0, 1, 2 };

    [Tooltip("The 3 Lower buttons positioned below the reels (e.g., below Reel 0, 1, 2)")]
    [SerializeField] private Button[] lowerButtons = new Button[3];

    [Tooltip("Target reel index for each Lower button (Default: 0, 1, 2)")]
    [SerializeField] private int[] lowerReelIndices = new int[] { 0, 1, 2 };

    [Header("Valid Move Pulse")]
    [SerializeField] private float pulseScale = 1.5f;
    [SerializeField] private float pulseHalfDuration = 0.3f; // 1 -> 1.5 in 0.3 s, back in 0.3 s

    [Header("Optional UI Elements")]
    [Tooltip("Parent GameObject or container for the nudge controls to show/hide during nudge phase")]
    [SerializeField] private GameObject nudgeContainer;

    // FF: Track currently valid nudge moves
    private readonly List<ValidNudge> validNudges = new List<ValidNudge>();

    public struct ValidNudge
    {
        public int reel;
        public string direction;

        public ValidNudge(int r, string d)
        {
            reel = r;
            direction = d;
        }
    }

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        // FF: Cache SlotView reference
        if (slotView == null)
        {
            if (gameManager != null && gameManager.SlotView != null)
            {
                slotView = gameManager.SlotView;
            }
            else
            {
                slotView = FindFirstObjectByType<SlotView>();
            }
        }

        SetupButtons();
        DisableAllButtons();

#if UNITY_EDITOR
        RunSelfTest();
#endif
    }

    private void OnDisable()
    {
        // FF: Kill any active tweens to avoid DOTween warnings on disable
        KillAllTweensAndResetScale();
    }

    private void OnDestroy()
    {
        // FF: Kill any active tweens on destroy
        KillAllTweensAndResetScale();
    }

    // FF: Helper for button-to-reel index mapping
    private int GetReelIndex(int[] indices, int buttonIndex) =>
        (indices != null && buttonIndex < indices.Length) ? indices[buttonIndex] : buttonIndex;

    private void SetupButtons()
    {
        if (raiseButtons != null)
        {
            for (int i = 0; i < raiseButtons.Length; i++)
            {
                int btnIndex = i;
                int reelIndex = GetReelIndex(raiseReelIndices, btnIndex);

                if (raiseButtons[btnIndex] != null)
                {
                    raiseButtons[btnIndex].onClick.AddListener(() => OnNudgeClicked(reelIndex, "raise"));
                }
            }
        }

        if (lowerButtons != null)
        {
            for (int i = 0; i < lowerButtons.Length; i++)
            {
                int btnIndex = i;
                int reelIndex = GetReelIndex(lowerReelIndices, btnIndex);

                if (lowerButtons[btnIndex] != null)
                {
                    lowerButtons[btnIndex].onClick.AddListener(() => OnNudgeClicked(reelIndex, "lower"));
                }
            }
        }
    }

    /// <summary>
    /// FF: Called when spin stops with awaitingNudge == true.
    /// ALL 6 buttons are visible and interactable.
    /// Valid move(s) receive a looping pulse animation (1 -> pulseScale -> 1).
    /// Fallback: if no valid move found, logs warning and leaves all 6 visible without pulse.
    /// </summary>
    public void EnableNudgeMode(List<int> above = null, List<int> below = null, List<ServerNudgeOption> nudgeOptions = null)
    {
        validNudges.Clear();

        if (nudgeContainer != null)
        {
            nudgeContainer.SetActive(true);
        }

        // FF: Play nudge show SFX when nudge buttons become available
        AudioManager.Instance?.PlayNudgeShow();

        bool[] raiseValid = new bool[3];
        bool[] lowerValid = new bool[3];

        // FF: If server provided explicit nudgeOptions, use them directly
        if (nudgeOptions != null && nudgeOptions.Count > 0)
        {
            foreach (var opt in nudgeOptions)
            {
                if (opt == null) continue;
                validNudges.Add(new ValidNudge(opt.reel, opt.direction));

                for (int i = 0; i < 3; i++)
                {
                    int rRaise = GetReelIndex(raiseReelIndices, i);
                    int rLower = GetReelIndex(lowerReelIndices, i);

                    if (rRaise == opt.reel && string.Equals(opt.direction, "raise", StringComparison.OrdinalIgnoreCase))
                    {
                        raiseValid[i] = true;
                    }
                    if (rLower == opt.reel && string.Equals(opt.direction, "lower", StringComparison.OrdinalIgnoreCase))
                    {
                        lowerValid[i] = true;
                    }
                }
            }

            if (validNudges.Count > 0)
            {
                ApplyButtonStates(raiseValid, lowerValid);
                return;
            }
        }

        if (slotView == null)
        {
            if (gameManager != null && gameManager.SlotView != null)
                slotView = gameManager.SlotView;
            else
                slotView = FindFirstObjectByType<SlotView>();
        }

        List<List<int>> currentMatrix = slotView != null ? slotView.GetCurrentDisplayMatrix() : null;

        // FF: Fallback check if matrix is missing - show all 6, none pulsing
        if (currentMatrix == null || currentMatrix.Count < 3)
        {
            Debug.LogWarning("[NudgeController] FF: Matrix unavailable; enabling all 6 buttons as fallback.");
            ApplyButtonStates(raiseValid, lowerValid);
            return;
        }

        // FF: Physical movement:
        // lower on reel r -> reel moves DOWN -> visible TOP symbol (matrix[r][0]) goes to center (row 1)
        // raise on reel r -> reel moves UP   -> visible BOTTOM symbol (matrix[r][2]) goes to center (row 1)
        List<ValidNudge> evaluated = EvaluateValidMovesForMatrix(currentMatrix);
        foreach (var vn in evaluated)
        {
            validNudges.Add(vn);
            for (int i = 0; i < 3; i++)
            {
                int rRaise = GetReelIndex(raiseReelIndices, i);
                int rLower = GetReelIndex(lowerReelIndices, i);

                if (rRaise == vn.reel && vn.direction == "raise")
                    raiseValid[i] = true;
                if (rLower == vn.reel && vn.direction == "lower")
                    lowerValid[i] = true;
            }
        }

        // FF: Fallback - If no move is valid, log warning and show all 6 without pulse
        if (validNudges.Count == 0)
        {
            Debug.LogWarning("[NudgeController] FF: No valid nudge move found; falling back to enabling all 6.");
            ApplyButtonStates(raiseValid, lowerValid);
            return;
        }

        // FF: All 6 visible & interactable; valid buttons pulse
        ApplyButtonStates(raiseValid, lowerValid);
    }

    /// <summary>
    /// FF: Evaluates valid moves for a client matrix[col][row] (reels 0..4, rows 0..2).
    /// lower on reel r -> testCenter[r] = matrix[r][0] (top visible)
    /// raise on reel r -> testCenter[r] = matrix[r][2] (bottom visible)
    /// Valid = 3+ identical on center row from reel 0 (reels 3-4 count toward 4/5).
    /// </summary>
    public static List<ValidNudge> EvaluateValidMovesForMatrix(List<List<int>> matrix)
    {
        var result = new List<ValidNudge>();
        if (matrix == null || matrix.Count < 3) return result;

        int[] currentCenter = new int[5];
        for (int c = 0; c < 5; c++)
        {
            currentCenter[c] = (c < matrix.Count && matrix[c] != null && matrix[c].Count > 1) ? matrix[c][1] : -1;
        }

        for (int r = 0; r < 3; r++)
        {
            // Raise on reel r: reel moves UP -> visible bottom symbol (matrix[r][2]) goes to center
            if (r < matrix.Count && matrix[r] != null && matrix[r].Count > 2)
            {
                int[] testRaise = (int[])currentCenter.Clone();
                testRaise[r] = matrix[r][2];
                if (CheckCenterRowWin(testRaise))
                {
                    result.Add(new ValidNudge(r, "raise"));
                }
            }

            // Lower on reel r: reel moves DOWN -> visible top symbol (matrix[r][0]) goes to center
            if (r < matrix.Count && matrix[r] != null && matrix[r].Count > 0)
            {
                int[] testLower = (int[])currentCenter.Clone();
                testLower[r] = matrix[r][0];
                if (CheckCenterRowWin(testLower))
                {
                    result.Add(new ValidNudge(r, "lower"));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// FF: A move is valid if the center row has 3+ identical symbols starting from reel 0
    /// (left to right, center row only, reels 3-4 included for 4/5 of a kind).
    /// </summary>
    private static bool CheckCenterRowWin(int[] center)
    {
        if (center == null || center.Length < 3) return false;
        int firstSym = center[0];
        if (firstSym < 0) return false;

        // Starting from reel 0: reels 0, 1, 2 must all match firstSym
        return (center[1] == firstSym && center[2] == firstSym);
    }

#if UNITY_EDITOR
    [ContextMenu("FF: Run Nudge Self-Test")]
    public void RunSelfTest()
    {
        // Test 1: center [5,0,5,8,10] with matrix[1][2]==5 -> only raise reel 1
        var m1 = new List<List<int>>
        {
            new List<int> { 1, 5, 2 },   // reel 0: top 1, center 5, bot 2
            new List<int> { 8, 0, 5 },   // reel 1: top 8, center 0, bot 5 (raise brings 5 to center)
            new List<int> { 9, 5, 10 },  // reel 2: top 9, center 5, bot 10
            new List<int> { 0, 8, 0 },   // reel 3: center 8
            new List<int> { 0, 10, 0 }   // reel 4: center 10
        };
        var valid1 = EvaluateValidMovesForMatrix(m1);
        bool t1Passed = valid1.Count == 1 && valid1[0].reel == 1 && valid1[0].direction == "raise";

        // Test 2: center [3,3,5,8,10] with matrix[2][0]==3 -> only lower reel 2
        var m2 = new List<List<int>>
        {
            new List<int> { 1, 3, 2 },   // reel 0: top 1, center 3, bot 2
            new List<int> { 8, 3, 5 },   // reel 1: top 8, center 3, bot 5
            new List<int> { 3, 5, 10 },  // reel 2: top 3, center 5, bot 10 (lower brings 3 to center)
            new List<int> { 0, 8, 0 },   // reel 3: center 8
            new List<int> { 0, 10, 0 }   // reel 4: center 10
        };
        var valid2 = EvaluateValidMovesForMatrix(m2);
        bool t2Passed = valid2.Count == 1 && valid2[0].reel == 2 && valid2[0].direction == "lower";

        if (t1Passed && t2Passed)
        {
            Debug.Log("<color=green>[NudgeController] FF: Self-Test PASSED! (T1: only raise reel 1, T2: only lower reel 2)</color>");
        }
        else
        {
            Debug.LogError($"[NudgeController] FF: Self-Test FAILED! T1 passed: {t1Passed}, T2 passed: {t2Passed}");
        }
    }
#endif

    /// <summary>
    /// FF: Returns first valid nudge move for Autoplay or fallback.
    /// </summary>
    public bool TryGetFirstValidNudge(out int reel, out string direction)
    {
        if (validNudges != null && validNudges.Count > 0)
        {
            reel = validNudges[0].reel;
            direction = validNudges[0].direction;
            return true;
        }

        reel = 0;
        direction = "lower";
        return false;
    }

    /// <summary>
    /// FF: Kills active tweens, resets scales to Vector3.one, and disables/hides all 6 nudge buttons.
    /// </summary>
    public void DisableAllButtons()
    {
        validNudges.Clear();
        KillAllTweensAndResetScale();

        if (raiseButtons != null)
        {
            for (int i = 0; i < raiseButtons.Length; i++)
            {
                var btn = raiseButtons[i];
                if (btn == null) continue;
                btn.interactable = false;
                btn.gameObject.SetActive(false);
            }
        }

        if (lowerButtons != null)
        {
            for (int i = 0; i < lowerButtons.Length; i++)
            {
                var btn = lowerButtons[i];
                if (btn == null) continue;
                btn.interactable = false;
                btn.gameObject.SetActive(false);
            }
        }

        if (nudgeContainer != null)
        {
            nudgeContainer.SetActive(false);
        }
    }

    /// <summary>
    /// FF: Kills active DOTween tweens on all nudge buttons and resets their scale to Vector3.one.
    /// </summary>
    private void KillAllTweensAndResetScale()
    {
        if (raiseButtons != null)
        {
            for (int i = 0; i < raiseButtons.Length; i++)
            {
                if (raiseButtons[i] != null)
                {
                    raiseButtons[i].transform.DOKill();
                    raiseButtons[i].transform.localScale = Vector3.one;
                }
            }
        }

        if (lowerButtons != null)
        {
            for (int i = 0; i < lowerButtons.Length; i++)
            {
                if (lowerButtons[i] != null)
                {
                    lowerButtons[i].transform.DOKill();
                    lowerButtons[i].transform.localScale = Vector3.one;
                }
            }
        }
    }

    /// <summary>
    /// FF: Shows all 6 buttons and makes them interactable.
    /// Valid buttons pulse continuously (1 -> pulseScale -> 1).
    /// Invalid buttons stay at scale 1 without pulse.
    /// </summary>
    private void ApplyButtonStates(bool[] raiseValid, bool[] lowerValid)
    {
        KillAllTweensAndResetScale();

        // 3 Raise Buttons
        if (raiseButtons != null)
        {
            for (int i = 0; i < raiseButtons.Length; i++)
            {
                var btn = raiseButtons[i];
                if (btn == null) continue;

                btn.gameObject.SetActive(true);
                btn.interactable = true;

                bool isValid = raiseValid != null && i < raiseValid.Length && raiseValid[i];
                if (isValid)
                {
                    btn.transform.DOScale(pulseScale, pulseHalfDuration)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo);
                }
            }
        }

        // 3 Lower Buttons
        if (lowerButtons != null)
        {
            for (int i = 0; i < lowerButtons.Length; i++)
            {
                var btn = lowerButtons[i];
                if (btn == null) continue;

                btn.gameObject.SetActive(true);
                btn.interactable = true;

                bool isValid = lowerValid != null && i < lowerValid.Length && lowerValid[i];
                if (isValid)
                {
                    btn.transform.DOScale(pulseScale, pulseHalfDuration)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo);
                }
            }
        }
    }

    private void OnNudgeClicked(int reelIndex, string direction)
    {
        // FF: Immediately kill tweens, reset scale, and disable all 6 buttons (debouncing prevents spam-tapping)
        DisableAllButtons();

        // FF: Play nudge tap sound
        AudioManager.Instance?.PlayNudgeTap();

        // Trigger Nudge via GameManager
        if (gameManager != null)
        {
            gameManager.ExecuteNudge(reelIndex, direction);
        }
        else
        {
            Debug.LogError("[NudgeController] GameManager reference is missing!");
        }
    }
}
