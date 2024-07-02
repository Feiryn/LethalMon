using System;
using System.Collections.Generic;
using LethalMon.CatchableEnemy;
using static LethalMon.Utils;

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
        { Enemy.Flowerman.ToString(), new CatchableFlowerman() },
        { Enemy.HoarderBug.ToString(), new CatchableHoarderBug() },
        { Enemy.RedLocustBees.ToString(), new CatchableRedLocustBees() },
        { Enemy.Puffer.ToString(), new CatchableSporeLizard() }
    };
}