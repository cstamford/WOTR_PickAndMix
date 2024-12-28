using PickAndMix;
using HarmonyLib;
using Kingmaker.View.MapObjects.InteractionRestrictions;
using Owlcat.Runtime.Visual.RenderPipeline;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.Assertions;

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
