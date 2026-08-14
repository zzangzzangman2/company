#if UNITY_EDITOR
using System;
using System.Linq;
using FamilyCompany.Presentation.Unity.Stamina;
using FamilyCompany.Simulation.Stamina;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OverheadStaminaBarValidation
    {
        [MenuItem("Family Company/Validate Overhead Stamina Bars")]
        public static void Run()
        {
            GameObject root = null;
            GameObject owner = null;
            try
            {
                ValidateColorAndFillBoundaries();

                root = new GameObject("OverheadStaminaBarValidation");
                OverheadStaminaBarPresenter presenter =
                    root.AddComponent<OverheadStaminaBarPresenter>();
                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                AssertEqual(1, canvases.Length, "one shared world canvas");
                AssertEqual(RenderMode.WorldSpace, canvases[0].renderMode,
                    "world-space capture-compatible canvas");
                Component[] components = root.GetComponentsInChildren<Component>(true);
                AssertEqual(0, components.Count(item =>
                        item.GetType().FullName == "UnityEngine.UI.GraphicRaycaster"),
                    "bars never intercept input");
                AssertEqual(0, components.Count(item =>
                        item.GetType().FullName == "UnityEngine.UI.Text" ||
                        item.GetType().FullName == "TMPro.TextMeshProUGUI"),
                    "production bars contain no text");
                AssertEqual(0, presenter.BoundBarCount, "presenter owns no manual character bars");

                owner = new GameObject("StaminaBindingOwner");
                var roster = new CharacterStaminaRoster(
                    1,
                    CharacterStaminaCatalog.CreateCommonDefault(),
                    new[] { "player", "future_employee" });
                CharacterStaminaPresentationBinding.Bind(owner, roster);
                AssertTrue(CharacterStaminaPresentationBinding.TryGet(
                        out ICharacterStaminaReadModel readModel,
                        out _),
                    "transient read model binds");
                AssertTrue(ReferenceEquals(roster, readModel),
                    "presentation binding never creates semantic state");
                UnityEngine.Object.DestroyImmediate(owner);
                owner = null;
                AssertFalse(CharacterStaminaPresentationBinding.TryGet(out _, out _),
                    "destroyed integration owner clears stale binding");

                Debug.Log("FAMILY_COMPANY_OVERHEAD_STAMINA_BAR_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_OVERHEAD_STAMINA_BAR_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateColorAndFillBoundaries()
        {
            AssertEqual(OverheadStaminaColorBand.Stable, ResolveBand(10_000),
                "full stamina remains visible stable data");
            AssertEqual(OverheadStaminaColorBand.Stable, ResolveBand(5_001),
                "stable lower boundary");
            AssertEqual(OverheadStaminaColorBand.Caution, ResolveBand(5_000),
                "caution inclusive boundary");
            AssertEqual(OverheadStaminaColorBand.Caution, ResolveBand(2_501),
                "caution upper critical boundary");
            AssertEqual(OverheadStaminaColorBand.Critical, ResolveBand(2_500),
                "critical inclusive 25 percent boundary");
            AssertEqual(0f, OverheadStaminaBarPresenter.ResolveFillRatio(-1),
                "fill clamps below zero");
            AssertEqual(1f, OverheadStaminaBarPresenter.ResolveFillRatio(10_001),
                "fill clamps above full");
            AssertEqual(1f, OverheadStaminaBarPresenter.ResolveFillRatio(10_000),
                "full bar is not hidden");
        }

        private static OverheadStaminaColorBand ResolveBand(int ratioBasisPoints)
        {
            var snapshot = new CharacterStaminaReadSnapshot(
                "qa",
                ratioBasisPoints,
                10_000,
                ratioBasisPoints,
                2_500,
                5_000,
                0,
                StaminaRecoveryPhase.Working,
                StaminaRecoveryActivity.None);
            return OverheadStaminaBarPresenter.ResolveColorBand(snapshot);
        }

        private static void AssertTrue(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Assertion failed: " + label);
        }

        private static void AssertFalse(bool value, string label)
        {
            if (value) throw new InvalidOperationException("Assertion failed: " + label);
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException(
                    $"Assertion failed: {label}; expected={expected}, actual={actual}.");
        }
    }
}
#endif
