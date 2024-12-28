#if ENABLE_PROFILER

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.Assertions;

namespace PickAndMix;
public record struct DynamicTranspilerErrors(List<(Assembly, Exception)> Assembly, List<(Type, Exception)> Types, List<(MethodBase, Exception)> Methods) {
    public readonly bool Success => Assembly.Count == 0 && Types.Count == 0 && Methods.Count == 0;
}

public class DynamicTranspiler(Harmony harmony, DynamicTranspiler.Transpiler transpiler, DynamicTranspiler.MethodSelector methods) {
    public delegate IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, Assembly assembly, Type type, MethodBase method);
    public delegate IEnumerable<MethodBase> MethodSelector(Assembly assembly, Type type);

    public static DynamicTranspilerErrors Apply(Harmony harmony, Transpiler transpiler, MethodSelector methods = null)
        => new DynamicTranspiler(harmony, transpiler, methods).Apply();

    public DynamicTranspilerErrors Apply() {
        _errors = new([], [], []);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            try {
                ApplyAssembly(assembly);
            } catch (Exception e) {
                _errors.Assembly.Add((assembly, e));
            }
        }

        return _errors;
    }

    private DynamicTranspilerErrors _errors;


    private void ApplyAssembly(Assembly assembly) {
        List<Type> types = [];

        try {
            types.AddRange(assembly.GetTypes());
        } catch (ReflectionTypeLoadException e) {
            types.AddRange(e.Types.Where(x => x != null));
        }

        foreach (Type type in types) {
            try {
                ApplyType(assembly, type);
            } catch (Exception e) {
                _errors.Types.Add((type, e));
            }
        }

    }

    private void ApplyType(Assembly assembly, Type type) {
        foreach (MethodBase method in methods(assembly, type)) {
            try {
                ApplyPatch(assembly, type, method);
            } catch (Exception e) {
                _errors.Methods.Add((method, e));
            }
        }
    }

    private void ApplyPatch(Assembly assembly, Type type, MethodBase method) {
        using Trampoline trampoline = new(transpiler, assembly, type, method);
        harmony.Patch(method, transpiler: new() { method = trampoline.MethodInfo });
    }


    private class Trampoline : IDisposable {
        public Trampoline(Transpiler transpiler, Assembly assembly, Type type, MethodBase method) {
            Assert.IsNull(_transpiler);
            Assert.IsNull(_assembly);
            Assert.IsNull(_type);
            Assert.IsNull(_method);

            _transpiler = transpiler;
            _assembly = assembly;
            _type = type;
            _method = method;
        }

        public void Dispose() {
            Assert.IsNotNull(_transpiler);
            Assert.IsNotNull(_assembly);
            Assert.IsNotNull(_type);
            Assert.IsNotNull(_method);

            _transpiler = null;
            _assembly = null;
            _type = null;
            _method = null;
        }

        public Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>> Method => Impl;
        public MethodInfo MethodInfo => Method.Method;

        private static Transpiler _transpiler;
        private static Assembly _assembly;
        private static Type _type;
        private static MethodBase _method;

        private static IEnumerable<CodeInstruction> Impl(IEnumerable<CodeInstruction> instructions) {
            return _transpiler(instructions, _assembly, _type, _method);
        }
    }
}

#endif
