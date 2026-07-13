# AI Search and Evaluation

## 1. Overview

WinDama uses a conventional board-game search pipeline adapted to mandatory
capture, flying Damas, and maximal multicaptures.

The search layer depends on the Core rules engine and does not generate moves
independently.

```text
Opening book
      |
      v
Iterative deepening
      |
      v
Alpha-beta search
      |
      +-- MoveGenerator
      +-- MoveExecutor
      +-- TranspositionTable
      +-- Quiescence search
      +-- Evaluation
```

## 2. Search entry point

A search request defines:

- current position;
- side to move;
- search mode;
- depth or time budget;
- evaluation profile;
- cancellation token;
- optional analysis callbacks.

The result should include:

- best legal move;
- score;
- completed depth;
- principal variation;
- node count;
- elapsed time;
- optional ranked candidates and transposition-table statistics.

## 3. Opening-book phase

Before tree search, the engine can query the active opening book.

A book move must be validated against the current legal move set before it is
played. This protects the application from:

- incompatible orientation;
- malformed imported lines;
- rule changes;
- ambiguous or obsolete notation.

When several book moves are available, selection can use frequency, score, or
controlled randomness.

## 4. Iterative deepening

Iterative deepening searches depth 1, then 2, then 3, and so on.

Benefits include:

- a usable best move after every completed iteration;
- improved move ordering from the previous principal variation;
- predictable cancellation under a time limit;
- progressively updated analysis for the interface.

Only a fully completed iteration should normally replace the last trusted
result, unless the implementation explicitly handles partial iterations.

## 5. Alpha-beta search

Alpha-beta pruning evaluates the minimax tree while avoiding branches that
cannot influence the final decision.

```text
Search(position, depth, alpha, beta)
    if terminal
        return terminal score

    if depth == 0
        return Quiescence(position, alpha, beta)

    moves = ordered legal moves

    for move in moves
        score = -Search(child(move), depth - 1, -beta, -alpha)

        if score >= beta
            return beta cutoff

        alpha = max(alpha, score)

    return alpha
```

The actual implementation may use negamax or explicit maximizing and
minimizing branches, but score perspective must be consistent.

## 6. Mandatory captures and search

`MoveGenerator` already returns the complete legal set after mandatory and
longest-capture filtering.

Search must not:

- add quiet moves when captures exist;
- shorten a required multicapture;
- replace the maximal-capture rule with a heuristic preference.

When several maximal sequences remain, normal search and move ordering choose
among them.

## 7. Time management

WinDama supports:

- fixed depth;
- fixed time per move;
- game-clock-based budgets.

Time management should:

1. reserve time for later moves;
2. stop starting new iterations when the remaining budget is insufficient;
3. propagate cancellation through recursive search;
4. return the best result from the latest completed iteration;
5. distinguish user cancellation from normal time expiration.

## 8. Transposition table

A transposition table stores information about previously searched positions.

A typical entry contains:

```text
Zobrist key
depth
score
bound type
best move
generation or age
```

Bound types are:

- exact;
- lower bound;
- upper bound.

Mate or terminal scores may need ply normalization when stored and retrieved.

The table's best move is also valuable for move ordering.

## 9. Zobrist hashing

Zobrist hashing combines random keys for:

- piece type and square;
- side to move;
- any additional state that changes legal moves.

Move execution should update the hash consistently, or the position can be
rehash-verified during development.

Hash-collision risk is low but not zero, so stored keys must be checked before
using an entry.

## 10. Quiescence and capture extensions

A static evaluation at a tactically unstable capture position can produce a
horizon error.

Quiescence search therefore continues selected forcing moves, especially
captures, until the position is sufficiently quiet or a safety limit is
reached.

Because capture is mandatory, quiescence must respect exactly the same legal
capture set as normal search.

Useful safeguards include:

- maximum quiescence ply;
- terminal checks;
- repetition or cycle protection where relevant;
- cancellation checks;
- careful score perspective.

## 11. Move ordering

