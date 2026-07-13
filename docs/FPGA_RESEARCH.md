# Boolean and FPGA Move-Generation Research

This document describes the Boolean, table-driven, and FPGA-oriented
move-generation research track for WinDama. The software rules engine and
future bitboard implementation provide the reference models for verification.
A second research direction is a Boolean and table-driven representation of the move generator, suitable for FPGA acceleration. This is related to the idea of expressing rule legality as Boolean conditions over fixed-width board vectors.

## Boolean board model

The 32 playable squares can be represented as Boolean vectors:

```text
p1_man[32]
p1_dama[32]
p2_man[32]
p2_dama[32]
empty[32]
```

Move legality can then be expressed using Boolean equations or lookup tables over relevant local masks.

## Simple moves as Boolean equations

For ordinary men, each possible source/target pair can be encoded as a fixed rule:

```text
legal_simple_move_i_j = my_man[i] AND empty[j] AND direction_ok(i, j)
```

Only legal diagonal neighbor pairs need to be encoded.

## Capture moves as Boolean equations

For ordinary men:

```text
legal_capture_i_k_j = my_man[i] AND opponent[k] AND empty[j]
```

where `i` is the source, `k` is the jumped square, and `j` is the landing square.

For Damas, each candidate capture can be represented using:

```text
source square
captured square
landing square
path mask before captured piece
path mask after captured piece
```

A Dama capture is legal when:

```text
my_dama[source]
AND opponent[captured]
AND empty[landing]
AND no blocker on path(source, captured)
AND no blocker on path(captured, landing)
```

The path masks can be stored in ROM tables.

## Table-driven FPGA architecture

A practical FPGA architecture can use precomputed tables:

```text
MoveTable       : source -> candidate quiet moves
CaptureTable    : source -> candidate captures
PathMaskTable   : source/captured/landing -> blocker masks
RayTable        : Dama diagonal rays
PromotionTable  : landing square -> promotes?
```

The hardware pipeline can evaluate many candidate moves in parallel by applying Boolean tests to the board vectors.

## Multi-capture FPGA approach

Multi-capture generation can be implemented with either:

1. a small finite-state machine that performs DFS over capture states;
2. a microcoded capture explorer;
3. a bounded-depth parallel expansion network;
4. a hybrid CPU/FPGA approach where FPGA generates capture successors and CPU controls DFS.

Each capture state stores:

```text
current square
piece type
P1/P2 bitboards
capturedMask
capture count
path encoding
```

The FPGA can also compute the maximum capture length by comparing all generated capture paths and returning only maximal sequences.

## Verification strategy

The Boolean/FPGA move generator must be verified against the reference Core engine:

```text
1. Load each JSON tactical position.
2. Generate legal moves with the C# reference MoveGenerator.
3. Generate legal moves with the Boolean/bitboard model.
4. Compare move sets exactly.
5. Repeat for random positions and AI-vs-AI generated positions.
```

Useful verification targets:

```text
mandatory capture
longest multi-capture
Dama flying captures
promotion-capture stop
no recapturing the same piece
blocked/no-move positions
```

This creates a path from a readable C# reference engine to a compact bitboard engine and then to a Boolean/table-driven FPGA accelerator.

