using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;
using UnityEngine;
using UniRx;
using static UnityEngine.GUILayout;
using static PickAndMix.GUILayoutExtensions;
using System;
using Kingmaker.RuleSystem;

namespace PickAndMix;

[Flags]
public enum PickAndMixDiceTypeFlags {
    D2 = 1 << 0,
    D3 = 1 << 1,
    D4 = 1 << 2,
    D6 = 1 << 3,
    D8 = 1 << 4,
    D10 = 1 << 5,
    D12 = 1 << 6,
    D20 = 1 << 7,
    D100 = 1 << 8,
}

public static class PickAndMixDiceTypeFlagsExtensions {
    public static bool Matches(this PickAndMixDiceTypeFlags flags, DiceType diceType) => diceType switch {
        DiceType.D2 => flags.HasFlag(PickAndMixDiceTypeFlags.D2),
        DiceType.D3 => flags.HasFlag(PickAndMixDiceTypeFlags.D3),
        DiceType.D4 => flags.HasFlag(PickAndMixDiceTypeFlags.D4),
        DiceType.D6 => flags.HasFlag(PickAndMixDiceTypeFlags.D6),
        DiceType.D8 => flags.HasFlag(PickAndMixDiceTypeFlags.D8),
        DiceType.D10 => flags.HasFlag(PickAndMixDiceTypeFlags.D10),
        DiceType.D12 => flags.HasFlag(PickAndMixDiceTypeFlags.D12),
        DiceType.D20 => flags.HasFlag(PickAndMixDiceTypeFlags.D20),
        DiceType.D100 => flags.HasFlag(PickAndMixDiceTypeFlags.D100),
        _ => false,
    };
}

public class PickAndMixSettings : UnityModManager.ModSettings {
    // Shadow settings
    [Range(1024f, 16384f)] public int AtlasSize;
    [Range(128f, 4096f)] public int DirectionalLightCascadeResolution;
    [Range(1, 4)] public int DirectionalLightCascadeCount;
    [Range(128f, 2048f)] public int PointLightResolution;
    [Range(128f, 2048f)] public int SpotLightResolution;

    // Tweaks
    public bool DisableLockJamming;
    public PickAndMixDiceTypeFlags MainCharacterHasAdvantageOnRollsFlags;
    public bool AvoidAmbushFromRandomEncounters;
    public bool HideFamiliars;

    public PickAndMixSettings() => ApplyOptimizedDefaults();

    public override void Save(UnityModManager.ModEntry modEntry) {
        Save(this, modEntry);
    }

    public void ApplyWOTRDefaults() {
        AtlasSize = 4096;
        DirectionalLightCascadeResolution = 1024;
        DirectionalLightCascadeCount = 1;
        PointLightResolution = 512;
        SpotLightResolution = 512;
        DisableLockJamming = false;
        MainCharacterHasAdvantageOnRollsFlags =
            default;
        AvoidAmbushFromRandomEncounters = false;
        HideFamiliars = false;
    }

    public void ApplyOptimizedDefaults() {
        AtlasSize = 16384;
        DirectionalLightCascadeResolution = 4096;
        DirectionalLightCascadeCount = 1;
        PointLightResolution = 2048;
        SpotLightResolution = 2048;
        DisableLockJamming = true;
        MainCharacterHasAdvantageOnRollsFlags = 
            PickAndMixDiceTypeFlags.D4 |
            PickAndMixDiceTypeFlags.D6 |
            PickAndMixDiceTypeFlags.D8 |
            PickAndMixDiceTypeFlags.D10 |
            PickAndMixDiceTypeFlags.D12 |
            PickAndMixDiceTypeFlags.D20;
        AvoidAmbushFromRandomEncounters = true;
        HideFamiliars = true;
    }
}

public static class ErrorText {
    public static string HideFamiliarsDisabledDueToError =
        $"Exception when trying to apply HideFamiliars. The hidden familiar functionality will not work. " +
        $"This was likely caused by an old version of Harmony. Make sure your version of Wrath_Data/Managed/0Harmony.dll is 2.3.1.1 or later. " +
        $"If your version of Harmony is old, you may be able to fix it by replacing it with the one from Wrath_Data/Managed/UnityModManager/0Harmony.dll";
}

#if DEBUG
[EnableReloading]
#endif
public static class Main {
    public static int AtlasSize => _settings.AtlasSize;
    public static int DirectionalLightCascadeCount => _settings.DirectionalLightCascadeCount;
    public static int DirectionalLightCascadeResolution => _settings.DirectionalLightCascadeResolution;
    public static int PointLightResolution => _settings.PointLightResolution;
    public static int SpotLightResolution => _settings.SpotLightResolution;
    public static bool DisableLockJamming => _settings.DisableLockJamming;
    public static PickAndMixDiceTypeFlags MainCharacterHasAdvantageOnRollsFlags => _settings.MainCharacterHasAdvantageOnRollsFlags;
    public static bool AvoidAmbushFromRandomEncounters => _settings.AvoidAmbushFromRandomEncounters;
    public static bool HideFamiliars => _settings.HideFamiliars;
    public static Exception HideFamiliarsException { get; set; } = null;

