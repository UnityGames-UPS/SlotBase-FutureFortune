using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#region Server Communication Models

[Serializable]
public class InitData
{
    public string id = "initData";
    public ServerGameData gameData;
    public ServerFeatures features;
    public ServerUIData uiData;
    public ServerPlayer player;
    public JackpotData jackpotData;
    // FF: Task 7 - Server state on initData
    public ServerState state;
}

// FF: Task 7 - InitData State for reconnect/refresh
[Serializable]
public class ServerState
{
    public int freeSpinsRemaining;
    public double totalFreeSpinsWin;
    public bool isFreeGame;
    public bool awaitingNudge;
    [Newtonsoft.Json.JsonProperty("pending")]
    public Newtonsoft.Json.Linq.JToken pending;
}

[Serializable]
public class JackpotData
{
    public JackpotValues values;//jackpotfeature

}

[Serializable]
public class JackpotValues
{
    public string miniJackpot;
    public string minorJackpot;
    public string majorJackpot;
    public string grandJackpot;
}

[Serializable]
public class JackpotSyncData
{
    public string gameId;
    public JackpotValues values;
}

[Serializable]
public class ServerGameData
{
    public List<List<int>> lines;
    public List<double> bets;
    public double creditDivisor = 20; // FF: Task 2d - Default creditDivisor fallback 20
    public int totalLines;
}

[Serializable]
public class ServerFeatures
{
    public USpinFeature uSpin;
    public MoneyBagFeature moneyBag;
    public FreeGamesFeature freeGames;
    public int betMultiplier;
    public int maxWinMultiplier;
    public int minWinMultiplier;
}

[Serializable]
public class USpinFeature
{
    public bool enabled;
    public int minTrigger;
    public int symbolId;
    public List<USpinSegment> segments;
}

[Serializable]
public class USpinSegment
{
    public int sliceIndex;
    public string type;
    public double multiplier;
    public int freeGames;
}

[Serializable]
public class MoneyBagFeature
{
    public bool enabled;
    public int minTrigger;
    public int symbolId;
    public int bagCount;
}

[Serializable]
public class FreeGamesFeature
{
    public bool enabled;
    public double payMultiplier;
    public int maxTotalFreeGames;
}

[Serializable]
public class ExtraSpinsData
{
    [JsonProperty("2")] public int _2; // Keep for safety/compatibility with UI
    [JsonProperty("3")] public int _3;
    [JsonProperty("4")] public int _4;
    [JsonProperty("5")] public int _5;
}

[Serializable]
public class ServerUIData
{
    public PaylineData paylines;
}

[Serializable]
public class PaylineData
{
    public List<ServerSymbolInfo> symbols;
}

[Serializable]
public class ServerSymbolInfo
{
    public int id;
    public string name;
    public List<double> multiplier; // Keep for fallback compatibility
    public List<double> payout;
    public string description;
    public int minMatch;
    public string group;           // FF: premium / high / mid / low / scatter
}


[Serializable]
public class ServerPlayer
{
    public double balance;
}

// ============================================================================
// FIXED: Server Response Models - Must match actual server JSON structure
// ============================================================================

[Serializable]
public class ServerSpinResponse
{
    public string id = "ResultData";
    public bool success;
    public List<List<string>> matrix; // Root level matrix sent by server
    public ServerPlayerBalance player;
    public ServerPayload payload;
}

[Serializable]
public class ServerPlayerBalance
{
    public double? balance; // Nullable because server sends null
}

[Serializable]
public class ServerPayload
{
    public List<List<string>> reels;        // Keep for fallback compatibility
    public double totalWin;                  // Keep for fallback compatibility
    public int scatterCount;
    public bool scatterTriggered;
    public bool isRoundOver;                 // True when free spin round is over
    public double totalRoundWin;             // Total round win (at payload level when isRoundOver)

    // CNY fields
    public double winAmount;
    public double grandTotalWin;
    public double netReturnRatio;
    public List<ServerWaysWin> waysWins;
    public ServerUSpinResult uSpin;
    public ServerMoneyBagResult moneyBag;
    public ServerFreeGamesResult freeGames;

    // Future Fortune Nudge fields
    public List<int> above;
    public List<int> below;
    public bool awaitingNudge;
    public List<ServerNudgeOption> nudgeOptions; // FF: Server pre-calculated valid moves

