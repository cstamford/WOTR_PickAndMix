#if ENABLE_PROFILER

using HarmonyLib;
using Owlcat.Runtime.Core.Physics.PositionBasedDynamics.Scene;
using Owlcat.Runtime.Core.Physics.PositionBasedDynamics;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine.Profiling;

namespace PickAndMix;

[HarmonyPatch]
public static class ProfilePBD {
    [HarmonyPatch(typeof(TimeService), nameof(TimeService.Tick)), HarmonyPrefix] public static void TimeService_Tick_Prefix() => Profiler.BeginSample("TimeService.Tick");
    [HarmonyPatch(typeof(TimeService), nameof(TimeService.Tick)), HarmonyPostfix] public static void TimeService_Tick_Postfix() => Profiler.EndSample();

    [HarmonyPatch(typeof(Simulation), nameof(Simulation.Simulate)), HarmonyPrefix] public static void Simulation_Simulate_Prefix() => Profiler.BeginSample("Simulation.Simulate");
    [HarmonyPatch(typeof(Simulation), nameof(Simulation.Simulate)), HarmonyPostfix] public static void Simulation_Simulate_Postfix() => Profiler.EndSample();

    [HarmonyPatch(typeof(PBDSceneController), nameof(PBDSceneController.Tick)), HarmonyPrefix] public static void PBDSceneController_Tick_Prefix() => Profiler.BeginSample("PBDSceneController.Tick");
    [HarmonyPatch(typeof(PBDSceneController), nameof(PBDSceneController.Tick)), HarmonyPostfix] public static void PBDSceneController_Tick_Postfix() => Profiler.EndSample();
}

public static class ProfileSubscribeCalls {
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, Assembly assembly, Type type, MethodBase method) {
        foreach (CodeInstruction instruction in instructions) {
            bool isSubscribeCall = CallsSubscribe(instruction.opcode, instruction.operand);

            if (isSubscribeCall) {
                yield return new CodeInstruction(OpCodes.Ldstr, $"{type.Name}_{method.Name}");
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Profiler), nameof(Profiler.BeginSample)));
            }

            yield return instruction;

            if (isSubscribeCall) {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Profiler), nameof(Profiler.EndSample)));
            }
        }
    }

    public static bool CallsSubscribe(KeyValuePair<OpCode, object> inst) => CallsSubscribe(inst.Key, inst.Value);
    public static bool CallsSubscribe(OpCode opcode, object operand) =>
        (opcode == OpCodes.Call || opcode == OpCodes.Callvirt) &&
        (operand is MethodInfo methodInfo && methodInfo.Name == "Subscribe");

}

#endif
