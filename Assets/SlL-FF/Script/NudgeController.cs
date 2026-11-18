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

    [Header("6 Nudge Buttons (3 Top / 3 Bottom)")]
    [Tooltip("The 3 Raise buttons positioned above the reels (e.g., above Reel 0, 1, 2)")]
    [SerializeField] private Button[] raiseButtons = new Button[3];

    [Tooltip("Target reel index for each Raise button (Default: 0, 1, 2)")]
    [SerializeField] private int[] raiseReelIndices = new int[] { 0, 1, 2 };

    [Tooltip("The 3 Lower buttons positioned below the reels (e.g., below Reel 0, 1, 2)")]
    [SerializeField] private Button[] lowerButtons = new Button[3];

    [Tooltip("Target reel index for each Lower button (Default: 0, 1, 2)")]
    [SerializeField] private int[] lowerReelIndices = new int[] { 0, 1, 2 };

    /*[Header("Optional Frame Sprites (Visual Feedback)")]
    [Tooltip("raiseframe.png")]
    [SerializeField] private Sprite raiseNormalSprite;
    [Tooltip("raiseframegreen.png")]
    [SerializeField] private Sprite raiseActiveGreenSprite;
    [Tooltip("lowerframe.png")]
    [SerializeField] private Sprite lowerNormalSprite;
    [Tooltip("lowerframegreen.png")]
    [SerializeField] private Sprite lowerActiveGreenSprite;*/

    [Header("Optional UI Elements")]
    [Tooltip("Parent GameObject or container for the nudge controls to show/hide during nudge phase")]
    [SerializeField] private GameObject nudgeContainer;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
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
    /// Called when the spin stops with awaitingNudge == true.
    /// Activates the 6 buttons and lights them up green.
    /// </summary>
    public void EnableNudgeMode(List<int> above = null, List<int> below = null)
    {
        if (nudgeContainer != null)
        {
            nudgeContainer.SetActive(true);
        }

        SetButtonsState(true);
    }

    /// <summary>
    /// Disables all 6 nudge buttons instantly and resets them to inactive state.
    /// </summary>
    public void DisableAllButtons()
    {
        SetButtonsState(false);

        if (nudgeContainer != null)
        {
            nudgeContainer.SetActive(false);
        }
    }

    private void SetButtonsState(bool active)
    {
        // 3 Raise Buttons
        if (raiseButtons != null)
        {
            for (int i = 0; i < raiseButtons.Length; i++)
            {
                var btn = raiseButtons[i];
                if (btn == null) continue;

                btn.interactable = active;
                btn.gameObject.SetActive(active);

               /* // Swap to green active frame when active
                if (btn.image != null)
                {
                    if (active && raiseActiveGreenSprite != null)
                    {
                        btn.image.sprite = raiseActiveGreenSprite;
                    }
                    else if (!active && raiseNormalSprite != null)
                    {
                        btn.image.sprite = raiseNormalSprite;
                    }
                }*/
            }
        }

        // 3 Lower Buttons
        if (lowerButtons != null)
        {
            for (int i = 0; i < lowerButtons.Length; i++)
            {
                var btn = lowerButtons[i];
                if (btn == null) continue;

                btn.interactable = active;
                btn.gameObject.SetActive(active);

                // Swap to green active frame when active
               /* if (btn.image != null)
                {
                    if (active && lowerActiveGreenSprite != null)
                    {
                        btn.image.sprite = lowerActiveGreenSprite;
                    }
                    else if (!active && lowerNormalSprite != null)
                    {
                        btn.image.sprite = lowerNormalSprite;
                    }
                }*/
            }
        }
    }

    private void OnNudgeClicked(int reelIndex, string direction)
    {
        // 1. Immediately disable all 6 buttons (debouncing prevents spam)
        DisableAllButtons();

        // 2. Play audio feedback
        AudioManager.Instance?.PlayButton();

        // 3. Trigger Nudge via GameManager
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
