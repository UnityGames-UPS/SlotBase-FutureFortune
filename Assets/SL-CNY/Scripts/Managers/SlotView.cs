using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Spine.Unity;

public class SlotView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Future Fortune Symbol Sprites - IDs match server initData")]
    [SerializeField] private Sprite spriteDiamondShot;       // ID: 0  DiamondShot
    [SerializeField] private Sprite spriteDiamond;           // ID: 1  Diamond
    [SerializeField] private Sprite spritePilot;             // ID: 2  Pilot (blonde)
    [SerializeField] private Sprite spriteSeven3;            // ID: 3  777
    [SerializeField] private Sprite spriteBonus;             // ID: 4  Bonus / Shooting Star (scatter)
    [SerializeField] private Sprite spriteSeven2;            // ID: 5  77
    [SerializeField] private Sprite spriteSeven;             // ID: 6  7
    [SerializeField] private Sprite spriteScout;             // ID: 7  Scout (green-hair)
    [SerializeField] private Sprite spriteBar5;              // ID: 8  B5R
    [SerializeField] private Sprite spriteBar3;              // ID: 9  B3R
    [SerializeField] private Sprite spriteBar;               // ID: 10 BAR

    private const int SymbolCount = 11;

    // Internal array built from named sprites
    private Sprite[] symbolSprites;

    // Win animation sprite sequences (fallback when a symbol has no Spine)
    private List<Sprite>[] animationSpriteArrays;

    [Header("Reel Containers")]
    [SerializeField] private Transform[] reelTransforms;

    [Header("Reel Images - 7 images per reel")]
    [SerializeField] private List<ReelImages> reelImagesList;

    [Header("Spin Settings")]
    [SerializeField] private float symbolHeight = 100f;
    [SerializeField] private float spinSpeed = 2000f;
    [SerializeField] private float reelStartStagger = 0.08f;
    [SerializeField] private float reelStopStagger = 0.12f;

    [Header("Animation Settings - Casino Style")]
    [SerializeField] private float anticipationUpDistance = 20f;
    [SerializeField] private float anticipationUpDuration = 0.12f;

    [Header("Stop Animation Settings")]
    [SerializeField] private float stopOvershootDistance = 50f;
    [SerializeField] private float stopOvershootDuration = 0.20f;
    [SerializeField] private float stopSettleDuration = 0.30f;

    [Header("Quick Spin Settings")]
    [SerializeField] private float quickStopStagger = 0.06f;
    [SerializeField] private float quickStopOvershoot = 20f;
    [SerializeField] private float quickStopDuration = 0.2f;
    [SerializeField] private int minSpinCyclesBeforeStop = 3;

    // FF: Turbo - all reels land together (reference: ~0.25 s spin, no stagger)
    [Header("Turbo Stop")]
    [SerializeField] private float turboStopStagger = 0f;

    // FF: Motion blur while spinning - blurred copy of each symbol (index = symbol ID 0-10)
    [Header("Spin Blur")]
    [SerializeField] private bool useSpinBlur = true;
    [SerializeField] private Sprite[] blurSymbolSprites = new Sprite[11];
    private bool[] reelBlurring = new bool[5];
    private Dictionary<Sprite, int> sharpSpriteToId;

    // FF: Debug - logs spin start and each reel stop time
    [SerializeField] private bool debugSpinTiming = false;
    private float spinStartTime;

    [Header("Win Animation Settings")]
    [SerializeField] private float winSymbolLoopDuration = 1.5f;
    [SerializeField] private int winSymbolLoopCount = 3;
    [Tooltip("Delay between enabling winBox overlay and starting the ImageAnimation - for sync timing")]
    [SerializeField] private float winLineBoxToAnimationDelay = 0.05f;

    [Header("Phase 1 Total Win Presentation")]
    [Header("FF: Win Amount Text (one shared text, e.g. SlotObject/WinLineText)")]
    [UnityEngine.Serialization.FormerlySerializedAs("phase1TotalWinText")]
    [SerializeField] private TMPro.TMP_Text winAmountText;
    [SerializeField] private float winAmountShowTime = 2f; // seconds before the text is cleared
    private Coroutine winAmountClearRoutine;

    [Header("Win Box Overlays — Col 0..4  (each has 3 rows: 0=top .. 2=bottom)")]
    [SerializeField] private ColumnOverlays[] winBoxColumns = new ColumnOverlays[5];

    [Header("Win Animation Objects — Col 0..4  (each has 3 rows, contains ImageAnimation component)")]
    [Tooltip("GameObject references for win animations. Each should have an ImageAnimation component attached.")]
    [SerializeField] private ColumnOverlays[] winAnimationColumns = new ColumnOverlays[5];


    [Header("Symbol Info Card")]
    [SerializeField] private SymbolInfoCard symbolInfoCard;

    // FF: Task 6 - Grid layout switch cover image
    [Header("FF: Grid Layout Switch")]
    [SerializeField] private GameObject last2SlotCoverImage;


    [Header("FF: Nudge Animation")]
    [SerializeField] private float nudgeSlideDuration = 0.7f;   // time to slide one symbol
    [SerializeField] private float nudgeSettleDuration = 0.25f; // time to settle back from the overshoot
    [SerializeField] private float nudgeOvershoot = 8f;         // px past the target (0 = no bounce)
    [SerializeField] private Ease nudgeSlideEase = Ease.InOutSine;

    // FF: Task 10 - Spine win animations
    // Folder -> ID mapping:
    // 0: D_S_Character
    // 1: D_Character
    // 2: Pilot = Girl_Character or Gun_Character (blonde)
    // 3: 777
    // 4: B_Robot
    // 5: 77
    // 6: 7
    // 7: Scout = the other character (green hair)
    // 8: Bar_5
    // 9: Bar_3
    // 10: Bar
    [Header("FF: Spine Win Animations")]
    [SerializeField] private SkeletonDataAsset[] symbolSkeletons = new SkeletonDataAsset[11];
    // FF: Per-symbol Spine scaling and positioning
    [Header("FF: Symbol Spine Scale & Offset")]
    [SerializeField] private float[] symbolSpineScale = new float[11] {
        0.40f, 0.40f, 0.40f, 0.40f, 0.40f, 0.40f, 0.40f, 0.40f, 0.40f, 0.40f, 0.40f
    };
    [SerializeField] private Vector2[] symbolSpineOffset = new Vector2[11] {
        new Vector2(0f, 5f),   // 0: Diamond Shot
        new Vector2(0f, -7f),  // 1: Diamond
        new Vector2(0f, 0f),   // 2: Pilot
        new Vector2(0f, 0f),   // 3: 777
        new Vector2(0f, -2f),  // 4: Bonus Robot
        new Vector2(0f, 0f),   // 5: 77
        new Vector2(0f, 0f),   // 6: 7
        new Vector2(0f, 0f),   // 7: Scout
        new Vector2(0f, -7f),  // 8: Bar 5
        new Vector2(0f, -6f),  // 9: Bar 3
        new Vector2(0f, -3f)   // 10: Bar
    };
    [System.Obsolete("FF: WinBox W_Frame child handles win frame presentation now.")]
    [SerializeField] private SkeletonDataAsset winFrameSkeleton;
    [SerializeField] private SpineAnimController[] cellSpines = new SpineAnimController[15]; // col * 3 + row
    [System.Obsolete("FF: WinBox W_Frame child handles win frame presentation now.")]
    [SerializeField] private SpineAnimController[] cellFrameSpines = new SpineAnimController[15]; // col * 3 + row

    // FF: Task 1 - Cached W_Frame SpineAnimControllers inside WinBox prefabs (5 cols x 3 rows)
    private SpineAnimController[,] winBoxFrameSpines = new SpineAnimController[5, 3];
    private bool winBoxFrameSpinesCached = false;


    private float middlePosition = 0f;
    private float cycleDistance;


    private List<Tween> spinTweens = new List<Tween>();
    private List<Tween> winTweens = new List<Tween>();
    private List<int> reelCycleCount = new List<int>();
    private Coroutine winAnimationCoroutine;


    internal List<List<int>> currentDisplayMatrix;
    private List<int> currentAbove;
    private List<int> currentBelow;
    private int bonusLandCount = 0; // FF: Count of reels landing with Bonus (ID 4) so far in current spin

    private bool isSpinning;

    #region Initialization

    private void Awake()
    {
        BuildSymbolSpriteArray();
        InitializeReels();
        CacheWinBoxFrameSpines(); // FF: Task 1 - Cache win box W_Frame SpineAnimControllers
    }

    private void Start()
    {
        if (symbolSprites == null || symbolSprites.Length == 0)
        {
            BuildSymbolSpriteArray();
        }
        DisableAllOverlays();
        SetupSymbolButtons();
        // FF: Task 1 - Initially hide top/bottom cells of reels 3-4 in base game
        SetAllSideCellsVisible(false);
    }

    private void DisableAllOverlays()
    {
        DisableWinBoxColumns();
        DisableColumns(winAnimationColumns);
        ClearWinAmountText();
        if (symbolInfoCard) symbolInfoCard.HideCard();
    }

    private void SetupSymbolButtons()
    {
        if (reelImagesList == null) return;
        for (int col = 0; col < reelImagesList.Count; col++)
        {
            var reel = reelImagesList[col];
            if (reel == null || reel.images == null) continue;
            int visibleStartIndex = 2;
            int rowCount = 3;
            for (int row = 0; row < rowCount; row++)
            {
                int imageIndex = visibleStartIndex + row;
                if (imageIndex < reel.images.Count && reel.images[imageIndex] != null)
                {
                    Image img = reel.images[imageIndex];
                    SymbolButtonHandler btnHandler = img.GetComponent<SymbolButtonHandler>();
                    if (btnHandler == null)
                    {
                        btnHandler = img.gameObject.AddComponent<SymbolButtonHandler>();
                    }
                    btnHandler.Init(col, row, this);
                }
            }
        }
    }

    internal void HideSymbolInfoCard()
    {
        if (symbolInfoCard != null) symbolInfoCard.HideCard();
    }

    internal void OnBetChanged()
    {
        if (symbolInfoCard != null && symbolInfoCard.gameObject.activeSelf)
        {
            symbolInfoCard.RefreshCard(gameManager);
        }
    }

    internal void OnSymbolClicked(int col, int row, RectTransform symbolRect)
    {
        if (isSpinning)
        {
            if (symbolInfoCard != null) symbolInfoCard.HideCard();
            return;
        }

        if (currentDisplayMatrix == null || col >= currentDisplayMatrix.Count || row >= currentDisplayMatrix[col].Count)
        {
            return;
        }

        int symbolId = currentDisplayMatrix[col][row];
        if (symbolInfoCard != null)
        {
            symbolInfoCard.ShowCard(symbolId, col, row, symbolRect, gameManager);
        }
    }

    private static void DisableColumns(ColumnOverlays[] cols)
    {
        if (cols == null) return;
        foreach (var col in cols)
            if (col?.rows != null)
                foreach (var go in col.rows)
                    if (go) go.SetActive(false);
    }

    private static GameObject WinBox(ColumnOverlays[] cols, int col, int row)
        => (col >= 0 && col < cols?.Length && cols[col]?.rows != null && row >= 0 && row < cols[col].rows.Length)
            ? cols[col].rows[row] : null;

    // FF: Task 1 - Caches SpineAnimController on child W_Frame of each WinBox (col 0..4, row 0..2)
    private void CacheWinBoxFrameSpines()
    {
        if (winBoxFrameSpinesCached) return;
        winBoxFrameSpinesCached = true;

        if (winBoxColumns == null) return;
        for (int col = 0; col < winBoxColumns.Length && col < 5; col++)
        {
            if (winBoxColumns[col]?.rows == null) continue;
            for (int row = 0; row < winBoxColumns[col].rows.Length && row < 3; row++)
            {
                var go = winBoxColumns[col].rows[row];
                if (go != null)
                {
                    winBoxFrameSpines[col, row] = go.GetComponentInChildren<SpineAnimController>(true);
                }
            }
        }
    }

    private SpineAnimController GetWinBoxFrameSpine(int col, int row)
    {
        if (col < 0 || col >= 5 || row < 0 || row >= 3) return null;
        if (!winBoxFrameSpinesCached) CacheWinBoxFrameSpines();
        return winBoxFrameSpines[col, row];
    }

    private void StopAllWinBoxFrameSpines()
    {
        if (!winBoxFrameSpinesCached) CacheWinBoxFrameSpines();
        for (int col = 0; col < 5; col++)
        {
            for (int row = 0; row < 3; row++)
            {
                var fs = winBoxFrameSpines[col, row];
                if (fs != null)
                {
                    fs.Stop();
                }
            }
        }
    }

    private void BuildSymbolSpriteArray()
    {
        symbolSprites = new Sprite[SymbolCount];
        symbolSprites[0] = spriteDiamondShot;
        symbolSprites[1] = spriteDiamond;
        symbolSprites[2] = spritePilot;
        symbolSprites[3] = spriteSeven3;
        symbolSprites[4] = spriteBonus;
        symbolSprites[5] = spriteSeven2;
        symbolSprites[6] = spriteSeven;
        symbolSprites[7] = spriteScout;
        symbolSprites[8] = spriteBar5;
        symbolSprites[9] = spriteBar3;
        symbolSprites[10] = spriteBar;

        for (int i = 0; i < symbolSprites.Length; i++)
        {
            if (symbolSprites[i] == null)
            {
                Debug.LogError($"[SlotView] Symbol sprite at index {i} is not assigned in inspector!");
            }
        }

        // Build the animation sprite arrays
        // Sized 13 so legacy CNY code that indexes [11] cannot go out of range; FF entries stay empty for now
        animationSpriteArrays = new List<Sprite>[Mathf.Max(SymbolCount, 13)];
    }

    private void InitializeReels()
    {
        cycleDistance = symbolHeight;
        middlePosition = 0f;

        int rowCount = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : 3;

        currentDisplayMatrix = new List<List<int>>();
        for (int col = 0; col < 5; col++)
        {
            var defaultCol = new List<int>();
            for (int r = 0; r < rowCount; r++)
            {
                defaultCol.Add(0);
            }
            currentDisplayMatrix.Add(defaultCol);
            reelCycleCount.Add(0);
        }
    }

    internal void SetInitialMatrix(List<List<int>> matrix)
    {
        if (matrix == null || matrix.Count != 5) return;

        int rowCount = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : 3;

        for (int col = 0; col < 5; col++)
        {
            if (matrix[col].Count != rowCount) return;
        }

        currentDisplayMatrix = matrix;

        for (int col = 0; col < 5; col++)
        {
            SetReelSymbols(col, matrix[col], true);
        }

        // FF: Task 1 - Hide top/bottom cells of reels 3-4 in base game
        SetAllSideCellsVisible(false);
    }

    #endregion

    #region FF: Task 1 - Side Cells Visibility

    // FF: Task 1 - Control visibility of top/bottom cells for reels 3 and 4
    private void SetSideCellsVisible(int col, bool visible)
    {
        if (col != 3 && col != 4) return;
        if (reelImagesList == null || col >= reelImagesList.Count) return;
        var reel = reelImagesList[col];
        if (reel == null || reel.images == null || reel.images.Count < 5) return;

        bool isInFreeSpins = gameManager != null && gameManager.isInFreeSpins;
        bool show = visible || isInFreeSpins;

        if (reel.images[2] != null) reel.images[2].enabled = show;
        if (reel.images[4] != null) reel.images[4].enabled = show;
    }

    internal void SetAllSideCellsVisible(bool visible)
    {
        SetSideCellsVisible(3, visible);
        SetSideCellsVisible(4, visible);
    }

    #endregion

    #region Symbol Display

    private void SetReelSymbols(int columnIndex, List<int> visibleSymbolIds, bool isInitial = false, int? aboveSymbolId = null, int? belowSymbolId = null)
    {
        if (columnIndex >= reelImagesList.Count)
        {
            Debug.LogError($"SetReelSymbols: Invalid column index {columnIndex}, max is {reelImagesList.Count - 1}");
            return;
        }

        int rowCount = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : 3;

        if (visibleSymbolIds == null || visibleSymbolIds.Count != rowCount)
        {
            Debug.LogError($"SetReelSymbols: Invalid visibleSymbolIds count {visibleSymbolIds?.Count}, expected {rowCount}");
            return;
        }

        var reel = reelImagesList[columnIndex];

        // FF: Relax check from strict != 7 to >= 5 (visibleStartIndex 2 + 3 rows) to support custom/expanded capacity (e.g. 14 images)
        if (reel.images == null || reel.images.Count < 5)
        {
            Debug.LogError($"SetReelSymbols: Reel {columnIndex} has invalid image count {reel.images?.Count}, expected at least 5");
            return;
        }

        int visibleStartIndex = 2;
        for (int row = 0; row < rowCount; row++)
        {
            int imageIndex = visibleStartIndex + row;
            if (imageIndex < reel.images.Count)
            {
                int symbolId = visibleSymbolIds[row];
                reel.images[imageIndex].sprite = GetSymbolSprite(symbolId);
            }
        }

        for (int i = 0; i < visibleStartIndex; i++)
        {
            reel.images[i].sprite = GetSymbolSprite(RandomFillerSymbolId());
        }

        for (int i = visibleStartIndex + rowCount; i < reel.images.Count; i++)
        {
            reel.images[i].sprite = GetSymbolSprite(RandomFillerSymbolId());
        }

        // Future Fortune Nudge: Buffer exact above and below symbols
        if (aboveSymbolId.HasValue && visibleStartIndex - 1 >= 0)
        {
            reel.images[visibleStartIndex - 1].sprite = GetSymbolSprite(aboveSymbolId.Value);
        }
        if (belowSymbolId.HasValue && visibleStartIndex + rowCount < reel.images.Count)
        {
            reel.images[visibleStartIndex + rowCount].sprite = GetSymbolSprite(belowSymbolId.Value);
        }

        if (isInitial && reelTransforms[columnIndex] != null)
        {
            reelTransforms[columnIndex].localPosition = new Vector3(
                reelTransforms[columnIndex].localPosition.x,
                middlePosition,
                0
            );
        }
    }

    /// <summary>
    /// Random symbol for off-screen / spinning filler. Never the Bonus (scatter),
    /// so filler can't look like a free-game trigger.
    /// </summary>
    private int RandomFillerSymbolId()
    {
        int scatterId = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.scatterSymbolId : 4;
        int id;
        do { id = Random.Range(0, SymbolCount); } while (id == scatterId);
        return id;
    }

    // FF: Spin blur helpers ------------------------------------------------
    private Sprite GetBlurSprite(int symbolId)
    {
        if (useSpinBlur && blurSymbolSprites != null && symbolId >= 0 && symbolId < blurSymbolSprites.Length
            && blurSymbolSprites[symbolId] != null)
            return blurSymbolSprites[symbolId];
        return GetSymbolSprite(symbolId);   // missing blur -> sharp, never null
    }

    // FF: Swap every image of a reel to its blurred version (spin start)
    private void SetReelBlur(int col)
    {
        if (!useSpinBlur || col < 0 || col >= reelImagesList.Count) return;
        if (sharpSpriteToId == null)
        {
            sharpSpriteToId = new Dictionary<Sprite, int>();
            if (symbolSprites == null || symbolSprites.Length == 0) BuildSymbolSpriteArray();
            for (int i = 0; i < symbolSprites.Length; i++)
                if (symbolSprites[i] != null && !sharpSpriteToId.ContainsKey(symbolSprites[i]))
                    sharpSpriteToId.Add(symbolSprites[i], i);
        }
        var imgs = reelImagesList[col].images;
        if (imgs == null) return;
        foreach (var img in imgs)
        {
            if (img == null || img.sprite == null) continue;
            if (sharpSpriteToId.TryGetValue(img.sprite, out int id)) img.sprite = GetBlurSprite(id);
        }
        if (col < reelBlurring.Length) reelBlurring[col] = true;
    }

    private bool IsTurbo => gameManager != null && gameManager.currentSpinSpeed == SpinSpeed.Turbo;

    private Sprite GetSymbolSprite(int symbolId)
    {
        if (symbolSprites == null || symbolSprites.Length == 0)
        {
            BuildSymbolSpriteArray();
        }

        if (symbolSprites == null || symbolSprites.Length == 0)
        {
            Debug.LogError("[SlotView] symbolSprites array is null or empty after BuildSymbolSpriteArray!");
            return null;
        }

        // Validate symbolId range (0-12)
        if (symbolId < 0 || symbolId >= symbolSprites.Length)
        {
            Debug.LogWarning($"[SlotView] Invalid symbolId {symbolId}, using default sprite 0. Total sprites: {symbolSprites.Length}");
            return symbolSprites[0];
        }

        if (symbolSprites[symbolId] == null)
        {
            Debug.LogError($"[SlotView] Symbol sprite for ID {symbolId} is null!");
            return symbolSprites[0];
        }

        return symbolSprites[symbolId];
    }

    #endregion

    #region Spin Animation

    internal void StartSpin()
    {
        if (isSpinning) return;

        // FF: Task 1 - Make all cells visible while spinning
        SetAllSideCellsVisible(true);

        if (symbolInfoCard != null) symbolInfoCard.HideCard();

        isSpinning = true;
        bonusLandCount = 0; // FF: Reset bonus landing count for new spin
        KillAllTweens();
        // FF: Task 10 - Stop all Spine animations on StartSpin
        StopAllCellSpines();

        DisableAllOverlays();

        for (int i = 0; i < reelCycleCount.Count; i++)
        {
            reelCycleCount[i] = 0;
        }

        spinStartTime = Time.time;   // FF: debug timing
        if (debugSpinTiming) Debug.Log("[FF SpinTiming] spin start");
        for (int col = 0; col < 5; col++)
        {
            SetReelBlur(col);   // FF: blurred symbols while spinning
            StartReelCycleWithDelay(col, col * reelStartStagger);
        }
    }

    private void StartReelCycleWithDelay(int columnIndex, float delay)
    {
        if (columnIndex >= reelTransforms.Length) return;

        Transform slotTransform = reelTransforms[columnIndex];

        Sequence startSequence = DOTween.Sequence();

        if (delay > 0)
        {
            startSequence.AppendInterval(delay);
        }

        // FF: hop only if configured (reference has no hop -> anticipationUpDistance = 0)
        if (anticipationUpDistance > 0f && anticipationUpDuration > 0f)
        {
            startSequence.Append(
                slotTransform.DOLocalMoveY(middlePosition + anticipationUpDistance, anticipationUpDuration)
                    .SetEase(Ease.OutQuad)
            );
            startSequence.Append(
                slotTransform.DOLocalMoveY(middlePosition, anticipationUpDuration * 0.5f)
                    .SetEase(Ease.InQuad)
            );
        }
        else if (delay <= 0f)
        {
            // FF: no hop and no delay -> start cycling right away (no empty sequence)
            startSequence.Kill();
            if (isSpinning) StartReelCycle(columnIndex);
            return;
        }

        startSequence.OnComplete(() => {
            if (isSpinning)
            {
                StartReelCycle(columnIndex);
            }
        });

        startSequence.Play();

        if (spinTweens.Count <= columnIndex)
            spinTweens.Add(startSequence);
        else
            spinTweens[columnIndex] = startSequence;
    }

    private void StartReelCycle(int columnIndex)
    {
        if (columnIndex >= reelTransforms.Length) return;
        if (!isSpinning) return;

        Transform slotTransform = reelTransforms[columnIndex];

        slotTransform.localPosition = new Vector3(slotTransform.localPosition.x, middlePosition, 0);

        float currentSpeed = spinSpeed;

        float cycleDuration = symbolHeight / currentSpeed;

        Sequence cycleSequence = DOTween.Sequence();

        cycleSequence.Append(
            slotTransform.DOLocalMoveY(middlePosition - symbolHeight, cycleDuration)
                .SetEase(Ease.Linear)
        );

        cycleSequence.OnComplete(() => {
            if (isSpinning)
            {
                CycleReelSymbols(columnIndex);

                if (columnIndex < reelCycleCount.Count)
                {
                    reelCycleCount[columnIndex]++;
                }

                StartReelCycle(columnIndex);
            }
        });

        cycleSequence.Play();

        if (spinTweens.Count <= columnIndex)
            spinTweens.Add(cycleSequence);
        else
            spinTweens[columnIndex] = cycleSequence;
    }

    private void CycleReelSymbols(int columnIndex)
    {
        var reel = reelImagesList[columnIndex];
        // FF: Dynamic cycle loop supporting any image count >= 5
        if (reel.images == null || reel.images.Count < 5) return;

        for (int i = reel.images.Count - 1; i > 0; i--)
        {
            if (reel.images[i] != null && reel.images[i - 1] != null)
                reel.images[i].sprite = reel.images[i - 1].sprite;
        }

        if (reel.images.Count > 0 && reel.images[0] != null)
        {
            int fillerId = RandomFillerSymbolId();
            // FF: blurred filler while this reel is still spinning
            bool blurring = columnIndex < reelBlurring.Length && reelBlurring[columnIndex];
            reel.images[0].sprite = blurring ? GetBlurSprite(fillerId) : GetSymbolSprite(fillerId);
        }
    }

    #endregion

    #region Stop Spin

    internal void StopSpin(List<List<int>> resultMatrix, System.Action onComplete, List<int> above = null, List<int> below = null)
    {
        currentAbove = above;
        currentBelow = below;

        if (!isSpinning)
        {
            currentDisplayMatrix = resultMatrix;
            for (int col = 0; col < 5; col++)
            {
                int? aboveSym = (currentAbove != null && col < currentAbove.Count) ? (int?)currentAbove[col] : null;
                int? belowSym = (currentBelow != null && col < currentBelow.Count) ? (int?)currentBelow[col] : null;
                SetReelSymbols(col, resultMatrix[col], false, aboveSym, belowSym);
            }
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(StopSpinSequence(resultMatrix, onComplete, false));
    }

    private IEnumerator StopSpinSequence(List<List<int>> resultMatrix, System.Action onComplete, bool isQuickStop)
    {
        currentDisplayMatrix = resultMatrix;

        while (true)
        {
            bool allReelsReady = true;
            for (int col = 0; col < 5; col++)
            {
                if (reelCycleCount[col] < minSpinCyclesBeforeStop)
                {
                    allReelsReady = false;
                    break;
                }
            }

            if (allReelsReady) break;
            yield return null;
        }

        // FF: turbo -> all reels land together with the short quick-stop bounce
        bool turbo = !isQuickStop && IsTurbo;
        bool useQuickBounce = isQuickStop || turbo;
        float stagger = isQuickStop ? quickStopStagger : (turbo ? turboStopStagger : reelStopStagger);
        for (int col = 0; col < 5; col++)
        {
            float delay = col * stagger;
            StartCoroutine(StopSingleReel(col, resultMatrix[col], delay, useQuickBounce));
        }

        float longestStopTime;
        if (useQuickBounce)
        {
            longestStopTime = (4 * stagger) + quickStopDuration;
        }
        else
        {
            longestStopTime = (4 * stagger) + stopOvershootDuration + stopSettleDuration;
        }

        yield return new WaitForSeconds(longestStopTime);

        isSpinning = false;
        // FF: Stop reel spin loop once all reels have stopped
        AudioManager.Instance?.StopReelSpinLoop();

        // FF: Task 1 - Ensure reels 3-4 top/bottom hidden when finished stopping in base game
        SetAllSideCellsVisible(false);

        onComplete?.Invoke();
    }

    private IEnumerator StopSingleReel(int columnIndex, List<int> targetSymbols, float delay, bool isQuickStop)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        if (columnIndex < spinTweens.Count && spinTweens[columnIndex] != null)
        {
            spinTweens[columnIndex].Kill();
        }

        Transform slotTransform = reelTransforms[columnIndex];

        int? aboveSym = (currentAbove != null && columnIndex < currentAbove.Count) ? (int?)currentAbove[columnIndex] : null;
        int? belowSym = (currentBelow != null && columnIndex < currentBelow.Count) ? (int?)currentBelow[columnIndex] : null;
        SetReelSymbols(columnIndex, targetSymbols, false, aboveSym, belowSym);   // FF: sharp sprites from here on
        if (columnIndex < reelBlurring.Length) reelBlurring[columnIndex] = false;
        if (debugSpinTiming) Debug.Log($"[FF SpinTiming] reel {columnIndex} stop at {Time.time - spinStartTime:0.00}s");

        float currentY = slotTransform.localPosition.y;
        float targetY = middlePosition;
        float offset = (currentY - targetY) % cycleDistance;
        if (offset < 0) offset += cycleDistance;

        slotTransform.localPosition = new Vector3(
            slotTransform.localPosition.x,
            targetY + offset,
            0
        );

        // FF: Play reel-stop sound with per-clip cooldown (handles turbo 5 reels stopping together)
        AudioManager.Instance?.PlayFFReelStop();

        // FF: Detect Bonus landing (Symbol ID 4)
        // Base game = center row only (targetSymbols[1]); Free games = any visible cell (targetSymbols 0, 1, 2)
        const int BONUS_SYMBOL_ID = 4;
        bool isFreeSpins = gameManager != null && gameManager.isInFreeSpins;
        bool hasBonus = false;
        if (targetSymbols != null && targetSymbols.Count >= 3)
        {
            if (isFreeSpins)
            {
                for (int r = 0; r < 3 && r < targetSymbols.Count; r++)
                {
                    if (targetSymbols[r] == BONUS_SYMBOL_ID) { hasBonus = true; break; }
                }
            }
            else
            {
                if (targetSymbols[1] == BONUS_SYMBOL_ID)
                {
                    hasBonus = true;
                }
            }
        }

        if (hasBonus)
        {
            bonusLandCount++;
            AudioManager.Instance?.PlayBonusLand(bonusLandCount);
        }

        if (isQuickStop)
        {
            Sequence quickStopSequence = DOTween.Sequence();

            quickStopSequence.Append(
                slotTransform.DOLocalMoveY(middlePosition - quickStopOvershoot, quickStopDuration * 0.3f)
                    .SetEase(Ease.OutQuad)
            );

            quickStopSequence.Append(
                slotTransform.DOLocalMoveY(middlePosition, quickStopDuration * 0.7f)
                    .SetEase(Ease.InOutQuad)
            );

            quickStopSequence.OnComplete(() => PlayStopAnimationsForColumn(columnIndex));

            spinTweens[columnIndex] = quickStopSequence;
        }
        else
        {
            Sequence stopSequence = DOTween.Sequence();

            stopSequence.Append(
                slotTransform.DOLocalMoveY(middlePosition - stopOvershootDistance, stopOvershootDuration)
                    .SetEase(Ease.OutQuad)
            );

            stopSequence.Append(
                slotTransform.DOLocalMoveY(middlePosition, stopSettleDuration)
                    .SetEase(Ease.OutBack) // FF: small spring-back rebound after the overshoot
            );

            stopSequence.OnComplete(() => PlayStopAnimationsForColumn(columnIndex));

            spinTweens[columnIndex] = stopSequence;
        }
    }

    #endregion

    #region Quick Spin

    internal void QuickStop(List<List<int>> resultMatrix, System.Action onComplete = null)
    {
        if (!isSpinning)
        {
            currentDisplayMatrix = resultMatrix;
            for (int col = 0; col < 5; col++)
            {
                if (col < reelTransforms.Length)
                {
                    SetReelSymbols(col, resultMatrix[col], false);
                    reelTransforms[col].localPosition = new Vector3(
                        reelTransforms[col].localPosition.x,
                        middlePosition,
                        0
                    );
                    PlayStopAnimationsForColumn(col);
                }
            }

            // FF: Task 1 - Ensure reels 3-4 top/bottom hidden when finished quick stop in base game
            SetAllSideCellsVisible(false);
            
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(StopSpinSequence(resultMatrix, onComplete, true));
    }

    #region Interactive Nudge


    // FF: Task 2 - Reel nudge slide + small overshoot (~0.25s total)
    internal void AnimateReelNudge(int columnIndex, string direction, System.Action onComplete = null)
    {
        if (columnIndex < 0 || columnIndex >= reelTransforms.Length)
        {
            onComplete?.Invoke();
            return;
        }

        Transform slotTransform = reelTransforms[columnIndex];
        float currentY = slotTransform.localPosition.y;

        // "lower" moves reel down (bringing 'above' symbol into view)
        // "raise" moves reel up (bringing 'below' symbol into view)
        bool isLower = !string.IsNullOrEmpty(direction) && direction.ToLower().Contains("lower");
        float shift = isLower ? -symbolHeight : symbolHeight;
        float targetY = currentY + shift;
        float overshoot = isLower ? -nudgeOvershoot : nudgeOvershoot;

        // FF: Smooth nudge - slow start and slow end (InOutSine), small soft overshoot, gentle settle.
        // Tune in the Inspector: SlotView > FF: Nudge Animation.
        slotTransform.DOKill();
        Sequence nudgeSeq = DOTween.Sequence();
        nudgeSeq.Append(slotTransform.DOLocalMoveY(targetY + overshoot, nudgeSlideDuration).SetEase(nudgeSlideEase));
        nudgeSeq.Append(slotTransform.DOLocalMoveY(targetY, nudgeSettleDuration).SetEase(Ease.OutSine));
        nudgeSeq.OnComplete(() =>
        {
            AudioManager.Instance?.PlayNudgeSlide();
            onComplete?.Invoke();
        });
    }

    // FF: Task 2 - Redraw symbols and snap reel back to middlePosition in the exact same frame (no visible jump)
    internal void SnapNudgedReel(int columnIndex, List<int> newSymbols)
    {
        if (columnIndex < 0 || columnIndex >= reelTransforms.Length) return;
        if (newSymbols == null) return;

        // Redraw the 3 visible symbols
        SetReelSymbols(columnIndex, newSymbols, false);

        // Snap reel transform position back to middlePosition (0f) in the same frame
        if (reelTransforms[columnIndex] != null)
        {
            reelTransforms[columnIndex].DOKill();
            Vector3 pos = reelTransforms[columnIndex].localPosition;
            reelTransforms[columnIndex].localPosition = new Vector3(pos.x, middlePosition, pos.z);
        }
    }

    // FF: Task 4 - Reset reel position to middlePosition on timeout or error
    internal void ResetReelPosition(int columnIndex)
    {
        if (columnIndex >= 0 && columnIndex < reelTransforms.Length && reelTransforms[columnIndex] != null)
        {
            reelTransforms[columnIndex].DOKill();
            Vector3 pos = reelTransforms[columnIndex].localPosition;
            reelTransforms[columnIndex].localPosition = new Vector3(pos.x, middlePosition, pos.z);
        }
    }

    internal void UpdateMatrixAfterNudge(List<List<int>> newMatrix)
    {
        if (newMatrix != null)
        {
            currentDisplayMatrix = newMatrix;
        }
    }

    #endregion

    #endregion

    #region Stop Symbol Animations

    private void PlayStopAnimationsForColumn(int col)
    {
        // FF: Task 1 - Hide top/bottom cells of reels 3-4 when reel stops in base game
        SetSideCellsVisible(col, false);

        if (currentDisplayMatrix == null || col >= currentDisplayMatrix.Count) return;
        
        for (int row = 0; row < currentDisplayMatrix[col].Count; row++)
        {
            int symId = currentDisplayMatrix[col][row];
            int wildId = gameManager?.gameConfig != null ? gameManager.gameConfig.wildSymbolId : -1;
            bool isWild = (symId == wildId);
            
            if (isWild)
            {
                AnimateSymbolSingleLoop(col, row, 1);
            }
        }
    }

    internal void AnimateAllScatters(int loopCount)
    {
        if (currentDisplayMatrix == null) return;

        // Clear any individual hit animations before starting the collective one
        KillWinTweens();

        int actualScatterId = gameManager?.gameConfig != null ? gameManager.gameConfig.scatterSymbolId : -1;
        if (actualScatterId < 0) return;
        
        for (int col = 0; col < 5; col++)
        {
            for (int row = 0; row < currentDisplayMatrix[col].Count; row++)
            {
                if (currentDisplayMatrix[col][row] == actualScatterId)
                {
                    AnimateSymbolSingleLoop(col, row, loopCount);
                }
            }
        }
    }

    private void AnimateSymbolSingleLoop(int column, int row, int loopCount = 1)
    {
        if (column >= reelImagesList.Count) return;

        var reel = reelImagesList[column];
        if (reel.images == null || reel.images.Count < 5) return; // FF: relaxed from != 7

        int imageIndex = 2 + row;
        if (imageIndex >= reel.images.Count) return;

        Image symbolImage = reel.images[imageIndex];
        if (symbolImage == null) return;

        var animGO = WinBox(winAnimationColumns, column, row);
        if (animGO == null) return;

        ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
        if (imageAnim == null) return;

        int symbolId = currentDisplayMatrix[column][row];
        if (symbolId < 0 || symbolId >= animationSpriteArrays.Length) return;

        List<Sprite> animSprites = animationSpriteArrays[symbolId];
        if (animSprites == null || animSprites.Count == 0) return;

        imageAnim.textureArray = animSprites;

        Color originalColor = new Color(symbolImage.color.r, symbolImage.color.g, symbolImage.color.b, 1f);

        Sequence seq = DOTween.Sequence();
        
        seq.AppendCallback(() => {
            animGO.SetActive(true);
            Image animRenderer = imageAnim.rendererDelegate;
            if (animRenderer != null)
            {
                animRenderer.DOKill();
                Color c = animRenderer.color;
                animRenderer.color = new Color(c.r, c.g, c.b, 1f);
            }
            symbolImage.DOKill();
            symbolImage.DOFade(0f, 0.2f);
            
            imageAnim.StartAnimation();
        });

        seq.AppendInterval(winSymbolLoopDuration * loopCount);

        seq.AppendCallback(() => {
            Image animRenderer = imageAnim != null ? imageAnim.rendererDelegate : null;

            if (animRenderer != null)
            {
                animRenderer.DOKill();
                Color c = animRenderer.color;
                animRenderer.color = new Color(c.r, c.g, c.b, 1f);
            }

            if (imageAnim != null) imageAnim.StopAnimation();
            if (animGO != null) animGO.SetActive(false);

            if (symbolImage != null)
            {
                symbolImage.DOKill();
                symbolImage.DOFade(originalColor.a, 0.2f);
            }
        });

        winTweens.Add(seq);
    }

    #endregion

    #region Win Line Animation

    internal void ShowWinLineAnimation(List<WinLine> winLines, System.Action onComplete)
    {
        if (winLines == null || winLines.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        KillWinTweens();
        winAnimationCoroutine = StartCoroutine(PlayTwoPhaseWinLines(winLines, onComplete));
    }

    private IEnumerator PlayTwoPhaseWinLines(List<WinLine> winLines, System.Action onComplete)
    {
        int rowLimit = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : 3;

        // ==========================================
        // PHASE 1: Show all winning icons at once
        // ==========================================
        HashSet<int> allWinPositions = new HashSet<int>();
        foreach (var winLine in winLines)
        {
            if (winLine.positions != null)
            {
                foreach (int flatIndex in winLine.positions)
                {
                    allWinPositions.Add(flatIndex);
                }
            }
        }

        Debug.Log($"[PlayTwoPhaseWinLines] Phase 1: Showing all {allWinPositions.Count} winning icons at once for {winLines.Count} win lines");

        // Calculate Phase 1 Total Win Amount
        double totalWinAmount = 0;
        foreach (var winLine in winLines)
        {
            totalWinAmount += winLine.winAmount;
        }
        if (totalWinAmount <= 0 && gameManager != null && gameManager.lastResult != null)
        {
            totalWinAmount = gameManager.lastResult.winAmount;
        }

        // FF: No middle "total win" text - the bottom WIN box shows the total; only per-line amounts on cells
        ClearWinAmountText();

        // FF: Show the TOTAL win once, on the first winning line's reel-3 cell (clears itself after winAmountShowTime)
        if (winLines.Count > 0 && winLines[0].positions != null && winLines[0].positions.Count > 0)
        {
            int textCell = GetWinTextCell(winLines[0]);
            ShowWinLineTextOnIcon(textCell % 5, textCell / 5, totalWinAmount);
        }

        // FF: Win presentation sound: big win vs small win based on GameManager threshold
        bool isBigWin = false;
        if (gameManager != null && gameManager.lastResult != null)
        {
            double totalPay = gameManager.GetTotalPay();
            double mult = totalPay > 0 ? (gameManager.lastResult.winAmount / totalPay) : 0;
            if (mult >= gameManager.BigWinMultiplierThreshold)
            {
                isBigWin = true;
            }
        }

        if (isBigWin)
        {
            AudioManager.Instance?.PlayWinBig();
        }
        else
        {
            AudioManager.Instance?.PlayWinSmall();
        }

        // Animate all winning symbols and wait for their ImageAnimation loops to complete
        yield return StartCoroutine(AnimateWinPositions(allWinPositions));

        KillWinTweens(false);
        HideAllWinLineTexts();
        ClearWinAmountText();

        // Invoke onComplete immediately after Phase 1 so game logic (Free Spins / Autoplay / Win complete) can proceed
        onComplete?.Invoke();

        // Skip Phase 2 if in Free Spins, Autoplay, or if the free-games trigger was hit
        bool hasSpecialFeature = gameManager != null && gameManager.lastResult != null
            && gameManager.lastResult.freeSpinData != null && gameManager.lastResult.freeSpinData.isTriggered;

        bool skipPhase2 = (gameManager != null && (gameManager.isInFreeSpins || gameManager.isAutoPlaying)) || hasSpecialFeature;
        if (skipPhase2)
        {
            yield break;
        }

        // ==========================================
        // PHASE 2: Individual Win Line presentation loop
        // ==========================================
        while (true)
        {
            foreach (var winLine in winLines)
            {
                if (winLine.positions == null || winLine.positions.Count == 0) continue;

                KillWinTweens(false);
                HideAllWinLineTexts();

                // FF: No win amount in the Phase 2 loop - the amount is shown only once (Phase 1)

                // Animate win line symbols and wait for their ImageAnimation loops to complete
                yield return StartCoroutine(AnimateWinPositions(winLine.positions));
            }
        }
    }

    // FF: Task 10 - Spine win animations with fallback
    private IEnumerator AnimateWinPositions(IEnumerable<int> flatPositions)
    {
        if (flatPositions == null) yield break;

        int rowLimit = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : 3;
        int loopCountTarget = (gameManager != null && (gameManager.isInFreeSpins || gameManager.isAutoPlaying)) ? 1 : winSymbolLoopCount;

        int totalActiveAnims = 0;
        int completedCount = 0;
        bool isCompleted = false;

        // FF: Task 4 - Track Spine animation durations for safety timeout
        float maxSpineDuration = 0f;
        bool anySpineActive = false;

        // Filter valid cells to animate
        List<int> validPositions = new List<int>();
        foreach (int flatIndex in flatPositions)
        {
            int row = flatIndex / 5;
            int col = flatIndex % 5;

            if (col < 0 || col >= 5 || row < 0 || row >= rowLimit) continue;

            // FF: Task 2c - In base game reels 3-4 show only center cell (row 1); skip hidden top/bottom cells
            bool isBaseGame = gameManager == null || !gameManager.isInFreeSpins;
            if (isBaseGame && col >= 3 && row != 1) continue;

            validPositions.Add(flatIndex);
        }

        if (validPositions.Count == 0) yield break;

        foreach (int flatIndex in validPositions)
        {
            int row = flatIndex / 5;
            int col = flatIndex % 5;
            int cellIndex = col * 3 + row;

            if (col >= reelImagesList.Count) continue;
            var reel = reelImagesList[col];
            if (reel.images == null || reel.images.Count < 5) continue; // FF: relaxed from != 7

            int imageIndex = 2 + row;
            if (imageIndex >= reel.images.Count) continue;

            Image symbolImage = reel.images[imageIndex];
            if (symbolImage == null) continue;

            if (col >= currentDisplayMatrix.Count || row >= currentDisplayMatrix[col].Count) continue;
            int symbolId = currentDisplayMatrix[col][row];

            bool hasSpine = symbolSkeletons != null
                && symbolId >= 0 && symbolId < symbolSkeletons.Length
                && symbolSkeletons[symbolId] != null
                && cellSpines != null
                && cellIndex >= 0 && cellIndex < cellSpines.Length
                && cellSpines[cellIndex] != null;

            totalActiveAnims++;

            // FF: Task 2 - Enable WinBox (with W_Frame) for every winning cell in BOTH paths
            EnableWinBox(col, row);

            if (hasSpine)
            {
                anySpineActive = true;
                var spineCtrl = cellSpines[cellIndex];
                spineCtrl.SetSkeletonData(symbolSkeletons[symbolId]);

                // FF: Apply per-symbol scale and offset (fallback 0.446 if scale is 0)
                float spineScale = (symbolSpineScale != null && symbolId >= 0 && symbolId < symbolSpineScale.Length && symbolSpineScale[symbolId] > 0f)
                    ? symbolSpineScale[symbolId]
                    : 0.446f;
                Vector2 spineOffset = (symbolSpineOffset != null && symbolId >= 0 && symbolId < symbolSpineOffset.Length)
                    ? symbolSpineOffset[symbolId]
                    : Vector2.zero;

                RectTransform spineRt = spineCtrl.GetComponent<RectTransform>();
                if (spineRt != null)
                {
                    spineRt.localScale = new Vector3(spineScale, spineScale, 1f);
                    spineRt.anchoredPosition = spineOffset;
                }

                // FF: Task 4 - Record longest Spine duration among active cells
                float spineDur = spineCtrl.GetAnimationDuration();
                if (spineDur > maxSpineDuration) maxSpineDuration = spineDur;

                spineCtrl.gameObject.SetActive(true);

                symbolImage.DOKill();
                symbolImage.DOFade(0f, 0.2f);

                spineCtrl.Play(loopCountTarget, () =>
                {
                    if (spineCtrl != null) spineCtrl.Stop();

                    if (symbolImage != null)
                    {
                        symbolImage.DOKill();
                        symbolImage.DOFade(1f, 0.2f);
                    }

                    completedCount++;
                    if (completedCount >= totalActiveAnims)
                    {
                        isCompleted = true;
                    }
                });
            }
            else
            {
                // FF: Fallback: symbol pulse 1.15x + short alpha flash (WinBox already enabled above)
                symbolImage.DOKill();
                symbolImage.transform.localScale = Vector3.one;

                Sequence seq = DOTween.Sequence();
                // Symbol pulse 1.15x
                seq.Append(symbolImage.transform.DOScale(1.15f, 0.25f).SetEase(Ease.OutQuad));
                seq.Append(symbolImage.transform.DOScale(1.0f, 0.25f).SetEase(Ease.InQuad));

                // Short alpha flash
                Sequence flashSeq = DOTween.Sequence();
                flashSeq.Append(symbolImage.DOFade(0.4f, 0.12f).SetEase(Ease.OutQuad));
                flashSeq.Append(symbolImage.DOFade(1.0f, 0.12f).SetEase(Ease.InQuad));
                seq.Join(flashSeq);

                seq.SetLoops(loopCountTarget);
                seq.OnComplete(() =>
                {
                    if (symbolImage != null)
                    {
                        symbolImage.transform.localScale = Vector3.one;
                        Color c = symbolImage.color;
                        symbolImage.color = new Color(c.r, c.g, c.b, 1f);
                    }
                    completedCount++;
                    if (completedCount >= totalActiveAnims)
                    {
                        isCompleted = true;
                    }
                });
                winTweens.Add(seq);
            }
        }

        if (totalActiveAnims > 0)
        {
            // FF: Task 2 & Task 4 - Win animation safety timeout calculation
            // W_Frame loops continuously, so the wait depends on symbol Spines or fallback pulse.
            float maxWait;
            if (anySpineActive && maxSpineDuration > 0f)
            {
                maxWait = (maxSpineDuration * loopCountTarget) + 1.0f;
            }
            else
            {
                maxWait = (0.5f * loopCountTarget) + 1.0f;
            }
            if (maxWait < 1.5f) maxWait = 1.5f;

            float timer = 0f;
            while (!isCompleted && timer < maxWait)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!isCompleted)
            {
                Debug.LogWarning($"[FF] Win animation timeout after {timer:F2}s (maxWait: {maxWait:F2}s) - forcing completion");
                StopAllCellSpines();

                // Restore every symbol image alpha to 1 and scale to 1
                if (reelImagesList != null)
                {
                    foreach (var reel in reelImagesList)
                    {
                        if (reel?.images != null)
                        {
                            foreach (var img in reel.images)
                            {
                                if (img != null)
                                {
                                    img.DOKill();
                                    img.transform.localScale = Vector3.one;
                                    Color c = img.color;
                                    img.color = new Color(c.r, c.g, c.b, 1f);
                                }
                            }
                        }
                    }
                }

                // Respect Task 1: after restoring images, base-game reels 3-4 top/bottom must stay hidden
                SetAllSideCellsVisible(false);
            }
        }
        else
        {
            yield return new WaitForSeconds(winSymbolLoopDuration);
        }
    }

    // FF: ONE shared win-amount text (SlotView > winAmountText) is moved onto the winning cell, filled with
    // sprite digits, and cleared after winAmountShowTime. No SetActive - the object always stays active.
    private void ShowWinLineTextOnIcon(int col, int row, double winAmount)
    {
        // FF: no win amount on the reels during autoplay
        if (gameManager != null && gameManager.isAutoPlaying) return;
        if (winAmountText == null) return;

        if (col < 0 || col >= reelImagesList.Count) return;
        var reel = reelImagesList[col];
        if (reel.images == null) return;
        int imageIndex = 2 + row;
        if (imageIndex >= reel.images.Count) return;

        Image symbolImage = reel.images[imageIndex];
        if (symbolImage == null) return;

        if (winAmountClearRoutine != null) StopCoroutine(winAmountClearRoutine);

        Transform textTransform = winAmountText.transform;
        textTransform.position = symbolImage.rectTransform.TransformPoint(symbolImage.rectTransform.rect.center);
        winAmountText.text = ToSpriteDigits(winAmount.ToString("0.00"));

        // Small pop (own tween, not in winTweens, so killing win tweens never leaves it at scale 0)
       /* textTransform.DOKill();
        textTransform.localScale = Vector3.zero;
        DOTween.Sequence()
            .Append(textTransform.DOScale(1.2f, 0.15f).SetEase(Ease.OutQuad))
            .Append(textTransform.DOScale(1f, 0.10f).SetEase(Ease.InQuad))
            .SetTarget(textTransform);*/

        winAmountClearRoutine = StartCoroutine(ClearWinAmountTextAfter(winAmountShowTime));
    }

    // FF: "0.30" -> sprite tags by INDEX in the WinlineTextNumbers sprite asset: index d shows digit d,
    // index 10 shows '.'. (The sprite NAMES are off by one - e.g. "Numbers_0" shows 1 - so don't use names.)
    private static string ToSpriteDigits(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length * 24);
        foreach (char c in value)
        {
            if (c >= '0' && c <= '9') sb.Append("<sprite=").Append(c - '0').Append('>');
            else if (c == '.' || c == ',') sb.Append("<sprite=10>");
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private void HideAllWinLineTexts()
    {
        ClearWinAmountText();
    }

    // FF: Cell that gets the win amount: the winning cell on reel 3 (col 2) so the number stays out of the
    // nudge box. Lines always include col 2 (they start at col 0, min 3 of a kind). Scatter (positions in any
    // order): rightmost Bonus on cols 0-2; only if none there, the rightmost visible one on cols 3-4.
    private const int WinTextMaxCol = 2;

    private int GetWinTextCell(WinLine winLine)
    {
        int fallback = winLine.positions[winLine.positions.Count - 1];
        bool isBaseGame = gameManager == null || !gameManager.isInFreeSpins;
        int best = -1;
        int bestOver = -1;
        foreach (int flat in winLine.positions)
        {
            int row = flat / 5;
            int col = flat % 5;
            if (isBaseGame && col >= 3 && row != 1) continue; // hidden cells in base game
            if (col <= WinTextMaxCol)
            {
                if (best < 0 || IsRightOf(flat, best)) best = flat;
            }
            else if (bestOver < 0 || IsRightOf(flat, bestOver))
            {
                bestOver = flat;
            }
        }
        if (best >= 0) return best;
        return bestOver >= 0 ? bestOver : fallback;
    }

    // FF: true if cell a is further right than b (same column: higher row wins = top)
    private static bool IsRightOf(int a, int b)
    {
        int aCol = a % 5, bCol = b % 5;
        return aCol > bCol || (aCol == bCol && a / 5 < b / 5);
    }

    // FF: Clear the shared amount text (object stays active; only the text is emptied)
    private void ClearWinAmountText()
    {
        if (winAmountClearRoutine != null)
        {
            StopCoroutine(winAmountClearRoutine);
            winAmountClearRoutine = null;
        }
        if (winAmountText == null) return;
        winAmountText.transform.DOKill();
        winAmountText.transform.localScale = Vector3.one;
        winAmountText.text = string.Empty;
    }

    private IEnumerator ClearWinAmountTextAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        winAmountClearRoutine = null;
        if (winAmountText != null) winAmountText.text = string.Empty;
    }

    private void AnimateTextScaleAppear(Transform textTransform, float popScale = 1.2f, float durationUp = 0.15f, float durationDown = 0.10f)
    {
        if (textTransform == null) return;
        textTransform.DOKill();
        textTransform.localScale = Vector3.zero;
        textTransform.gameObject.SetActive(true);

        Sequence seq = DOTween.Sequence();
        seq.Append(textTransform.DOScale(popScale, durationUp).SetEase(Ease.OutQuad));
        seq.Append(textTransform.DOScale(1.0f, durationDown).SetEase(Ease.InQuad));
        winTweens.Add(seq);
    }
    private void EnableWinBox(int col, int row)
    {
        var go = WinBox(winBoxColumns, col, row);
        if (go)
        {
            // FF: Ensure the column parent is active (WinBoxOverlay itself stays always on)
            if (go.transform.parent != null)
            {
                go.transform.parent.gameObject.SetActive(true);
            }
            go.SetActive(true);

            // FF: Task 1 - Activate W_Frame child and Play(true) (loop) from the start every time
            var frameSpine = GetWinBoxFrameSpine(col, row);
            if (frameSpine != null)
            {
                frameSpine.gameObject.SetActive(true);
                frameSpine.Play(true);
            }
        }
        else
        {
            Debug.LogError($"[EnableWinBox] WinBox GameObject is NULL at col: {col}, row: {row}");
        }
    }

    private void DisableWinBox(int col, int row)
    {
        // FF: Task 1 - Stop() W_Frame spine before hiding the box so next win starts from frame 0
        var frameSpine = GetWinBoxFrameSpine(col, row);
        if (frameSpine != null)
        {
            frameSpine.Stop();
        }

        var go = WinBox(winBoxColumns, col, row);
        if (go) go.SetActive(false);
    }

    // FF: Task 2A & Task 1 - Stop all 15 W_Frame spines, and disable all win boxes (WinBoxOverlay stays on)
    private void DisableWinBoxColumns()
    {
        StopAllWinBoxFrameSpines();
        DisableColumns(winBoxColumns); // FF: only the boxes go off; WinBoxOverlay (with the nudge box frame) stays on
    }

    private void ResetSymbolScale(int col, int row)
    {
        if (col >= reelImagesList.Count) return;
        var reel = reelImagesList[col];
        if (reel.images == null) return;
        int imageIndex = 2 + row;
        if (imageIndex >= reel.images.Count) return;
        if (reel.images[imageIndex] != null)
        {
            reel.images[imageIndex].DOKill();
            reel.images[imageIndex].transform.localScale = Vector3.one;
            // Restore alpha to full opacity
            Color c = reel.images[imageIndex].color;
            reel.images[imageIndex].color = new Color(c.r, c.g, c.b, 1f);
        }

        // Also ensure the corresponding animation object is disabled
        var animGO = WinBox(winAnimationColumns, col, row);
        if (animGO != null)
        {
            ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
            if (imageAnim != null)
            {
                if (imageAnim.rendererDelegate != null) imageAnim.rendererDelegate.DOKill();
                imageAnim.StopAnimation();
            }
            animGO.SetActive(false);
        }
    }


    private void AnimateWinSymbol(int column, int row)
    {

        if (column >= reelImagesList.Count)
        {
            Debug.LogError($"[AnimateWinSymbol] Invalid column {column}, max is {reelImagesList.Count - 1}");
            return;
        }

        var reel = reelImagesList[column];
        if (reel.images == null || reel.images.Count < 5) // FF: relaxed from != 7
        {
            Debug.LogError($"[AnimateWinSymbol] Reel {column} has invalid images list (expected at least 5, got {reel.images?.Count})");
            return;
        }

        int imageIndex = 2 + row;
        if (imageIndex >= reel.images.Count)
        {
            Debug.LogError($"[AnimateWinSymbol] Image index {imageIndex} out of range for reel {column}");
            return;
        }

        Image symbolImage = reel.images[imageIndex];
        if (symbolImage == null)
        {
            Debug.LogError($"[AnimateWinSymbol] Symbol image is NULL at col: {column}, row: {row}, imageIndex: {imageIndex}");
            return;
        }



        // Get the animation GameObject for this position
        var animGO = WinBox(winAnimationColumns, column, row);
        if (animGO == null)
        {
            Debug.LogError($"[AnimateWinSymbol] Animation GameObject is NULL at col: {column}, row: {row}");
            return;
        }

        // Get the ImageAnimation component
        ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
        if (imageAnim == null)
        {
            Debug.LogError($"[AnimateWinSymbol] ImageAnimation component not found on animation object at col: {column}, row: {row}");
            return;
        }

        // Get the current symbol ID at this position
        if (column >= currentDisplayMatrix.Count || row >= currentDisplayMatrix[column].Count)
        {
            Debug.LogError($"[AnimateWinSymbol] Invalid matrix position col: {column}, row: {row}");
            return;
        }

        int symbolId = currentDisplayMatrix[column][row];
        
        // Validate symbolId
        if (symbolId < 0 || symbolId >= animationSpriteArrays.Length)
        {
            Debug.LogError($"[AnimateWinSymbol] Invalid symbolId {symbolId} at col: {column}, row: {row}");
            return;
        }

        // Get the animation sprite array for this symbol
        List<Sprite> animSprites = animationSpriteArrays[symbolId];
        if (animSprites == null || animSprites.Count == 0)
        {
            // Expected for most symbols now
            return;
        }

        // Set the sprite array on the ImageAnimation component
        imageAnim.textureArray = animSprites;

        Color originalColor = new Color(symbolImage.color.r, symbolImage.color.g, symbolImage.color.b, 1f);

        Sequence seq = DOTween.Sequence();
        
        seq.AppendCallback(() => {
            animGO.SetActive(true);
            Image animRenderer = imageAnim.rendererDelegate;
            if (animRenderer != null)
            {
                animRenderer.DOKill();
                Color c = animRenderer.color;
                animRenderer.color = new Color(c.r, c.g, c.b, 1f);
            }
            symbolImage.DOKill();
            symbolImage.DOFade(0f, 0.2f);
        });

        if (winLineBoxToAnimationDelay > 0)
        {
            seq.AppendInterval(winLineBoxToAnimationDelay);
        }

        seq.AppendCallback(() => {
            imageAnim.StartAnimation();
        });

        int loopCount = (gameManager != null && (gameManager.isInFreeSpins || gameManager.isAutoPlaying)) ? 1 : winSymbolLoopCount;
        seq.AppendInterval(winSymbolLoopDuration * loopCount);

        seq.AppendCallback(() => {
            Image animRenderer = imageAnim != null ? imageAnim.rendererDelegate : null;

            if (animRenderer != null)
            {
                animRenderer.DOKill();
                Color c = animRenderer.color;
                animRenderer.color = new Color(c.r, c.g, c.b, 1f);
            }

            if (imageAnim != null) imageAnim.StopAnimation();
            if (animGO != null) animGO.SetActive(false);

            if (symbolImage != null)
            {
                symbolImage.DOKill();
                symbolImage.DOFade(originalColor.a, 0.2f);
            }
        });

        winTweens.Add(seq);
    }

    // FF: Task 2e - Stop all win animations, tweens, and hide win boxes
    internal void StopAllWinAnimations()
    {
        KillWinTweens(true);
    }

    // FF: Task 10 & Task 1 - Stop and clear all cell spine animations and win box frame spines
    private void StopAllCellSpines()
    {
        if (cellSpines != null)
        {
            for (int i = 0; i < cellSpines.Length; i++)
            {
                if (cellSpines[i] != null)
                {
                    cellSpines[i].Stop();
                }
            }
        }

        StopAllWinBoxFrameSpines();
    }

    private void KillWinTweens(bool stopCoroutine = true)
    {
        // FF: Task 10 - Stop all Spine animations on win tweens kill
        StopAllCellSpines();

        foreach (var tween in winTweens)
        {
            tween?.Kill();
        }
        winTweens.Clear();

        if (stopCoroutine && winAnimationCoroutine != null)
        {
            StopCoroutine(winAnimationCoroutine);
            winAnimationCoroutine = null;
        }

        // Stop all win animations and disable animation GameObjects
        if (winAnimationColumns != null)
        {
            foreach (var col in winAnimationColumns)
            {
                if (col?.rows != null)
                {
                    foreach (var animGO in col.rows)
                    {
                        if (animGO != null)
                        {
                            ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
                            if (imageAnim != null)
                            {
                                imageAnim.onLoopComplete = null;
                                if (imageAnim.rendererDelegate != null)
                                {
                                    imageAnim.rendererDelegate.DOKill();
                                    Color ac = imageAnim.rendererDelegate.color;
                                    imageAnim.rendererDelegate.color = new Color(ac.r, ac.g, ac.b, 1f);
                                }
                                imageAnim.StopAnimation();
                            }
                            if (animGO.activeSelf)
                            {
                                animGO.SetActive(false);
                            }
                        }
                    }
                }
            }
        }

        DisableWinBoxColumns();
        HideAllWinLineTexts();
        ClearWinAmountText();

        // Restore all symbol image alphas to full opacity
        foreach (var reel in reelImagesList)
        {
            if (reel.images != null)
            {
                foreach (var image in reel.images)
                {
                    if (image != null)
                    {
                        image.DOKill();
                        image.transform.localScale = Vector3.one;
                        Color c = image.color;
                        image.color = new Color(c.r, c.g, c.b, 1f);
                    }
                }
            }
        }

        // FF: Task 1 - Ensure reels 3-4 top/bottom stay hidden in base game after win animations cleanup
        if (!isSpinning)
        {
            SetAllSideCellsVisible(false);
        }
    }

    #endregion


    
    internal List<List<int>> GetCurrentDisplayMatrix()
    {
        return currentDisplayMatrix;
    }

    internal bool IsSpinning()
    {
        return isSpinning;
    }


    private void KillAllTweens()
    {
        foreach (var tween in spinTweens)
        {
            tween?.Kill();
        }
        spinTweens.Clear();

        KillWinTweens();
    }

    // FF: Task 3 - Reset spin visuals on mid-spin disconnect or failure
    internal void ResetSpinVisuals()
    {
        for (int col = 0; col < reelBlurring.Length; col++)
        {
            reelBlurring[col] = false;
        }

        if (currentDisplayMatrix != null)
        {
            for (int col = 0; col < currentDisplayMatrix.Count; col++)
            {
                if (col < reelTransforms.Length && currentDisplayMatrix[col] != null)
                {
                    int? aboveSym = (currentAbove != null && col < currentAbove.Count) ? (int?)currentAbove[col] : null;
                    int? belowSym = (currentBelow != null && col < currentBelow.Count) ? (int?)currentBelow[col] : null;
                    SetReelSymbols(col, currentDisplayMatrix[col], false, aboveSym, belowSym);
                }
            }
        }

        if (reelTransforms != null)
        {
            for (int col = 0; col < reelTransforms.Length; col++)
            {
                if (reelTransforms[col] != null)
                {
                    reelTransforms[col].DOKill();
                    Vector3 pos = reelTransforms[col].localPosition;
                    reelTransforms[col].localPosition = new Vector3(pos.x, middlePosition, pos.z);
                }
            }
        }

        foreach (var tween in spinTweens)
        {
            tween?.Kill();
        }
        spinTweens.Clear();

        isSpinning = false;
        SetAllSideCellsVisible(false);
    }

    #region FF: Grid Layout Switch

    // FF: Task 6 - Grid layout switch with CanvasGroup fading
    // FF: The nudge box ("nugget place") holds reels 3-4 and has a Mask + frame Image
    // that clip them to the single center row in the base game. Free games need it off (full 5x3).
    private UnityEngine.UI.Mask nudgeBoxMask;
    private UnityEngine.UI.Image nudgeBoxImage;

    // FF: same clip box over the win frames of reels 3-4 (WinBoxOverlay/NuggetPlaceWinBox/SlotParent)
    [SerializeField] private UnityEngine.UI.Mask nudgeBoxWinMask;
    [SerializeField] private UnityEngine.UI.Image nudgeBoxWinImage;
    // FF: the visible nudge box frame art (WinBoxOverlay/NuggetPlaceWinBox Image) - drawn above reels and win frames.
    // SlotHolder/nugget place keeps its Image OFF (it would draw under the symbols).
    [SerializeField] private UnityEngine.UI.Image nudgeBoxFrame;

    private void SetNudgeBoxMask(bool baseGame)
    {
        if (nudgeBoxMask == null && reelImagesList != null && reelImagesList.Count > 3
            && reelImagesList[3] != null && reelImagesList[3].images != null && reelImagesList[3].images.Count > 0
            && reelImagesList[3].images[0] != null)
        {
            nudgeBoxMask = reelImagesList[3].images[0].GetComponentInParent<UnityEngine.UI.Mask>(true);
            if (nudgeBoxMask != null) nudgeBoxImage = nudgeBoxMask.GetComponent<UnityEngine.UI.Image>();
        }
        if (nudgeBoxMask != null) nudgeBoxMask.enabled = baseGame;
        if (nudgeBoxImage != null) nudgeBoxImage.enabled = baseGame;
        if (nudgeBoxWinMask != null) nudgeBoxWinMask.enabled = baseGame;
        if (nudgeBoxWinImage != null) nudgeBoxWinImage.enabled = baseGame;
        if (nudgeBoxFrame != null) nudgeBoxFrame.enabled = baseGame;
    }

    internal void SetFreeGameLayout(bool full)
    {
        SetNudgeBoxMask(!full);
        if (last2SlotCoverImage == null) return;

        // full == true: Free Games (5x3 fully open, hide cover image)
        // full == false: Base Game (reels 3-4 top/bottom covered, show cover image)
        bool showCover = !full;

        CanvasGroup cg = last2SlotCoverImage.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = last2SlotCoverImage.AddComponent<CanvasGroup>();
        }

        cg.DOKill();
        if (showCover)
        {
            last2SlotCoverImage.SetActive(true);
            cg.blocksRaycasts = true;
            cg.interactable = true;
            if (!Application.isPlaying)
            {
                cg.alpha = 1f;
            }
            else
            {
                cg.DOFade(1f, 0.3f);
            }
        }
        else
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
            if (!Application.isPlaying)
            {
                cg.alpha = 0f;
            }
            else
            {
                cg.DOFade(0f, 0.3f).OnComplete(() =>
                {
                    if (cg != null)
                    {
                        cg.alpha = 0f;
                        cg.blocksRaycasts = false;
                        cg.interactable = false;
                    }
                });
            }
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        KillAllTweens();
    }

    #endregion

    // FF: serialized field kept outside #if so player builds have the same field layout
    [Header("FF: Editor Preview")]
    [Tooltip("Symbol ID (0-10) to preview in cell (col 2, row 1)")]
    [Range(0, 10)]
    [SerializeField] private int previewSymbolId = 0;

