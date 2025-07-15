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

        private PlayStyle _currentStyle = PlayStyle.Normal;
        private static readonly Random _random = new Random();

        public bool Running { get; private set; }
        public Color SideToMove => _board.SideToMove;

        internal void SetStyle(string styleName)
        {
            // The GUI sends "The_Chess.com_Cheater", so we replace underscores
            switch (styleName.Replace('_', ' '))
            {
                case "Easy":
                    _currentStyle = PlayStyle.Easy;
                    break;
                // ADD THE NEW CASE
                case "The Chess.com Cheater":
                    _currentStyle = PlayStyle.Cheater;
                    break;
                case "Normal":
                default:
                    _currentStyle = PlayStyle.Normal;
                    break;
            }
            Uci.Log($"Style set to {_currentStyle.Name}");
        }


        public void Start() { Stop(); Running = true; }
        internal void Quit() { Stop(); Running = false; }

        internal void SetupPosition(Board board)
        {
            Stop();
            _board = new Board(board);
            _history.Clear();
            _history.Add(new Board(_board));
        }

        internal void Play(Move move)
        {
            Stop();
            _board.Play(move);
            _history.Add(new Board(_board));
        }

        // CHANGE THE GO METHODS TO HANDLE THE NEW LOGIC
        internal void Go(int maxDepth, int maxTime, long maxNodes)
        {
            Stop();

            // Cheater Logic: Decide on the time budget BEFORE starting the search
            int timeForMove = maxTime;
            if (_currentStyle.Name == "The Chess.com Cheater" && IsInLosingPosition())
            {
                if (_random.NextDouble() < _currentStyle.PanicChance)
                {
                    // PANIC! Use a huge chunk of the remaining time.
                    // Let's say 1/4 of the total time, but not less than 10 seconds.
                    timeForMove = Math.Max(10000, _time.TimeRemainingWithMargin / 4);
                    Uci.Log("Cheater mode: PANICKING! Thinking for a long time...");
                }
            }

            _time.Go(timeForMove);
            StartSearch(maxDepth, maxNodes);
        }

        internal void Go(int maxTime, int increment, int movesToGo, int maxDepth, long maxNodes)
        {
            Stop();

            // Cheater Logic: Decide on the time budget BEFORE starting the search
            if (_currentStyle.Name == "The Chess.com Cheater" && IsInLosingPosition())
            {
                if (_random.NextDouble() < _currentStyle.PanicChance)
                {
                    // PANIC! Use a huge chunk of the remaining time.
                    int panicTime = Math.Max(10000, maxTime / 4);
                    Uci.Log("Cheater mode: PANICKING! Thinking for a long time...");
                    _time.Go(panicTime, increment, 1); // Use a movesToGo of 1 to spend the time now
                }
                else
                {
                    // Not panicking, play normally but save time.
                    // We'll pretend there are more moves to go than there really are.
                    int conservativeMovesToGo = movesToGo + 10;
                    _time.Go(maxTime, increment, conservativeMovesToGo);
                }
            }
            else
            {
                // Not the cheater style, or not losing. Play normally.
                _time.Go(maxTime, increment, movesToGo);
            }

            StartSearch(maxDepth, maxNodes);
        }

        // ADD A HELPER METHOD TO CHECK IF WE ARE LOSING
        private bool IsInLosingPosition()
        {
            // To check if we're losing, we need a quick evaluation of the position.
            // We can do a very shallow search (depth 1 or 2) to get a rough idea.
            // This is a "scout" search.
            var scoutSearch = new PortedSearch(_board, 1000); // Limit nodes
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
            int searchDepth = (maxDepth == 64) ? _currentStyle.MaxDepth : maxDepth;
            _maxNodes = maxNodes;

            Uci.Log($"Search scheduled to take {_time.TimePerMoveWithMargin}ms! Style: {_currentStyle.Name}, Max Depth: {searchDepth}");

            foreach (var position in _history)
                Transpositions.Store(position.ZobristHash, Transpositions.HISTORY, 0, SearchWindow.Infinite, 0, default);

            _search = new PortedSearch(_board, _maxNodes);
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
                    position.Play(move);

                while (result.Count < depth && Transpositions.GetBestMove(position, out Move move))
                {
                    if (move == default) break;
                    position.Play(move);
                    result.Add(move);
                }
            }
            return result.ToArray();
        }
    }
}