using AdvancedHeightMap.Helpers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;


namespace AdvancedHeightMap;

[HarmonyPatch]
internal static class HeightMapPatches
{
    private static readonly MethodInfo ClampMethod = AccessTools.Method(typeof(Mathf), nameof(Mathf.Clamp), [typeof(float), typeof(float), typeof(float)]);

    private static readonly CodeMatch[] TargetMatches = [
        new CodeMatch(instruction => instruction.LoadsConstant(-8f)),
        new CodeMatch(instruction => instruction.LoadsConstant(8f)),
        new CodeMatch(instruction => instruction.Calls(ClampMethod)),
    ];

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.RaiseTerrain))]
    private static IEnumerable<CodeInstruction> ChangeRaiseTerrainLimits(IEnumerable<CodeInstruction> instructions)
    {
        IEnumerable<CodeInstruction> codes = PatchHelper.Copy(instructions);
        CodeMatcher matcher = new CodeMatcher(codes);
        if (matcher.TryMatchStartForward("RaiseTerrain Transpiler", TargetMatches))
        {
            matcher.SetInstructionAndAdvance(Transpilers.EmitDelegate(AdvancedHeightMap.GetMinHeightChange));
            matcher.SetInstructionAndAdvance(Transpilers.EmitDelegate(AdvancedHeightMap.GetMaxHeightChange));
            return matcher.InstructionEnumeration();
        }
        return instructions;
    }


    [HarmonyTranspiler]
    [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.LevelTerrain))]
    private static IEnumerable<CodeInstruction> ChangeLevelTerrainLimits(IEnumerable<CodeInstruction> instructions)
    {
        IEnumerable<CodeInstruction> codes = PatchHelper.Copy(instructions);
        CodeMatcher matcher = new CodeMatcher(codes);
        if (matcher.TryMatchStartForward("LevelTerrain Transpiler", TargetMatches))
        {
            matcher.SetInstructionAndAdvance(Transpilers.EmitDelegate(AdvancedHeightMap.GetMinHeightChange));
            matcher.SetInstructionAndAdvance(Transpilers.EmitDelegate(AdvancedHeightMap.GetMaxHeightChange));
            return matcher.InstructionEnumeration();
        }
        return instructions;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.ApplyToHeightmap))]
    private static IEnumerable<CodeInstruction> ChangeApplyToHeightmapLimits(IEnumerable<CodeInstruction> instructions)
    {

        IEnumerable<CodeInstruction> codes = PatchHelper.Copy(instructions);
        CodeMatcher matcher = new CodeMatcher(codes);

        CodeMatch[] subtractMatch = [
            new CodeMatch(instruction => instruction.LoadsConstant(8f)),
            new CodeMatch(OpCodes.Sub)
        ];

        CodeMatch[] addMatch = [
            new CodeMatch(instruction => instruction.LoadsConstant(8f)),
            new CodeMatch(OpCodes.Add)
        ];

        if (matcher.TryMatchStartForward("ApplyToHeightMap Change Min Depth Transpiler", subtractMatch))
        {
            matcher.SetInstructionAndAdvance(Transpilers.EmitDelegate(AdvancedHeightMap.GetMinHeightChange));
            matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Add));
        }

        if (matcher.TryMatchStartForward("ApplyToHeightMap Change Max Height Transpiler", addMatch))
        {
            matcher.SetInstructionAndAdvance(Transpilers.EmitDelegate(AdvancedHeightMap.GetMaxHeightChange));
        }

        if (matcher.IsValid)
        {
            return matcher.InstructionEnumeration();
        }

        return instructions;
    }
}
