using System;

namespace LethalMon;

public static class Data
{
    public static readonly Random Random = new();
    
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
    
    public static readonly int[] DuplicationPrices = [30, 70, 120, 180, 250, 330, 420, 520, 630, 750];
}