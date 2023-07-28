using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private int[] _values = { 0, 100, 250, 350, 500, 900, 1000 };

    Move RandMove(IEnumerable<Move> moves)
    {
        var array = moves.ToArray();
        return array[Random.Shared.Next(array.Length)];
    }

    Move HighestValue(IEnumerable<Move> moves) =>  moves.Aggregate(
        new Move(), 
        (agg, move) => 
            _values[(int)agg.CapturePieceType] > _values[(int)move.CapturePieceType]
            ? agg : move);
    
    IEnumerable<Move> MovesWithNoBadTrades(IEnumerable<Move> move, Board board) => move.Where(m => !MoveHasBadTrade(m, board));
    // Okay, let's work through this. The tuple here has a move and then whether or not that move leads to mate
    IEnumerable<(Move, bool)> MovesWithChecks(IEnumerable<Move> moves, Board board) =>
        moves
            // Turn moves into tuples with the move and whether or not it makes a check
            .Select(m => (m, MoveMakesCheck(board, m)))
            // Filter down to ones where we have checks
            .Where(t => t.Item2.Item1 || t.Item2.Item2)
            // Finally, return a tuple that has the move and whether or not the move is a mate
            .Select(t => (t.Item1, t.Item2.Item2));
    
    // First item is for if this is a check, second is if it's a mate
    (bool, bool) MoveMakesCheck(Board board, Move move)
    {
        board.MakeMove(move);
        var isCheck = CheckForCheck(board);
        board.UndoMove(move);

        return isCheck;
    }
    // Same here
    (bool, bool) CheckForCheck(Board board) => (board.IsInCheck(), board.IsInCheckmate());

    bool MoveHasBadTrade(Move move, Board board)
    {
        var pieceValue = _values[(int)move.CapturePieceType];
        var moves = DoMoveGetMovesRevertMove(move, board);
        return moves.Any(m => _values[(int)m.CapturePieceType] > pieceValue);
    }
    
    bool WouldLosePieceNextTurn(Move move, Board board)
    {
        var moves = DoMoveGetMovesRevertMove(move, board, true);
        return moves.Any(move1 => move1.TargetSquare == move.TargetSquare);
    }

    Move[] DoMoveGetMovesRevertMove(Move move, Board board, bool capturesOnly = false)
    {
        board.MakeMove(move);
        var moves = board.GetLegalMoves(capturesOnly);
        board.UndoMove(move);

        return moves;
    }

    bool CheckMoveWithTest(Move move, Board board, Func<Board, Move, bool> tester)
    {
        board.MakeMove(move);
        var check = tester(board, move);
        board.UndoMove(move);

        return check;
    }

    bool MoveWouldRepeat(Move move, Board board) => CheckMoveWithTest(move, board, (b, _) => b.IsRepeatedPosition());

    bool MoveWouldLoseQueen(Move move, Board board)
    {
        board.MakeMove(move);
        var test = board.GetLegalMoves(true).Any(m => m is { IsCapture: true, CapturePieceType: PieceType.Queen });
        board.UndoMove(move);
        // bool Test(Board b, Move m) => DoMoveGetMovesRevertMove(m, b).Any(newMove => newMove is { IsCapture: true, CapturePieceType: PieceType.Queen });
        // var result = CheckMoveWithTest(move, board, Test);
        return test;
    }
        
    
    Move OneMoveSearch(Board board, Move[] moves)
    {
        var noLossMoves = 
            moves
                .Where(m => !WouldLosePieceNextTurn(m, board))
                .ToArray();
        
        var caps = 
            MovesWithNoBadTrades(board.GetLegalMoves(true), board)
                .ToArray();
        
        var checks = 
            MovesWithChecks(moves, board)
                .Where(t => !MoveHasBadTrade(t.Item1, board) && !MoveWouldLoseQueen(t.Item1, board))
                .ToArray();
        
        var checksCollapsed = 
            checks
                .Select(t => t.Item1)
                .ToArray();
        
        var checksWithCaps = 
            checks
                .Where(t => caps.Contains(t.Item1))
                .Select(t => t.Item1)
                .ToArray();
        
        // We first want to check for mates
        var movesWithMate = 
            checks.Where(m => m.Item2)
                .Select(m => m.Item1)
                .ToArray();

        var move = movesWithMate.Length > 0 ? movesWithMate[0] :
            checksWithCaps.Length > 0 ? HighestValue(checksWithCaps) :
            checksCollapsed.Length > 0 ? RandMove(checksCollapsed) :
            caps.Length > 0 ? HighestValue(caps) :
            noLossMoves.Length > 0 ? RandMove(noLossMoves) :
            RandMove(moves);
        return !move.IsPromotion ? move : new Move($"{move.StartSquare.Name}{move.TargetSquare.Name}q", board);
    }
    

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves().Where(m => !MoveWouldRepeat(m, board)).ToArray();
        
        
        
        return OneMoveSearch(board, moves);
    }
}