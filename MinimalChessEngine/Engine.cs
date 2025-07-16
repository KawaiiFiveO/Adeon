using System;
using MinimalChess;
using System.Collections.Generic;
using System.Threading;

namespace MinimalChessEngine
{
    class Engine
    {
        PortedSearch _search = null;
        Thread _searching = null;
        Move _best = default;
        //int _maxSearchDepth;
        long _maxNodes;
        TimeControl _time = new TimeControl();
        Board _board = new Board(Board.STARTING_POS_FEN);
        List<Board> _history = new List<Board>();

        private PlayStyle _currentStyle = new PlayStyle();
        private int _movesWithoutCaptureOrCheck = 0;
        private static readonly Random _random = new Random();

        public bool Running { get; private set; }
        public Color SideToMove => _board.SideToMove;

        internal void SetStyle(string styleUciName)
        {
            _currentStyle = StyleManager.GetStyle(styleUciName);
            Uci.Log($"Style set to {_currentStyle.Name} (UCI: {styleUciName})");
        }

        internal void Play(Move move)
        {
            Stop();
            bool isCapture = _board[move.ToSquare] != Piece.None;
            _board.Play(move);
            _history.Add(new Board(_board));
            bool isCheck = _board.IsChecked(_board.SideToMove);

            if (isCapture || isCheck)
                _movesWithoutCaptureOrCheck = 0;
            else
                _movesWithoutCaptureOrCheck++;
        }


        public void Start() { Stop(); Running = true; }
        internal void Quit() { Stop(); Running = false; }

        internal void SetupPosition(Board board)
        {
            Stop();
            _board = new Board(board);
            _history.Clear();
            _history.Add(new Board(_board));
            _movesWithoutCaptureOrCheck = 0; // Reset boredom on new position
        }

        // The Go methods are now simplified. They just pass the time info along.
        internal void Go(int maxDepth, int maxTime, long maxNodes)
        {
            if (OpeningBook.TryGetMove(_board, _currentStyle, out Move bookMove))
            {
                Uci.BestMove(bookMove);
                return;
            }
            Stop();
            _time.Go(maxTime);
            StartSearch(maxDepth, maxNodes);
        }

        internal void Go(int maxTime, int increment, int movesToGo, int maxDepth, long maxNodes)
        {
            if (OpeningBook.TryGetMove(_board, _currentStyle, out Move bookMove))
            {
                Uci.BestMove(bookMove);
                return;
            }
            Stop();
            // For the cheater, we might adjust movesToGo to save time, but we do it in StartSearch.
            if (_currentStyle.Name == "TheChessDotComCheater" && !IsInLosingPosition())
            {
                // Not losing, so play conservatively to save time for a potential future panic.
                movesToGo += 10;
            }
            _time.Go(maxTime, increment, movesToGo);
            StartSearch(maxDepth, maxNodes);
        }

        // ADD A HELPER METHOD TO CHECK IF WE ARE LOSING
        private bool IsInLosingPosition()
        {
            // To check if we're losing, we need a quick evaluation of the position.
            // We can do a very shallow search (depth 1 or 2) to get a rough idea.
            // This is a "scout" search.
            var scoutSearch = new PortedSearch(_board, 1000, _currentStyle, _movesWithoutCaptureOrCheck);
            scoutSearch.SearchDeeper(_board); // Search to depth 1
            int currentScore = scoutSearch.Score;

            Uci.Log($"Cheater mode: Scout search score is {currentScore}");

            // The score is from the perspective of the current player.
            // A negative score means we are losing.
            return currentScore < _currentStyle.PanicThreshold;
        }

        public void Stop()
        {
            if (_searching != null)
            {
                _time.Stop();
                _searching.Join();
                _searching = null;
            }
        }

