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

    // Win animation sprite sequences (FF uses Spine later; CNY USpin slot kept at index 11 for legacy code)
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

    [Header("Win Animation Settings")]
    [SerializeField] private float winPopDuration = 0.4f;
    [SerializeField] private int winPopRepeat = 3;


    [Header("Stop Animation Settings")]
    [SerializeField] private float stopOvershootDistance = 50f;
    [SerializeField] private float stopOvershootDuration = 0.20f;
    [SerializeField] private float stopSettleDuration = 0.30f;

    [Header("Quick Spin Settings")]
    [SerializeField] private float quickStopStagger = 0.06f;
    [SerializeField] private float quickStopOvershoot = 20f;
    [SerializeField] private float quickStopDuration = 0.2f;
    [SerializeField] private int minSpinCyclesBeforeStop = 3;


    [Header("Win Animation Settings")]
    [SerializeField] private float winAnimationDuration = 3.0f; // Total duration each win symbol animation plays
    [SerializeField] private float winSymbolLoopDuration = 1.5f;
    [SerializeField] private int winSymbolLoopCount = 3;
    [Tooltip("Delay between enabling winBox overlay and starting the ImageAnimation - for sync timing")]
    [SerializeField] private float winLineBoxToAnimationDelay = 0.05f;

    [Header("Phase 1 Total Win Presentation")]
    [SerializeField] private TMPro.TMP_Text phase1TotalWinText;

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
        HidePhase1TotalWinText();
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
        KillAllTweens();
        // FF: Task 10 - Stop all Spine animations on StartSpin
        StopAllCellSpines();

        DisableAllOverlays();

        for (int i = 0; i < reelCycleCount.Count; i++)
        {
            reelCycleCount[i] = 0;
        }

        for (int col = 0; col < 5; col++)
        {
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

        startSequence.Append(
            slotTransform.DOLocalMoveY(middlePosition + anticipationUpDistance, anticipationUpDuration)
                .SetEase(Ease.OutQuad)
        );

        startSequence.Append(
            slotTransform.DOLocalMoveY(middlePosition, anticipationUpDuration * 0.5f)
                .SetEase(Ease.InQuad)
        );

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
            reel.images[0].sprite = GetSymbolSprite(RandomFillerSymbolId());
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

        float stagger = isQuickStop ? quickStopStagger : reelStopStagger;

        for (int col = 0; col < 5; col++)
        {
            float delay = col * stagger;
            StartCoroutine(StopSingleReel(col, resultMatrix[col], delay, isQuickStop));
        }

        float longestStopTime;
        if (isQuickStop)
        {
            longestStopTime = (4 * stagger) + quickStopDuration;
        }
        else
        {
            longestStopTime = (4 * stagger) + stopOvershootDuration + stopSettleDuration;
        }

        yield return new WaitForSeconds(longestStopTime);

        isSpinning = false;

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
        SetReelSymbols(columnIndex, targetSymbols, false, aboveSym, belowSym);

        float currentY = slotTransform.localPosition.y;
        float targetY = middlePosition;
        float offset = (currentY - targetY) % cycleDistance;
        if (offset < 0) offset += cycleDistance;

        slotTransform.localPosition = new Vector3(
            slotTransform.localPosition.x,
            targetY + offset,
            0
        );

        // ── Play reel-stop sound immediately when symbols lock in ──────────
        AudioManager.Instance?.PlayReelStop();

        // Detect wild symbols in this column for hit sounds
        if (currentDisplayMatrix != null && columnIndex < currentDisplayMatrix.Count)
        {
            bool hasWild = false;
            int wildId = gameManager?.gameConfig != null ? gameManager.gameConfig.wildSymbolId : -1;
            foreach (int sym in currentDisplayMatrix[columnIndex])
            {
                if (sym == wildId) hasWild = true;
            }
            if (hasWild) AudioManager.Instance?.PlayReelStop();
        }
        // ──────────────────────────────────────────────────────────────────

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
                    .SetEase(Ease.InOutQuad)
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
        float overshoot = isLower ? -12f : 12f;

        Sequence nudgeSeq = DOTween.Sequence();
        // 0.16s slide with slight overshoot + 0.09s settle = 0.25s total
        nudgeSeq.Append(slotTransform.DOLocalMoveY(targetY + overshoot, 0.16f).SetEase(Ease.OutQuad));
        nudgeSeq.Append(slotTransform.DOLocalMoveY(targetY, 0.09f).SetEase(Ease.InOutSine));
        nudgeSeq.OnComplete(() =>
        {
            AudioManager.Instance?.PlayReelStop();
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

    internal void AnimateUSpinWin(System.Action onComplete = null)
    {
        if (currentDisplayMatrix == null)
        {
            onComplete?.Invoke();
            return;
        }

        KillWinTweens();
        AudioManager.Instance?.PlayWinLinePhase1Start();

        List<ImageAnimation> activeUSpinAnims = new List<ImageAnimation>();
        int completedCount = 0;
        int targetLoops = 2; // Exactly 2 loops of full animation

        for (int col = 0; col < 5; col++)
        {
            if (col >= currentDisplayMatrix.Count) continue;
            for (int row = 0; row < currentDisplayMatrix[col].Count; row++)
            {
                if (currentDisplayMatrix[col][row] == 11) // USpin Symbol ID
                {
                    EnableWinBox(col, row);
                    
                    var animGO = WinBox(winAnimationColumns, col, row);
                    if (animGO != null)
                    {
                        ImageAnimation imageAnim = animGO.GetComponent<ImageAnimation>();
                        int imageIndex = 2 + row;
                        Image symbolImage = (col < reelImagesList.Count && reelImagesList[col].images != null && imageIndex < reelImagesList[col].images.Count)
                            ? reelImagesList[col].images[imageIndex]
                            : null;

                        if (imageAnim != null)
                        {
                            activeUSpinAnims.Add(imageAnim);

                            List<Sprite> animSprites = animationSpriteArrays[11];
                            imageAnim.textureArray = animSprites;
                            imageAnim.doLoopAnimation = true;

                            animGO.SetActive(true);
                            Image animRenderer = imageAnim.rendererDelegate != null ? imageAnim.rendererDelegate : animGO.GetComponent<Image>();
                            if (animRenderer != null)
                            {
                                animRenderer.DOKill();
                                Color c = animRenderer.color;
                                animRenderer.color = new Color(c.r, c.g, c.b, 1f);
                            }
                            if (symbolImage != null)
                            {
                                symbolImage.DOKill();
                                symbolImage.DOFade(0f, 0.2f);
                            }

                            imageAnim.onLoopComplete = (loopCount) =>
                            {
                                if (loopCount >= targetLoops)
                                {
                                    imageAnim.onLoopComplete = null;
                                    imageAnim.StopAnimation();
                                    animGO.SetActive(false);

                                    if (symbolImage != null)
                                    {
                                        symbolImage.DOKill();
                                        symbolImage.DOFade(1f, 0.2f);
                                    }

                                    completedCount++;
                                    if (completedCount >= activeUSpinAnims.Count)
                                    {
                                        onComplete?.Invoke();
                                    }
                                }
                            };

                            imageAnim.StartAnimation();
                        }
                    }
                }
            }
        }

        if (activeUSpinAnims.Count == 0)
        {
            onComplete?.Invoke();
        }
    }

    internal void AnimateMoneyBagWin()
    {
        if (currentDisplayMatrix == null) return;

        KillWinTweens();
        AudioManager.Instance?.PlayWinLinePhase1Start();

        for (int col = 0; col < 5; col++)
        {
            if (col >= currentDisplayMatrix.Count) continue;
            for (int row = 0; row < currentDisplayMatrix[col].Count; row++)
            {
                if (currentDisplayMatrix[col][row] == 12) // MoneyBag Symbol ID
                {
                    EnableWinBox(col, row);
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

        // Show Phase 1 Total Win Text with final win value
        ShowPhase1TotalWin(totalWinAmount);

        // FF: Task 2A - Show win amount text on the last winning cell of each winning line in Phase 1
        foreach (var winLine in winLines)
        {
            if (winLine.positions != null && winLine.positions.Count > 0)
            {
                int lastFlatIndex = winLine.positions[winLine.positions.Count - 1];
                int lastRow = lastFlatIndex / 5;
                int lastCol = lastFlatIndex % 5;
                ShowWinLineTextOnIcon(lastCol, lastRow, winLine.winAmount);
            }
        }

        AudioManager.Instance?.PlayWinLinePhase1Start();

        // Animate all winning symbols and wait for their ImageAnimation loops to complete
        yield return StartCoroutine(AnimateWinPositions(allWinPositions));

        KillWinTweens(false);
        HideAllWinLineTexts();
        HidePhase1TotalWinText();

        // Invoke onComplete immediately after Phase 1 so game logic (Free Spins / Autoplay / Win complete) can proceed
        onComplete?.Invoke();

        // Skip Phase 2 if in Free Spins, Autoplay, or if a Special Feature (USpin, MoneyBag, Scatter trigger) was triggered
        bool hasSpecialFeature = (gameManager != null && gameManager.lastResult != null && (
            (gameManager.lastResult.uSpinData != null && gameManager.lastResult.uSpinData.triggered) ||
            (gameManager.lastResult.moneyBagData != null && gameManager.lastResult.moneyBagData.triggered) ||
            (gameManager.lastResult.freeSpinData != null && gameManager.lastResult.freeSpinData.isTriggered)
        ));

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

                // FF: Task 2A - Show WinLineText on the last icon of the active win line (single or multiple lines)
                int lastFlatIndex = winLine.positions[winLine.positions.Count - 1];
                int lastRow = lastFlatIndex / 5;
                int lastCol = lastFlatIndex % 5;
                ShowWinLineTextOnIcon(lastCol, lastRow, winLine.winAmount);

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

    private void ShowWinLineTextOnIcon(int col, int row, double winAmount)
    {
        if (col < 0 || col >= reelImagesList.Count) return;
        var reel = reelImagesList[col];
        if (reel.images == null) return;
        int imageIndex = 2 + row;
        if (imageIndex >= reel.images.Count) return;

        Image symbolImage = reel.images[imageIndex];
        if (symbolImage == null) return;

        Transform textTransform = symbolImage.transform.Find("WinLineText");
        if (textTransform != null)
        {
            var tmpText = textTransform.GetComponent<TMPro.TMP_Text>();
            if (tmpText != null)
            {
                // FF: Format as 0.00
                tmpText.text = winAmount.ToString("0.00");
            }
            AnimateTextScaleAppear(textTransform);
        }
    }

    private void HideAllWinLineTexts()
    {
        if (reelImagesList == null) return;
        foreach (var reel in reelImagesList)
        {
            if (reel.images != null)
            {
                foreach (var image in reel.images)
                {
                    if (image != null)
                    {
                        Transform textTransform = image.transform.Find("WinLineText");
                        if (textTransform != null)
                        {
                            textTransform.DOKill();
                            textTransform.localScale = Vector3.one;
                            textTransform.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }
    }

    private void ShowPhase1TotalWin(double totalWinAmount)
    {
        if (phase1TotalWinText != null)
        {
            phase1TotalWinText.text = totalWinAmount.ToString("0.###");
            AnimateTextScaleAppear(phase1TotalWinText.transform);
        }
    }

    private void HidePhase1TotalWinText()
    {
        if (phase1TotalWinText != null)
        {
            phase1TotalWinText.transform.DOKill();
            phase1TotalWinText.transform.localScale = Vector3.one;
            phase1TotalWinText.gameObject.SetActive(false);
        }
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
            // FF: Task 2A - Ensure parent column and grandparent WinBoxOverlay are active
            if (go.transform.parent != null)
            {
                if (go.transform.parent.parent != null)
                {
                    go.transform.parent.parent.gameObject.SetActive(true);
                }
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

    // FF: Task 2A & Task 1 - Stop all 15 W_Frame spines, disable all win box columns, and hide grandparent WinBoxOverlay
    private void DisableWinBoxColumns()
    {
        StopAllWinBoxFrameSpines();
        DisableColumns(winBoxColumns);
        if (winBoxColumns != null && winBoxColumns.Length > 0 && winBoxColumns[0]?.rows != null && winBoxColumns[0].rows.Length > 0)
        {
            var firstBox = winBoxColumns[0].rows[0];
            if (firstBox != null && firstBox.transform.parent != null && firstBox.transform.parent.parent != null)
            {
                firstBox.transform.parent.parent.gameObject.SetActive(false);
            }
        }
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
        HidePhase1TotalWinText();

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

    #region FF: Grid Layout Switch

    // FF: Task 6 - Grid layout switch
    internal void SetFreeGameLayout(bool full)
    {
        if (last2SlotCoverImage == null) return;

        // full == true: Free Games (5x3 fully open, hide cover image)
        // full == false: Base Game (reels 3-4 top/bottom covered, show cover image)
        bool showCover = !full;

        CanvasGroup cg = last2SlotCoverImage.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            if (showCover)
            {
                last2SlotCoverImage.SetActive(true);
                cg.DOKill();
                cg.DOFade(1f, 0.3f);
            }
            else
            {
                cg.DOKill();
                cg.DOFade(0f, 0.3f).OnComplete(() =>
                {
                    if (last2SlotCoverImage != null)
                    {
                        last2SlotCoverImage.SetActive(false);
                    }
                });
            }
        }
        else
        {
            last2SlotCoverImage.SetActive(showCover);
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        KillAllTweens();
    }

    #endregion
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