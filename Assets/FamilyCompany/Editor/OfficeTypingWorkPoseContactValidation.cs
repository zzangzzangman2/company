using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using FamilyCompany.Editor.OfficeGridQa;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Presentation.Unity.OfficeWorkActions;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Proves that every canonical northwest Typing frame remains registered to the
    /// independently approved Work/0 pose. This intentionally reads each source PNG:
    /// merely applying the same catalog coordinates to six Sprite references would be
    /// a tautological contact test and would not detect a shifted or rotated frame.
    /// </summary>
    public static class OfficeTypingWorkPoseContactValidation
    {
        private const int Northwest = (int)OfficeSeatFacing8.Northwest;
        private const int RequiredTypingFrameCount = 6;
        private const int CanvasSizePixels = 256;
        private const float PixelsPerUnit = 180f;
        private const byte OpaqueAlphaThreshold = 32;

        // Only fingertips/key highlights may differ from the approved Work/0 body.
        // The asymmetric vertical allowance covers the authored NW reach toward the
        // keyboard while keeping the pelvis, torso, head and root pixel-identical.
        private const int GestureHalfWidthPixels = 16;
        private const int GestureBelowHandPixels = 16;
        private const int GestureAboveHandPixels = 42;
        private const int ContactCoreRadiusPixels = 4;
        private const int MaximumGestureMaskDeltaPixels = 16;

        private static readonly MemberSpec[] Members =
        {
            new MemberSpec(
                "player",
                "Assets/FamilyCompany/Content/OfficeWorkActions/player_office_work_actions.asset"),
            new MemberSpec(
                "older_sister",
                "Assets/FamilyCompany/Content/OfficeWorkActions/older_sister_office_work_actions.asset"),
            new MemberSpec(
                "father",
                "Assets/FamilyCompany/Content/OfficeWorkActions/father_office_work_actions.asset"),
            new MemberSpec(
                "mother",
                "Assets/FamilyCompany/Content/OfficeWorkActions/mother_office_work_actions.asset")
        };

        [MenuItem("Family Company/Validate Office Typing Work0 Pose Contact")]
        public static void Validate()
        {
            OfficeCharacterSeatPoseCatalog poseCatalog =
                OfficeFurnitureAssetBuilder.LoadCharacterSeatPoseCatalog();
            var totalFrames = 0;
            var maximumGestureMaskDelta = 0;

            for (var memberIndex = 0; memberIndex < Members.Length; memberIndex++)
            {
                MemberSpec member = Members[memberIndex];
                MemberEvidence evidence = ValidateMember(poseCatalog, member);
                totalFrames += evidence.ValidatedFrames;
                maximumGestureMaskDelta = Math.Max(
                    maximumGestureMaskDelta,
                    evidence.MaximumGestureMaskDeltaPixels);
                Debug.Log(
                    $"OFFICE_TYPING_WORK0_POSE_MEMBER_PASS | member={member.MemberId} " +
                    $"typing={evidence.ValidatedFrames}/{RequiredTypingFrameCount} " +
                    $"direction={evidence.DirectionFrames}/{RequiredTypingFrameCount} " +
                    $"import={evidence.ImportFrames}/{RequiredTypingFrameCount} " +
                    $"pelvisOpaque={evidence.PelvisFrames}/{RequiredTypingFrameCount} " +
                    $"handOpaque={evidence.HandFrames}/{RequiredTypingFrameCount} " +
                    $"bodyRegistered={evidence.BodyFrames}/{RequiredTypingFrameCount} " +
                    $"contactCore={evidence.ContactFrames}/{RequiredTypingFrameCount} " +
                    $"uniqueVisuals={evidence.UniqueVisuals}/{RequiredTypingFrameCount} " +
                    $"maxBodyRgbaDelta=0 maxContactAlphaDelta=0 " +
                    $"maxGestureMaskDelta={evidence.MaximumGestureMaskDeltaPixels}");
            }

            Require(totalFrames == Members.Length * RequiredTypingFrameCount,
                $"Validated {totalFrames} typing frames instead of 24.");
            Debug.Log(
                "OFFICE_TYPING_WORK0_POSE_CONTACT_VALIDATION: PASS | " +
                $"members={Members.Length}/4 typing={totalFrames}/24 direction={totalFrames}/24 " +
                $"import={totalFrames}/24 pelvisOpaque={totalFrames}/24 handOpaque={totalFrames}/24 " +
                $"bodyRegistered={totalFrames}/24 contactCore={totalFrames}/24 " +
                $"uniqueVisuals={totalFrames}/24 maxBodyRgbaDelta=0 " +
                $"maxContactAlphaDelta=0 maxGestureMaskDelta={maximumGestureMaskDelta}");
        }

        public static void RunBatch()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("OFFICE_TYPING_WORK0_POSE_CONTACT_VALIDATION: FAIL");
                EditorApplication.Exit(1);
            }
        }

        private static MemberEvidence ValidateMember(
            OfficeCharacterSeatPoseCatalog poseCatalog,
            MemberSpec member)
        {
            var frameSet = AssetDatabase.LoadAssetAtPath<OfficeWorkActionFrameSet>(
                member.FrameSetAssetPath);
            Require(frameSet != null, "Typing frame set is missing: " + member.FrameSetAssetPath);
            Require(string.Equals(frameSet.MemberId, member.MemberId, StringComparison.Ordinal),
                $"Typing frame-set member mismatch: {frameSet.MemberId} != {member.MemberId}.");
            Require(frameSet.TryGetUsableClip(OfficeWorkMicroAction.Typing, out OfficeWorkActionClip clip),
                $"Canonical Typing clip is unavailable for {member.MemberId}.");
            Require(clip.FramesPerDirection == RequiredTypingFrameCount,
                $"{member.MemberId} Typing has {clip.FramesPerDirection} frames per direction instead of 6.");
            Require(clip.TotalFrameCount ==
                    RequiredTypingFrameCount * OfficeSeatingAnimationFrames.DirectionCount,
                $"{member.MemberId} Typing does not contain the complete frame-major 6x8 set.");

            OfficeCharacterSeatPoseProfile workProfile = poseCatalog.ResolveApproved(
                member.MemberId,
                Northwest,
                OfficeSeatingAnimationClip.Work,
                0);
            Require(workProfile.HumanApproved,
                $"{member.MemberId} Work/0 pose is not human-approved.");
            Require(Mathf.Abs(workProfile.UniformScale - 1f) <= 0.0001f,
                $"{member.MemberId} Work/0 pose scale is not 1.");
            Require(Mathf.Abs(workProfile.RotationDegrees) <= 0.01f,
                $"{member.MemberId} Work/0 pose rotation is not zero.");

            string workPath = OfficeSeatingAnimationFrames.AssetPath(
                member.MemberId,
                OfficeSeatFacing8.Northwest,
                OfficeSeatingAnimationClip.Work,
                0);
            Sprite workSprite = RequiredSprite(workPath);
            ValidateSpriteImport(workSprite, workPath);
            Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(workSprite, out int workDirection) &&
                    workDirection == Northwest,
                $"{member.MemberId} Work/0 Sprite is not independently named northwest.");
            Require(string.Equals(FileSha256(workPath), workProfile.SourceSpriteSha256,
                    StringComparison.OrdinalIgnoreCase),
                $"{member.MemberId} Work/0 Sprite differs from its human-approved source SHA-256.");

            using var referencePixels = PixelData.Load(workPath);
            Require(referencePixels.IsOpaque(workProfile.PelvisAnchorPx),
                $"{member.MemberId} Work/0 pelvis anchor is not on opaque source art.");
            Require(referencePixels.IsOpaque(workProfile.HandAnchorPx),
                $"{member.MemberId} Work/0 hand anchor is not on opaque source art.");

            var visualHashes = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var evidence = new MemberEvidence();
            for (var frame = 0; frame < RequiredTypingFrameCount; frame++)
            {
                long elapsedMilliseconds = checked((long)frame * clip.MillisecondsPerFrame);
                Sprite typingSprite = clip.ResolveFrame(Northwest, elapsedMilliseconds);
                Require(typingSprite != null,
                    $"{member.MemberId} Typing/{frame} northwest Sprite is missing.");
                string typingPath = AssetDatabase.GetAssetPath(typingSprite);
                string expectedName = $"{member.MemberId}_typing_{frame:00}_northwest_v1";
                Require(string.Equals(typingSprite.name, expectedName, StringComparison.Ordinal),
                    $"{member.MemberId} Typing/{frame} resolved '{typingSprite.name}', expected '{expectedName}'.");
                Require(typingPath.EndsWith(
                        $"/Frames/Typing/{expectedName}.png",
                        StringComparison.Ordinal),
                    $"{member.MemberId} Typing/{frame} is not the canonical Typing source: {typingPath}.");
                Require(paths.Add(typingPath),
                    $"{member.MemberId} Typing/{frame} repeats a Sprite asset path.");
                Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(
                            typingSprite,
                            out int namedDirection) && namedDirection == Northwest,
                    $"{member.MemberId} Typing/{frame} direction token is not northwest.");
                evidence.DirectionFrames++;

                ValidateSpriteImport(typingSprite, typingPath);
                evidence.ImportFrames++;
                using var typingPixels = PixelData.Load(typingPath);
                Require(typingPixels.IsOpaque(workProfile.PelvisAnchorPx),
                    $"{member.MemberId} Typing/{frame} loses the approved Work/0 pelvis anchor.");
                evidence.PelvisFrames++;
                Require(typingPixels.IsOpaque(workProfile.HandAnchorPx),
                    $"{member.MemberId} Typing/{frame} loses the approved Work/0 hand anchor.");
                evidence.HandFrames++;

                PixelComparison comparison = referencePixels.Compare(
                    typingPixels,
                    workProfile.HandAnchorPx);
                Require(comparison.BodyRgbaDifferenceCount == 0,
                    $"{member.MemberId} Typing/{frame} body/root drifted by " +
                    $"{comparison.BodyRgbaDifferenceCount} RGBA pixels outside the fingertip envelope.");
                evidence.BodyFrames++;
                Require(comparison.ContactCoreAlphaDifferenceCount == 0,
                    $"{member.MemberId} Typing/{frame} changed the approved +/-4px hand contact core by " +
                    $"{comparison.ContactCoreAlphaDifferenceCount} alpha pixels.");
                evidence.ContactFrames++;
                Require(comparison.GestureMaskDifferenceCount <= MaximumGestureMaskDeltaPixels,
                    $"{member.MemberId} Typing/{frame} fingertip silhouette delta " +
                    $"{comparison.GestureMaskDifferenceCount}px exceeds {MaximumGestureMaskDeltaPixels}px.");
                evidence.MaximumGestureMaskDeltaPixels = Math.Max(
                    evidence.MaximumGestureMaskDeltaPixels,
                    comparison.GestureMaskDifferenceCount);
                Require(visualHashes.Add(typingPixels.PixelSha256),
                    $"{member.MemberId} Typing/{frame} duplicates another Typing frame's rendered pixels.");
                evidence.UniqueVisuals++;
                evidence.ValidatedFrames++;
            }

            return evidence;
        }

        private static void ValidateSpriteImport(Sprite sprite, string assetPath)
        {
            Require(Mathf.Abs(sprite.rect.width - CanvasSizePixels) <= 0.01f &&
                    Mathf.Abs(sprite.rect.height - CanvasSizePixels) <= 0.01f,
                $"Sprite must be 256x256: {assetPath}.");
            Require(Vector2.Distance(sprite.pivot, new Vector2(128f, 0f)) <= 0.01f,
                $"Sprite pivot must be (128,0): {assetPath}.");
            Require(Mathf.Abs(sprite.pixelsPerUnit - PixelsPerUnit) <= 0.001f,
                $"Sprite PPU must be 180: {assetPath}.");
        }

        private static Sprite RequiredSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new FileNotFoundException("Sprite is missing.", path);
            return sprite;
        }

        private static string FileSha256(string assetPath)
        {
            using SHA256 algorithm = SHA256.Create();
            using FileStream stream = File.OpenRead(Path.GetFullPath(assetPath));
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class MemberSpec
        {
            public MemberSpec(string memberId, string frameSetAssetPath)
            {
                MemberId = memberId;
                FrameSetAssetPath = frameSetAssetPath;
            }

            public string MemberId { get; }
            public string FrameSetAssetPath { get; }
        }

        private sealed class MemberEvidence
        {
            public int ValidatedFrames;
            public int DirectionFrames;
            public int ImportFrames;
            public int PelvisFrames;
            public int HandFrames;
            public int BodyFrames;
            public int ContactFrames;
            public int UniqueVisuals;
            public int MaximumGestureMaskDeltaPixels;
        }

        private readonly struct PixelComparison
        {
            public PixelComparison(
                int bodyRgbaDifferenceCount,
                int contactCoreAlphaDifferenceCount,
                int gestureMaskDifferenceCount)
            {
                BodyRgbaDifferenceCount = bodyRgbaDifferenceCount;
                ContactCoreAlphaDifferenceCount = contactCoreAlphaDifferenceCount;
                GestureMaskDifferenceCount = gestureMaskDifferenceCount;
            }

            public int BodyRgbaDifferenceCount { get; }
            public int ContactCoreAlphaDifferenceCount { get; }
            public int GestureMaskDifferenceCount { get; }
        }

        private sealed class PixelData : IDisposable
        {
            private readonly Texture2D _texture;
            private readonly Color32[] _pixels;

            private PixelData(Texture2D texture)
            {
                _texture = texture;
                _pixels = texture.GetPixels32();
                PixelSha256 = PixelHash(_pixels);
            }

            public int Width => _texture.width;
            public int Height => _texture.height;
            public string PixelSha256 { get; }

            public static PixelData Load(string assetPath)
            {
                if (!File.Exists(assetPath))
                    throw new FileNotFoundException("Sprite PNG is missing.", assetPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (texture.LoadImage(File.ReadAllBytes(assetPath), false))
                    return new PixelData(texture);
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidDataException("Could not decode Sprite PNG: " + assetPath);
            }

            public bool IsOpaque(Vector2 anchor)
            {
                int x = Mathf.RoundToInt(anchor.x);
                int y = Mathf.RoundToInt(anchor.y);
                return x >= 0 && x < Width && y >= 0 && y < Height &&
                       _pixels[y * Width + x].a > OpaqueAlphaThreshold;
            }

            public PixelComparison Compare(PixelData other, Vector2 handAnchor)
            {
                Require(other != null, "Compared typing pixels are missing.");
                Require(Width == other.Width && Height == other.Height,
                    "Typing Sprite canvas differs from Work/0.");
                int handX = Mathf.RoundToInt(handAnchor.x);
                int handY = Mathf.RoundToInt(handAnchor.y);
                var bodyRgbaDifferences = 0;
                var contactCoreAlphaDifferences = 0;
                var gestureMaskDifferences = 0;
                for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                {
                    int index = y * Width + x;
                    Color32 reference = _pixels[index];
                    Color32 candidate = other._pixels[index];
                    bool sameRgba = reference.r == candidate.r &&
                                    reference.g == candidate.g &&
                                    reference.b == candidate.b &&
                                    reference.a == candidate.a;
                    bool insideGestureEnvelope =
                        Math.Abs(x - handX) <= GestureHalfWidthPixels &&
                        y >= handY - GestureBelowHandPixels &&
                        y <= handY + GestureAboveHandPixels;
                    if (!sameRgba && !insideGestureEnvelope) bodyRgbaDifferences++;

                    bool referenceOpaque = reference.a > OpaqueAlphaThreshold;
                    bool candidateOpaque = candidate.a > OpaqueAlphaThreshold;
                    if (referenceOpaque == candidateOpaque) continue;
                    if (Math.Abs(x - handX) <= ContactCoreRadiusPixels &&
                        Math.Abs(y - handY) <= ContactCoreRadiusPixels)
                    {
                        contactCoreAlphaDifferences++;
                    }
                    if (insideGestureEnvelope) gestureMaskDifferences++;
                }

                return new PixelComparison(
                    bodyRgbaDifferences,
                    contactCoreAlphaDifferences,
                    gestureMaskDifferences);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_texture);
            }

            private static string PixelHash(Color32[] pixels)
            {
                var bytes = new byte[pixels.Length * 4];
                for (var index = 0; index < pixels.Length; index++)
                {
                    int byteIndex = index * 4;
                    bytes[byteIndex] = pixels[index].r;
                    bytes[byteIndex + 1] = pixels[index].g;
                    bytes[byteIndex + 2] = pixels[index].b;
                    bytes[byteIndex + 3] = pixels[index].a;
                }
                using SHA256 algorithm = SHA256.Create();
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }
    }
}
