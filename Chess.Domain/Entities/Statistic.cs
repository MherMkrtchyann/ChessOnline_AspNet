﻿using Chess.Domain.Enums;

namespace Chess.Domain.Entities;

public class Statistic
{
    public Guid Id { get; set; }
    public GameType Type { get; set; }
    public int TypeId
    {
        set => Type = (GameType)value;
    }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int Rating { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; }
    public Guid UserId { get; set; }
}