using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Presentation.Unity.UIRemaster;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.UIRemaster
{
    public static class UiRemasterTypographyValidation
    {
        private const string CatalogPath =
            "Assets/FamilyCompany/Presentation.Unity/Resources/UiRemasterV3/UiRemasterFontCatalog_v3.asset";
        private const string ArtifactPath = "Artifacts/UiRemasterV3/TypographyValidation.txt";
        private const string RequiredStatsText =
            "능력치 개발력 기획력 디자인 업무 속도 체력 집중력 잠재력 교육 등급 가족 직원 연봉 부서 배치 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.,:;!?%+-/()[]·₩원";

        [MenuItem("Family Company/Validate UI Remaster V3 Typography")]
        public static void Run()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UiRemasterFontCatalog>(CatalogPath);
            Require(catalog != null && catalog.IsComplete, "UiRemasterFontCatalog_v3 is missing or incomplete.");
            Require(catalog.BodySource.name.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) >= 0,
                "Body source is not Maplestory Light.");
            Require(catalog.HeadingSource.name.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) >= 0,
                "Heading source is not Maplestory Bold.");

            var characters = CollectGameCharacters();
            foreach (var character in RequiredStatsText) characters.Add(character);
            var sample = new string(characters.OrderBy(item => item).ToArray());
            catalog.BodySource.RequestCharactersInTexture(sample, UiRemasterTypography.BodyPixels, FontStyle.Normal);
            catalog.HeadingSource.RequestCharactersInTexture(sample, UiRemasterTypography.PanelTitlePixels, FontStyle.Normal);
            catalog.FallbackSource.RequestCharactersInTexture(sample, UiRemasterTypography.BodyPixels, FontStyle.Normal);

            var missing = characters
                .Where(character => !char.IsControl(character) &&
                                    !catalog.BodySource.HasCharacter(character) &&
                                    !catalog.FallbackSource.HasCharacter(character))
                .OrderBy(character => character)
                .ToArray();
            Require(missing.Length == 0,
                "Missing glyphs in Maplestory Light + Pretendard fallback: " +
                string.Join(", ", missing.Select(character => $"{character}(U+{(int)character:X4})")));

            var boldMissing = RequiredStatsText
                .Where(character => !char.IsWhiteSpace(character) && !catalog.HeadingSource.HasCharacter(character))
                .Distinct()
                .ToArray();
            Require(boldMissing.Length == 0,
                "Maplestory Bold is missing required title/stat glyphs: " +
                string.Join(", ", boldMissing.Select(character => $"{character}(U+{(int)character:X4})")));

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ArtifactPath)) ?? "Artifacts");
            File.WriteAllText(Path.GetFullPath(ArtifactPath),
                "UI_REMASTER_V3_TYPOGRAPHY_PASS\n" +
                $"body={catalog.BodySource.name}\nheading={catalog.HeadingSource.name}\nfallback={catalog.FallbackSource.name}\n" +
                $"scannedCharacters={characters.Count}\nmissingGlyphs=0\nstatsGlyphs=PASS\n" +
                $"tiers720p=panel:{UiRemasterTypography.PanelTitlePixels},card:{UiRemasterTypography.CardTitlePixels},body:{UiRemasterTypography.BodyPixels},top:{UiRemasterTypography.TopHudPixels},bottom:{UiRemasterTypography.BottomNavigationPixels},button:{UiRemasterTypography.ButtonPixels}\n",
                new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("FAMILY_COMPANY_UI_REMASTER_V3_TYPOGRAPHY: PASS | missingGlyphs=0 statsGlyphs=PASS characters=" + characters.Count);
        }

        private static HashSet<char> CollectGameCharacters()
        {
            var result = new HashSet<char>();
            var root = Path.GetFullPath("Assets/FamilyCompany");
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                foreach (var character in File.ReadAllText(file))
                {
                    if (IsDisplayCharacter(character)) result.Add(character);
                }
            }
            return result;
        }

        private static bool IsDisplayCharacter(char character)
        {
            return character >= '\uAC00' && character <= '\uD7A3' ||
                   character >= '0' && character <= '9' ||
                   character >= 'A' && character <= 'Z' ||
                   character >= 'a' && character <= 'z' ||
                   " .,:;!?%+-/()[]{}·₩_'\"&".IndexOf(character) >= 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
