# Boolean and FPGA Move-Generation Research

## 1. Research objective

This document describes a Boolean, table-driven, and FPGA-oriented
move-generation track for WinDama.

The goal is not to replace the software engine immediately. The goal is to
derive a hardware-suitable model whose legal moves are exactly equivalent to
the readable C# reference implementation and the future bitboard engine.

## 2. Fixed-width board model

The 32 playable squares can be represented as Boolean vectors:

```text
p1_man[31:0]
p1_dama[31:0]
p2_man[31:0]
p2_dama[31:0]
```

Derived vectors are:

```text
p1       = p1_man OR p1_dama
p2       = p2_man OR p2_dama
occupied = p1 OR p2
empty    = NOT occupied AND playable_mask
```

Control inputs may include:

```text
side_to_move
request_valid
operation_mode
position_id or board vectors
```

Outputs can include:

```text
move_valid
source
destination
captured squares
capture count
promotion flag
last_move
busy
done
error
```

## 3. Boolean equations for quiet moves

For each legal ordinary-man source and target pair:

```text
legal_simple_i_j =
    my_man[i]
    AND empty[j]
    AND direction_ok(i, j)
```

Only geometrically valid pairs need hardware equations.

A table can map each source to up to two quiet destinations, reducing logic and
eliminating runtime coordinate calculations.

## 4. Boolean equations for ordinary captures

For each valid source, jumped square, and landing triple:

```text
legal_capture_i_k_j =
    my_man[i]
    AND opponent[k]
    AND empty[j]
```

Geometry is guaranteed by the table entry.

Each successful equation produces a candidate containing source, captured
square, landing square, and promotion information.

## 5. Boolean equations for Dama moves

Damas require path-clear conditions.

A quiet move from `i` to `j` is legal when:

```text
my_dama[i]
AND empty[j]
AND ((occupied AND path_mask[i,j]) == 0)
```

A capture candidate from source `i`, over opponent `k`, to landing `j` is legal
when:

```text
my_dama[i]
AND opponent[k]
AND empty[j]
AND ((occupied AND path_before[i,k]) == 0)
AND ((occupied AND path_after[k,j]) == 0)
AND first_relevant_blocker_is_k
```

The final condition prevents jumping over an earlier piece.

## 6. ROM and lookup tables

A practical implementation can use precomputed ROMs:

```text
MoveTable
    source -> quiet candidate destinations

ManCaptureTable
    source -> (captured, landing) candidates

DamaCaptureTable
    source -> (captured, landing, path IDs) candidates

PathMaskTable
    path ID -> 32-bit blocker mask

RayTable
    source and direction -> ordered ray squares or mask

PromotionTable
    player and landing -> promotion flag

SquareMapTable
    software coordinate <-> hardware index
```

Table-generation code should be shared with, or checked against, the software
model to prevent manually copied geometry errors.

## 7. Proposed top-level architecture

```text
                 +----------------------+
board vectors -->| Input/state register |
                 +----------+-----------+
                            |
                            v
                 +----------------------+
                 | Candidate ROM reader |
                 +----------+-----------+
                            |
                            v
                 +----------------------+
                 | Parallel legality    |
                 | evaluators           |
                 +----------+-----------+
                            |
                +-----------+-----------+
                |                       |
                v                       v
       +----------------+      +------------------+
       | Quiet selector |      | Capture explorer |
       +----------------+      +---------+--------+
                                         |
                                         v
                               +------------------+
                               | Max-capture      |
                               | filter           |
                               +---------+--------+
                                         |
                                         v
                               +------------------+
                               | Result encoder   |
                               +------------------+
```

If any capture exists, the quiet-selector output is suppressed.

## 8. Pipeline

A possible pipeline is:

### Stage 1 â€” Input

- latch board vectors and side to move;
- calculate occupied, empty, friendly, and opponent masks.

### Stage 2 â€” Candidate fetch

- read quiet and capture candidates for active pieces;
- fetch path masks and promotion flags.

### Stage 3 â€” Legality evaluation

- evaluate occupancy and ownership equations;
- produce valid first-step captures and quiet moves.

### Stage 4 â€” Capture exploration

- update a capture state;
- generate successor captures;
- push or schedule unexplored branches.

### Stage 5 â€” Maximum selection

- track the greatest completed capture count;
- discard shorter completed sequences;
- retain all tied maximal sequences.

### Stage 6 â€” Output

