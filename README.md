# Adeon

![Adeon](docs/Adeon.jpg)

A simple UCI-compliant chess engine, written in C# using the [MinimalChess](https://github.com/lithander/MinimalChessEngine) library. Based on code from the [TIChess 4.0](http://tict.ticalc.org/projects.html) engine.

### Features

*   **Search Algorithm:** A modernized port of TIChess's **NegaMax Alpha-Beta** search algorithm.
*   **Evaluation:** The engine's chess knowledge is based on TIChess 4.0, featuring:
    *   Material balance.
    *   Positional heuristics for piece placement (e.g., center control).
    *   Pawn structure analysis (doubled, isolated pawns).
    *   King safety and endgame king activity.
*   **Move Ordering:** Search efficiency is enhanced through several techniques:
    *   **Transposition Table:** A hash table is used to store previously analyzed positions, preventing redundant calculations.
    *   **MVV-LVA:** Captures are prioritized by sorting the "Most Valuable Victim" against the "Least Valuable Aggressor".
    *   **Killer Move Heuristic:** Quiet moves that cause a beta-cutoff are tried earlier in subsequent nodes at the same depth.
*   **Quiescence Search:** To avoid the horizon effect, the search is extended beyond its nominal depth to analyze tactical sequences (captures and check evasions) until the position is "quiet."
*   **Framework:** The core algorithm runs within a UCI-compliant shell that handles:
    *   Communication with chess GUIs (Universal Chess Interface).
    *   Iterative deepening and time management in a separate thread.

### Strength

Adeon's rating is unknown; more testing is required to determine this. However, it is currently capable of winning against ~1000 ELO rated engines such as Toledo NanoChess.

### TODO

* Implement opening book file

### Credits

Adeon is licensed under the [Tsundere Public License](https://llamawa.re/licenses/). See `LICENSE.md` for details.

MinimalChess is licensed under the MIT Public License. See `MinimalChessLicense` for details.

TIChess was created by [TICT-HQ](http://tict.ticalc.org/). Redistribution and usage of the source code is allowed with credit.