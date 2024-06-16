using System;
using System.Collections.Generic;
using LethalMon.CatchableEnemy;

namespace LethalMon;

public static class Data
{
    public static readonly Random Random = new Random();
    
    public static readonly double[][] CaptureProbabilities =
    {
        // Normal ball
        new[] { 0.95, 0.9, 0.8, 0.65, 0.5, 0.3, 0.1, 0.0, 0.0, 0.0 },
        
        // Great ball
        new[] { 1.0, 0.97, 0.95, 0.85, 0.7, 0.5, 0.3, 0.2, 0.15, 0.1 },
        
        // Ultra ball
        new[] { 1.0, 1.0, 1.0, 0.97, 0.95, 0.9, 0.8, 0.7, 0.6, 0.5 },
        
        // Master ball
        new[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 }
    };

    public static readonly Dictionary<string, CatchableEnemy.CatchableEnemy> CatchableMonsters = new()
    {
        { "Flowerman", new CatchableFlowerman() },
        { "HoarderBug", new CatchableHoarderBug() },
        { "RedLocustBees", new CatchableRedLocustBees() }
    };
}