- encode moves into a FIFO or result RAM;
- signal count and completion.

Pipelining benefits must be evaluated against control complexity and memory
bandwidth.

## 9. Multicapture architectures

Possible implementations include:

1. finite-state-machine depth-first search;
2. microcoded capture explorer;
3. bounded-depth parallel expansion;
4. several parallel DFS workers;
5. hybrid CPU/FPGA control.

A capture state may contain:

```text
current square
piece type
p1_man
p1_dama
p2_man
p2_dama
captured mask
capture count
path encoding
next-candidate index
```

Depth-first search usually minimizes memory but can reduce parallelism.
Breadth or bounded expansion exposes parallel branches but requires more state
storage.

## 10. Maximum-capture computation

The hardware must return only legal maximal sequences.

A straightforward design stores:

```text
best_capture_count
result_count
result_RAM
```

When a completed sequence has:

- a lower count: discard it;
- a greater count: clear previous results and store it;
- an equal count: append it.

An optional admissible upper bound may prune branches later, but the first
prototype should prefer simple verifiable control logic.

## 11. Software/FPGA interface

Possible interfaces include:

- memory-mapped registers;
- AXI4-Lite control plus BRAM result storage;
- streaming input and output;
- UART or USB for an educational prototype;
- a simulation-only file or DPI interface.

A transaction should specify the board and side to move, then return a
canonical move list comparable with C# output.

The interface must define:

- bit ordering;
- byte ordering;
- maximum result count;
- path encoding;
- timeout and error behaviour;
- version identifier.

## 12. Verification strategy

Verification proceeds in layers.

### Unit-level table verification

Check every table entry against coordinate geometry:

- source and target are playable;
- directions are legal;
- path masks contain exactly intermediate squares;
- promotion flags are correct.

### RTL module tests

Test:

- input latching;
- quiet equations;
- ordinary captures;
- Dama blocker detection;
- state updates;
- result storage;
- maximum-count filtering.

### Reference comparison

For each position:

```text
1. Generate moves with WinDama.Core.MoveGenerator.
2. Generate moves with the bitboard model.
3. Generate moves with the RTL simulator.
4. Convert all outputs to one canonical representation.
5. Compare the complete move sets exactly.
```

Use tactical JSON positions, random legal positions, opening-book positions,
and AI-vs-AI game positions.

### Regression targets

- mandatory capture;
- longest multicapture;
- tied maximal sequences;
- flying-Dama quiet rays;
- several Dama landing squares;
- blocked paths;
- promotion;
- no recapture of removed pieces;
- no-move positions;
- maximum expected path depth.

## 13. Simulation and synthesis workflow

A reproducible workflow can include:

```text
C# table generator
        |
        +-- generated JSON/CSV reference tables
        +-- generated SystemVerilog/VHDL constants
        |
        v
RTL simulator
        |
        +-- unit testbenches
        +-- reference position suite
        |
        v
Synthesis tool
        |
        +-- resource report
        +-- timing report
        +-- power estimate
```

ModelSim or Verilator can be used for behavioural regression. A later target
board and vendor toolchain should be documented when selected.

## 14. Comparison with the software engine

Evaluation should report more than raw clock frequency.

Compare:

- complete move-generation latency;
- positions processed per second;
- maximal-capture latency;
- host-transfer overhead;
- LUT, flip-flop, BRAM, and DSP use;
- maximum clock frequency;
- energy per position when measurable;
- complexity of updating rule tables;
- exact equivalence rate.

The readable C# engine optimizes development and verification. The bitboard
engine optimizes compact software execution. The FPGA design explores
parallel, predictable Boolean evaluation.

## 15. Research questions

The project can investigate:

- which parts of Dama generation benefit most from parallel hardware;
- whether ROM-based paths reduce logic enough to justify memory use;
- whether multicapture control or legality testing dominates latency;
- whether a hybrid CPU/FPGA design is preferable to full hardware DFS;
- how much interface overhead reduces end-to-end benefit;
- whether generated tables simplify formal verification;
- how bitboard and RTL models can share one geometric specification.

## 16. Future work

- generate tables automatically from the Core coordinate model;
- define a versioned hardware move format;
- implement an RTL quiet-move prototype;
- add ordinary capture generation;
- add Dama path evaluation;
- implement a DFS capture explorer;
- compare result sets with all Core tests;
- synthesize and report resources and timing;
- connect a prototype accelerator to a host application;
- investigate formal properties for local move legality.
