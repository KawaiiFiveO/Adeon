using MinimalChess;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MinimalChessEngine
{
    public class PortedSearch
    {
        // --- Constants ---
        private const int MAX_DEPTH = 64;
        private const int VALUE_MATE = 9999;
        private static readonly int[] PieceValues = { 0, 100, 325, 350, 500, 900, 20000 };

        // --- Members ---
        public int Depth { get; private set; }
        public int Score { get; private set; }
        public long NodesVisited { get; private set; }
        public bool Aborted { get; private set; }
        public Move[] PrincipalVariation { get; private set; } = Array.Empty<Move>();
        private Func<bool> _isTimeUp;
        private long _maxNodes;
        private readonly Move[][] _pvTable;
        private readonly Move[,] _killerMoves;

        // --- Style-specific members ---
        private readonly PlayStyle _currentStyle;
        private readonly int _movesWithoutCaptureOrCheck;

        public PortedSearch(Board board, long maxNodes, PlayStyle style, int boredomCounter)
        {
            _killerMoves = new Move[MAX_DEPTH, 2];
            _pvTable = new Move[MAX_DEPTH][];
            for (int i = 0; i < MAX_DEPTH; i++) _pvTable[i] = new Move[MAX_DEPTH];

            _currentStyle = style;
            _movesWithoutCaptureOrCheck = boredomCounter;
        }

        public void SearchDeeper(Board root, Func<bool> isTimeUp = null, long maxNodes = long.MaxValue)
        {
            Depth++;
            _isTimeUp = isTimeUp ?? (() => false);
            _maxNodes = maxNodes;
            NodesVisited = 0;
            Aborted = false;
            Score = AlphaBeta(root, 0, -VALUE_MATE - 1, VALUE_MATE + 1, Depth);
            if (!Aborted)
            {
                int pvLength = 0;
                while (pvLength < Depth && _pvTable[0][pvLength] != default) pvLength++;
                PrincipalVariation = new Move[pvLength];
                Array.Copy(_pvTable[0], PrincipalVariation, pvLength);
            }
        }

        private int AlphaBeta(Board position, int ply, int alpha, int beta, int depth)
        {
            if (ply >= MAX_DEPTH - 1) return (int)position.SideToMove * Evaluate(position);
            if (ply > 0)
            {
                if ((NodesVisited & 2047) == 0 && (_isTimeUp() || NodesVisited >= _maxNodes)) { Aborted = true; return 0; }
                if (alpha >= beta) return alpha;
            }
            _pvTable[ply][ply] = default;
            if (ply > 0 && Transpositions.GetScore(position.ZobristHash, depth, ply, new SearchWindow(alpha, beta), out int ttScore)) return ttScore;

            bool isChecked = position.IsChecked(position.SideToMove);
            if (isChecked) depth++;
            if (depth <= 0) return QuiescenceSearch(position, ply, alpha, beta);

            if (!isChecked && depth >= 3 && HasMajorPieces(position, position.SideToMove) && (int)position.SideToMove * Evaluate(position) >= beta)
            {
                int R = 2;
                Board nullMoveBoard = new Board(position);
                nullMoveBoard.PlayNullMove();
                int nullScore = -AlphaBeta(nullMoveBoard, ply + 1, -beta, -beta + 1, depth - 1 - R);
                if (nullScore >= beta) return beta;
            }

            NodesVisited++;
            var moves = GetOrderedMoves(position, ply, isChecked);
            if (moves.Count == 0) return isChecked ? Evaluation.Checkmate(position.SideToMove, ply) : 0;

            Move bestMove = default;
            int scoreType = 0;
            int legalMovesPlayed = 0;
            int movesSearched = 0;

            foreach (var move in moves)
            {
                movesSearched++;
                Board child = new Board(position, move);
                if (child.IsChecked(position.SideToMove)) continue;
                legalMovesPlayed++;

                int score;
                if (depth >= 3 && movesSearched > 4 && !isChecked && position[move.ToSquare] == Piece.None)
                {
                    score = -AlphaBeta(child, ply + 1, -alpha - 1, -alpha, depth - 2);
                    if (score > alpha && score < beta)
                    {
                        score = -AlphaBeta(child, ply + 1, -beta, -alpha, depth - 1);
                    }
                }
                else
                {
                    score = -AlphaBeta(child, ply + 1, -beta, -alpha, depth - 1);
                }

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
                            if (_pvTable[ply + 1][nextPly] == default) { _pvTable[ply][nextPly] = default; break; }
                            _pvTable[ply][nextPly] = _pvTable[ply + 1][nextPly];
                        }
                    }
                    if (alpha >= beta)
                    {
                        scoreType = 2;
                        if (position[move.ToSquare] == Piece.None) { _killerMoves[ply, 1] = _killerMoves[ply, 0]; _killerMoves[ply, 0] = move; }
                        break;
                    }
                }
            }
            if (legalMovesPlayed == 0) return isChecked ? Evaluation.Checkmate(position.SideToMove, ply) : 0;

            var window = new SearchWindow(alpha, beta);
            if (scoreType == 0) Transpositions.Store(position.ZobristHash, depth, ply, window, alpha, bestMove);
            if (scoreType == 1) Transpositions.Store(position.ZobristHash, depth, ply, window, alpha, bestMove);
            if (scoreType == 2) Transpositions.Store(position.ZobristHash, depth, ply, window, beta, bestMove);

            return alpha;
        }


        private int QuiescenceSearch(Board position, int ply, int alpha, int beta)
        {
            if (ply >= MAX_DEPTH - 1) return (int)position.SideToMove * Evaluate(position);
            NodesVisited++;
            if ((NodesVisited & 2047) == 0 && _isTimeUp()) { Aborted = true; return 0; }

            int standPatScore = (int)position.SideToMove * Evaluate(position);

            if (standPatScore >= beta) return beta;
            if (standPatScore > alpha) alpha = standPatScore;

            var captures = GetOrderedMoves(position, ply, position.IsChecked(position.SideToMove), true);

            foreach (var move in captures)
            {
                Board child = new Board(position, move);
                if (child.IsChecked(position.SideToMove)) continue;
                int score = -QuiescenceSearch(child, ply + 1, -beta, -alpha);
                if (Aborted) return 0;
                if (score >= beta) return beta;
                if (score > alpha) alpha = score;
            }
            return alpha;
        }

        private List<Move> GetOrderedMoves(Board position, int ply, bool isChecked, bool capturesOnly = false)
        {
            if (_currentStyle.Name == "Gambiteer" && _movesWithoutCaptureOrCheck >= 10 && !isChecked)
            {
                Uci.Log("Gambiteer is BORED! Forcing chaos...");
                var allMoves = new List<Move>();
                position.CollectMoves(m => allMoves.Add(m));
                var legalMoves = allMoves.Where(m => !new Board(position, m).IsChecked(position.SideToMove)).ToList();
                Move bestSacrifice = default;
                int bestSacValue = -1;
                foreach (var move in legalMoves)
                {
                    if (position[move.ToSquare] == Piece.None && position.IsSquareAttackedBy(move.ToSquare, Pieces.Flip(position.SideToMove)))
                    {
                        int pieceValue = PieceValues[Pieces.Order(position[move.FromSquare])];
                        if (pieceValue > bestSacValue) { bestSacValue = pieceValue; bestSacrifice = move; }
                    }
                }
                if (bestSacrifice != default) return new List<Move> { bestSacrifice };
            }

            var moveScores = new List<(Move move, int score)>();
            var allPseudoLegalMoves = new List<Move>();
            if (capturesOnly || isChecked)
            {
                if (isChecked) position.CollectMoves(m => allPseudoLegalMoves.Add(m));
                else position.CollectCaptures(m => allPseudoLegalMoves.Add(m));
                foreach (var m in allPseudoLegalMoves)
                {
                    Piece victim = position[m.ToSquare];
                    if (victim == Piece.None) victim = Piece.Pawn.OfColor(Pieces.Flip(position.SideToMove));
                    Piece aggressor = position[m.FromSquare];
                    moveScores.Add((m, 1000000 + (PieceValues[Pieces.Order(victim)] * 10) - PieceValues[Pieces.Order(aggressor)]));
                }
            }
            else
            {
                position.CollectMoves(m => allPseudoLegalMoves.Add(m));
                Transpositions.GetBestMove(position, out Move hashMove);
                if (hashMove != default && !allPseudoLegalMoves.Contains(hashMove)) hashMove = default;
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
            int finalScore = position.Score;

            if (_currentStyle.Name == "Gambiteer")
            {
                // --- 1. Material Weight Penalty (from White's perspective) ---
                int whiteMaterial = 0;
                int blackMaterial = 0;
                for (int i = 0; i < 64; i++)
                {
                    Piece p = position[i];
                    if (p != Piece.None && (p & Piece.TypeMask) != Piece.King)
                    {
                        if (p.Color() == Color.White) whiteMaterial += PieceValues[Pieces.Order(p)];
                        else blackMaterial += PieceValues[Pieces.Order(p)];
                    }
                }
                int whitePenalty = (int)((1.0 - _currentStyle.MaterialWeightMultiplier) * whiteMaterial);
                int blackPenalty = (int)((1.0 - _currentStyle.MaterialWeightMultiplier) * blackMaterial);
                finalScore -= whitePenalty;
                finalScore += blackPenalty;

                // --- 2. King Attack Bonus (from White's perspective) ---
                int blackKingSq = FindKing(position, Color.Black);
                if (blackKingSq != -1)
                {
                    int attackCount = 0;
                    foreach (int zoneSq in GetKingZone(blackKingSq))
                    {
                        if (position.IsSquareAttackedBy(zoneSq, Color.White))
                            attackCount++;
                    }
                    finalScore += attackCount * _currentStyle.KingAttackBonus;

                    if (!KingHasMoves(position, blackKingSq))
                        finalScore += _currentStyle.KingAttackBonus / 2; // Trapped king bonus
                }

                int whiteKingSq = FindKing(position, Color.White);
                if (whiteKingSq != -1)
                {
                    int attackCount = 0;
                    foreach (int zoneSq in GetKingZone(whiteKingSq))
                    {
                        if (position.IsSquareAttackedBy(zoneSq, Color.Black))
                            attackCount++;
                    }
                    finalScore -= attackCount * _currentStyle.KingAttackBonus;

                    if (!KingHasMoves(position, whiteKingSq))
                        finalScore -= _currentStyle.KingAttackBonus / 2; // Trapped king penalty
                }
            }

            return finalScore;
        }

        private bool HasMajorPieces(Board board, Color side)
        {
            for (int i = 0; i < 64; i++)
            {
                Piece p = board[i];
                if (p != Piece.None && p.Color() == side)
                {
                    Piece type = p & Piece.TypeMask;
                    if (type != Piece.Pawn && type != Piece.King) return true;
                }
            }
            return false;
        }

        private int FindKing(Board b, Color c)
        {
            Piece king = Piece.King.OfColor(c);
            for (int i = 0; i < 64; i++) if (b[i] == king) return i;
            return -1;
        }

        private IEnumerable<int> GetKingZone(int kingSq)
        {
            int rank = kingSq / 8;
            int file = kingSq % 8;
            for (int r = rank - 1; r <= rank + 1; r++)
            {
                for (int f = file - 1; f <= file + 1; f++)
                {
                    if (r >= 0 && r < 8 && f >= 0 && f < 8)
                    {
                        if (r != rank || f != file) // Exclude the king's own square
                            yield return r * 8 + f;
                    }
                }
            }
        }
        private bool KingHasMoves(Board b, int kingSq)
        {
            // We need to check from the perspective of the king's owner
            Board perspectiveBoard = new Board(b);

            // Use the new, safe, public method
            perspectiveBoard.SetSideToMoveForAnalysis(b[kingSq].Color());

            var moves = new List<Move>();
            perspectiveBoard.CollectMoves(kingSq, m => moves.Add(m));
            foreach (var move in moves)
            {
                // A move is legal if it doesn't result in the king being checked
                if (!new Board(perspectiveBoard, move).IsChecked(perspectiveBoard.SideToMove))
                    return true;
            }
            return false;
        }
    }
}