    // FF: Real server fields
    public List<List<int>> startMatrix;
    public List<List<int>> matrix;
    public List<ServerLineWin> lineWins;
    public double lineWin;
    public ServerScatter scatter;
    public ServerNudgeApplied nudge;
    public double bonusMultiplier;
    public int freeSpinsAwarded;
    public int freeSpinsRemaining;
    public double totalFreeSpinsWin;
    public bool isFreeSpin;
    public ServerJackpot jackpot;
    public double coin;
}

[Serializable]
public class ServerLineWin
{
    // FF: Task 1 - Server sends lineIndex (0-based) and matchCount, keep line/count for backward compatibility
    public int lineIndex = -1;
    public int line = -1;
    public int symbolId;
    public string symbolName;
    public int matchCount = -1;
    public int count = -1;
    public double basePayout;
    public double payoutCredits;
    public double multiplier = 1.0;
    public double winInCash;
    public List<ServerPosition> positions;

    public int GetLineIndex()
    {
        if (lineIndex >= 0) return lineIndex;
        if (line > 0) return line - 1;
        return 0;
    }

    public int GetMatchCount()
    {
        if (matchCount > 0) return matchCount;
        if (count > 0) return count;
        return 0;
    }

    public double GetPayout() => basePayout > 0 ? basePayout : payoutCredits;
}

[Serializable]
public class ServerScatter
{
    public bool triggered;
    public int count;
    public List<ServerPosition> positions;
    public double payoutCredits;
    public double winInCash;
    public int spinsAwarded;
    public string payMode;
}

[Serializable]
public class ServerNudgeApplied
{
    public bool applied;
    public int reel;
    public string direction;
}

[Serializable]
public class ServerJackpot
{
    public bool triggered;
    public string tier;
    public double amount;
}

[Serializable]
public class ServerWaysWin
{
    public int symbolId;
    public int matchCount;
    public int waysCount;
    public List<ServerPosition> matchedPositions;
    public double basePayout;
    public double appliedMultiplier;
    public double winInCredits;
    public double winInCash;
    public string winType;
}

[Serializable]
public class ServerPosition
{
    public int row;
    public int col;
}

[Serializable]
public class ServerUSpinResult
{
    public bool triggered;
    public ServerUSpinResultDetail result;
}

[Serializable]
public class ServerUSpinResultDetail
{
    public int sliceIndex;
    public string type;
    public double multiplierAwarded;
    public int freeGamesAwarded;
    public double winInCash;
}

[Serializable]
public class ServerMoneyBagResult
{
    public bool triggered;
    public ServerMoneyBagResultDetail result;
}

[Serializable]
public class ServerMoneyBagResultDetail
{
    public int pickedIndex;
    public List<int> revealed;
    public int creditsAwarded;
    public double winInCash;
}

[Serializable]
public class ServerFreeGamesResult
{
    public bool triggered;
    public int totalAwarded;
    public int played;
    public double totalFreeGamesWin;
}

// ============================================================================
// Client-Side Spin Request
// ============================================================================

[Serializable]
public class SpinRequest
{
    public string type = "SPIN";
    public SpinPayload payload;
}

[Serializable]
public class SpinPayload
{
    public int betIndex;
    public bool isFreeSpin;
}

[Serializable]
public class JackpotOpenRequest
{
    public string type = "JACKPOT_OPEN";
    public JackpotOpenPayload payload = new JackpotOpenPayload();
}

[Serializable]
public class JackpotOpenPayload
{
  public string tier;
}

[Serializable]
public class ServerNudgeOption
{
    public int reel;
    public string direction;
}

[Serializable]
public class NudgeRequest
{
    public string type = "BONUS";      // FF server: nudge is sent as a BONUS action
    public NudgePayload payload;
}

[Serializable]
public class NudgePayload
{
    public string Event = "nudge";     // server field name is "Event" (capital E)
    public int reel;                   // 0-based reel index
    public string direction;           // "raise" or "lower"
}


#endregion

#region Game Configuration (Client Side Converted)

[Serializable]
public class GameConfig
{
    public int reelCount = 5;
    public int rowCount = 3;
    public int symbolCount = 11;      // FF: 11 symbols (0-10)
    public int paylineCount = 243;
    public List<List<int>> paylines;
    public List<double> availableBets;
    public List<SymbolInfo> symbols;