    public static Harmony HarmonyInstance;
    public static UnityModManager.ModEntry ModEntry;

    public static bool Load(UnityModManager.ModEntry modEntry) {
        ModEntry = modEntry;

        _settings = UnityModManager.ModSettings.Load<PickAndMixSettings>(modEntry);

#if DEBUG
        modEntry.OnUnload = OnUnload;
#endif
        modEntry.OnGUI = OnGUI;
        modEntry.OnSaveGUI = OnSaveGUI;

        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

        return true;
    }

    private static readonly List<int> _AtlasSizes = [1024, 2048, 4096, 8192, 16384];
    private static readonly List<int> _DirectionalLightCascadeCount = [1, 2, 3, 4];
    private static readonly List<int> _DirectionalLightCascadeResolutions = [256, 512, 1024, 2048, 4096];
    private static readonly List<int> _PointLightResolutions = [128, 256, 512, 1024, 2048];
    private static readonly List<int> _SpotLightResolutions = [128, 256, 512, 1024, 2048];
    private static readonly List<string> _DiceFlagOptions = [..Enum.GetValues(typeof(PickAndMixDiceTypeFlags)).Cast<PickAndMixDiceTypeFlags>().Select(x => x.ToString())];

    public static void OnGUI(UnityModManager.ModEntry modEntry) {
        GUILayoutOption labelWidth = Width(128);
        GUILayoutOption shadowControlWidth = Width(192);
        GUILayoutOption tweaksControlWidth = Width(384);

        int spacing = 10;

        using (HorizontalScope _ = new()) {
            if (Button("Reset to Optimized defaults")) {
                _settings.ApplyOptimizedDefaults();
            }

            Space(spacing);

            if (Button("Reset to WOTR defaults")) {
                _settings.ApplyWOTRDefaults();
            }
        }

        Spacer(Color.black, spacing/2, spacing);

        using (HorizontalScope _ = new()) {
            OnGUI_ShadowAtlasSize(labelWidth, shadowControlWidth);
            Space(spacing);
            OnGUI_DirectionalLightCascadeResolution(labelWidth, shadowControlWidth);
        }

        Space(spacing);

        using (HorizontalScope _ = new()) {
            OnGUI_PointLightResolution(labelWidth, shadowControlWidth);
            Space(spacing);
            OnGUI_SpotLightResolution(labelWidth, shadowControlWidth);
        }

        Spacer(Color.black, spacing/2, spacing);

        using (HorizontalScope _ = new()) {
            OnGUI_DisableLockJamming(labelWidth, tweaksControlWidth);
        }

        Space(spacing);

        using (HorizontalScope _ = new()) {
            OnGUI_MainCharacterHasAdvantageOnRollsFlags(labelWidth, tweaksControlWidth);
        }

        Space(spacing);

        using (HorizontalScope _ = new()) {
            OnGUI_AvoidAmbushFromRandomEncounters(labelWidth, tweaksControlWidth);
        }

        Space(spacing);

        using (HorizontalScope _ = new()) {
            OnGUI_HideFamiliars(labelWidth, tweaksControlWidth);
        }

        Spacer(Color.black, spacing/2, spacing);
    }

    public static void OnGUI_ShadowAtlasSize(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        Label("Shadow Atlas Size", labelWidth);
        int currentUpscalerIdx = _AtlasSizes.IndexOf(_settings.AtlasSize);
        int upscalerIdx = SelectionGrid(currentUpscalerIdx, [.. _AtlasSizes.Select(x => x.ToString())], 1, controlWidth);
        _settings.AtlasSize = _AtlasSizes[upscalerIdx];
    }

    public static void OnGUI_DirectionalLightCascadeCount(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        Label("Directional Light Cascade Count", labelWidth);
        int currentDirectionalLightCascadeCountIdx = _DirectionalLightCascadeCount.IndexOf(_settings.DirectionalLightCascadeCount);
        int directionalLightCascadeCountIdx = SelectionGrid(currentDirectionalLightCascadeCountIdx, [.. _DirectionalLightCascadeCount.Select(x => x.ToString())], 1, controlWidth);
        _settings.DirectionalLightCascadeCount = _DirectionalLightCascadeCount[directionalLightCascadeCountIdx];
    }

    public static void OnGUI_DirectionalLightCascadeResolution(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        Label("Directional Light Cascade Resolution", labelWidth);
        int currentDirectionalLightCascadeResolutionIdx = _DirectionalLightCascadeResolutions.IndexOf(_settings.DirectionalLightCascadeResolution);
        int directionalLightCascadeResolutionIdx = SelectionGrid(currentDirectionalLightCascadeResolutionIdx, [.. _DirectionalLightCascadeResolutions.Select(x => x.ToString())], 1, controlWidth);
        _settings.DirectionalLightCascadeResolution = _DirectionalLightCascadeResolutions[directionalLightCascadeResolutionIdx];
    }

