using System;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeLayout
{
    public static class HighMotionDirectionManifestBuilder
    {
        public const string AssetPath =
            "Assets/FamilyCompany/Content/Resources/HighMotion/HighMotionDirectionManifest.asset";
        public static readonly string[] CharacterIds =
        {
            "player", "older_sister", "father", "mother",
            "kim_seoa", "lee_jian", "choi_iseo", "jung_arin",
            "park_haeun", "han_sua", "oh_jiwoo", "yoon_chaea"
        };

        [MenuItem("Family Company/Art/Rebuild High Motion Direction Manifest")]
        public static void RebuildIdentity()
        {
            HighMotionDirectionManifest manifest =
                AssetDatabase.LoadAssetAtPath<HighMotionDirectionManifest>(AssetPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<HighMotionDirectionManifest>();
                AssetDatabase.CreateAsset(manifest, AssetPath);
            }
            Undo.RecordObject(manifest, "Rebuild direction manifest");
            manifest.ConfigureIdentity(CharacterIds);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"HIGH_MOTION_DIRECTION_MANIFEST_BUILD_PASS | characters={manifest.Characters.Count}");
        }

        public static HighMotionDirectionManifest LoadRequired()
        {
            HighMotionDirectionManifest result =
                AssetDatabase.LoadAssetAtPath<HighMotionDirectionManifest>(AssetPath);
            if (result == null) throw new InvalidOperationException("HighMotionDirectionManifest.asset is missing.");
            result.Validate();
            return result;
        }
    }
}