#if UNITY_EDITOR
    #region FF: Editor Spine Preview

    [ContextMenu("FF: Preview Symbol Spine")]
    private void PreviewSymbolSpine()
    {
        int col = 2;
        int row = 1;
        int cellIndex = col * 3 + row; // Cell (2,1) = index 7

        if (cellSpines == null || cellIndex >= cellSpines.Length || cellSpines[cellIndex] == null)
        {
            Debug.LogError($"[SlotView] CellSpine at cell ({col},{row}) is null!");
            return;
        }

        int symId = Mathf.Clamp(previewSymbolId, 0, 10);
        if (symbolSkeletons == null || symId >= symbolSkeletons.Length || symbolSkeletons[symId] == null)
        {
            Debug.LogError($"[SlotView] symbolSkeletons[{symId}] is null!");
            return;
        }

        // Show static symbol in cell (2, 1) with semi-transparency so both Spine and static sprite are visible
        if (reelImagesList != null && col < reelImagesList.Count && reelImagesList[col]?.images != null)
        {
            int imageIndex = 2 + row;
            if (imageIndex < reelImagesList[col].images.Count)
            {
                var staticImg = reelImagesList[col].images[imageIndex];
                if (staticImg != null)
                {
                    staticImg.sprite = GetSymbolSprite(symId);
                    Color c = staticImg.color;
                    c.a = 0.5f;
                    staticImg.color = c;
                    staticImg.gameObject.SetActive(true);
                }
            }
        }

        // Setup Spine in cell (2, 1)
        var spineCtrl = cellSpines[cellIndex];
        spineCtrl.SetSkeletonData(symbolSkeletons[symId]);

        float spineScale = (symbolSpineScale != null && symId < symbolSpineScale.Length && symbolSpineScale[symId] > 0f)
            ? symbolSpineScale[symId]
            : 0.446f;
        Vector2 spineOffset = (symbolSpineOffset != null && symId < symbolSpineOffset.Length)
            ? symbolSpineOffset[symId]
            : Vector2.zero;

        RectTransform spineRt = spineCtrl.GetComponent<RectTransform>();
        if (spineRt != null)
        {
            spineRt.localScale = new Vector3(spineScale, spineScale, 1f);
            spineRt.anchoredPosition = spineOffset;
        }

        spineCtrl.gameObject.SetActive(true);

        if (Application.isPlaying)
        {
            spineCtrl.Play(true);
        }
        else if (spineCtrl.SkeletonGraphic != null)
        {
            spineCtrl.SkeletonGraphic.Initialize(true);
            spineCtrl.SkeletonGraphic.Update(0f);
            spineCtrl.SkeletonGraphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
        }

        Debug.Log($"<color=green>[SlotView] Previewing Symbol {symId} Spine in cell ({col},{row}): scale={spineScale:F3}, offset={spineOffset}</color>");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("FF: Reset Preview Symbol Spine")]
    private void ResetPreviewSymbolSpine()
    {
        int col = 2;
        int row = 1;
        int cellIndex = col * 3 + row;

        if (cellSpines != null && cellIndex < cellSpines.Length && cellSpines[cellIndex] != null)
        {
            cellSpines[cellIndex].Stop();
            cellSpines[cellIndex].gameObject.SetActive(false);
        }

        if (reelImagesList != null && col < reelImagesList.Count && reelImagesList[col]?.images != null)
        {
            int imageIndex = 2 + row;
            if (imageIndex < reelImagesList[col].images.Count)
            {
                var staticImg = reelImagesList[col].images[imageIndex];
                if (staticImg != null)
                {
                    Color c = staticImg.color;
                    c.a = 1f;
                    staticImg.color = c;
                    if (currentDisplayMatrix != null && col < currentDisplayMatrix.Count && row < currentDisplayMatrix[col].Count)
                    {
                        staticImg.sprite = GetSymbolSprite(currentDisplayMatrix[col][row]);
                    }
                }
            }
        }

        Debug.Log("[SlotView] Preview reset for cell (2,1).");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void OnValidate()
    {
        // Live update preview in editor if cell (2,1) spine is currently active
        int cellIndex = 2 * 3 + 1;
        if (cellSpines != null && cellIndex < cellSpines.Length && cellSpines[cellIndex] != null && cellSpines[cellIndex].gameObject.activeSelf)
        {
            int symId = Mathf.Clamp(previewSymbolId, 0, 10);
            float spineScale = (symbolSpineScale != null && symId < symbolSpineScale.Length && symbolSpineScale[symId] > 0f)
                ? symbolSpineScale[symId]
                : 0.446f;
            Vector2 spineOffset = (symbolSpineOffset != null && symId < symbolSpineOffset.Length)
                ? symbolSpineOffset[symId]
                : Vector2.zero;

            RectTransform spineRt = cellSpines[cellIndex].GetComponent<RectTransform>();
            if (spineRt != null)
            {
                spineRt.localScale = new Vector3(spineScale, spineScale, 1f);
                spineRt.anchoredPosition = spineOffset;
            }
        }
    }

    #endregion
#endif
}

[System.Serializable]
public class ReelImages
{
    public List<Image> images = new List<Image>(16);
}


[System.Serializable]
public class ColumnOverlays
{
    [Tooltip("Row 0 = top, Row 1, Row 2 = bottom")]
    public GameObject[] rows = new GameObject[3];
}