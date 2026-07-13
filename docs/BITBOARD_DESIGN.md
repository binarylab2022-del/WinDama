# Bitboard Design and Implementation Research

## 1. Introduction

This document describes the planned high-performance bitboard implementation
for WinDama.

The readable `WinDama.Core` rules engine remains the correctness reference.
The first objective of the bitboard track is exact legal-move equivalence, not
premature optimization.

## 2. Why bitboards?

The graphical board naturally uses row and column coordinates, but only 32 of
the 64 squares are playable.

A bitboard represents a set of playable squares with one machine-sized integer.
This permits:

- compact positions;
- fast set operations;
- efficient occupancy tests;
- precomputed masks and rays;
- lower allocation pressure;
- reproducible move-set comparison;
- a representation close to Boolean and FPGA models.

Bitboards should coexist with the readable engine until equivalence is proven.

## 3. Board representation

A position can use four 32-bit masks:

```text
P1Men    ordinary Player 1 pieces
P1Damas  Player 1 Damas
P2Men    ordinary Player 2 pieces
P2Damas  Player 2 Damas
```

Derived masks are:

```text
P1Pieces = P1Men | P1Damas
P2Pieces = P2Men | P2Damas
Occupied = P1Pieces | P2Pieces
Empty    = ~Occupied & PlayableMask32
```

Additional state includes:

- side to move;
- optional incremental hash;
- move counters or repetition information when required;
- orientation information only if it is not globally fixed.

## 4. Square numbering

The implementation requires one deterministic mapping:

```text
(row, column) <-> playable index 0..31
```

One possible numbering scans playable squares row by row:

```text
row 0:  0  1  2  3
row 1:  4  5  6  7
row 2:  8  9 10 11
...
row 7: 28 29 30 31
```

The exact dark-square parity must match the WPF board.

Conversion functions should be centralized:

```text
ToBitIndex(row, column)
ToBoardCoordinate(bitIndex)
ToSquareMask(bitIndex)
```

Tests must cover every playable square and reject light squares.

## 5. Masks and precomputed tables

Useful static data includes:

- playable-square mask;
- promotion-row masks;
- left and right edge masks;
- forward-neighbor masks;
- diagonal-neighbor masks;
- jump masks for ordinary captures;
- four Dama ray masks per square;
- between-square path masks;
- landing masks beyond a potential captured square.

Tables should be generated from coordinate rules in testable build-time or
startup code, then optionally replaced by generated constants.

## 6. Quiet move generation

### Ordinary men

Ordinary quiet moves can be generated with directional transforms and edge
masks.

Conceptually:

```text
candidateTargets =
    ShiftForwardLeft(myMen)
    |
    ShiftForwardRight(myMen)

legalTargets = candidateTargets & Empty
```

The implementation must recover each source square for the generated target.
A direct source-loop with precomputed destination masks may initially be easier
to validate than highly compressed shift code.

### Flying Damas

For each Dama and diagonal direction:

1. read the precomputed ray;
2. identify the nearest blocker;
3. retain all empty squares before that blocker;
4. create one move for each retained destination.

Bit scans or occupancy-indexed lookup tables can accelerate blocker discovery.

## 7. Ordinary captures

For each ordinary piece and capture direction:

1. identify the adjacent square;
2. require an opposing piece;
3. identify the landing square;
4. require it to be empty;
5. create a capture successor.

Precomputed entries can store:

```text
source
captured square
landing square
source mask
captured mask
landing mask
```

Mandatory capture is applied after capture generation for all pieces.

## 8. Flying-Dama captures

For each Dama ray:

1. find the first occupied square;
2. reject the direction if it contains a friendly piece;
3. require the blocker to be an opponent;
4. collect every empty landing square beyond it until the next blocker.

A candidate record can contain:

```text
source
captured square
landing square
path-before mask
path-after mask
```

Legality then becomes a small set of bitwise occupancy tests.

## 9. Multicaptures

