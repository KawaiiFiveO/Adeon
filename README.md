# Adeon

![Adeon](docs/Adeon.jpg)

A simple UCI-compliant chess engine, written in C# using the [MinimalChess](https://github.com/lithander/MinimalChessEngine) library. Based on code from the [TIChess 4.0](http://tict.ticalc.org/projects.html) engine.

Adeon has a notable feature: its "Style" profiles allow you to customize the playstyle of the engine. Choose from existing profiles, or create your own!

### Features

*   **Search Algorithm:** An improved version of TIChess's **NegaMax Alpha-Beta** search algorithm.
*   **Evaluation:** The engine's chess knowledge is based on TIChess 4.0, featuring:
    *   Material balance.
    *   Positional heuristics for piece placement (e.g., center control).
    *   Pawn structure analysis (doubled, isolated pawns).
    *   King safety and endgame king activity.
*   **Opening Book:** Loaded from a customizable `book.json` file.
*   **Style Profiles:** `styles.json` enables support for different engine personalities and playing styles.
*   **Move Ordering & Search Efficiency:** Search performance is enhanced through several techniques:
    *   **Transposition Table:** A hash table is used to store previously analyzed positions, preventing redundant calculations.
    *   **MVV-LVA:** Captures are prioritized by sorting the "Most Valuable Victim" against the "Least Valuable Aggressor".
    *   **Killer Move Heuristic:** Quiet moves that cause a beta-cutoff are tried earlier in subsequent nodes at the same depth.
    *   **Null Move Pruning:** The engine speculatively gives the opponent a free move; if the resulting position is still very strong for the engine, it assumes the original line is not worth a deep search and prunes it, saving valuable time.
    *   **Late Move Reductions:** Saves time by focusing effort on the most promising moves, while a re-search mechanism ensures that surprisingly good moves are not missed.
*   **Quiescence Search:** To avoid the horizon effect, the search is extended beyond its nominal depth to analyze tactical sequences (captures and check evasions) until the position is "quiet."
*   **Framework:** The core algorithm runs within a UCI-compliant shell that handles:
    *   Communication with chess GUIs (Universal Chess Interface).
    *   Iterative deepening and time management in a separate thread.

### Usage

Download a binary from the [Releases](https://github.com/KawaiiFiveO/Adeon/releases), then use it with a frontend such as [CuteChess](https://github.com/cutechess/cutechess).

### Profiles

In your frontend, you can set a Style for Adeon. These profiles change the behavior of the engine. Currently, the profiles are:

*  **Normal:** Adeon will play at full strength.
*  **Easy:** Adeon will limit searching to a depth of 5. This is comparable to playing TIChess on a real calculator.
*  **The Chess.com Cheater:** Try it for yourself and see ;)
*  **The Unhinged Gambiteer:** ?!

### Sample Games

*  [Adeon (Easy) vs. Toledo NanoChess (~1000 Elo)](https://lichess.org/PuLBl7df)
*  [MinimalChess 0.4 (1816 Elo) vs. Adeon (Cheater)](https://lichess.org/zuWyxMC2)

### Strength

Adeon (Normal/Cheater) is currently estimated to play at a ~2100 Elo level, but profiles such as Unhinged Gambiteer are weaker, estimated around ~1600.

### TODO

* Add more playing profiles
* Improve search efficiency and depth

### Credits

Adeon is licensed under the [Tsundere Public License](https://llamawa.re/licenses/). See `LICENSE.md` for details.

MinimalChess is licensed under the MIT Public License. See `MinimalChessLicense` for details.

TIChess was created by [TICT-HQ](http://tict.ticalc.org/). Redistribution and usage of the source code is allowed with credit.