    // Wild configuration
    public int wildSymbolId = -1;      // FF: no wild symbol

    // Scatter configuration
    public int scatterSymbolId = 4;    // FF: Bonus (Shooting Star) is ID 4

    public int betMultiplier = 1;      // CNY is cash-bet based, multiplier default is 1
    public double creditDivisor = 20;  // FF: Task 2d - Default creditDivisor fallback 20
    public int maxWinMultiplier = 10000;
    public int minWinMultiplier = 10;
    public int initialFreeSpins = 12;
    public ExtraSpinsData extraSpinsData; // Keep to avoid compilation error in UI

    // uSpin
    public List<USpinSegment> uSpinSegments;
}

[Serializable]
public class SymbolInfo
{
    public int id;
    public string name;
    public List<double> multipliers;
    public bool isWild;
    public bool isScatter;
    public int wildMultiplier = 1;
    public int minMatch;
}

#endregion

#region Player & Game State (Client Side)

[Serializable]
public class PlayerData
{
    public double balance;
    public int currentBetIndex;
}

[Serializable]
public class SpinResult
{
    public List<List<int>> resultMatrix;  // Client uses int matrix
    public double winAmount;
    public double grandTotalWin;
    public List<WinLine> winLines;
    public PlayerData playerData;
    public FreeSpinData freeSpinData;
    public ScatterData scatterData;
    public OverlayScatterData overlayScatterData; // Keep for safety/UI compilation
    public Dictionary<string, int> stickyWilds;  // Keep for safety/UI compilation

    // Server-authoritative free spin state
    public int serverSpinsRemaining;
    public int serverSpinsUsed;
    public int serverTotalSpins;
    public double serverTotalRoundWin;
    public bool isRoundOver;
    
    // Server-authoritative wheel data
    public USpinResultData uSpinData;
    public MoneyBagResultData moneyBagData;

    // Future Fortune Nudge fields
    public List<int> above;
    public List<int> below;
    public bool awaitingNudge;
    public List<ServerNudgeOption> nudgeOptions; // FF: Server pre-calculated valid moves

    // FF: Task 3 - Real server fields
    public List<ServerLineWin> lineWins;
    public double lineWin;
    public ServerScatter scatter;
    public ServerNudgeApplied nudge;
    public double bonusMultiplier;
    public int freeSpinsAwarded;
    public int freeSpinsRemaining;
    public double totalFreeSpinsWin;
    public bool isFreeSpin;
    public ServerJackpot jackpot;

    public double GetMoneyBagWin()
    {
        return (moneyBagData != null && moneyBagData.triggered) ? moneyBagData.winInCash : 0;
    }

    public double GetUSpinCashWin()
    {
        return (uSpinData != null && uSpinData.triggered && uSpinData.type == "MULTIPLIER") ? uSpinData.winInCash : 0;
    }

    public double GetTotalFeatureDeferredWins()
    {
        return GetMoneyBagWin() + GetUSpinCashWin();
    }
}

[Serializable]
public class WinLine
{
    public int lineId;
    public int symbolId;
    public List<int> positions;  // Flat list: [row * 5 + col]
    public double winAmount;
}

[Serializable]
public class FreeSpinData
{
    public bool isTriggered;
    public int spinsAwarded;
    public int remainingSpins;
    public bool isBought;
}

[Serializable]
public class ScatterData
{
    public bool isTriggered;
    public int scatterCount;
    public double winAmount;
}

[Serializable]
public class OverlayScatterData
{
    public bool isTriggered;
    public int count;
    public int extraSpins;
    public List<List<int>> positions;
}

[Serializable]
public class USpinResultData
{
    public bool triggered;
    public int sliceIndex;
    public string type;
    public double multiplierAwarded;
    public int freeGamesAwarded;
    public double winInCash;
}

[Serializable]
public class MoneyBagResultData
{
    public bool triggered;
    public int pickedIndex;
    public List<int> revealed;
    public int creditsAwarded;
    public double winInCash;
}

#endregion

#region Platform Communication

[Serializable]
public class AuthData
{
    public string token;
    public string socketURL;
    public string nameSpace;
}

#endregion

#region Enums

public enum GameState
{
    Initializing,
    Idle,
    Spinning,
    Stopping,
    AwaitingNudge,
    ShowingWin,
    FreeSpinMode
}