Good ordering increases alpha-beta cutoffs without changing legal results.

The current search can prioritize:

1. transposition-table best move;
2. previous principal-variation move;
3. captures and capture quality;
4. promotion moves;
5. killer moves;
6. history-heuristic score;
7. Dama activity and safety indicators;
8. stable fallback ordering.

Randomness should only be introduced among moves that are effectively equal
under a defined tolerance and should be disabled for deterministic tests.

## 12. Killer heuristic

The killer heuristic remembers noncapture moves that caused beta cutoffs at a
given ply.

It is mainly useful when quiet alternatives exist. Mandatory-capture nodes may
derive less benefit because all returned moves are captures.

Killer moves must always be revalidated for the current position.

## 13. History heuristic

The history table increases the score of moves that repeatedly cause useful
cutoffs.

It should be periodically reduced or normalized to avoid overflow and stale
dominance.

History values affect ordering only; they must not alter the evaluation or
legal move set.

## 14. Evaluation

The evaluator estimates a nonterminal position.

Possible features include:

- ordinary-man material;
- Dama material;
- advancement;
- promotion potential;
- mobility;
- center control;
- edge structure;
- trapped or vulnerable pieces;
- immediate capture threats;
- tempo and side-to-move effects.

Weights are stored in evaluation profiles so experiments can compare
configurations without changing search code.

## 15. Linear learned evaluator

WinDama includes support for a linear evaluator trained from exported
positions.

A linear model has advantages for early research:

- fast inference;
- interpretable weights;
- straightforward serialization;
- easy comparison with handcrafted evaluation;
- low integration risk.

Training and inference must use the same feature definitions and score
perspective.

## 16. Principal variation

The principal variation is the best line discovered by the latest completed
search iteration.

It can be reconstructed using:

- child results returned during search;
- a principal-variation table;
- validated transposition-table best moves.

Every move in a displayed principal variation should be legal in the position
created by the previous move.

## 17. Continuous analysis

Continuous analysis repeatedly deepens the current position without committing
a move.

The UI may display:

- depth;
- score;
- best move;
- principal variation;
- nodes and nodes per second;
- elapsed time;
- transposition-table usage;
- top candidate moves.

Pause, resume, and stop must use explicit synchronization and cancellation.
Background search must not directly access WPF controls.

## 18. Tactical benchmark

The tactical benchmark runs selected positions with expected information such
as a best move, acceptable alternatives, or score conditions.

A useful result records:

- solved or unsolved;
- selected move;
- score;
- depth;
- nodes;
- time;
- active profile;
- diagnostic message.

Benchmarks should be deterministic unless a test explicitly studies
randomized selection.

## 19. Profile tournaments

The tournament runner compares evaluation and search profiles through AI-vs-AI
games.

For credible comparisons:

- alternate colors;
- use the same opening set;
- record time controls and engine settings;
- preserve game logs;
- report wins, draws, losses, and aggregate score;
- use enough games to avoid conclusions from a tiny sample.

## 20. Dataset export

Search can export labeled positions in JSONL, CSV, or JSON.

A sample may include:

- board state;
- side to move;
- static evaluation;
- search score;
- best move;
- principal variation;
- candidate moves;
- material and feature values;
- profile and search metadata;
- final game result when known.

Datasets should include a schema or version field when formats evolve.

## 21. Search tests

Important automated tests include:

- legal best move returned;
- forced single move;
- mandatory-capture nodes;
- equally long capture alternatives;
- terminal win and loss scores;
- transposition-table retrieval;
- fixed-depth completion;
- cancellation and time-budget behaviour;
- stable principal variation;
- evaluator serialization;
- opening-book legality;
- deterministic benchmark output.

## 22. Future work

Potential extensions include:

- aspiration windows;
- principal-variation search;
- improved replacement strategies;
- endgame databases;
- parallel root search;
- automated parameter tuning;
- larger learned evaluators after sufficient data exists;
- NNUE-style incremental evaluation;
- bitboard search integration and performance comparison.
