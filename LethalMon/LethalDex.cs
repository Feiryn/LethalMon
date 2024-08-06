using System;
using System.Linq;
using TerminalApi.Classes;
using UnityEngine;

namespace LethalMon;

public class LethalDex
{
    /*
    private const string TopLeftJoint = "╔";
    private const string TopRightJoint = "╗";
    private const string BottomLeftJoint = "╚";
    private const string BottomRightJoint = "╝";
    private const string TopJoint = "╦";
    private const string BottomJoint = "╩";
    private const string LeftJoint = "╠";
    private const string MiddleJoint = "╬";
    private const string RightJoint = "╣";
    private const char HorizontalLine = '═';
    private const string VerticalLine = "║";
    */
    
    /*
    private const string TopLeftJoint = "┌";
    private const string TopRightJoint = "┐";
    private const string BottomLeftJoint = "└";
    private const string BottomRightJoint = "┘";
    private const string TopJoint = "┬";
    private const string BottomJoint = "┴";
    private const string LeftJoint = "├";
    private const string MiddleJoint = "┼";
    private const string RightJoint = "┤";
    private const char HorizontalLine = '─';
    private const string VerticalLine = "│";
    */
    
    private const string TopLeftJoint = "+";
    private const string TopRightJoint = "+";
    private const string BottomLeftJoint = "+";
    private const string BottomRightJoint = "+";
    private const string TopJoint = "+";
    private const string BottomJoint = "+";
    private const string LeftJoint = "+";
    private const string MiddleJoint = "+";
    private const string RightJoint = "+";
    private const char HorizontalLine = '-';
    private const string VerticalLine = "|";
    
    private static string _commandText = "Something went wrong :(";
    
    public static void Register()
    {
        _commandText = InitCommandText();
            
        TerminalApi.TerminalApi.AddCommand("lethaldex", new CommandInfo
        {
            Title = "LethalDex",
            Category = "Other",
            Description = "Shows LethalMon's monsters details",
            DisplayTextSupplier = () => _commandText
        });
        
        LethalMon.Log("Registered LethalDex terminal command");
    }

    private static string InitCommandText()
    {
        string res = "Capture probabilities:\n\nPB = Pokeball, GB = Great ball, UB = Ultra ball, MB = Master ball\n";

        CatchableEnemy.CatchableEnemy[] catchableEnemies = Data.CatchableMonsters.Values.OrderBy(enemy => enemy.CatchDifficulty).ToArray();

        int maxEnemyNameLength = catchableEnemies.Max(enemy => enemy.DisplayName.Length);
        string tier1Text = " PB ";
        string tier2Text = " GB ";
        string tier3Text = " UB ";
        string tier4Text = " MB ";
        
        res += TopLeftJoint + new string(HorizontalLine, maxEnemyNameLength + 2)
                            + TopJoint + new string(HorizontalLine, tier1Text.Length + 2)
                            + TopJoint + new string(HorizontalLine, tier2Text.Length + 2)
                            + TopJoint + new string(HorizontalLine, tier3Text.Length + 2)
                            + TopJoint + new string(HorizontalLine, tier4Text.Length + 2)
                            + TopRightJoint + "\n";
        res += VerticalLine + new string(' ', maxEnemyNameLength + 2)
                            + VerticalLine + " " + tier1Text + " "
                            + VerticalLine + " " + tier2Text + " "
                            + VerticalLine + " " + tier3Text + " "
                            + VerticalLine + " " + tier4Text + " "
                            + VerticalLine + "\n";
        string middleLine = LeftJoint + new string(HorizontalLine, maxEnemyNameLength + 2) + MiddleJoint
                            + new string(HorizontalLine, tier1Text.Length + 2) + MiddleJoint
                            + new string(HorizontalLine, tier2Text.Length + 2) + MiddleJoint
                            + new string(HorizontalLine, tier3Text.Length + 2) + MiddleJoint
                            + new string(HorizontalLine, tier4Text.Length + 2) + RightJoint;
        for (int i = 0; i < catchableEnemies.Length; ++i)
        {
            CatchableEnemy.CatchableEnemy enemy = catchableEnemies[i];
            res += middleLine + "\n";
            string probabilityPercentTier1 = ((int) Mathf.Floor(enemy.GetCaptureProbability(0) * 100)).ToString();
            string probabilityPercentTier2 = ((int) Mathf.Floor(enemy.GetCaptureProbability(1) * 100)).ToString();
            string probabilityPercentTier3 = ((int) Mathf.Floor(enemy.GetCaptureProbability(2) * 100)).ToString();
            string probabilityPercentTier4 = ((int) Mathf.Floor(enemy.GetCaptureProbability(3) * 100)).ToString();
            res += VerticalLine + " " + enemy.DisplayName + new string(' ', maxEnemyNameLength - enemy.DisplayName.Length) + " "
                   + VerticalLine + " " + new string(' ', Math.Clamp(3 - probabilityPercentTier1.Length, 0, 3)) + probabilityPercentTier1 + "% "
                   + VerticalLine + " " + new string(' ', Math.Clamp(3 - probabilityPercentTier2.Length, 0, 3)) + probabilityPercentTier2 + "% "
                   + VerticalLine + " " + new string(' ', Math.Clamp(3 - probabilityPercentTier3.Length, 0, 3)) + probabilityPercentTier3 + "% "
                   + VerticalLine + " " + new string(' ', Math.Clamp(3 - probabilityPercentTier4.Length, 0, 3)) + probabilityPercentTier4 + "% "
                   + VerticalLine + "\n";
        }
        
        res += BottomLeftJoint + new string(HorizontalLine, maxEnemyNameLength + 2) + BottomJoint
               + new string(HorizontalLine, tier1Text.Length + 2) + BottomJoint
               + new string(HorizontalLine, tier2Text.Length + 2) + BottomJoint
               + new string(HorizontalLine, tier3Text.Length + 2) + BottomJoint
               + new string(HorizontalLine, tier4Text.Length + 2) + BottomRightJoint
               + "\n\n";

        return res;
    }
}