public enum SpinSpeed
{
    Normal,
    Turbo,
    QuickSpin
}

public enum WinPopupType
{
    RegularWin,         // Normal credit win (multiplier < 500x)
    BigWin,             // Big win (multiplier >= 500x)
    FreeSpinTrigger,    // Free spins awarded from wheel
    MoneyBagCollect,    // Money bag feature collect
    FreeSpinComplete    // All free spins completed
}

#endregion

#region Helper Classes for Conversion

/// <summary>
/// Converts server data to client GameConfig
/// </summary>
public static class InitDataConverter
{
    internal static GameConfig ConvertToGameConfig(InitData serverData)
    {
        var config = new GameConfig
        {
            reelCount = 5,
            rowCount = (serverData.gameData.totalLines == 243) ? 3 : (serverData.gameData.totalLines == 1024 ? 4 : 3),
            symbolCount = serverData.uiData.paylines.symbols.Count,
            paylineCount = serverData.gameData.totalLines,
            paylines = serverData.gameData.lines,
            availableBets = serverData.gameData.bets,
            creditDivisor = (serverData.gameData != null && serverData.gameData.creditDivisor > 0) ? serverData.gameData.creditDivisor : 20, // FF: Task 2d
            symbols = new List<SymbolInfo>()
        };

        foreach (var serverSymbol in serverData.uiData.paylines.symbols)
        {
            var symbolInfo = new SymbolInfo
            {
                id = serverSymbol.id,
                name = serverSymbol.name,
                multipliers = new List<double>(),
                isWild = serverSymbol.name.ToLower().Contains("wild") || serverSymbol.group == "wild",
                isScatter = serverSymbol.group == "scatter" ||
                            serverSymbol.name.ToLower().Contains("scatter") ||
                            serverSymbol.name.ToLower().Contains("bonus") ||
                            serverSymbol.name.ToLower().Contains("uspin") ||
                            serverSymbol.name.ToLower().Contains("moneybag"),
                minMatch = serverSymbol.minMatch
            };

            // Store raw payout values for info page
            if (serverSymbol.payout != null)
            {
                for (int i = serverSymbol.payout.Count - 1; i >= 0; i--)
                {
                    symbolInfo.multipliers.Add(serverSymbol.payout[i]);
                }
            }
            config.symbols.Add(symbolInfo);

            if (symbolInfo.isWild)
            {
                config.wildSymbolId = symbolInfo.id;
            }
            if (symbolInfo.isScatter && (serverSymbol.group == "scatter" ||
                                         symbolInfo.name.ToLower().Contains("bonus") ||
                                         symbolInfo.name.ToLower().Contains("uspin")))
            {
                config.scatterSymbolId = symbolInfo.id;
            }
        }

        if (serverData.features != null)
        {
            config.betMultiplier = serverData.features.betMultiplier > 0 ? serverData.features.betMultiplier : 1;
            config.maxWinMultiplier = serverData.features.maxWinMultiplier;
            config.minWinMultiplier = serverData.features.minWinMultiplier;

            if (serverData.features.freeGames != null)
            {
                config.initialFreeSpins = serverData.features.freeGames.maxTotalFreeGames;
            }

            if (serverData.features.uSpin != null && serverData.features.uSpin.segments != null)
            {
                config.uSpinSegments = serverData.features.uSpin.segments;
            }
        }

        return config;
    }

    internal static PlayerData ConvertToPlayerData(ServerPlayer serverPlayer, int defaultBetIndex = 0)
    {
        return new PlayerData
        {
            balance = serverPlayer.balance,
            currentBetIndex = defaultBetIndex
        };
    }

