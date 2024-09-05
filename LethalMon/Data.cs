using System;
using System.Collections.Generic;
using LethalMon.CatchableEnemy;
using static LethalMon.Utils;

namespace LethalMon;

public static class Data
{
    public static readonly Random Random = new Random();
    
    public static readonly float[][] CaptureProbabilities =
    {
        // Normal ball
        new[] { 0.95f, 0.9f, 0.8f, 0.65f, 0.5f, 0.3f, 0.1f, 0.05f, 0.02f, 0.01f },
        
        // Great ball
        new[] { 1.0f, 0.97f, 0.95f, 0.85f, 0.7f, 0.5f, 0.3f, 0.2f, 0.15f, 0.1f },
        
        // Ultra ball
        new[] { 1.0f, 1.0f, 1.0f, 0.97f, 0.95f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f },
        
        // Master ball
        new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f }
    };

    public static readonly Dictionary<string, CatchableEnemy.CatchableEnemy> CatchableMonsters = new()
    {
        { Enemy.Flowerman.ToString(), new CatchableFlowerman() },
        { Enemy.HoarderBug.ToString(), new CatchableHoarderBug() },
        { Enemy.RedLocustBees.ToString(), new CatchableRedLocustBees() },
        { Enemy.Puffer.ToString(), new CatchableSporeLizard() },
        { Enemy.MouthDog.ToString(), new CatchableMouthDog() },
        { Enemy.FlowerSnake.ToString(), new CatchableTulipSnake() },
        { Enemy.DressGirl.ToString(), new CatchableGhostGirl() },
        { Enemy.Nutcracker.ToString(), new CatchableNutcracker() },
        { Enemy.Butler.ToString(), new CatchableButler() },
        //{ Enemy.BushWolf.ToString(), new CatchableKidnapperFox() },
        { Enemy.Crawler.ToString(), new CatchableCrawler() },
        { Enemy.MaskedPlayerEnemy.ToString(), new CatchableMasked() },
        { Enemy.BaboonHawk.ToString(), new CatchableBaboonHawk() },
        { Enemy.SandSpider.ToString(), new CatchableSpider() }
        { Enemy.BaboonHawk.ToString(), new CatchableBaboonHawk() },
        { Enemy.CompanyMonster.ToString(), new CatchableCompanyMonster() }
    };
}