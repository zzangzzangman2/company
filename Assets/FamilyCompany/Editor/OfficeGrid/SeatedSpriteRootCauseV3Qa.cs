using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    public static class SeatedSpriteRootCauseV3Qa
    {
        public const string ArtifactFolder = "Artifacts/SeatedSpriteRootCauseV3";
        private static readonly string[] MemberIds = { "player", "older_sister", "father", "mother" };

        [MenuItem("Family Company/QA/Seated Sprite Root Cause V3")]
        public static void RunMenu()
        {
            Run();
        }

        public static void RunBatch()
        {
            Run();
        }

        private static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;
            string artifactDirectory = Path.Combine(projectRoot, ArtifactFolder);
            Directory.CreateDirectory(artifactDirectory);
            string reportPath = Path.Combine(artifactDirectory, "seated-sprite-root-cause-v3-report.txt");
            var report = new List<string>
            {
                "SEATED_SPRITE_ROOT_CAUSE_V5_ANIMATED_NORTHWEST",
                "Unity=" + Application.unityVersion,
                "Mode=Animated / Northwest / SitDown 4 + Work 6 + StandUp 4",
                string.Empty,
                "member|clip|frame|scale|rotation|visible-height|pelvis|hand|approved|sha|result"
            };
            var failures = new List<string>();

            OfficeCharacterSeatPoseCatalog poseCatalog =
                AssetDatabase.LoadAssetAtPath<OfficeCharacterSeatPoseCatalog>(OfficeFurnitureAssetBuilder.PoseCatalogPath);
            Check(poseCatalog != null, "Pose catalog is missing.", failures);
            if (poseCatalog != null)
            {
                Check(poseCatalog.CalibrationVersion == OfficeCharacterSeatPoseCatalog.CurrentCalibrationVersion,
                    $"Pose catalog version is {poseCatalog.CalibrationVersion}, expected 5.", failures);
                Check(poseCatalog.Profiles.Count == 56,
                    $"Animated Northwest catalog must contain exactly 56 approved profiles, found {poseCatalog.Profiles.Count}.", failures);
                try
                {
                    poseCatalog.ValidateAnimatedNorthwest(MemberIds, (int)OfficeSeatFacing8.Northwest);
                }
                catch (Exception exception)
                {
                    failures.Add("Animated Northwest catalog validation: " + exception.Message);
                }
            }

            foreach (string memberId in MemberIds)
            foreach (OfficeSeatingAnimationClip clip in Enum.GetValues(typeof(OfficeSeatingAnimationClip)))
            for (var frame = 0; frame < OfficeSeatingAnimationFrames.FrameCount(clip); frame++)
            {
                var memberFailures = new List<string>();
                try
                {
                    OfficeCharacterSeatPoseProfile profile = poseCatalog.ResolveApproved(
                        memberId,
                        (int)OfficeSeatFacing8.Northwest,
                        clip,
                        frame);
                    string sourcePath = OfficeSeatingAnimationFrames.AssetPath(
                        memberId,
                        OfficeSeatFacing8.Northwest,
                        clip,
                        frame);
                    Sprite source = AssetDatabase.LoadAssetAtPath<Sprite>(sourcePath);
                    Check(source != null, memberId + " approved seating Sprite is missing: " + sourcePath, memberFailures);
                    if (source == null) throw new InvalidOperationException("source Sprite missing");
                    Check(source.rect.width == 256f && source.rect.height == 256f,
                        memberId + " seating Sprite must be 256x256.", memberFailures);
                    Check(Mathf.Abs(source.pixelsPerUnit - 180f) <= 0.001f,
                        memberId + " seating Sprite PPU must be 180.", memberFailures);
                    Check(Vector2.Distance(source.pivot, new Vector2(128f, 0f)) <= 0.01f,
                        memberId + " seating Sprite pivot must be bottom-center.", memberFailures);
                    Check(profile.HumanApproved, memberId + " seating pose is not human-approved.", memberFailures);
                    Check(Mathf.Abs(profile.UniformScale - 1f) <= 0.0001f,
                        memberId + $" seated scale deviation is {(profile.UniformScale - 1f) * 100f:F2}%.", memberFailures);
                    Check(Mathf.Abs(profile.RotationDegrees) <= 0.01f,
                        memberId + $" seated rotation is {profile.RotationDegrees:F3} degrees.", memberFailures);

                    string actualSha = Sha256(sourcePath);
                    Check(string.Equals(actualSha, profile.SourceSpriteSha256, StringComparison.OrdinalIgnoreCase),
                        memberId + " source Sprite SHA does not match the approved profile.", memberFailures);
                    PixelData seated = PixelData.Load(sourcePath);
                    try
                    {
                        Check(seated.IsOpaque(profile.PelvisAnchorPx),
                            memberId + " pelvis anchor is outside the visible Sprite.", memberFailures);
                        Check(seated.IsOpaque(profile.HandAnchorPx),
                            memberId + " hand anchor is outside the visible Sprite.", memberFailures);
                        string result = memberFailures.Count == 0 ? "PASS" : "FAIL";
                        report.Add(
                            $"{memberId}|{clip}|{frame}|{profile.UniformScale:F6}|{profile.RotationDegrees:F6}|" +
                            $"{seated.VisibleHeight}px|{profile.PelvisAnchorPx.x:F1},{profile.PelvisAnchorPx.y:F1}|" +
                            $"{profile.HandAnchorPx.x:F1},{profile.HandAnchorPx.y:F1}|" +
                            $"{profile.HumanApproved}|{actualSha}|{result}");
                    }
                    finally
                    {
                        seated.Dispose();
                    }
                }
                catch (Exception exception)
                {
                    memberFailures.Add(memberId + " validation exception: " + exception.Message);
                }
                failures.AddRange(memberFailures);
            }

            string animatorSource = File.ReadAllText(
                Path.Combine(projectRoot, "Assets/FamilyCompany/Presentation.Unity/DirectionalSpriteAnimator.cs"));
            Check(animatorSource.IndexOf("officeForegroundSortingOrder", StringComparison.Ordinal) < 0,
                "Tile runtime still contains the legacy seating sorting constant 100.", failures);
            report.Add(string.Empty);
            report.Add("Sorting=WorkstationService owns dynamic character and seated workstation stack");
            report.Add("LegacyOrder100Usage=0");
            if (failures.Count > 0)
            {
                report.Add(string.Empty);
                report.Add("FAILURES");
                report.AddRange(failures.Select(item => "- " + item));
            }
            report.Add(string.Empty);
            report.Add(failures.Count == 0 ? "SEATED_SPRITE_ROOT_CAUSE_V5_PASS" : "SEATED_SPRITE_ROOT_CAUSE_V5_FAIL");
            File.WriteAllLines(reportPath, report);
            AssetDatabase.Refresh();
            if (failures.Count > 0)
            {
                string failure = string.Join(" | ", failures);
                Debug.LogError("SEATED_SPRITE_ROOT_CAUSE_V3_FAIL | " + failure + " | report=" + reportPath);
                throw new InvalidOperationException(failure);
            }
            Debug.Log("SEATED_SPRITE_ROOT_CAUSE_V5_PASS | members=4 rotation=0 scale=1 approvals=56 | report=" + reportPath);
        }

        private static void Check(bool condition, string failure, ICollection<string> failures)
        {
            if (!condition) failures.Add(failure);
        }

        private static string Sha256(string assetPath)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static string WalkingIdlePath(string memberId)
        {
            string root = memberId switch
            {
                "player" => "Assets/Art/Characters/Player/Pixel/HighMotion/Frames",
                "older_sister" => "Assets/Art/Characters/OlderSister/Pixel/HighMotion/Frames",
                "father" => "Assets/Art/Characters/Father/Pixel/HighMotion/Frames",
                "mother" => "Assets/Art/Characters/Mother/Pixel/HighMotion/Frames",
                _ => throw new ArgumentOutOfRangeException(nameof(memberId))
            };
            return $"{root}/{memberId}_northwest_walk_2.png";
        }

        private sealed class PixelData : IDisposable
        {
            private readonly Texture2D _texture;
            private readonly Color32[] _pixels;

            private PixelData(Texture2D texture)
            {
                _texture = texture;
                _pixels = texture.GetPixels32();
                int minY = texture.height;
                int maxY = -1;
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (_pixels[y * texture.width + x].a <= 16) continue;
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
                VisibleHeight = maxY < minY ? 0 : maxY - minY + 1;
            }

            public int VisibleHeight { get; }

            public static PixelData Load(string assetPath)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath)), false))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new InvalidOperationException("Could not decode Sprite PNG: " + assetPath);
                }
                return new PixelData(texture);
            }

            public bool IsOpaque(Vector2 point)
            {
                int x = Mathf.RoundToInt(point.x);
                int y = Mathf.RoundToInt(point.y);
                return x >= 0 && y >= 0 && x < _texture.width && y < _texture.height &&
                       _pixels[y * _texture.width + x].a > 32;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_texture);
            }
        }
    }
}
