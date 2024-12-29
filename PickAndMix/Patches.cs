using PickAndMix;
using HarmonyLib;
using Kingmaker.View.MapObjects.InteractionRestrictions;
using Owlcat.Runtime.Visual.RenderPipeline;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.Assertions;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.RandomEncounters;
using System.Linq;
using Kingmaker.UnitLogic;
using Kingmaker.Blueprints.Classes;
using Kingmaker;
using Kingmaker.GameModes;
using Kingmaker.Visual.Critters;
using System;
using Kingmaker.RuleSystem;
using static Kingmaker.Dungeon.Utils.PersistentRandom;

[HarmonyPatch(typeof(OwlcatRenderPipeline), nameof(OwlcatRenderPipeline.InitializeShadowData), [typeof(ShadowingData)], [ArgumentType.Out])]
public static class FixShadowResolution {
    public static void Postfix(ref ShadowingData shadowData) {
        if (Game.Instance.CurrentMode == GameModeType.CutsceneGlobalMap ||
            Game.Instance.CurrentMode == GameModeType.GlobalMap ||
            Game.Instance.CurrentMode == GameModeType.Kingdom || 
            Game.Instance.CurrentMode == GameModeType.Rest)
        {
            // Don't apply shadow tweaks unless we're in regular play.  Point light resolution causes visual aliasing (shadow bias seems off).
            return;
        }

        shadowData.AtlasSize = Main.AtlasSize;
        shadowData.DirectionalLightCascadeResolution = Main.DirectionalLightCascadeResolution;
        shadowData.PointLightResolution = Main.PointLightResolution;
        shadowData.SpotLightResolution = Main.SpotLightResolution;
    }
}

[HarmonyPatch(typeof(DisableDeviceRestrictionPart), nameof(DisableDeviceRestrictionPart.CheckRestriction))]
public static class DisableLockJamming {
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> insts) {
        foreach (CodeInstruction inst in insts) {
            yield return inst;

            if (inst.LoadsField(AccessTools.Field(typeof(DisableDeviceRestrictionPart), nameof(DisableDeviceRestrictionPart.Jammed)))) {
                MethodInfo method = AccessTools.Method(typeof(DisableLockJamming), nameof(IsJammed));
                Assert.IsTrue(method != null);
                yield return new(OpCodes.Call, method);
            }
        }
    }

    public static bool IsJammed(bool jammed) => !Main.DisableLockJamming && jammed;
}

[HarmonyPatch(typeof(RulebookEvent.Dice), nameof(RulebookEvent.Dice.D), [typeof(DiceFormula)])]
public static class MainCharacterRollsWithAdvantage {
    [HarmonyReversePatch]
    public static int Original(DiceFormula formula) => throw new("Stub");

    public static void Postfix(DiceFormula formula, ref int __result) {
        int originalResult = __result;
        int advantageResult = 0;

        if (Main.MainCharacterHasAdvantageOnRollsFlags.Matches(formula.Dice) &&
            (Game.Instance.Rulebook.Context.CurrentEvent?.Initiator.IsMainCharacter ?? false)
        ) {
            advantageResult = Original(formula);
            __result = Math.Max(originalResult, advantageResult);
        }

#if DEBUG
        Main.ModEntry.Logger.Log($"{formula} -> original: {originalResult}, advantage: {advantageResult}, final: {__result}");
#endif
    }
}

[HarmonyPatch(typeof(RuleRollDice), nameof(RuleRollDice.Roll))]
public static class MainCharacterRollsWithAdvantage_D20 {
    public static void Postfix(RuleRollDice __instance) {
#if DEBUG
        Main.ModEntry.Logger.Log($"{__instance.DiceFormula.Dice} {__instance.Initiator}");
#endif

        if (__instance.Initiator.IsMainCharacter && Main.MainCharacterHasAdvantageOnRollsFlags.Matches(__instance.DiceFormula.Dice)) {
            __instance.AddReroll(1, true, __instance.Initiator.Facts.GetAll<Feature>().First(i => i.Blueprint.HasGroup(FeatureGroup.Deities)));
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            return TranspilerImpl(instructions);
        } catch (TypeLoadException e) {
            Main.MainCharacterHasAdvantageOnRollsFlagsException = e;
            Main.ModEntry.Logger.Error($"{ErrorText.MainCharacterHasAdvantageDisabledDueToError}");
            Main.ModEntry.Logger.LogException(e);
            return instructions;
        }
    }

    public static IEnumerable<CodeInstruction> TranspilerImpl(IEnumerable<CodeInstruction> instructions) => new CodeMatcher(instructions)
        .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(RulebookEvent.Dice), nameof(RulebookEvent.Dice.D), [typeof(DiceFormula)])))
        .ThrowIfInvalid("MainCharacterRollsWithAdvantage_D20 - find call to RulebookEvent.Dice.D")
        .RemoveInstruction()
        .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MainCharacterRollsWithAdvantage), nameof(MainCharacterRollsWithAdvantage.Original))))
        .Instructions();

}

[HarmonyPatch(typeof(RandomEncountersController), nameof(RandomEncountersController.GetAvoidanceCheckResult))]
public static class AvoidAmbushFromRandomEncounters {
    public static void Postfix(ref RandomEncounterAvoidanceCheckResult __result) {
        __result = Main.AvoidAmbushFromRandomEncounters ? RandomEncounterAvoidanceCheckResult.Success : __result;
    }
}

[HarmonyPatch(typeof(Familiar), nameof(Familiar.Update))]
public static class HideFamiliars {
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        try {
            return TranspilerImpl(instructions, generator);
        } catch (TypeLoadException e) {
            Main.HideFamiliarsException = e;
            Main.ModEntry.Logger.Error($"{ErrorText.HideFamiliarsDisabledDueToError}");
            Main.ModEntry.Logger.LogException(e);
            return instructions;
        }
    }

    private static IEnumerable<CodeInstruction> TranspilerImpl(IEnumerable<CodeInstruction> instructions, ILGenerator generator) => new CodeMatcher(instructions, generator)
        .MatchStartForward(
            CodeMatch.LoadsArgument(),
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(Familiar), nameof(Familiar.State))),
            CodeMatch.LoadsConstant(1),
            CodeMatch.Branches(),
            CodeMatch.LoadsArgument(),
            CodeMatch.LoadsField(AccessTools.Field(typeof(Familiar), nameof(Familiar.m_MoveAgent)))
        )
        .ThrowIfInvalid("HideFamiliars - find label")
        .CreateLabel(out Label setHiddenAndReturnLabel)
        .Start()
        .MatchStartForward(
            CodeMatch.LoadsArgument(),
            CodeMatch.Calls(AccessTools.PropertyGetter(typeof(Familiar), nameof(Familiar.HideInCapital))),
            CodeMatch.Branches()
        )
        .ThrowIfInvalid("HideFamiliars - jump to label")
        .Insert(
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Main), nameof(Main.HideFamiliars))),
            new CodeInstruction(OpCodes.Brtrue, setHiddenAndReturnLabel)
        )
        .Instructions();
}