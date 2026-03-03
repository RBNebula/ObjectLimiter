using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace MineMogul.ObjectLimiter
{
    [HarmonyPatch(typeof(SettingsMenu), "OnEnable")]
    internal static class SettingsMenuOreLimiterPatch
    {
        // 1) Keys / constants
        internal const string PrefUnlimitedKey = "OreLimiter_Unlimited";
        internal const string PrefLimitKey     = "OreLimiter_Limit";

        private static bool HasPrefKey(string key)
        {
            // PlayerPrefs.HasKey exists, use it.
            return PlayerPrefs.HasKey(key);
        }


        // 3) Harmony hook
        [HarmonyPostfix]
        private static void Postfix(SettingsMenu __instance)
        {
            try
            {
                Inject(__instance);
            }
            catch (Exception ex)
            {
                OreLimiterPlugin.Log.LogWarning(
                    $"OreLimiter: Settings injection failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // 4) Main logic
        private static void Inject(SettingsMenu menu)
        {
            var t = typeof(SettingsMenu);

            var accessibilityPage = (GameObject)t
                .GetField("_accessibilityPage", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(menu);

            if (accessibilityPage == null) return;

            // If we already injected into this page, don't do it again.
            if (accessibilityPage.GetComponentsInChildren<SettingToggle>(true)
                    .Any(x => x != null && x.name == "OreLimiter_SettingToggle_Unlimited") ||
                accessibilityPage.GetComponentsInChildren<SettingSlider>(true)
                    .Any(x => x != null && x.name == "OreLimiter_SettingSlider_Limit"))
            {
                return;
            }

            // Find templates INSIDE the Accessibility page so we clone correct style + scroll parent
            var templateToggle = accessibilityPage.GetComponentInChildren<SettingToggle>(true);
            var templateSlider = accessibilityPage.GetComponentInChildren<SettingSlider>(true);

            if (templateToggle == null || templateSlider == null)
            {
                // Some UIs build children after enable; retry next frame.
                menu.StartCoroutine(RetryNextFrame(menu));
                return;
            }

            var toggleTemplateGo = templateToggle.gameObject;
            var sliderTemplateGo = templateSlider.gameObject;

            var rowParent = sliderTemplateGo.transform.parent; // should be the scroll content/layout group
            if (rowParent == null) return;

            var toggleGo = UnityEngine.Object.Instantiate(toggleTemplateGo, rowParent);
            toggleGo.name = "OreLimiter_SettingToggle_Unlimited";

            var sliderGo = UnityEngine.Object.Instantiate(sliderTemplateGo, rowParent);
            sliderGo.name = "OreLimiter_SettingSlider_Limit";

            var toggleClone = toggleGo.GetComponent<SettingToggle>();
            var sliderClone = sliderGo.GetComponent<SettingSlider>();
            if (toggleClone == null || sliderClone == null) return;

            // Insert near the template slider (right after it)
            int insertIndex = sliderTemplateGo.transform.GetSiblingIndex() + 1;
            toggleGo.transform.SetSiblingIndex(insertIndex);
            sliderGo.transform.SetSiblingIndex(insertIndex + 1);

            // Configure Toggle
            SetPrivate(toggleClone, "settingKey", PrefUnlimitedKey);
            SetPrivate(toggleClone, "defaultValue", true);
            SetPrivate(toggleClone, "onText", "On");
            SetPrivate(toggleClone, "offText", "Off");
            SetRowLabel(toggleGo, "Ore limiter: Unlimited");

            // Configure Slider
            SetPrivate(sliderClone, "settingKey", PrefLimitKey);
            SetPrivate(sliderClone, "defaultValue", 250f);
            SetPrivate(sliderClone, "minValue", 0f);
            SetPrivate(sliderClone, "maxValue", 6000f);
            SetPrivate(sliderClone, "useInts", true);
            SetPrivate(sliderClone, "showAsPercent", false);
            SetRowLabel(sliderGo, "Ore limiter: Limit");

            // Ensure PlayerPrefs keys exist and match plugin defaults on first run
            var plugin = OreLimiterPlugin.Instance;
            if (plugin == null) return;

            if (!HasPrefKey(PrefUnlimitedKey))
                PlayerPrefs.SetInt(PrefUnlimitedKey, plugin.IsUnlimited() ? 1 : 0);

            if (!HasPrefKey(PrefLimitKey))
                PlayerPrefs.SetInt(PrefLimitKey, plugin.GetLimit());

            PlayerPrefs.Save();

            // Refresh from PlayerPrefs (uses our keys)
            toggleClone.RefreshFromSaved();
            sliderClone.RefreshFromSaved();

            // Pull UI truth and push it into the plugin once
            bool uiUnlimited = PlayerPrefs.GetInt(PrefUnlimitedKey, 1) == 1;
            int uiLimit = PlayerPrefs.GetInt(PrefLimitKey, 250);

            plugin.SetUnlimited(uiUnlimited);
            plugin.SetLimit(uiLimit);

            // Make the slider interactable based on UI truth
            SetSliderInteractable(sliderClone, !uiUnlimited);

            // Wire runtime behaviour
            toggleClone.onValueChanged += (v) =>
            {
                plugin.SetUnlimited(v);
                SetSliderInteractable(sliderClone, !v);
                plugin.EnforceLimitNow();
            };

            sliderClone.onValueChanged += (v) =>
            {
                plugin.SetLimit(Mathf.RoundToInt(v));
                plugin.EnforceLimitNow();
            };
        }

        // 5) Retry helper
        private static bool _retryQueued;
        private static System.Collections.IEnumerator RetryNextFrame(SettingsMenu menu)
        {
            if (_retryQueued) yield break;
            _retryQueued = true;

            yield return null;

            try { Inject(menu); }
            catch { }
            finally { _retryQueued = false; }
        }

        // 6) UI helpers
        private static void SetSliderInteractable(SettingSlider slider, bool interactable)
        {
            var uiSlider = GetPrivate<UnityEngine.UI.Slider>(slider, "slider");
            if (uiSlider != null) uiSlider.interactable = interactable;

            var input = GetPrivate<TMP_InputField>(slider, "valueInput");
            if (input != null) input.interactable = interactable;
        }

        private static void SetRowLabel(GameObject rowRoot, string label)
        {
            var baseOption = rowRoot.GetComponent<BaseSettingOption>();
            if (baseOption == null)
                return;

            // Set the "source of truth"
            var displayNameField = typeof(BaseSettingOption)
                .GetField("displayName", BindingFlags.Instance | BindingFlags.NonPublic);

            displayNameField?.SetValue(baseOption, label);

            // Immediately update the rendered label (UpdateLabel is private, so call via reflection)
            var updateLabelMethod = typeof(BaseSettingOption)
                .GetMethod("UpdateLabel", BindingFlags.Instance | BindingFlags.NonPublic);

            updateLabelMethod?.Invoke(baseOption, null);
        }

        // 7) Reflection helpers
        private static void SetPrivate(object obj, string fieldName, object value)
        {
            var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) return;
            f.SetValue(obj, value);
        }

        private static T? GetPrivate<T>(object obj, string fieldName) where T : class
        {
            var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(obj) as T;
        }
    }
}
