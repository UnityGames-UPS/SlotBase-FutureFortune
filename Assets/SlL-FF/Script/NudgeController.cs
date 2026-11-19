using System;
using System.Collections.Generic;
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
    }

    private void SetupButtons()
    {
        if (raiseButtons != null)
        {
            for (int i = 0; i < raiseButtons.Length; i++)
            {
                int btnIndex = i;
                int reelIndex = (raiseReelIndices != null && btnIndex < raiseReelIndices.Length)
                    ? raiseReelIndices[btnIndex]
                    : btnIndex;

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
                int reelIndex = (lowerReelIndices != null && btnIndex < lowerReelIndices.Length)
                    ? lowerReelIndices[btnIndex]
                    : btnIndex;

                if (lowerButtons[btnIndex] != null)
                {
                    lowerButtons[btnIndex].onClick.AddListener(() => OnNudgeClicked(reelIndex, "lower"));
                }
            }
        }
    }

    /// <summary>
    /// FF: Task 1 - Called when spin stops with awaitingNudge == true.
    /// Evaluates which nudge moves result in a 3+ identical center-line win from reel 0.
    /// Only enables valid buttons, disables/hides invalid ones.
    /// Fallback: if no valid move found, logs warning and enables all 6.
    /// </summary>
    public void EnableNudgeMode(List<int> above = null, List<int> below = null)
    {
        validNudges.Clear();

        if (nudgeContainer != null)
        {
            nudgeContainer.SetActive(true);
        }

        if (slotView == null)
        {
            if (gameManager != null && gameManager.SlotView != null)
                slotView = gameManager.SlotView;
            else
                slotView = FindFirstObjectByType<SlotView>();
        }

        List<List<int>> currentMatrix = slotView != null ? slotView.GetCurrentDisplayMatrix() : null;

        // FF: Fallback check if matrix or above/below are missing
        if (currentMatrix == null || currentMatrix.Count < 3 || above == null || below == null)
        {
            Debug.LogWarning("[NudgeController] FF: Matrix or above/below data unavailable; enabling all 6 buttons as fallback.");
            EnableAllButtons();
            return;
        }

        // FF: Build current center row (row 1 on 5 reels)
        int[] currentCenter = new int[5];
        for (int c = 0; c < 5; c++)
        {
            if (c < currentMatrix.Count && currentMatrix[c] != null && currentMatrix[c].Count > 1)
            {
                currentCenter[c] = currentMatrix[c][1];
            }
            else
            {
                currentCenter[c] = -1;
            }
        }

        bool[] raiseValid = new bool[3];
        bool[] lowerValid = new bool[3];

        // FF: Evaluate reels 0..2 for Raise and Lower moves
        for (int i = 0; i < 3; i++)
        {
            int rRaise = (raiseReelIndices != null && i < raiseReelIndices.Length) ? raiseReelIndices[i] : i;
            int rLower = (lowerReelIndices != null && i < lowerReelIndices.Length) ? lowerReelIndices[i] : i;

            // Check "raise" for reel rRaise: reel moves UP -> symbol from below[rRaise] enters center
            if (rRaise >= 0 && rRaise < 5 && rRaise < below.Count)
            {
                int[] testRaise = (int[])currentCenter.Clone();
                testRaise[rRaise] = below[rRaise];
                if (IsCenterRowWinning(testRaise))
                {
                    raiseValid[i] = true;
                    validNudges.Add(new ValidNudge(rRaise, "raise"));
                }
            }

            // Check "lower" for reel rLower: reel moves DOWN -> symbol from above[rLower] enters center
            if (rLower >= 0 && rLower < 5 && rLower < above.Count)
            {
                int[] testLower = (int[])currentCenter.Clone();
                testLower[rLower] = above[rLower];
                if (IsCenterRowWinning(testLower))
                {
                    lowerValid[i] = true;
                    validNudges.Add(new ValidNudge(rLower, "lower"));
                }
            }
        }

        // FF: Task 1 Fallback - If no move is valid, log warning and enable all 6
        if (validNudges.Count == 0)
        {
            Debug.LogWarning("[NudgeController] FF: No valid nudge move found; falling back to enabling all 6.");
            EnableAllButtons();
            return;
        }

        // FF: Enable only valid buttons; hide/disable the rest
        ApplyButtonVisibility(raiseValid, lowerValid);
    }

    /// <summary>
    /// FF: Task 1 - A move is valid if the center row has 3+ identical symbols starting from reel 0
    /// (left to right, center row only, reels 3-4 included for 4/5 of a kind).
    /// </summary>
    private bool IsCenterRowWinning(int[] center)
    {
        if (center == null || center.Length < 3) return false;
        int firstSym = center[0];
        if (firstSym < 0) return false;

        // Starting from reel 0: reels 0, 1, 2 must all match firstSym
        return (center[1] == firstSym && center[2] == firstSym);
    }

    /// <summary>
    /// FF: Task 1 & Task 3 - Returns first valid nudge move for Autoplay or fallback.
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
    /// FF: Disables all 6 nudge buttons instantly and resets them to inactive state.
    /// </summary>
    public void DisableAllButtons()
    {
        validNudges.Clear();
        bool[] allFalse = new bool[] { false, false, false };
        ApplyButtonVisibility(allFalse, allFalse);

        if (nudgeContainer != null)
        {
            nudgeContainer.SetActive(false);
        }
    }

    /// <summary>
    /// FF: Fallback - Enables and shows all 6 buttons.
    /// </summary>
    private void EnableAllButtons()
    {
        bool[] allTrue = new bool[] { true, true, true };
        ApplyButtonVisibility(allTrue, allTrue);
    }

    /// <summary>
    /// FF: Applies visibility and interactability to raise and lower buttons.
    /// </summary>
    private void ApplyButtonVisibility(bool[] raiseActive, bool[] lowerActive)
    {
        // 3 Raise Buttons
        if (raiseButtons != null)
        {
            for (int i = 0; i < raiseButtons.Length; i++)
            {
                var btn = raiseButtons[i];
                if (btn == null) continue;

                bool active = raiseActive != null && i < raiseActive.Length && raiseActive[i];
                btn.interactable = active;
                btn.gameObject.SetActive(active);
            }
        }

        // 3 Lower Buttons
        if (lowerButtons != null)
        {
            for (int i = 0; i < lowerButtons.Length; i++)
            {
                var btn = lowerButtons[i];
                if (btn == null) continue;

                bool active = lowerActive != null && i < lowerActive.Length && lowerActive[i];
                btn.interactable = active;
                btn.gameObject.SetActive(active);
            }
        }
    }

    private void OnNudgeClicked(int reelIndex, string direction)
    {
        // FF: Immediately disable all 6 buttons (debouncing prevents spam-tapping)
        DisableAllButtons();

        // Play audio feedback
        AudioManager.Instance?.PlayButton();

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