    public static void OnGUI_PointLightResolution(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        Label("Point Light Resolution", labelWidth);
        int currentPointLightResolutionIdx = _PointLightResolutions.IndexOf(_settings.PointLightResolution);
        int pointLightResolutionIdx = SelectionGrid(currentPointLightResolutionIdx, [.. _PointLightResolutions.Select(x => x.ToString())], 1, controlWidth);
        _settings.PointLightResolution = _PointLightResolutions[pointLightResolutionIdx];
    }

    public static void OnGUI_SpotLightResolution(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        Label("Spot Light Resolution", labelWidth);
        int currentSpotLightResolutionIdx = _SpotLightResolutions.IndexOf(_settings.SpotLightResolution);
        int spotLightResolutionIdx = SelectionGrid(currentSpotLightResolutionIdx, [.. _SpotLightResolutions.Select(x => x.ToString())], 1, controlWidth);
        _settings.SpotLightResolution = _SpotLightResolutions[spotLightResolutionIdx];
    }

    public static void OnGUI_DisableLockJamming(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        Label("Lock Jamming", labelWidth);
        int currentDisableLockJammingIdx = _settings.DisableLockJamming ? 0 : 1;
        int disableLockJammingIdx = SelectionGrid(currentDisableLockJammingIdx, ["Locks will never jam", "Locks may jam"], 1, controlWidth);
        _settings.DisableLockJamming = disableLockJammingIdx == 0;
    }

    public static void OnGUI_MainCharacterHasAdvantageOnRollsFlags(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        Label("Main Character Has Advantage on Rolls", labelWidth);

        using VerticalScope _ = new();

        foreach (string flag in _DiceFlagOptions) {
            PickAndMixDiceTypeFlags flagValue = (PickAndMixDiceTypeFlags)Enum.Parse(typeof(PickAndMixDiceTypeFlags), flag);
            bool flagEnabled = _settings.MainCharacterHasAdvantageOnRollsFlags.HasFlag(flagValue);
            bool newFlagEnabled = Toggle(flagEnabled, flag);

            if (newFlagEnabled != flagEnabled) {
                if (newFlagEnabled) {
                    _settings.MainCharacterHasAdvantageOnRollsFlags |= flagValue;
                } else {
                    _settings.MainCharacterHasAdvantageOnRollsFlags &= ~flagValue;
                }
            }
        }
    }

    public static void OnGUI_AvoidAmbushFromRandomEncounters(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        Label("Avoid Ambush from Random Encounters", labelWidth);
        int currentAvoidAmbushFromRandomEncountersIdx = _settings.AvoidAmbushFromRandomEncounters ? 0 : 1;
        int avoidAmbushFromRandomEncountersIdx = SelectionGrid(currentAvoidAmbushFromRandomEncountersIdx, ["Ambushes are avoided", "Ambushes are not avoided"], 1, controlWidth);
        _settings.AvoidAmbushFromRandomEncounters = avoidAmbushFromRandomEncountersIdx == 0;
    }

    public static void OnGUI_HideFamiliars(GUILayoutOption labelWidth, GUILayoutOption controlWidth) {
        GUI.enabled = HideFamiliarsException == null;

        Label("Hide Familiars", labelWidth);
        int currentHideFamiliarsIdx = _settings.HideFamiliars ? 0 : 1;
        int hideFamiliarsIdx = SelectionGrid(currentHideFamiliarsIdx, ["Familiars are hidden", "Familiars are not hidden"], 1, controlWidth);
        _settings.HideFamiliars = hideFamiliarsIdx == 0;

        if (HideFamiliarsException != null) {
            Label($"{ErrorText.HideFamiliarsDisabledDueToError}\n{HideFamiliarsException}");
        }

        GUI.enabled = true;
    }

    private static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
        _settings.Save(modEntry);
    }

#if DEBUG
    public static bool OnUnload(UnityModManager.ModEntry modEntry) {
        HarmonyInstance.UnpatchAll(modEntry.Info.Id);
        return true;
    }
#endif

    private static PickAndMixSettings _settings;
}

public static class GUILayoutExtensions {
    public static void Spacer(Color color, int topSpacing = 0, int bottomSpacing = 0) {
        if (topSpacing > 0) {
            Space(topSpacing);
        }

        Color c = GUI.color;
        GUI.color = color;
        Box(GUIContent.none, _lineStyle);
        GUI.color = c;

        if (bottomSpacing > 0) {
            Space(bottomSpacing);
        }
    }

    private static readonly GUIStyle _lineStyle = new() {
        fixedHeight = 1,
        margin = new(0, 0, 4, 4),
        normal = new() {
            background = Texture2D.whiteTexture
        },
    };

}
