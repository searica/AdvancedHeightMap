using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Logging;

namespace AdvancedHeightMap.Helpers; 


/// <summary>Helpers shared by this mod's patches.</summary>
internal static class PatchHelper {
    /// <summary>Pass as <c>expected</c> to <see cref="ReplaceCalls"/> or <see cref="ReplaceAllMatches"/> to accept any non-zero count.</summary>
    internal const int AnyCount = -1;

    /// <summary>A deep copy of a transpiler's input, safe to rewrite in place.</summary>
    /// <remarks>
    /// Harmony hands the same CodeInstruction objects to every transpiler in the chain, so a
    /// plain <c>new List&lt;CodeInstruction&gt;(instructions)</c> would let an in-place edit
    /// leak into the input. Every transpiler here bails out by returning the untouched input
    /// when it finds something unexpected, and that bail-out is only honest over a real copy.
    /// </remarks>
    internal static List<CodeInstruction> Copy(IEnumerable<CodeInstruction> instructions) {
        List<CodeInstruction> copy = instructions is ICollection<CodeInstruction> known
            ? new List<CodeInstruction>(known.Count)
            : new List<CodeInstruction>();

        foreach (CodeInstruction instruction in instructions) { copy.Add(new CodeInstruction(instruction)); }

        return copy;
    }

    /// <summary>
    /// Rewrites every call to <paramref name="original"/> into a static call to
    /// <paramref name="replacement"/>, which must take the same stack and return the same type.
    /// </summary>
    /// <remarks>
    /// Returns the input untouched, and logs why, when the number of calls found is not
    /// <paramref name="expected"/> (or is zero for <see cref="AnyCount"/>). That usually means
    /// another mod has already rewritten the method, in which case standing down is the right
    /// outcome; see the Priority.Last note in ValheimCommunityPatch.ApplyPatches.
    /// </remarks>
    internal static IEnumerable<CodeInstruction> ReplaceCalls(
        IEnumerable<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement,
        string site, int expected = AnyCount) {
        if (original == null || replacement == null) {
            Log.LogWarning($"{site}: a method this fix needs could not be resolved, so it is inactive here.");
            return instructions;
        }

        List<CodeInstruction> codes = Copy(instructions);

        int found = 0;
        for (int i = 0; i < codes.Count; i++) {
            if (!codes[i].Calls(original)) { continue; }

            codes[i].opcode = OpCodes.Call;
            codes[i].operand = replacement;
            found++;
        }

        bool ok = expected == AnyCount ? found > 0 : found == expected;
        if (ok) { return codes; }

        string wanted = expected == AnyCount ? "at least 1" : expected.ToString();
        Log.LogWarning(
            $"{site}: expected {wanted} call(s) to {original.DeclaringType?.Name}.{original.Name}, " +
            $"found {found}, so this fix is inactive here. Another mod has most likely already " +
            "rewritten the method - if so, nothing is wrong.");
        return instructions;
    }

    /// <summary>
    /// Rewrites every run of instructions matching <paramref name="pattern"/> into a fresh copy of
    /// <paramref name="replacement"/>, which must take the same stack and leave the same type.
    /// </summary>
    /// <remarks>
    /// A <see cref="CodeMatch"/> built from a null operand matches any operand, so the caller
    /// must check every member it resolved for the pattern before calling this. Labels and
    /// exception blocks on a match's first instruction move to the replacement's first; a
    /// label or block on any later one means something jumps into the middle of the match, so
    /// the method is left alone. Returns the input untouched, and logs why, when that happens
    /// or when the number of matches is not <paramref name="expected"/> (or is zero for
    /// <see cref="AnyCount"/>), as <see cref="ReplaceCalls"/> does.
    /// </remarks>
    internal static IEnumerable<CodeInstruction> ReplaceAllMatches(
        IEnumerable<CodeInstruction> instructions, CodeMatch[] pattern, CodeInstruction[] replacement,
        string site, int expected = AnyCount) {
        // CodeMatcher copies every instruction it is given, so the input stays untouched for a bail-out.
        CodeMatcher matcher = new CodeMatcher(instructions);

        int found = 0;
        while (matcher.MatchStartForward(pattern).IsValid) {
            for (int i = 1; i < pattern.Length; i++) {
                CodeInstruction inner = matcher.InstructionAt(i);
                if (inner.labels.Count == 0 && inner.blocks.Count == 0) { continue; }

                Log.LogWarning(
                    $"{site}: a jump or exception block lands inside the code this fix rewrites, " +
                    "so it is inactive here. Another mod has most likely already rewritten the " +
                    "method - if so, nothing is wrong.");
                return instructions;
            }

            // Fresh instructions per match: Harmony re-runs every transpiler each time a mod
            // patches the method, and a shared instance would carry one run's edits into the next.
            CodeInstruction[] inserted = new CodeInstruction[replacement.Length];
            for (int i = 0; i < inserted.Length; i++) { inserted[i] = replacement[i].Clone(); }
            inserted[0].MoveLabelsFrom(matcher.Instruction).MoveBlocksFrom(matcher.Instruction);

            matcher.RemoveInstructions(pattern.Length).InsertAndAdvance(inserted);
            found++;
        }

        bool ok = expected == AnyCount ? found > 0 : found == expected;
        if (ok) { return matcher.InstructionEnumeration(); }

        string wanted = expected == AnyCount ? "at least 1" : expected.ToString();
        Log.LogWarning(
            $"{site}: expected {wanted} match(es) of the code this fix rewrites, found {found}, " +
            "so this fix is inactive here. Another mod has most likely already rewritten the " +
            "method - if so, nothing is wrong.");
        return instructions;
    }


