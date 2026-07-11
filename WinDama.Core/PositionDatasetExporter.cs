using System;
using System.Collections.Generic;
using System.Linq;

namespace WinDama.Core;

/// <summary>
/// Converts game databases and search results into supervised position datasets.
/// This is the first bridge toward linear evaluators, neural evaluators, or NNUE.
/// </summary>
public sealed class PositionDatasetExporter
{
    private readonly MoveGenerator moveGenerator;

    public PositionDatasetExporter()
        : this(new MoveGenerator())
    {
    }

    public PositionDatasetExporter(MoveGenerator moveGenerator)
    {
        this.moveGenerator = moveGenerator ?? throw new ArgumentNullException(nameof(moveGenerator));
    }

    public PositionDataset FromGameDatabase(GameDatabase database, int maxSamples = 0)
    {
        if (database == null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        List<PositionSample> samples = new List<PositionSample>();
        int limit = maxSamples <= 0 ? int.MaxValue : maxSamples;

        foreach (GameRecord game in database.Games)
        {
            int[,] board = game.CloneInitialBoard();
            int player = game.InitialPlayer == -1 ? -1 : 1;

            foreach (GameMoveRecord moveRecord in game.Moves.OrderBy(move => move.Ply))
            {
                if (samples.Count >= limit)
                {
                    return CreateDataset(database, samples);
                }

                Move recordedMove = moveRecord.ToMove();
                List<Move> legalMoves = moveGenerator.GetPlayerCapturesOrMoves(board, player);
                Move? legalMove = legalMoves.FirstOrDefault(candidate => SameMove(candidate, recordedMove));
                if (legalMove == null)
                {
                    // Stop replaying this game rather than producing corrupted labels.
                    break;
                }

                string playerProfile = player == 1 ? game.PlayerOneProfile : game.PlayerTwoProfile;
                string opponentProfile = player == 1 ? game.PlayerTwoProfile : game.PlayerOneProfile;
                samples.Add(PositionSample.FromGameMove(board, game, moveRecord, playerProfile, opponentProfile));

                MoveExecutor.ApplyMoveInPlace(board, legalMove, moveGenerator);
                player = -player;
            }
        }

        return CreateDataset(database, samples);
    }

    public PositionSample FromSearchResult(
        int[,] board,
        int playerToMove,
        SearchResult result,
        string playerProfile = "",
        string opponentProfile = "",
        string source = "search")
    {
        return PositionSample.FromSearchResult(board, playerToMove, result, source, playerProfile, opponentProfile);
    }

    private static PositionDataset CreateDataset(GameDatabase database, List<PositionSample> samples)
    {
        return new PositionDataset
        {
            Name = $"Dataset from {database.Name}",
            Samples = samples
        };
    }

    private static bool SameMove(Move left, Move right)
    {
        return left.Start.Equals(right.Start)
            && left.End.Equals(right.End)
            && left.CapturedPieces.SequenceEqual(right.CapturedPieces);
    }
}
