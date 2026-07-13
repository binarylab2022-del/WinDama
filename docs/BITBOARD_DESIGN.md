# Bitboard Design and Implementation Research

This document describes the planned high-performance bitboard implementation
for WinDama. The current readable `WinDama.Core` engine remains the reference
implementation used for correctness validation.
The current stable engine uses readable Core classes (`MoveGenerator`, `MoveExecutor`, `GameController`) as the reference implementation. A parallel research/development track is planned for a high-performance bitboard implementation of the same Spanish-checkers rules.

## Board representation

The bitboard model uses the 32 playable dark squares of the 8x8 board. Each position can be represented by four 32-bit masks:

```text
P1Men    : bits occupied by Player 1 ordinary men
P1Damas  : bits occupied by Player 1 Damas / kings
P2Men    : bits occupied by Player 2 ordinary men
P2Damas  : bits occupied by Player 2 Damas / kings
```

Derived masks:

```text
P1Pieces = P1Men | P1Damas
P2Pieces = P2Men | P2Damas
Occupied = P1Pieces | P2Pieces
Empty    = ~Occupied & PlayableMask32
```

The implementation should keep a deterministic mapping between board coordinates and playable-square indices:

```text
(row, column) <-> playable index 0..31
```

This makes it possible to convert between the WPF board, JSON test positions, opening-book moves, and the compact bitboard representation.

## Simple man moves

Ordinary non-capturing man moves can be generated with directional shifts and edge masks. For each side, only forward diagonals are legal for quiet moves. A typical bitboard move-generation step is:

```text
candidateTargets = ShiftForwardLeft(myMen) | ShiftForwardRight(myMen)
legalTargets     = candidateTargets & Empty
```

Edge masks are required so pieces do not wrap from one side of the board to the other after a shift.

## Dama / king quiet moves

Spanish-checkers Damas are flying pieces. Their quiet moves can be generated with precomputed diagonal rays:

```text
Ray[square][direction] -> bit mask of all squares along that diagonal direction
```

For each Dama and each diagonal direction:

1. read the ray mask;
2. find the first occupied square on the ray;
3. all empty squares before that blocker are legal quiet destinations;
4. if there is no blocker, all ray squares are legal quiet destinations.

This can be implemented either with bit scans or with precomputed blocker-to-move lookup tables.

## Simple captures

For ordinary men, a capture exists when an adjacent diagonal square contains an opponent piece and the square immediately beyond it is empty:

```text
opponentAdjacent = ShiftDiagonal(myMen) & OpponentPieces
landingSquare    = ShiftSameDirection(opponentAdjacent) & Empty
```

The current rule engine requires mandatory capture, so if any capture exists, quiet moves must be discarded.

## Dama captures

For a flying Dama, a capture ray is valid when the ray contains an opponent piece as the first relevant capturable piece and at least one empty landing square beyond it. The generator must:

1. scan the ray from the Dama square;
2. locate the first occupied square;
3. require that this blocker is an opponent piece;
4. collect all empty landing squares beyond the captured piece until the next blocker.

The move record must store both the landing square and the captured square.

## Multi-captures and longest-capture rule

Spanish checkers requires the longest capture sequence. The bitboard engine should therefore generate captures with a DFS/backtracking procedure:

```text
GenerateCaptures(position, pieceSquare, capturedMask, path)
```

At each recursive step:

1. generate all legal captures for the current piece;
2. remove the captured piece from the opponent bitboard;
3. move the capturing piece to the landing square;
4. mark the captured square in `capturedMask` so the same piece cannot be captured twice;
5. continue until no further capture is available.

After all sequences are generated, keep only those with maximum captured-piece count:

```text
maxLen = max(sequence.CapturedCount)
legalCaptures = sequences where CapturedCount == maxLen
```

Promotion during a capture must follow the current engine rule: when an ordinary man reaches the promotion row during a capture, it promotes and the capture sequence stops; the newly promoted Dama does not continue capturing in the same turn.

## Suggested bitboard API

A future `WinDama.Core.Bitboards` namespace can expose:

```text
BitboardPosition
BitboardMove
BitboardMoveGenerator
BitboardMoveExecutor
BitboardSearchAdapter
BitboardPerft
```

The first goal should be functional equivalence with the existing reference engine. Every bitboard move generator result should be compared against `MoveGenerator` using the existing JSON tactical positions.