    /// <summary>
    /// Runs a forward match without mutating anything when it fails. A failed
    /// <see cref="CodeMatcher.MatchStartForward"/> leaves Pos at -1, so any chained
    /// RemoveInstruction/Insert throws an out of range exception - and a throwing
    /// transpiler makes HarmonyX discard every patch on the target method, including
    /// other mods'. Bail out through this instead so a stolen anchor only costs us
    /// our own patch.
    /// </summary>
    public static bool TryMatchStartForward(this CodeMatcher matcher, string failureMessage, params CodeMatch[] matches)
    {
        matcher.MatchStartForward(matches);
        if (matcher.IsInvalid)
        {
            Log.LogWarning($"{failureMessage} Anchor instruction not found, it was likely consumed by another mod. Skipping this patch.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Runs a forward match without mutating anything when it fails. A failed
    /// <see cref="CodeMatcher.MatchStartForward"/> leaves Pos at -1, so any chained
    /// RemoveInstruction/Insert throws an out of range exception - and a throwing
    /// transpiler makes HarmonyX discard every patch on the target method, including
    /// other mods'. Bail out through this instead so a stolen anchor only costs us
    /// our own patch.
    /// </summary>
    public static bool TryMatchStartForward(this CodeMatcher matcher, params CodeMatch[] matches)
    {
        matcher.MatchStartForward(matches);
        if (matcher.IsInvalid)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Runs a backwards match without mutating anything when it fails. A failed
    /// <see cref="CodeMatcher.MatchStartBackwards"/> leaves Pos at -1, so any chained
    /// RemoveInstruction/Insert throws an out of range exception - and a throwing
    /// transpiler makes HarmonyX discard every patch on the target method, including
    /// other mods'. Bail out through this instead so a stolen anchor only costs us
    /// our own patch.
    /// </summary>
    public static bool TryMatchStartBackwards(this CodeMatcher matcher, string failureMessage, params CodeMatch[] matches)
    {
        matcher.MatchStartBackwards(matches);
        if (matcher.IsInvalid)
        {
            Log.LogWarning($"{failureMessage} Anchor instruction not found, it was likely consumed by another mod. Skipping this patch.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Runs a backwards match without mutating anything when it fails. A failed
    /// <see cref="CodeMatcher.MatchStartBackwards"/> leaves Pos at -1, so any chained
    /// RemoveInstruction/Insert throws an out of range exception - and a throwing
    /// transpiler makes HarmonyX discard every patch on the target method, including
    /// other mods'. Bail out through this instead so a stolen anchor only costs us
    /// our own patch.
    /// </summary>
    public static bool TryMatchStartBackwards(this CodeMatcher matcher, params CodeMatch[] matches)
    {
        matcher.MatchStartBackwards(matches);
        if (matcher.IsInvalid)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// <see cref="TryMatchStartForward"/>, but leaving the position on the last matched
    /// instruction instead of the first.
    /// </summary>
    public static bool TryMatchEndForward(this CodeMatcher matcher, string failureMessage, params CodeMatch[] matches)
    {
        matcher.MatchEndForward(matches);
        if (matcher.IsInvalid)
        {
            Log.LogWarning($"{failureMessage} Anchor instruction not found, it was likely consumed by another mod. Skipping this patch.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// <see cref="TryMatchStartForward"/>, but leaving the position on the last matched
    /// instruction instead of the first.
    /// </summary>
    public static bool TryMatchEndForward(this CodeMatcher matcher, params CodeMatch[] matches)
    {
        matcher.MatchEndForward(matches);
        if (matcher.IsInvalid)
        {
            return false;
        }
        return true;
    }
}
