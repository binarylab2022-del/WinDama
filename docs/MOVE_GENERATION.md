# Move Generation and Rules

## 1. Scope

This document explains how WinDama generates and validates legal moves for
Algerian / Spanish checkers.

The readable `WinDama.Core.MoveGenerator` implementation is the reference
rules model. Search, the graphical interface, benchmarks, datasets, and future
bitboard or FPGA implementations must produce equivalent legal move sets.

## 2. Position information

Move generation requires at least:

- piece locations;
- piece type: ordinary man or Dama;
- piece owner;
- side to move;
- board boundaries;
- squares captured earlier in the current sequence, when exploring a
  multicapture.

A move record should identify:

- source square;
- final destination;
- ordered landing path;
- captured squares;
- promotion information;
- notation or display information when required.

## 3. Generation order

The legal-move procedure follows this high-level order:

```text
GenerateLegalMoves(position, player)
    captures = GenerateAllCaptureSequences(position, player)

    if captures is not empty
        maximum = largest captured-piece count
        return captures having that count

    return GenerateQuietMoves(position, player)
```

This ordering ensures that quiet moves are never returned when a capture is
available.

## 4. Mandatory capture

Capture is compulsory. The engine first examines every piece belonging to the
side to move.

When at least one capture sequence exists:

- all quiet moves are discarded;
- only capture sequences remain;
- the maximal-capture rule is then applied.

The user interface should highlight only moves returned by the Core generator.
It should not independently decide that a quiet move is acceptable.

## 5. Longest-capture rule

When several capture sequences exist, WinDama retains those that capture the
maximum number of opposing pieces.

```text
maxCaptured = sequences.Max(move => move.CapturedCount)
legalMoves  = sequences.Where(move => move.CapturedCount == maxCaptured)
```

More than one legal move can remain when different sequences have the same
maximum capture count. Search may order or evaluate these alternatives, but it
must not remove one merely because another was generated first.

## 6. Ordinary-man quiet moves

An ordinary man moves one playable square diagonally forward into an empty
square.

For each piece:

1. determine the two forward diagonal destinations;
2. reject coordinates outside the board;
3. reject occupied destinations;
4. create one move for each remaining destination.

Quiet moves are generated only after the engine confirms that no capture
exists anywhere for the side to move.

## 7. Ordinary-man captures

An ordinary man captures by jumping over an adjacent opposing piece into the
empty square immediately beyond it.

For each legal diagonal direction:

1. locate the adjacent square;
2. require an opponent piece on it;
3. locate the landing square beyond it;
4. require that landing square to be inside the board and empty;
5. create a successor state and continue capture exploration.

Capture directions must follow the rule set implemented by WinDama and remain
covered by rule tests.

## 8. Flying-Dama quiet moves

A Dama is a flying piece. It may move through any number of consecutive empty
squares along a diagonal.

For each of four diagonal directions:

1. advance one square at a time;
2. add each empty square as a legal destination;
3. stop at the first occupied square or board boundary.

The occupied square itself is not a quiet destination.

## 9. Flying-Dama captures

For a Dama capture:

1. scan outward along a diagonal;
2. ignore empty squares before the first occupied square;
3. require the first occupied square to contain an opponent;
4. inspect squares beyond that opponent;
5. add every empty square beyond it, until the next blocker, as a possible
   landing square.

Each landing square creates a separate branch in multicapture exploration.

A friendly blocker, a second blocker before landing, or the edge of the board
terminates that direction.

## 10. Recursive multicapture generation

Capture sequences are generated with depth-first search or equivalent
backtracking.

```text
Explore(position, currentSquare, path, capturedSquares)
    nextCaptures = GenerateCapturesForPiece(position, currentSquare)

    if nextCaptures is empty
        save completed path
        return

    for each capture in nextCaptures
        successor = ApplyCapture(position, capture)
        Explore(
            successor,
            capture.Destination,
            path + capture,
            capturedSquares + capture.CapturedSquare)
```

Every branch must use an isolated successor position or correctly restore all
modified state during backtracking.

## 11. Captured-piece tracking

A capture explorer must not capture the same opposing piece twice in one turn.

Implementations can enforce this by:

- removing each captured piece from the successor position;
- maintaining an explicit captured-square set or mask;
- using both techniques for validation.

The completed move stores the captured squares in path order.

## 12. Promotion

A man promotes when it reaches the promotion row according to the active
player orientation.

The current WinDama baseline treats promotion during a capture as a terminal
event for that sequence: the piece promotes and does not continue as a Dama in
the same turn.

Because promotion rules differ among draughts variants, this behaviour must be
documented and protected by tests.

## 13. Move validation

A move submitted by the interface, opening book, game database, or external
tool should be matched against the legal move set generated for the current
position.

Validation should compare enough information to distinguish multicaptures:

- source;
- destination;
- ordered path;
- captured squares.

Checking only source and final destination can be ambiguous.

## 14. Move execution

`MoveExecutor` is the authoritative state transition component.

Execution should:

1. remove the moving piece from its source;
2. remove every captured piece;
3. place the piece on its destination;
4. apply promotion;
5. update the side to move and game metadata at the controller level;
6. preserve enough information for snapshots and diagnostics.

Search and UI code should not duplicate these operations.

## 15. Terminal positions

After a legal move is applied, the controller checks whether the next player:

- has no pieces;
- has no legal moves;
- satisfies another configured terminal condition.

The rules layer should expose a clear terminal result rather than making the
WPF layer infer one from visuals.

## 16. Reference tests

Important test categories include:

- no-capture quiet positions;
- one mandatory capture;
- quiet moves suppressed by a remote capture;
- several captures with different lengths;
- several equally maximal captures;
- ordinary-man multicaptures;
- flying-Dama captures with several landing choices;
- blocked Dama rays;
- capture paths near board edges;
- promotion after a quiet move;
- promotion during capture;
- no recapture of an already removed piece;
- no-move and no-piece terminal positions.

JSON tactical positions under `TestPositions/` should be used for regression
tests and contributor examples.

## 17. Equivalence testing

Future generators should be checked against the reference engine:

```text
reference = MoveGenerator.GenerateLegalMoves(position)
candidate = OtherGenerator.GenerateLegalMoves(position)

AssertSameCanonicalMoveSet(reference, candidate)
```

Canonical comparison should ignore irrelevant object identity but preserve
source, path, captured squares, destination, and promotion semantics.

## 18. Contributor checklist

Before changing move generation:

- identify the precise rule being modified;
- add a failing test first;
- verify mandatory and longest-capture behaviour;
- test both players and board orientations;
- test ordinary men and Damas;
- test undo/redo after the move;
- run the complete NUnit suite;
- compare generated move sets on tactical and random positions.