        private void StartSearch(int maxDepth, long maxNodes)
        {
            int searchDepth = (maxDepth == 99) ? _currentStyle.MaxDepth : maxDepth;
            _maxNodes = maxNodes;

            const int scrambleTimeMs = 1500; // 1.5 second threshold for a scramble

            if (_time.TimeRemainingWithMargin <= scrambleTimeMs)
            {
                Uci.Log("Time scramble! Making a move as fast as possible.");
                // Override time control for a super fast move.
                _time.Go(100, 0, 1); // 100ms budget
                //searchDepth = 1; // Force a depth-1 search for speed.
            }
            else if (_currentStyle.Name == "TheChessDotComCheater" && IsInLosingPosition())
            {
                if (_random.NextDouble() < _currentStyle.PanicChance)
                {
                    Uci.Log("Cheater mode: PANICKING! Thinking deeper and for a long time...");

                    // 1. Think Deeper: Override the style's depth limit for this move.
                    searchDepth = 99; // Use "Normal" depth

                    // 2. Think Longer (Safely):
                    const int safetyBufferMs = 2000; // 2 seconds
                    int availableTime = _time.TimeRemainingWithMargin - safetyBufferMs;

                    if (availableTime > 0)
                    {
                        // Use up to 50% of the available time.
                        int panicTime = availableTime / 2;
                        _time.Go(panicTime, _time.Increment, 1); // Use the existing increment
                    }
                    // If not enough time to panic safely, it will just use the default time calculated in Go().
                }
            }


            Uci.Log($"Search scheduled to take {_time.TimePerMoveWithMargin}ms! Style: {_currentStyle.Name}, Max Depth: {searchDepth}");

            foreach (var position in _history)
                Transpositions.Store(position.ZobristHash, Transpositions.HISTORY, 0, SearchWindow.Infinite, 0, default);

            _search = new PortedSearch(_board, _maxNodes, _currentStyle, _movesWithoutCaptureOrCheck);
            _time.StartInterval();

            // DEBUG
            //Console.WriteLine("[Engine.StartSearch] Starting initial depth 1 search...");
            _search.SearchDeeper(_board, null, _maxNodes);
            //Console.WriteLine("[Engine.StartSearch] Initial depth 1 search finished.");

            Collect();

            if (_search.Aborted || _best == default)
            {
                if (_best != default) Uci.BestMove(_best);
                else Uci.Log("Could not find a best move in initial search.");
                return;
            }

            // Pass the calculated search depth to the search thread
            _searching = new Thread(() => Search(searchDepth)) { Priority = ThreadPriority.Highest };
            _searching.Start();
        }

        private void Search(int maxSearchDepth)
        {
            while (CanSearchDeeper(maxSearchDepth))
            {
                _time.StartInterval();
                _search.SearchDeeper(_board, _time.CheckTimeBudget, _maxNodes);

                if (_search.Aborted)
                    break;

                Collect();
            }

            if (_best != default)
                Uci.BestMove(_best);

            _search = null;
        }

        private bool CanSearchDeeper(int maxSearchDepth)
        {
            if (_search == null || _search.Depth >= maxSearchDepth)
                return false;

            return _time.CanSearchDeeper();
        }

        private void Collect()
        {
            // DEBUG
            //Console.WriteLine($"[Engine.Collect] Collecting results. PV Length: {(_search.PrincipalVariation?.Length ?? -1)}");

            if (_search.PrincipalVariation == null || _search.PrincipalVariation.Length == 0)
            {
                // DEBUG
                //Console.WriteLine("[Engine.Collect] PV is empty. Skipping UCI info update.");
                return;
            }

            _best = _search.PrincipalVariation[0];

            Uci.Info(
                depth: _search.Depth,
                score: _search.Score,
                nodes: _search.NodesVisited,
                timeMs: _time.Elapsed,
                pv: GetPrintablePV(_search.PrincipalVariation, _search.Depth)
            );
        }

        private Move[] GetPrintablePV(Move[] pv, int depth)
        {
            List<Move> result = new(pv);
            if (result.Count < depth)
            {
                Board position = new Board(_board);
                foreach (Move move in pv)
                {
                    position.Play(move);
                }

                while (result.Count < depth && Transpositions.GetBestMove(position, out Move move))
                {
                    // 1. Check if the move is pseudo-legal for the current position.
                    if (move == default || !position.IsPlayable(move))
                    {
                        break; // Stop if the move is invalid or not found
                    }

                    // 2. Check if the move is truly legal (doesn't leave king in check).
                    Board child = new Board(position, move);
                    if (child.IsChecked(position.SideToMove))
                    {
                        break; // Stop if the move is illegal
                    }

                    position.Play(move);
                    result.Add(move);
                }
            }
            return result.ToArray();
        }
    }
}