Capture sequences require state updates and recursion.

```text
GenerateCaptures(position, pieceSquare, capturedMask, path)
```

At each step:

1. generate legal captures for the current piece;
2. remove the captured opponent bit;
3. move the capturing piece bit;
4. add the captured square to `capturedMask`;
5. continue from the landing square;
6. save the path when no successor exists.

A value-type position can make branching inexpensive, but profiling should
confirm actual costs.

## 10. Longest-capture rule

After all complete sequences are generated:

```text
maximum = sequences.Max(s => s.CapturedCount)
legal   = sequences.Where(s => s.CapturedCount == maximum)
```

The generator must retain every sequence tied for the maximum.

Filtering only at the root is simple and reliable. More advanced pruning can
be added later if an admissible upper bound proves that a branch cannot match
the current maximum.

## 11. Promotion

Promotion masks identify the final rank for each player.

The current WinDama baseline ends a capture sequence when an ordinary man
reaches its promotion row. The resulting piece becomes a Dama for subsequent
turns.

This rule must be verified against the readable generator before optimization.

## 12. Proposed API

A future namespace can expose:

```text
WinDama.Core.Bitboards
|
+-- BitboardPosition
+-- BitboardMove
+-- BitboardMovePath
+-- BitboardTables
+-- BitboardMoveGenerator
+-- BitboardMoveExecutor
+-- BitboardPositionConverter
+-- BitboardPerft
+-- BitboardSearchAdapter
```

Suggested responsibilities:

- `BitboardPosition`: compact immutable or copyable state;
- `BitboardMove`: source, destination, path, captures, promotion;
- `BitboardTables`: validated masks, rays, and paths;
- `BitboardMoveGenerator`: complete legal move set;
- `BitboardMoveExecutor`: trusted state transition;
- `BitboardPositionConverter`: conversion to and from reference positions;
- `BitboardPerft`: deterministic move-tree counting;
- `BitboardSearchAdapter`: integration with the existing search API.

## 13. Testing and equivalence

The bitboard implementation is accepted only after exact comparison with the
reference engine.

Test sources should include:

- existing unit-test positions;
- JSON tactical positions;
- opening-book positions;
- AI-vs-AI positions;
- randomly generated legal positions;
- edge and promotion cases;
- deep multicapture cases.

Canonical move-set comparison should preserve:

- source;
- ordered landing path;
- captured squares;
- final destination;
- promotion result.

Perft-style counts at several depths provide an additional regression signal.

## 14. Performance measurement

No speedup should be claimed before measurement.

Benchmarks should compare:

- legal-move generation time;
- capture-generation time;
- allocations;
- positions or nodes per second;
- search depth reached under equal time;
- conversion overhead between representations.

Use the same positions, release build, runtime, hardware, and warm-up strategy.

Expected benefits are reduced allocation and faster occupancy operations.
Flying-Dama multicaptures may remain dominated by branching rather than simple
bitwise operations, so results must be measured separately.

## 15. Implementation phases

### Phase 1 â€” Representation

- implement square mapping;
- implement conversion;
- generate and test masks and rays.

### Phase 2 â€” Quiet moves

- ordinary quiet moves;
- Dama quiet moves;
- exact comparison tests.

### Phase 3 â€” Captures

- ordinary captures;
- Dama captures;
- recursive multicaptures;
- maximal-capture filtering.

### Phase 4 â€” Execution and perft

- bitboard move execution;
- promotion;
- position hashing;
- perft and random equivalence tests.

### Phase 5 â€” Search integration

- search adapter;
- benchmark against the readable engine;
- profile memory and speed;
- preserve the reference implementation.

## 16. Future work

Possible extensions include:

- occupancy-indexed ray lookup;
- generated source code for masks and paths;
- SIMD experiments where useful;
- incremental evaluation;
- direct Zobrist updates;
- specialized endgame representations;
- shared tables with the FPGA model;
- formal or exhaustive verification of local move rules.
