using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

public enum TranspositionBound
{
    Exact,
    LowerBound,
    UpperBound
}

public sealed class TranspositionTableEntry
{
    public ulong Key { get; init; }
    public int Depth { get; init; }
    public int Score { get; init; }
    public TranspositionBound Bound { get; init; }
    public Move? BestMove { get; init; }
}

/// <summary>
/// Small deterministic Zobrist-hash transposition table for the core alpha-beta search.
/// Scores are valid only for the same board, side-to-move, and root player because the
/// evaluator returns scores from the root player's perspective.
/// </summary>
public sealed class TranspositionTable
{
    private const int PieceKindCount = 4;
    private readonly ulong[,,] pieceSquareKeys = new ulong[8, 8, PieceKindCount];
    private readonly ulong[] currentPlayerKeys = new ulong[2];
    private readonly ulong[] rootPlayerKeys = new ulong[2];
    private readonly Dictionary<ulong, TranspositionTableEntry> entries = new();
    private readonly int maximumEntries;

    public TranspositionTable(int maximumEntries = 250_000)
    {
        this.maximumEntries = Math.Max(0, maximumEntries);
        InitializeKeys();
    }

    public int Count => entries.Count;
    public long Hits { get; private set; }
    public long Stores { get; private set; }
    public long Cutoffs { get; private set; }

    public void ResetStatistics()
    {
        Hits = 0;
        Stores = 0;
        Cutoffs = 0;
    }

    public ulong ComputeHash(int[,] board, int currentPlayer, int rootPlayer)
    {
        if (board == null)
        {
            throw new ArgumentNullException(nameof(board));
        }

        ulong key = 0;
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                int piece = board[row, column];
                int pieceIndex = PieceToIndex(piece);
                if (pieceIndex >= 0)
                {
                    key ^= pieceSquareKeys[row, column, pieceIndex];
                }
            }
        }

        key ^= currentPlayerKeys[PlayerToIndex(currentPlayer)];
        key ^= rootPlayerKeys[PlayerToIndex(rootPlayer)];
        return key;
    }

    public bool TryProbe(ulong key, int depth, int alpha, int beta, out int score, out Move? bestMove)
    {
        score = 0;
        bestMove = null;

        if (maximumEntries == 0 || !entries.TryGetValue(key, out TranspositionTableEntry? entry))
        {
            return false;
        }

        if (entry.Depth < depth)
        {
            bestMove = entry.BestMove;
            return false;
        }

        Hits++;
        bestMove = entry.BestMove;

        switch (entry.Bound)
        {
            case TranspositionBound.Exact:
                score = entry.Score;
                Cutoffs++;
                return true;
            case TranspositionBound.LowerBound when entry.Score >= beta:
                score = entry.Score;
                Cutoffs++;
                return true;
            case TranspositionBound.UpperBound when entry.Score <= alpha:
                score = entry.Score;
                Cutoffs++;
                return true;
            default:
                return false;
        }
    }

    public Move? TryGetBestMove(ulong key)
    {
        return entries.TryGetValue(key, out TranspositionTableEntry? entry) ? entry.BestMove : null;
    }

    public void Store(ulong key, int depth, int score, TranspositionBound bound, Move? bestMove)
    {
        if (maximumEntries == 0)
        {
            return;
        }

        if (entries.TryGetValue(key, out TranspositionTableEntry? existing) && existing.Depth > depth)
        {
            return;
        }

        if (!entries.ContainsKey(key) && entries.Count >= maximumEntries)
        {
            // Simple, deterministic safeguard for desktop use. A more advanced
            // replacement policy can be added later when benchmarking is available.
            entries.Clear();
        }

        entries[key] = new TranspositionTableEntry
        {
            Key = key,
            Depth = depth,
            Score = score,
            Bound = bound,
            BestMove = CloneMove(bestMove)
        };
        Stores++;
    }

    public IReadOnlyList<Move> OrderBestMoveFirst(IReadOnlyList<Move> moves, Move? bestMove)
    {
        if (bestMove == null || moves.Count <= 1)
        {
            return moves;
        }

        return moves
            .OrderByDescending(move => SameMove(move, bestMove) ? 1 : 0)
            .ToList();
    }

    private void InitializeKeys()
    {
        ulong seed = 0x9E3779B97F4A7C15UL;
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                for (int piece = 0; piece < PieceKindCount; piece++)
                {
                    pieceSquareKeys[row, column, piece] = NextRandom(ref seed);
                }
            }
        }

        for (int i = 0; i < 2; i++)
        {
            currentPlayerKeys[i] = NextRandom(ref seed);
            rootPlayerKeys[i] = NextRandom(ref seed);
        }
    }

    private static ulong NextRandom(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static int PieceToIndex(int piece)
    {
        return piece switch
        {
            1 => 0,
            2 => 1,
            -1 => 2,
            -2 => 3,
            _ => -1
        };
    }

    private static int PlayerToIndex(int player)
    {
        return player >= 0 ? 0 : 1;
    }

    private static Move? CloneMove(Move? move)
    {
        return move == null
            ? null
            : new Move(move.Start, move.End, new List<(int, int)>(move.CapturedPieces));
    }

    private static bool SameMove(Move left, Move right)
    {
        return left.Start == right.Start
            && left.End == right.End
            && left.CapturedPieces.SequenceEqual(right.CapturedPieces);
    }
}