    /// <summary>
    /// Converts server response to client SpinResult
    /// </summary>
    public static SpinResult ConvertServerResponseToSpinResult(ServerSpinResponse serverResponse, double currentBalance, double betAmount, GameConfig gameConfig)
    {
        double winAmountVal = 0;
        if (serverResponse.payload != null)
        {
            winAmountVal = serverResponse.payload.winAmount > 0 
                ? serverResponse.payload.winAmount 
                : (serverResponse.payload.lineWin > 0 ? serverResponse.payload.lineWin : serverResponse.payload.totalWin);
        }

        double totalPay = (gameConfig != null && gameConfig.creditDivisor > 0) ? betAmount * gameConfig.creditDivisor : betAmount * 20; // FF: Task 2d
        double newBalance = serverResponse.player?.balance ?? CalculateNewBalance(currentBalance, totalPay, winAmountVal);

        int spinsRemaining = 0;
        int spinsUsed = 0;
        int totalSpins = 0;
        double totalRoundWin = 0;
        bool isRoundOver = false;

        // FF: Task 4 - Real server free games fields
        if (serverResponse.payload != null)
        {
            if (serverResponse.payload.freeSpinsRemaining > 0 || serverResponse.payload.freeSpinsAwarded > 0 || serverResponse.payload.isFreeSpin)
            {
                spinsRemaining = serverResponse.payload.freeSpinsRemaining;
                totalRoundWin = serverResponse.payload.totalFreeSpinsWin > 0 ? serverResponse.payload.totalFreeSpinsWin : serverResponse.payload.totalRoundWin;
                totalSpins = spinsRemaining + serverResponse.payload.freeSpinsAwarded;
                isRoundOver = serverResponse.payload.isRoundOver || (serverResponse.payload.isFreeSpin && serverResponse.payload.freeSpinsRemaining == 0);
            }
            else if (serverResponse.payload.freeGames != null)
            {
                // Fallback to legacy freeGames object
                spinsRemaining = serverResponse.payload.freeGames.totalAwarded - serverResponse.payload.freeGames.played;
                spinsUsed = serverResponse.payload.freeGames.played;
                totalSpins = serverResponse.payload.freeGames.totalAwarded;
                totalRoundWin = serverResponse.payload.freeGames.totalFreeGamesWin;
                isRoundOver = serverResponse.payload.freeGames.played >= serverResponse.payload.freeGames.totalAwarded && serverResponse.payload.freeGames.totalAwarded > 0;
            }
            else
            {
                // FF: Task 1 - Normal base spins send isRoundOver=true and totalRoundWin=winAmount.
                // Do NOT mark isRoundOver as true for free spins handling unless actually in free spins.
                isRoundOver = false;
                totalRoundWin = 0;
            }
        }

        double grandTotalWinVal = (serverResponse.payload != null && serverResponse.payload.grandTotalWin > 0)
            ? serverResponse.payload.grandTotalWin
            : winAmountVal;

        // FF: Task 3 - Convert lineWins (primary), waysWins (fallback), and append scatter
        var winLinesList = ConvertWinningLines(serverResponse.payload?.lineWins, serverResponse.payload?.waysWins, serverResponse.payload?.scatter, gameConfig);

        UnityEngine.Debug.Log($"[FF SpinResult] winAmount: {winAmountVal}, lineWins: {serverResponse.payload?.lineWins?.Count ?? 0}, scatterTriggered: {serverResponse.payload?.scatter?.triggered ?? false}, freeSpinsRemaining: {spinsRemaining}");

        FreeSpinData fsData = null;
        if (serverResponse.payload != null)
        {
            if (serverResponse.payload.freeSpinsAwarded > 0 && !serverResponse.payload.isFreeSpin)
            {
                fsData = new FreeSpinData
                {
                    isTriggered = true,
                    spinsAwarded = serverResponse.payload.freeSpinsAwarded,
                    remainingSpins = serverResponse.payload.freeSpinsRemaining > 0 ? serverResponse.payload.freeSpinsRemaining : serverResponse.payload.freeSpinsAwarded,
                    isBought = false
                };
            }
            else if (serverResponse.payload.freeGames != null && serverResponse.payload.freeGames.triggered)
            {
                fsData = new FreeSpinData
                {
                    isTriggered = true,
                    spinsAwarded = serverResponse.payload.freeGames.totalAwarded,
                    remainingSpins = serverResponse.payload.freeGames.totalAwarded - serverResponse.payload.freeGames.played,
                    isBought = false
                };
            }
        }

        var result = new SpinResult
        {
            resultMatrix = ConvertReelsToMatrix(serverResponse.payload?.reels, serverResponse.matrix, serverResponse.payload?.waysWins, gameConfig),
            winAmount = winAmountVal,
            grandTotalWin = grandTotalWinVal,
            winLines = winLinesList,
            lineWins = serverResponse.payload?.lineWins,
            lineWin = serverResponse.payload?.lineWin ?? 0,
            scatter = serverResponse.payload?.scatter,
            nudge = serverResponse.payload?.nudge,
            bonusMultiplier = serverResponse.payload?.bonusMultiplier ?? 1,
            freeSpinsAwarded = serverResponse.payload?.freeSpinsAwarded ?? 0,
            freeSpinsRemaining = spinsRemaining,
            totalFreeSpinsWin = totalRoundWin,
            isFreeSpin = serverResponse.payload?.isFreeSpin ?? false,
            jackpot = serverResponse.payload?.jackpot,
            above = serverResponse.payload?.above,
            below = serverResponse.payload?.below,
            awaitingNudge = serverResponse.payload != null && serverResponse.payload.awaitingNudge,
            nudgeOptions = serverResponse.payload?.nudgeOptions,

            playerData = new PlayerData
            {
                balance = newBalance,
                currentBetIndex = 0
            },

            freeSpinData = fsData,

            scatterData = (serverResponse.payload != null && (serverResponse.payload.scatterTriggered || (serverResponse.payload.scatter != null && serverResponse.payload.scatter.triggered)))
                ? new ScatterData
                {
                    isTriggered = true,
                    scatterCount = serverResponse.payload.scatter != null ? serverResponse.payload.scatter.count : serverResponse.payload.scatterCount,
                    winAmount = serverResponse.payload.scatter != null ? serverResponse.payload.scatter.winInCash : 0
                }
                : null,

            overlayScatterData = null,
            stickyWilds = null,

            serverSpinsRemaining = spinsRemaining,
            serverSpinsUsed = spinsUsed,
            serverTotalSpins = totalSpins,
            serverTotalRoundWin = totalRoundWin,
            isRoundOver = isRoundOver,
            
            uSpinData = (serverResponse.payload != null && serverResponse.payload.uSpin != null && serverResponse.payload.uSpin.triggered && serverResponse.payload.uSpin.result != null)
                ? new USpinResultData
                {
                    triggered = true,
                    sliceIndex = serverResponse.payload.uSpin.result.sliceIndex,
                    type = serverResponse.payload.uSpin.result.type,
                    multiplierAwarded = serverResponse.payload.uSpin.result.multiplierAwarded,
                    freeGamesAwarded = serverResponse.payload.uSpin.result.freeGamesAwarded,
                    winInCash = serverResponse.payload.uSpin.result.winInCash
                }
                : null,
                
            moneyBagData = (serverResponse.payload != null && serverResponse.payload.moneyBag != null && serverResponse.payload.moneyBag.triggered && serverResponse.payload.moneyBag.result != null)
                ? new MoneyBagResultData
                {
                    triggered = true,
                    pickedIndex = serverResponse.payload.moneyBag.result.pickedIndex,
                    revealed = serverResponse.payload.moneyBag.result.revealed,
                    creditsAwarded = serverResponse.payload.moneyBag.result.creditsAwarded,
                    winInCash = serverResponse.payload.moneyBag.result.winInCash
                }
                : null
        };

        return result;
    }

