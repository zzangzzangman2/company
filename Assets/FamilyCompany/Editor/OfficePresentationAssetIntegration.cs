using System;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeWorkActions;
using FamilyCompany.Simulation.OfficeWorkActions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficePresentationAssetIntegration
    {
        public const string FrameSetRoot = "Assets/FamilyCompany/Content/OfficeWorkActions";
        public const string TextMeshProSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string TextMeshProEssentialsPackagePath =
            "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        private static readonly string[] Directions =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        private static readonly MemberDefinition[] Members =
        {
            new MemberDefinition("player", "Assets/Art/Characters/Player/Pixel/OfficeWorkActionsV1"),
            new MemberDefinition("older_sister", "Assets/Art/Characters/Family/OlderSister/Pixel/OfficeWorkActionsV1"),
            new MemberDefinition("father", "Assets/Art/Characters/Family/Father/Pixel/OfficeWorkActionsV1"),
            new MemberDefinition("mother", "Assets/Art/Characters/Family/Mother/Pixel/OfficeWorkActionsV1")
        };

        private static readonly ActionDefinition[] Actions =
        {
            new ActionDefinition(OfficeWorkMicroAction.Typing, "Typing", "typing", 6, 95),
            new ActionDefinition(OfficeWorkMicroAction.Mouse, "Mouse", "mouse", 6, 120),
            new ActionDefinition(OfficeWorkMicroAction.Drink, "Drink", "drink", 8, 140)
        };

        [MenuItem("Family Company/Art/Integrate Office Work Actions V1")]
        public static void BuildMenu()
        {
            EnsureFrameSets();
            Debug.Log("FAMILY_COMPANY_OFFICE_WORK_ACTION_FRAME_SETS_V1: BUILT");
        }

        public static void EnsureFrameSets()
        {
            EnsureTextMeshProSettings();
            EnsureFolder(FrameSetRoot);
            foreach (var member in Members) BuildFrameSet(member);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureTextMeshProSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TextMeshProSettingsPath);
            if (settings != null && Shader.Find("TextMeshPro/Mobile/Distance Field") != null) return;

            var packagePath = Path.GetFullPath(TextMeshProEssentialsPackagePath);
            if (!File.Exists(packagePath))
                throw new FileNotFoundException("TMP Essential Resources package is missing.", packagePath);
            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();
            settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TextMeshProSettingsPath);
            if (settings == null || Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
                throw new InvalidOperationException("TMP Essential Resources did not import correctly.");
        }

        private static void BuildFrameSet(MemberDefinition member)
        {
            var clips = new OfficeWorkActionClip[Actions.Length];
            for (var actionIndex = 0; actionIndex < Actions.Length; actionIndex++)
            {
                var action = Actions[actionIndex];
                var sprites = new Sprite[action.FrameCount * Directions.Length];
                var spriteIndex = 0;
                for (var frame = 0; frame < action.FrameCount; frame++)
                {
                    foreach (var direction in Directions)
                    {
                        var path =
                            $"{member.Root}/Frames/{action.Directory}/" +
                            $"{member.Id}_{action.FileStem}_{frame:00}_{direction}_v1.png";
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (sprite == null)
                            throw new InvalidOperationException("Missing office work action frame: " + path);
                        sprites[spriteIndex++] = sprite;
                    }
                }

                var clip = new OfficeWorkActionClip();
                clip.Configure(action.Kind, sprites, action.MillisecondsPerFrame, true);
                if (!clip.IsUsable)
                    throw new InvalidOperationException($"Unusable office work action clip: {member.Id}/{action.Kind}");
                clips[actionIndex] = clip;
            }

            var assetPath = $"{FrameSetRoot}/{member.Id}_office_work_actions.asset";
            var frameSet = AssetDatabase.LoadAssetAtPath<OfficeWorkActionFrameSet>(assetPath);
            if (frameSet == null)
            {
                frameSet = ScriptableObject.CreateInstance<OfficeWorkActionFrameSet>();
                AssetDatabase.CreateAsset(frameSet, assetPath);
            }

            frameSet.Configure(member.Id, clips);
            EditorUtility.SetDirty(frameSet);
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private readonly struct MemberDefinition
        {
            public MemberDefinition(string id, string root)
            {
                Id = id;
                Root = root;
            }

            public string Id { get; }
            public string Root { get; }
        }

        private readonly struct ActionDefinition
        {
            public ActionDefinition(
                OfficeWorkMicroAction kind,
                string directory,
                string fileStem,
                int frameCount,
                int millisecondsPerFrame)
            {
                Kind = kind;
                Directory = directory;
                FileStem = fileStem;
                FrameCount = frameCount;
                MillisecondsPerFrame = millisecondsPerFrame;
            }

            public OfficeWorkMicroAction Kind { get; }
            public string Directory { get; }
            public string FileStem { get; }
            public int FrameCount { get; }
            public int MillisecondsPerFrame { get; }
        }
    }
}
