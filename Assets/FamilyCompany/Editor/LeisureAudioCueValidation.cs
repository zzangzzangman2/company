using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Simulation.Leisure;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class LeisureAudioCueValidation
    {
        private const int ExpectedActivityCount = 12;
        private const int ExpectedCueCount = ExpectedActivityCount * 3;
        private const string BgmAssetRoot = "Assets/Audio/Resources/Audio/BGM/";
        private const string SfxAssetRoot = "Assets/Audio/Resources/Audio/SFX/";

        [MenuItem("Family Company/Validate Leisure Audio Cues")]
        public static void Run()
        {
            try
            {
                ValidateCoverageAndIds();
                ValidateCueSemanticsAndResources();
                ValidatePaceProfiles();
                Debug.Log("FAMILY_COMPANY_LEISURE_AUDIO_CUE_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_LEISURE_AUDIO_CUE_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateCoverageAndIds()
        {
            var activities = LeisureActivityCatalog.All;
            var audioDefinitions = LeisureAudioCueCatalog.All;

            AssertEqual(ExpectedActivityCount, activities.Count, "canonical leisure activity count");
            AssertEqual(ExpectedActivityCount, audioDefinitions.Count, "audio definition count");
            AssertEqual(
                audioDefinitions.Count,
                audioDefinitions.Select(item => item.ActivityId).Distinct(StringComparer.Ordinal).Count(),
                "unique audio activity IDs");
            AssertEqual(
                audioDefinitions.Count,
                audioDefinitions.Select(item => item.SceneId).Distinct(StringComparer.Ordinal).Count(),
                "unique ImageGen scene IDs");

            var canonicalIds = new HashSet<string>(activities.Select(item => item.Id), StringComparer.Ordinal);
            var audioIds = new HashSet<string>(audioDefinitions.Select(item => item.ActivityId), StringComparer.Ordinal);
            AssertTrue(canonicalIds.SetEquals(audioIds), "audio IDs exactly cover the canonical 12 activities");

            foreach (var definition in audioDefinitions)
            {
                AssertEqual(definition.ActivityId, definition.SceneId, definition.ActivityId + " one-to-one scene ID");
                AssertTrue(
                    ReferenceEquals(definition, LeisureAudioCueCatalog.FindByActivityId(definition.ActivityId)),
                    definition.ActivityId + " lookup returns canonical definition");
            }

            var cues = audioDefinitions.SelectMany(item => item.Cues).ToArray();
            AssertEqual(ExpectedCueCount, cues.Length, "three cues per activity");
            AssertEqual(
                cues.Length,
                cues.Select(item => item.CueId).Distinct(StringComparer.Ordinal).Count(),
                "unique semantic cue IDs");
        }

        private static void ValidateCueSemanticsAndResources()
        {
            foreach (var definition in LeisureAudioCueCatalog.All)
            {
                AssertCueRole(definition.EnterCue, LeisureAudioCueRole.Enter, definition.ActivityId);
                AssertCueRole(definition.LoopCue, LeisureAudioCueRole.Loop, definition.ActivityId);
                AssertCueRole(definition.CompleteCue, LeisureAudioCueRole.Complete, definition.ActivityId);

                foreach (var cue in definition.Cues)
                {
                    AssertTrue(cue.VolumeScale > 0f && cue.VolumeScale <= 1f, cue.CueId + " volume boundary");
                    AssertTrue(
                        cue.TransitionFadeSeconds >= 0f && cue.TransitionFadeSeconds <= 5f,
                        cue.CueId + " fade boundary");
                    AssertTrue(
                        cue.ClipId.IndexOfAny(new[] { '/', '\\', '.' }) < 0,
                        cue.CueId + " coordinator-compatible file ID");

                    if (cue.Role == LeisureAudioCueRole.Loop)
                    {
                        AssertEqual(LeisureAudioChannel.Bgm, cue.Channel, cue.CueId + " loop channel");
                        AssertTrue(cue.Repeats, cue.CueId + " repeats");
                        AssertTrue(cue.TransitionFadeSeconds > 0f, cue.CueId + " uses a cross-fade");
                    }
                    else
                    {
                        AssertEqual(LeisureAudioChannel.Sfx, cue.Channel, cue.CueId + " one-shot channel");
                        AssertTrue(!cue.Repeats, cue.CueId + " does not repeat");
                        AssertEqual(0f, cue.TransitionFadeSeconds, cue.CueId + " one-shot fade");
                    }

                    var assetPath = ResourceAssetPath(cue);
                    var absolutePath = Path.GetFullPath(assetPath);
                    AssertTrue(File.Exists(absolutePath), cue.CueId + " resource file exists at " + assetPath);
                    AssertTrue(
                        AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath) != null,
                        cue.CueId + " imports as AudioClip at " + assetPath);
                }
            }
        }

        private static void ValidatePaceProfiles()
        {
            var quietCount = 0;
            var activeCount = 0;
            foreach (var definition in LeisureAudioCueCatalog.All)
            {
                if (definition.Pace == LeisureAudioPace.Quiet)
                {
                    quietCount++;
                    AssertTrue(definition.LoopCue.VolumeScale <= 0.35f, definition.ActivityId + " quiet loop volume");
                    AssertTrue(
                        definition.LoopCue.TransitionFadeSeconds >= 1.20f,
                        definition.ActivityId + " quiet fade duration");
                }
                else if (definition.Pace == LeisureAudioPace.Active)
                {
                    activeCount++;
                    AssertTrue(definition.LoopCue.VolumeScale >= 0.40f, definition.ActivityId + " active loop volume");
                    AssertTrue(
                        definition.LoopCue.TransitionFadeSeconds <= 0.90f,
                        definition.ActivityId + " active fade duration");
                }
                else
                {
                    throw new InvalidOperationException(definition.ActivityId + ": unknown pace profile.");
                }
            }

            AssertTrue(quietCount > 0, "quiet activity profile exists");
            AssertTrue(activeCount > 0, "active activity profile exists");
        }

        private static string ResourceAssetPath(LeisureAudioCueDefinition cue)
        {
            var root = cue.Channel == LeisureAudioChannel.Bgm ? BgmAssetRoot : SfxAssetRoot;
            return root + cue.ClipId + ".ogg";
        }

        private static void AssertCueRole(
            LeisureAudioCueDefinition cue,
            LeisureAudioCueRole expectedRole,
            string activityId)
        {
            AssertEqual(expectedRole, cue.Role, activityId + " " + expectedRole + " role");
            AssertTrue(
                cue.CueId.StartsWith(activityId + ":", StringComparison.Ordinal),
                cue.CueId + " is namespaced by activity ID");
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label + ": expected true.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual + ".");
        }
    }
}