    // FF: Task 7 - Helper to convert server rows to client columns
    internal static List<List<int>> ConvertServerRowsToClientColumns(List<List<int>> serverRows)
    {
        if (serverRows == null || serverRows.Count == 0) return null;
        int totalRows = serverRows.Count;
        int totalCols = serverRows[0].Count;
        var matrix = new List<List<int>>();
        for (int col = 0; col < totalCols; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < totalRows; row++)
            {
                column.Add(serverRows[row][col]);
            }
            matrix.Add(column);
        }
        return matrix;
    }

    internal static List<List<int>> ConvertReelsToMatrix(List<List<string>> serverReels, List<List<string>> serverMatrix, List<ServerWaysWin> waysWins, GameConfig gameConfig)
    {
        var sourceReels = serverMatrix ?? serverReels;
        int rowCount = gameConfig != null ? gameConfig.rowCount : 3;

        if (sourceReels == null || sourceReels.Count == 0)
        {
            UnityEngine.Debug.LogError("Invalid server reels/matrix: sourceReels is null or empty");
            return GenerateDefaultMatrix(rowCount);
        }

        int totalRows = sourceReels.Count;
        int totalCols = sourceReels[0].Count;

        var matrix = new List<List<int>>();

        for (int col = 0; col < totalCols; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < totalRows; row++)
            {
                if (col >= sourceReels[row].Count)
                {
                    UnityEngine.Debug.LogError($"Invalid server data at row {row}, col {col}");
                    column.Add(0);
                    continue;
                }

                string symbolStr = sourceReels[row][col];
                if (!int.TryParse(symbolStr, out int symbolId))
                {
                    UnityEngine.Debug.LogError($"Failed to parse symbol: {symbolStr}");
                    column.Add(0);
                    continue;
                }

                column.Add(symbolId);
            }
            matrix.Add(column);
        }

        return matrix;
    }

    private static List<List<int>> GenerateDefaultMatrix(int rowCount)
    {
        var matrix = new List<List<int>>();
        for (int col = 0; col < 5; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                column.Add(0);
            }
            matrix.Add(column);
        }
        return matrix;
    }

    // FF: Task 3 - Convert lineWins (primary), waysWins (fallback), and append scatter
    private static List<WinLine> ConvertWinningLines(
        List<ServerLineWin> serverLineWins,
        List<ServerWaysWin> serverWaysWins,
        ServerScatter scatter,
        GameConfig gameConfig)
    {
        var winLines = new List<WinLine>();

        // 1. Primary: Line wins from server
        if (serverLineWins != null && serverLineWins.Count > 0)
        {
            foreach (var lw in serverLineWins)
            {
                if (lw == null) continue;
                var flatPositions = new List<int>();

                // FF: Task 1 - Map lineIndex (0-based) and matchCount
                int effectiveLineIndex = lw.GetLineIndex();
                int effectiveMatchCount = lw.GetMatchCount();

                if (lw.positions != null && lw.positions.Count > 0)
                {
                    foreach (var pos in lw.positions)
                    {
                        flatPositions.Add(pos.row * 5 + pos.col);
                    }
                }
                else if (gameConfig != null && gameConfig.paylines != null && gameConfig.paylines.Count > 0)
                {
                    // Fallback: build from payline pattern using lineIndex (0-based)
                    int pIndex = effectiveLineIndex;
                    if (pIndex >= 0 && pIndex < gameConfig.paylines.Count)
                    {
                        var payline = gameConfig.paylines[pIndex];
                        int matchCount = UnityEngine.Mathf.Clamp(effectiveMatchCount, 0, 5);
                        for (int col = 0; col < matchCount && col < payline.Count; col++)
                        {
                            int row = payline[col];
                            flatPositions.Add(row * 5 + col);
                        }
                    }
                }

                winLines.Add(new WinLine
                {
                    lineId = effectiveLineIndex,
                    symbolId = lw.symbolId,
                    positions = flatPositions,
                    winAmount = lw.winInCash
                });
            }
        }
        else if (serverWaysWins != null && serverWaysWins.Count > 0)
        {
            // Fallback: Legacy ways wins
            int index = 0;
            foreach (var waysWin in serverWaysWins)
            {
                if (waysWin == null) continue;
                var flatPositions = new List<int>();
                if (waysWin.matchedPositions != null)
                {
                    foreach (var pos in waysWin.matchedPositions)
                    {
                        flatPositions.Add(pos.row * 5 + pos.col);
                    }
                }

                winLines.Add(new WinLine
                {
                    lineId = index++,
                    symbolId = waysWin.symbolId,
                    positions = flatPositions,
                    winAmount = waysWin.winInCash
                });
            }
        }

        // 2. Add scatter WinLine (symbolId 4) from scatter.positions when scatter.winInCash > 0 and not already covered
        if (scatter != null && scatter.winInCash > 0)
        {
            bool alreadyCovered = false;
            foreach (var wl in winLines)
            {
                if (wl.symbolId == 4 && UnityEngine.Mathf.Approximately((float)wl.winAmount, (float)scatter.winInCash))
                {
                    alreadyCovered = true;
                    break;
                }
            }

            if (!alreadyCovered)
            {
                var flatScatter = new List<int>();
                if (scatter.positions != null)
                {
                    foreach (var pos in scatter.positions)
                    {
                        flatScatter.Add(pos.row * 5 + pos.col);
                    }
                }

                winLines.Add(new WinLine
                {
                    lineId = 999, // Scatter line ID
                    symbolId = 4, // Bonus / Scatter
                    positions = flatScatter,
                    winAmount = scatter.winInCash
                });
            }
        }

        return winLines;
    }

    private static double CalculateNewBalance(double currentBalance, double totalPay, double winAmount)
    {
        return currentBalance + winAmount;
    }
}

#endregion
