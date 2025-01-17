using Chess.Application.Interfaces.Caching;
using Chess.Application.Interfaces.Repositories;
using Chess.Application.Interfaces.Services;
using Chess.Domain.Entities;
using Chess.Domain.Enums;
using Newtonsoft.Json.Linq;
using System.Numerics;

namespace Chess.Application.Services;

public class GameService : IGameService
{
    private readonly IGameCache _gameCache;
    private readonly IPlayerCache _playerCache;
    private readonly IGameRepository _gameRepository;
    private readonly IStatisticRepository _statisticRepository;

    public GameService(IGameCache gameCache, IPlayerCache playerCache, IGameRepository gameRepository,IStatisticRepository statisticRepository)
    {
        _gameCache = gameCache;
        _playerCache = playerCache;
        _gameRepository = gameRepository;
        _statisticRepository = statisticRepository;
    }
    public Game Find(Guid id)
    {
        return _gameCache.Find(id);
    }
    public void Remove(Game game)
    {
        _gameCache.Remove(game);
    }

    public Game Create(Invite invite)
    {
        Game game = new Game();
        if (invite.FromColor == Color.White)
        {
            game.WhitePlayerId = invite.FromId;
            game.WhitePlayer = invite.From;
            game.BlackPlayerId = invite.ToId;
            game.BlackPlayer = invite.To;
        }
        else
        {
            game.WhitePlayerId = invite.ToId;
            game.WhitePlayer = invite.To;
            game.BlackPlayerId = invite.FromId;
            game.BlackPlayer = invite.From;
        }
        game.Timer = invite.Timer;
        game.TimerIncrement = invite.TimerIncrement;
        game.TypeId = invite.TypeId;
        _gameCache.Add(game);
        return game;
    }
    public bool IsPlayerTurn(Player player, Game game)
    {
        return game.Board.Turn == (player == game.WhitePlayer ? PieceColor.White : PieceColor.Black);
    }
    public Player GetOpponent(Guid id, bool onlineOnly = true)
    {
        Game game = _gameCache.Find(id);
        if (game == null)
        {
            return null;
        }
        Player opponent = game.WhitePlayerId == id ? game.BlackPlayer : game.WhitePlayer;
        if (onlineOnly)
        {
            return _playerCache.Find(opponent.Id);
        }
        return opponent;
    }
    public MoveResultType MakeMove(Player player, Game game, string from, string to)
    {
        Move move = new Move(new Position(from), new Position(to));
        if (IsPlayerTurn(player, game) && game.Board.IsValidMove(move))
        {
            game.Board.Move(move);
            game.IsDrawOffered = false;
            if (game.Board.IsEndGame)
            {
                return MoveResultType.EndGame;
            }
            return MoveResultType.ValidMove;
        }
        else
        {
            return MoveResultType.InvalidMove;
        }
    }
    public async Task<EndgameType?> EndGameAsync(Player player)
    {
        Game game = _gameCache.Find(player.Id);
        if (game == null || !game.Board.IsEndGame)
        {
            return null;
        }

        game.EndTime = DateTime.Now;
        EndGameInfo endGame = game.Board.EndGame!;
        if (endGame.EndgameType == EndgameType.Checkmate || endGame.EndgameType == EndgameType.Resigned)
        {
            game.Winner = endGame.WonSide == PieceColor.White ? game.WhitePlayer : game.BlackPlayer;
            game.WinnerId = game.Winner.Id;
        }
        else
        {
            game.Winner = null;
        }
        game.Pgn = game.Board.ToPgn();
        await UpdateStats(game);
        await _gameRepository.AddAsync(game);
        Remove(game);
        return endGame.EndgameType;
    }
    private async Task UpdateStats(Game game)
    {
        bool? isWhiteWinner, isBlackWinner;
        if (game.Winner == null)
        {
            isWhiteWinner = null;
            isBlackWinner = null;
        }
        else if (game.Winner == game.WhitePlayer)
        {
            isWhiteWinner = true;
            isBlackWinner = false;
        }
        else
        {
            isBlackWinner = true;
            isWhiteWinner = false;
        }
        Statistic oldWhiteStatistic = game.WhitePlayer.Statistics[game.TypeId-1];
        Statistic oldBlackStatistic = game.BlackPlayer.Statistics[game.TypeId-1];

        int oldWhiteRating = oldWhiteStatistic.Rating;
        Statistic newWhiteStatistic = UpdateStatistic(oldWhiteStatistic,oldBlackStatistic.Rating,isWhiteWinner);
        Statistic newBlackStatistic = UpdateStatistic(oldBlackStatistic,oldWhiteRating, isBlackWinner);

        await _statisticRepository.UpdateAsync(newWhiteStatistic);
        await _statisticRepository.UpdateAsync(newBlackStatistic);
    }
    public bool Resign(Player player)
    {
        Game game = _gameCache.Find(player.Id);
        if (game == null)
        {
            return false;
        }
        game.Board.Resign(game.WhitePlayer == player ? PieceColor.White : PieceColor.Black);
        return true;
    }
    public bool OfferDraw(Player player)
    {
        Player opponent = GetOpponent(player.Id);
        Game game = _gameCache.Find(player.Id);
        if (game != null && opponent != null && IsPlayerTurn(player, game) && !game.IsDrawOffered)
        {
            game.IsDrawOffered = true;
            return true;
        }
        return false;
    }
    public bool AcceptDraw(Player player)
    {
        Game game = _gameCache.Find(player.Id);
        if (game != null && game.IsDrawOffered)
        {
            game.Board.Draw();
            return true;
        }
        return false;
    }
    public bool RejectDraw(Player player)
    {
        Game game = _gameCache.Find(player.Id);
        if (game != null && game.IsDrawOffered)
        {
            return true;
        }
        return false;
    }
    public Statistic UpdateStatistic(Statistic statistic, int opponentRating, bool? isWinner)
    {
        double score;

        statistic.GamesPlayed++;
        if (isWinner == null)
        {
            statistic.Draws++;
            score = 0.5;
        }
        else if (isWinner == true)
        {
            statistic.Wins++;
            score = 1.0;
        }
        else
        {
            statistic.Losses++;
            score = 0;
        }
        double k = 32; // The K-factor determines the magnitude of the change in rating
        double winProbability = 1.0 / (1.0 + Math.Pow(10, (opponentRating - statistic.Rating) / 400.0)); // Calculate the win probability

        statistic.Rating = (int)Math.Round(statistic.Rating + k * (score - winProbability)); // Update the rating using the formula
        return statistic;
    }
    public enum MoveResultType
    {
        ValidMove,
        InvalidMove,
        EndGame,
    }
}