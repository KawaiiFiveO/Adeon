using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using MinimalChess;

namespace MinimalChessEngine
{
    public static class OpeningBook
    {
        private static Dictionary<string, List<BookMove>> _book;
        private static readonly Random _random = new Random();

        public static void Load()
        {
            _book = new Dictionary<string, List<BookMove>>();
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string exeDir = Path.GetDirectoryName(exePath);
            string bookFile = Path.Combine(exeDir, "book.json");
            try
            {
                if (File.Exists(bookFile))
                {
                    string json = File.ReadAllText(bookFile);
                    // Use the new context for deserialization
                    _book = JsonSerializer.Deserialize(json, typeof(Dictionary<string, List<BookMove>>), JsonContext.Default) as Dictionary<string, List<BookMove>>;
                    Uci.Log("Opening book loaded successfully.");
                }
            }
            catch (Exception ex)
            {
                Uci.Log($"Error loading opening book: {ex.Message}");
            }
        }


        public static bool TryGetMove(Board board, PlayStyle style, out Move move)
        {
            move = default;
            if (_book == null || _book.Count == 0) return false;

            string fenKey = board.ToFenKey();
            //Uci.Log($"[DEBUG] Trying to find book key: '{fenKey}'");

            if (_book.TryGetValue(fenKey, out List<BookMove> bookMoves))
            {
                List<BookMove> movesToConsider = bookMoves;

                if (style.Name == "Gambiteer")
                {
                    var legalBookMoves = bookMoves.Where(bm => board.IsPlayable(new Move(bm.Uci))).ToList();
                    var acceptingMoves = legalBookMoves.Where(bm => board[new Move(bm.Uci).ToSquare] != Piece.None).ToList();
                    if (acceptingMoves.Any())
                    {
                        Uci.Log("Gambiteer accepts the gambit!");
                        movesToConsider = acceptingMoves;
                    }
                    else
                    {
                        var gambitOffers = legalBookMoves.Where(bm => bm.IsGambit).ToList();
                        if (gambitOffers.Any())
                        {
                            Uci.Log("Gambiteer offers a gambit!");
                            movesToConsider = gambitOffers;
                        }
                    }
                }

                if (!movesToConsider.Any()) return false;

                int index = _random.Next(movesToConsider.Count);
                move = new Move(movesToConsider[index].Uci);
                Uci.Log($"Playing book move: {move} ({movesToConsider[index].Comment})");
                return true;
            }
            return false;
        }
    }
}