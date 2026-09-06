using System;
using System.Collections.Generic;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGrid
{
    // Uses the production font/styles and reserved rectangles, without opening a game window.
    // This checks text metrics only, not a presented IMGUI frame or native purchase interaction.
    public static class OfficeShopTextLayoutValidation
    {
        [Serializable] private sealed class Sample
        {
            public int screenHeight;
            public string label;
            public float width, height, requiredWidth, requiredHeight;
        }
        [Serializable] private sealed class Report
        {
            public string scope = "Actual production font/style metrics, not native UI pixels or Release approval";
            public bool passed;
            public List<Sample> samples = new List<Sample>();
        }

        public static void RunBatch()
        {
            var report = new Report();
            try
            {
                foreach (int height in new[] { 720, 900, 1080, 1440, 2160 })
                {
                    var skin = new OfficeLayoutEditModeSkin();
                    skin.EnsureBuilt(height);
                    int R(float value) => skin.Round(value);
                    float rowWidth = R(440) - 2 * R(14) - R(18);
                    float textWidth = rowWidth - R(84) - R(10) - R(6) - R(66) - R(8);
                    Check(report, height, "catalog title", "책상·PC·의자 세트", skin.CatalogTitleStyle, textWidth, R(22));
                    Check(report, height, "catalog price", "400,000원 · 3칸 점유", skin.CatalogHintStyle, textWidth, R(20));
                    Check(report, height, "catalog stock", "보유 999 · 배치 999", skin.CatalogHintStyle, textWidth, R(19));
                    Check(report, height, "purchase", "구매", skin.ButtonStyle, R(78), R(30));
                    Check(report, height, "stored placement", "보관 배치", skin.DisabledButtonStyle, R(78), R(30));
                    float detailsWidth = R(440) - 2 * R(14) - 2 * R(10);
                    Check(report, height, "details title", "책상·PC·의자 세트", skin.TitleStyle, detailsWidth, R(24));
                    Check(report, height, "details cost", "구매가 400,000원\n배치 후 잔액 4,600,000원", skin.BodyStyle, detailsWidth, R(44));
                    Check(report, height, "details footprint", "책상 2칸 · 의자 1칸 · 유지비 1,000원/일", skin.HintStyle, detailsWidth, R(20));
                    Check(report, height, "empty hint", "가구 선택 또는 세트 구매 후 배치하세요", skin.BodyStyle, detailsWidth, R(24));
                    Check(report, height, "controls", "선택/집기 · 미리보기 · 타일 중심 스냅 · R 90° 회전\nESC/우클릭 취소 · 확정 전에는 차감 없음", skin.HintStyle, detailsWidth, R(42));
                    foreach (string facing in new[] { "남동", "남서", "북서", "북동" })
                        Check(report, height, "rotation", "R 90° 회전 · 현재 " + facing, skin.BodyStyle, detailsWidth, R(44));
                    Check(report, height, "placement error", "● 다른 가구와 겹칩니다. 위치를 바꾸세요.", skin.HintStyle, detailsWidth, R(38));
                    float availableDetails = R(260) - R(8);
                    float usedDetails = R(10) + R(26) + R(22) + R(46) + R(40) + R(40) + R(34);
                    if (usedDetails > availableDetails) throw new InvalidOperationException("Detail buttons exceed panel.");
                }
                report.passed = true;
                Debug.Log("OFFICE_SHOP_TEXT_METRICS: PASS samples=" + report.samples.Count + " nativePixels=false");
            }
            catch (Exception exception) { Debug.LogException(exception); }
            string path = Path.GetFullPath("Artifacts/ShopTextMetrics/report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        private static void Check(Report report, int height, string label, string text, GUIStyle style, float width, float boxHeight)
        {
            if (style.font == null) throw new InvalidOperationException("Approved Korean font is missing.");
            style.font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);
            var content = new GUIContent(text);
            float requiredWidth = style.CalcSize(content).x;
            float requiredHeight = style.CalcHeight(content, width);
            report.samples.Add(new Sample { screenHeight = height, label = label, width = width, height = boxHeight,
                requiredWidth = requiredWidth, requiredHeight = requiredHeight });
            // These known messages should fit without clipping or unplanned extra wraps.
            if (requiredWidth > width + 0.01f || requiredHeight > boxHeight + 0.01f)
                throw new InvalidOperationException($"Text overflow at {height}: {label} needs {requiredWidth}x{requiredHeight}, has {width}x{boxHeight}.");
        }
    }
}
