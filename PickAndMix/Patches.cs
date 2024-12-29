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

[HarmonyPatch(typeof(OwlcatRenderPipeline), nameof(OwlcatRenderPipeline.InitializeShadowData), [typeof(ShadowingData)], [ArgumentType.Out])]
public static class FixShadowResolution {
    public static void Postfix(ref ShadowingData shadowData) {
        if (Game.Instance.CurrentMode == GameModeType.GlobalMap ||
            Game.Instance.CurrentMode == GameModeType.Kingdom)
        {
            // Don't apply shadow tweaks unless we're in regular play.  Point light resolution causes visual aliasing (shadow bias seems off).
            return;
        }

        shadowData.AtlasSize = Main.AtlasSize;
        shadowData.DirectionalLightCascades.Count = Main.DirectionalLightCascadeCount;
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

[HarmonyPatch]
public static class MainCharacterHasAdvantageOnRolls {
    [HarmonyPrefix, HarmonyPatch(typeof(RuleRollDice), nameof(RuleRollDice.Roll))]
    public static void RuleRollDice_Roll(RuleRollDice __instance) {
#if DEBUG
        Main.ModEntry.Logger.Log($"{__instance.DiceFormula.Dice} {__instance.Initiator}");
#endif

        if (__instance.Initiator.IsMainCharacter && Main.MainCharacterHasAdvantageOnRollsFlags.Matches(__instance.DiceFormula.Dice)) {
            __instance.AddReroll(1, true, __instance.Initiator.Facts.GetAll<Feature>().First(i => i.Blueprint.HasGroup(FeatureGroup.Deities)));
        }
    }

#if TODO
    [HarmonyPatch(typeof(TacticalCombatHelper), nameof(TacticalCombatHelper.GetDiceResult))]
    public static class TacticalCombatHelper_GetDiceResult {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => new CodeMatcher(instructions)
            .MatchEndForward(CodeMatch.WithOpcodes([OpCodes.Ret]))
            .Repeat(cm => cm.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TacticalCombatHelper_GetDiceResult), nameof(CalculateRoll))
            )))
            .Instructions();

        private static int CalculateRoll(int originalRoll, DiceFormula dice) {
#if DEBUG
            Main.ModEntry.Logger.Log($"{dice} -> original: {originalRoll}");
#endif

            if (Main.MainCharacterHasAdvantageOnRollsFlags.Matches(dice.Dice) && false) { // TODO: No context on the roller, so we can't check if it's the main character
                return Math.Max(originalRoll, GetDiceResult_Original(dice));
            }

            return originalRoll;
        }

        [HarmonyReversePatch]
        private static int GetDiceResult_Original(DiceFormula dice) => throw new("Stub");
    }
#endif
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