using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;
using UnityEngine;
using UniRx;
using static UnityEngine.GUILayout;
using static PickAndMix.GUILayoutExtensions;

namespace PickAndMix;

public class PickAndMixSettings : UnityModManager.ModSettings {
    // Shadow settings
    [Range(1024f, 16384f)] public int AtlasSize;
    [Range(128f, 4096f)] public int DirectionalLightCascadeResolution;
    [Range(1, 4)] public int DirectionalLightCascadeCount;
    [Range(128f, 2048f)] public int PointLightResolution;
    [Range(128f, 2048f)] public int SpotLightResolution;

    // Tweaks
    public bool DisableLockJamming;

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
    }

    public void ApplyOptimizedDefaults() {
        AtlasSize = 16384;
        DirectionalLightCascadeResolution = 4096;
        DirectionalLightCascadeCount = 1;
        PointLightResolution = 2048;
        SpotLightResolution = 2048;
        DisableLockJamming = true;
    }
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
    private static readonly List<int> _DirectionalLightCascadeResolutions = [128, 256, 512, 1024, 2048, 4096];
    private static readonly List<int> _PointLightResolutions = [128, 256, 512, 1024, 2048];
    private static readonly List<int> _SpotLightResolutions = [128, 256, 512, 1024, 2048];

    public static void OnGUI(UnityModManager.ModEntry modEntry) {
        GUILayoutOption labelWidth = Width(100);
        GUILayoutOption controlWidth = Width(167);
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
            OnGUI_ShadowAtlasSize(labelWidth, controlWidth);
            Space(spacing);
            OnGUI_DirectionalLightCascadeCount(labelWidth, controlWidth);
        }

        Space(spacing);

        using (HorizontalScope _ = new()) {
            OnGUI_DirectionalLightCascadeResolution(labelWidth, controlWidth);
            Space(spacing);
            OnGUI_PointLightResolution(labelWidth, controlWidth);
            Space(spacing);
            OnGUI_SpotLightResolution(labelWidth, controlWidth);
        }

        Spacer(Color.black, spacing/2, spacing);

        using (HorizontalScope _ = new()) {
            OnGUI_DisableLockJamming(labelWidth);
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

    public static void OnGUI_DisableLockJamming(GUILayoutOption labelWidth) {
        Label("Lock Jamming", labelWidth);
        int currentDisableLockJammingIdx = _settings.DisableLockJamming ? 0 : 1;
        int disableLockJammingIdx = SelectionGrid(currentDisableLockJammingIdx, ["Locks will never jam", "Locks may jam"], 2);
        _settings.DisableLockJamming = disableLockJammingIdx == 0;
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
