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
        int _maxSearchDepth;
        long _maxNodes;
        TimeControl _time = new TimeControl();
        Board _board = new Board(Board.STARTING_POS_FEN);
        List<Board> _history = new List<Board>();

        public bool Running { get; private set; }
        public Color SideToMove => _board.SideToMove;

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

        internal void Go(int maxDepth, int maxTime, long maxNodes)
        {
            Stop();
            _time.Go(maxTime);
            StartSearch(maxDepth, maxNodes);
        }

        internal void Go(int maxTime, int increment, int movesToGo, int maxDepth, long maxNodes)
        {
            Stop();
            _time.Go(maxTime, increment, movesToGo);
            StartSearch(maxDepth, maxNodes);
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
            _maxSearchDepth = maxDepth;
            _maxNodes = maxNodes;

            Uci.Log($"Search scheduled to take {_time.TimePerMoveWithMargin}ms!");

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

            _searching = new Thread(Search) { Priority = ThreadPriority.Highest };
            _searching.Start();
        }

        private void Search()
        {
            while (CanSearchDeeper())
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

        private bool CanSearchDeeper()
        {
            if (_search == null || _search.Depth >= _maxSearchDepth)
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
                score: (int)SideToMove * _search.Score,
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