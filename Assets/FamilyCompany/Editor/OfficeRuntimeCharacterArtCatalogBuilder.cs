using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeRuntimeCharacterArtCatalogBuilder
    {
        public const string AssetPath =
            "Assets/FamilyCompany/Content/Resources/HighMotion/OfficeRuntimeCharacterArtCatalog.asset";
        private static readonly string[] Directions =
            { "south", "southwest", "west", "northwest", "north", "northeast", "east", "southeast" };
        private static readonly CharacterSource[] Sources =
        {
            new CharacterSource("kim_seoa", "Employees/KimSeoa"),
            new CharacterSource("lee_jian", "Employees/LeeJian"),
            new CharacterSource("choi_iseo", "Employees/ChoiIseo"),
            new CharacterSource("jung_arin", "Employees/JungArin"),
            new CharacterSource("park_haeun", "Employees/ParkHaeun"),
            new CharacterSource("han_sua", "Employees/HanSua"),
            new CharacterSource("oh_jiwoo", "Employees/OhJiwoo"),
            new CharacterSource("yoon_chaea", "Employees/YoonChaea")
        };

        [MenuItem("Family Company/Art/Build Runtime Character Art Catalog")]
        public static void Build()
        {
            var entries = new List<OfficeRuntimeCharacterArtEntry>();
            foreach (CharacterSource source in Sources)
            {
                var frames = new Sprite[DirectionalSpriteAnimator.RequiredFrameCount];
                int index = 0;
                for (int phase = 0; phase < DirectionalSpriteAnimator.WalkFrameCount; phase++)
                foreach (string direction in Directions)
                {
                    string path =
                        $"Assets/Art/Characters/{source.Directory}/Pixel/HighMotion/Frames/" +
                        $"{source.MemberId}_{direction}_walk_{phase}.png";
                    frames[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (frames[index] == null) throw new InvalidOperationException("Missing employee frame: " + path);
                    index++;
                }
                var entry = new OfficeRuntimeCharacterArtEntry();
                entry.Configure(source.MemberId, frames);
                entries.Add(entry);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<OfficeRuntimeCharacterArtCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficeRuntimeCharacterArtCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }
            catalog.Configure(entries.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("OFFICE_RUNTIME_CHARACTER_ART_CATALOG: BUILT | employees=" + entries.Count);
        }

        private readonly struct CharacterSource
        {
            public CharacterSource(string memberId, string directory)
            {
                MemberId = memberId;
                Directory = directory;
            }
            public string MemberId { get; }
            public string Directory { get; }
        }
    }
}
