using MinimalChess;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MinimalChessEngine
{
    public class PortedSearch
    {
        // --- UNCHANGED SECTIONS (Constants, Tables, etc.) ---
        private const int MAX_DEPTH = 64;
        private const int VALUE_MATE = 9999;
        private const int VALUE_PAWN = 100;
        private const int VALUE_KNIGHT = 325;
        private const int VALUE_BISHOP = 350;
        private const int VALUE_ROOK = 500;
        private const int VALUE_QUEEN = 900;
        private const int VALUE_KING = 0;
        private const int VALUE_ENDGAME = (4 * VALUE_ROOK + (2 * VALUE_BISHOP));
        private static readonly int[] PieceValues = { 0, VALUE_PAWN, VALUE_KNIGHT, VALUE_BISHOP, VALUE_ROOK, VALUE_QUEEN, VALUE_KING };
        private static readonly sbyte[] CenterTable = {
             0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
             0,  4,  0,  8, 12, 12,  8,  0,  4,  0, 0,  4,  8, 12, 16, 16, 12,  8,  4,  0,
             0,  8, 12, 16, 20, 20, 16, 12,  8,  0, 0, 12, 16, 20, 24, 24, 20, 16, 12,  0,
             0, 12, 16, 20, 24, 24, 20, 16, 12,  0, 0,  8, 12, 16, 20, 20, 16, 12,  8,  0,
             0,  4,  8, 12, 16, 16, 12,  8,  4,  0, 0,  4,  0,  8, 12, 12,  8,  0,  4,  0,
             0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0
        };
        private static readonly sbyte[] WpFieldValues = {
             0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
             0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0,  4,  4,  0,  0,  0,  6,  6,  6,  0,
             0,  6,  6,  8,  8,  8,  4,  6,  6,  0, 0,  8,  8, 16, 22, 22,  4,  4,  4,  0,
             0, 10, 10, 20, 26, 26, 10, 10, 10, 0, 0, 12, 12, 22, 28, 28, 14, 14, 14, 0,
             0, 18, 18, 28, 32, 32, 20, 20, 20, 0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
             0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0
        };
        private static readonly sbyte[] BpFieldValues = {
             0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
             0, 18, 18, 28, 32, 32, 20, 20, 20, 0, 0, 12, 12, 22, 28, 28, 14, 14, 14, 0,
             0, 10, 10, 20, 26, 26, 10, 10, 10, 0, 0,  8,  8, 16, 22, 22,  4,  4,  4,  0,
             0,  6,  6,  8,  8,  8,  4,  6,  6,  0, 0,  4,  4,  0,  0,  0,  6,  6,  6,  0,
             0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
             0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0
        };
        private static readonly int[] _map64toMailbox;
        static PortedSearch() { _map64toMailbox = new int[64]; for (int r = 0; r < 8; r++) for (int f = 0; f < 8; f++) _map64toMailbox[r * 8 + f] = (r + 2) * 10 + (f + 1); }
        public int Depth { get; private set; }
        public int Score { get; private set; }
        public long NodesVisited { get; private set; }
        public bool Aborted { get; private set; }
        public Move[] PrincipalVariation { get; private set; } = Array.Empty<Move>();
        private Func<bool> _isTimeUp;
        private long _maxNodes;
        private readonly Move[][] _pvTable;
        private readonly Move[,] _killerMoves;
        public PortedSearch(Board board, long maxNodes = long.MaxValue) { _killerMoves = new Move[MAX_DEPTH, 2]; _pvTable = new Move[MAX_DEPTH][]; for (int i = 0; i < MAX_DEPTH; i++) _pvTable[i] = new Move[MAX_DEPTH]; }
        public PortedSearch(int searchDepth, Board board) : this(board) { while (Depth < searchDepth) SearchDeeper(board); }

        public void SearchDeeper(Board root, Func<bool> isTimeUp = null, long maxNodes = long.MaxValue)
        {
            Depth++;
            _isTimeUp = isTimeUp ?? (() => false);
            _maxNodes = maxNodes;
            NodesVisited = 0;
            Aborted = false;

            Score = AlphaBeta(root, 0, -VALUE_MATE, VALUE_MATE, Depth);

            if (!Aborted)
            {
                int pvLength = 0;
                while (pvLength < Depth && _pvTable[0][pvLength] != default)
                    pvLength++;
                PrincipalVariation = new Move[pvLength];
                Array.Copy(_pvTable[0], PrincipalVariation, pvLength);
            }
        }

        private int AlphaBeta(Board position, int ply, int alpha, int beta, int depth)
        {
            if (ply >= MAX_DEPTH - 1)
            {
                return Evaluate(position);
            }

            if (ply > 0)
            {
                if ((NodesVisited & 2047) == 0 && (_isTimeUp() || NodesVisited >= _maxNodes))
                {
                    Aborted = true;
                    return 0;
                }
                alpha = Math.Max(alpha, -VALUE_MATE + ply);
                beta = Math.Min(beta, VALUE_MATE - ply - 1);
                if (alpha >= beta)
                    return alpha;
            }

            _pvTable[ply][ply] = default;

            if (Transpositions.GetScore(position.ZobristHash, depth, ply, new SearchWindow(alpha, beta), out int ttScore))
                return ttScore;

            bool isChecked = position.IsChecked(position.SideToMove);
            if (isChecked)
                depth++;

            if (depth <= 0)
            {
                return QuiescenceSearch(position, ply, alpha, beta);
            }

            NodesVisited++;
            var moves = GetOrderedMoves(position, ply, isChecked);
            if (moves.Count == 0)
                return isChecked ? -VALUE_MATE + ply : 0;

            Move bestMove = default;
            int scoreType = 0;
            int legalMovesPlayed = 0;

            foreach (var move in moves)
            {
                Board child = new Board(position, move);

                // CRITICAL FIX: Check if the move was legal.
                // A pseudo-legal move is illegal if it leaves the king in check.
                // The color of the king to check is the one that just moved.
                if (child.IsChecked(position.SideToMove))
                {
                    continue; // Skip illegal moves
                }
                legalMovesPlayed++;

                int score = -AlphaBeta(child, ply + 1, -beta, -alpha, depth - 1);

                if (Aborted) return 0;

                if (score > alpha)
                {
                    alpha = score;
                    bestMove = move;
                    scoreType = 1;

                    _pvTable[ply][ply] = move;
                    if (ply + 1 < MAX_DEPTH)
                    {
                        for (int nextPly = ply + 1; nextPly < MAX_DEPTH; nextPly++)
                        {
                            if (_pvTable[ply + 1][nextPly] == default)
                            {
                                _pvTable[ply][nextPly] = default;
                                break;
                            }
                            _pvTable[ply][nextPly] = _pvTable[ply + 1][nextPly];
                        }
                    }

                    if (alpha >= beta)
                    {
                        scoreType = 2;
                        bool isCapture = position[move.ToSquare] != Piece.None;
                        if (!isCapture)
                        {
                            _killerMoves[ply, 1] = _killerMoves[ply, 0];
                            _killerMoves[ply, 0] = move;
                        }
                        break;
                    }
                }
            }

            // If no legal moves were found, it's either checkmate or stalemate.
            if (legalMovesPlayed == 0)
            {
                return isChecked ? -VALUE_MATE + ply : 0;
            }

            var window = new SearchWindow(alpha, beta);
            if (scoreType == 0) Transpositions.Store(position.ZobristHash, depth, ply, window, alpha, bestMove);
            if (scoreType == 1) Transpositions.Store(position.ZobristHash, depth, ply, window, alpha, bestMove);
            if (scoreType == 2) Transpositions.Store(position.ZobristHash, depth, ply, window, beta, bestMove);

            return alpha;
        }

        private int QuiescenceSearch(Board position, int ply, int alpha, int beta)
        {
            if (ply >= MAX_DEPTH - 1)
            {
                return Evaluate(position);
            }

            NodesVisited++;
            if ((NodesVisited & 2047) == 0 && _isTimeUp())
            {
                Aborted = true;
                return 0;
            }

            int standPatScore = (int)position.SideToMove * position.Score;

            if (standPatScore >= beta)
                return beta;
            if (standPatScore > alpha)
                alpha = standPatScore;

            var captures = GetOrderedMoves(position, ply, position.IsChecked(position.SideToMove), true);

            foreach (var move in captures)
            {
                Board child = new Board(position, move);
                // Add the same legality check here for safety, especially for check evasions.
                if (child.IsChecked(position.SideToMove))
                {
                    continue;
                }

                int score = -QuiescenceSearch(child, ply + 1, -beta, -alpha);

                if (Aborted) return 0;

                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }

            return alpha;
        }

        private List<Move> GetOrderedMoves(Board position, int ply, bool isChecked, bool capturesOnly = false)
        {
            var moveScores = new List<(Move move, int score)>();
            var allPseudoLegalMoves = new List<Move>();

            if (capturesOnly || isChecked)
            {
                if (isChecked)
                    position.CollectMoves(m => allPseudoLegalMoves.Add(m));
                else
                    position.CollectCaptures(m => allPseudoLegalMoves.Add(m));

                foreach (var m in allPseudoLegalMoves)
                {
                    Piece victim = position[m.ToSquare];
                    if (victim == Piece.None) victim = Piece.Pawn.OfColor(Pieces.Flip(position.SideToMove));
                    Piece aggressor = position[m.FromSquare];
                    int score = 1000000 + (PieceValues[Pieces.Order(victim)] * 10) - PieceValues[Pieces.Order(aggressor)];
                    moveScores.Add((m, score));
                }
            }
            else
            {
                position.CollectMoves(m => allPseudoLegalMoves.Add(m));

                Transpositions.GetBestMove(position, out Move hashMove);

                if (hashMove != default && !allPseudoLegalMoves.Contains(hashMove))
                {
                    hashMove = default;
                }

                Move killer1 = _killerMoves[ply, 0];
                Move killer2 = _killerMoves[ply, 1];

                foreach (var m in allPseudoLegalMoves)
                {
                    int score = 0;
                    bool isCapture = position[m.ToSquare] != Piece.None;

                    if (m == hashMove) score = 2000000;
                    else if (isCapture)
                    {
                        Piece victim = position[m.ToSquare];
                        Piece aggressor = position[m.FromSquare];
                        score = 1000000 + (PieceValues[Pieces.Order(victim)] * 10) - PieceValues[Pieces.Order(aggressor)];
                    }
                    else if (m == killer1) score = 900000;
                    else if (m == killer2) score = 800000;

                    moveScores.Add((m, score));
                }
            }

            return moveScores.OrderByDescending(item => item.score).Select(item => item.move).ToList();
        }

        private int Evaluate(Board position)
        {
            int[] whitePawnsPerColumn = new int[8];
            int[] blackPawnsPerColumn = new int[8];
            int[] whiteRooksPerColumn = new int[8];
            int[] blackRooksPerColumn = new int[8];
            int whiteMaterial = 0, blackMaterial = 0;
            int whiteBishops = 0, blackBishops = 0;

            for (int i = 0; i < 64; i++)
            {
                Piece piece = position[i];
                if (piece == Piece.None) continue;
                int file = i % 8;
                int value = PieceValues[Pieces.Order(piece)];
                if (piece.IsWhite()) whiteMaterial += value; else blackMaterial += value;

                switch (piece & Piece.TypeMask)
                {
                    case Piece.Pawn: if (piece.IsWhite()) whitePawnsPerColumn[file]++; else blackPawnsPerColumn[file]++; break;
                    case Piece.Rook: if (piece.IsWhite()) whiteRooksPerColumn[file]++; else blackRooksPerColumn[file]++; break;
                    case Piece.Bishop: if (piece.IsWhite()) whiteBishops++; else blackBishops++; break;
                }
            }

            int materialBalance = whiteMaterial - blackMaterial;
            int materialSum = whiteMaterial + blackMaterial;
            int posValue = 0;

            for (int i = 0; i < 64; i++)
            {
                Piece piece = position[i];
                if (piece == Piece.None) continue;

                int mailboxIndex = _map64toMailbox[i];
                int rank = i / 8;
                int file = i % 8;
                Color color = piece.Color();

                switch (piece & Piece.TypeMask)
                {
                    case Piece.Pawn:
                        if (color == Color.White) posValue += WhitePawnEvaluation(rank, file, materialSum, whitePawnsPerColumn, blackRooksPerColumn);
                        else posValue -= BlackPawnEvaluation(rank, file, materialSum, blackPawnsPerColumn, whiteRooksPerColumn);
                        break;
                    case Piece.Knight:
                        posValue += (color == Color.White ? 1 : -1) * (CenterTable[mailboxIndex] >> 1);
                        break;
                    case Piece.Rook:
                        if (color == Color.White && blackPawnsPerColumn[file] == 0) posValue += 8;
                        if (color == Color.Black && whitePawnsPerColumn[file] == 0) posValue -= 8;
                        break;
                    case Piece.King:
                        if (materialSum < VALUE_ENDGAME) posValue += (color == Color.White ? 1 : -1) * CenterTable[mailboxIndex];
                        else posValue -= (color == Color.White ? 1 : -1) * (CenterTable[mailboxIndex] << 2);
                        break;
                }
            }

            if (whiteBishops >= 2) posValue += 15;
            if (blackBishops >= 2) posValue -= 15;

            int finalScore = materialBalance + posValue;
            return (int)position.SideToMove * finalScore;
        }

        private int WhitePawnEvaluation(int rank, int file, int materialSum, int[] myPawns, int[] enemyRooks)
        {
            int value = 0;
            int mailboxIndex = _map64toMailbox[rank * 8 + file];
            if (materialSum > VALUE_ENDGAME) value += WpFieldValues[mailboxIndex];
            else value += (rank - 1) * 8;
            if (myPawns[file] > 1) value -= 15;
            if ((file == 0 || myPawns[file - 1] == 0) && (file == 7 || myPawns[file + 1] == 0)) value -= 12;
            if (enemyRooks[file] > 0) value -= 8;
            return value;
        }

        private int BlackPawnEvaluation(int rank, int file, int materialSum, int[] myPawns, int[] enemyRooks)
        {
            int value = 0;
            int mailboxIndex = _map64toMailbox[rank * 8 + file];
            if (materialSum > VALUE_ENDGAME) value += BpFieldValues[mailboxIndex];
            else value += (6 - rank) * 8;
            if (myPawns[file] > 1) value -= 15;
            if ((file == 0 || myPawns[file - 1] == 0) && (file == 7 || myPawns[file + 1] == 0)) value -= 12;
            if (enemyRooks[file] > 0) value -= 8;
            return value;
        }
    }
}