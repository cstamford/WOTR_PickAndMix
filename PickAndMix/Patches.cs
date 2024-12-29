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

[HarmonyPatch(typeof(OwlcatRenderPipeline), nameof(OwlcatRenderPipeline.InitializeShadowData), [typeof(ShadowingData)], [ArgumentType.Out])]
public static class FixShadowResolution {
    public static void Postfix(ref ShadowingData shadowData) {
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

[HarmonyPatch(typeof(RuleRollDice), nameof(RuleRollDice.Roll))]
public static class MainCharacterHasAdvantageOnRolls {
    public static void Prefix(RuleRollDice __instance) {
        if (Main.MainCharacterHasAdvantageOnRolls && __instance.Initiator.IsMainCharacter) {
            __instance.AddReroll(1, true, __instance.Initiator.Facts.GetAll<Feature>().First(i => i.Blueprint.HasGroup(FeatureGroup.Deities)));
        }
    }
}

[HarmonyPatch(typeof(RandomEncountersController), nameof(RandomEncountersController.GetAvoidanceCheckResult))]
public static class AvoidAmbushFromRandomEncounters {
    public static void Postfix(ref RandomEncounterAvoidanceCheckResult __result) {
        __result = Main.AvoidAmbushFromRandomEncounters ? RandomEncounterAvoidanceCheckResult.Success : __result;
    }
}