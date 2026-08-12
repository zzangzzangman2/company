using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeLayout
{
    public static class OfficeCharacterDirectionQa
    {
        public const string ArtifactFolder = "Artifacts/StarterOfficeDirectionQa";
        public const string ContactSheetPath =
            ArtifactFolder + "/office-character-direction-contact-sheet.png";
        private static readonly string[] FamilyIds =
            { "player", "older_sister", "father", "mother" };
        private static readonly string[] DirectionNames =
            { "South", "SouthWest", "West", "NorthWest", "North", "NorthEast", "East", "SouthEast" };
        private static readonly string[] DirectionArrows =
            { "v", "< v", "<", "< ^", "^", "^ >", ">", "v >" };
        private static readonly Vector2[] DirectionVectors =
        {
            new Vector2(0f, -1f), new Vector2(-1f, -1f),
            new Vector2(-1f, 0f), new Vector2(-1f, 1f),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 0f), new Vector2(1f, -1f)
        };

        [MenuItem("Family Company/QA/Build And Validate Office Character Directions")]
        public static void Run()
        {
            ValidateApprovedDirections();
            BuildContactSheet();
            Debug.Log(
                $"OFFICE_CHARACTER_DIRECTION_QA_PASS | math=8/8 directions=32/32 frames=192/192 artifact={ContactSheetPath}");
        }

        public static void ValidateApprovedDirections()
        {
            ValidateDirectionMath();
            HighMotionDirectionManifest manifest = HighMotionDirectionManifestBuilder.LoadRequired();
            RequireFamilyVisualApproval(manifest);
            Debug.Log("OFFICE_CHARACTER_DIRECTION_APPROVAL_VALIDATION_PASS | math=8/8 directions=32/32 frames=192/192");
        }

        [MenuItem("Family Company/QA/Build Office Direction Contact Sheet")]
        public static void BuildContactSheet()
        {
            var root = new GameObject("OfficeCharacterDirectionQaRoot");
            var cameraObject = new GameObject("OfficeCharacterDirectionQaCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(27, 33, 46, 255);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            try
            {
                for (var row = 0; row < FamilyIds.Length; row++)
                for (var direction = 0; direction < DirectionNames.Length; direction++)
                {
                    float x = (direction - 3.5f) * 2.25f;
                    float y = (1.5f - row) * 2.65f;
                    string memberId = FamilyIds[row];
                    string frameName = HighMotionCharacterArtBuilder.GetFrameNames(memberId)[direction];
                    string spritePath = HighMotionCharacterArtBuilder.GetFrameFolder(memberId) +
                                        "/" + frameName + ".png";
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite == null) throw new FileNotFoundException("Direction sprite is missing.", spritePath);

                    var spriteObject = new GameObject(memberId + "_" + DirectionNames[direction]);
                    spriteObject.transform.SetParent(root.transform, false);
                    spriteObject.transform.position = new Vector3(x, y - 0.78f, 0f);
                    SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = 1;
                    CreateText(root.transform, DirectionArrows[direction] + "  " + DirectionNames[direction],
                        new Vector3(x, y + 0.92f, 0f), 0.115f, 10);
                    CreateText(root.transform, memberId, new Vector3(x, y + 0.62f, 0f), 0.09f, 10);
                    CreateText(root.transform, frameName, new Vector3(x, y - 1.12f, 0f), 0.046f, 10);
                }
                Capture(camera, ContactSheetPath, 1920, 1200);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateDirectionMath()
        {
            for (var direction = 0; direction < DirectionVectors.Length; direction++)
            {
                int actual = DirectionalSpriteAnimator.ResolveTileDirection(DirectionVectors[direction]);
                if (actual != direction)
                    throw new InvalidOperationException(
                        $"Tile direction mismatch for {DirectionNames[direction]}: expected {direction}, actual {actual}.");
            }
            int retained = DirectionalSpriteAnimator.ResolveTileDirection(Vector2.zero, 6);
            if (retained != 6)
                throw new InvalidOperationException("A zero displacement must retain the last facing.");
        }

        private static void RequireFamilyVisualApproval(HighMotionDirectionManifest manifest)
        {
            foreach (string memberId in FamilyIds)
            {
                HighMotionDirectionManifest.CharacterDirectionEntry entry = manifest.Characters.Single(item =>
                    string.Equals(item.MemberId, memberId, StringComparison.Ordinal));
                for (var direction = 0; direction < DirectionNames.Length; direction++)
                {
                    if (!entry.VisualApproval[direction])
                        throw new InvalidOperationException(
                            $"Human direction approval is incomplete: {memberId}/{DirectionNames[direction]}.");
                }
                for (var phase = 0; phase < HighMotionDirectionManifest.WalkFrameCount; phase++)
                for (var direction = 0; direction < DirectionNames.Length; direction++)
                {
                    int frameIndex = phase * HighMotionDirectionManifest.DirectionCount + direction;
                    if (!entry.FrameVisualApproval[frameIndex])
                        throw new InvalidOperationException(
                            $"Human frame approval is incomplete: {memberId}/{DirectionNames[direction]}/phase-{phase}.");
                }
            }
        }

        private static void CreateText(
            Transform parent,
            string value,
            Vector3 position,
            float characterSize,
            int sortingOrder)
        {
            var textObject = new GameObject("Label_" + value);
            textObject.transform.SetParent(parent, false);
            textObject.transform.position = position;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = characterSize;
            text.fontSize = 42;
            text.color = new Color32(239, 244, 255, 255);
            textObject.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
        }

        private static void Capture(Camera camera, string path, int width, int height)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, path));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? projectRoot);
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                output.Apply(false, false);
                File.WriteAllBytes(absolutePath, output.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(output);
            }
        }
    }

    public sealed class OfficeCharacterDirectionQaWindow : EditorWindow
    {
        private HighMotionDirectionManifest _manifest;
        private int _characterIndex;
        private int _phaseIndex;
        private Vector2 _scroll;

        [MenuItem("Family Company/Office/Character Direction Visual Approval")]
        public static void Open()
        {
            GetWindow<OfficeCharacterDirectionQaWindow>("Direction Approval").Show();
        }

        private void OnEnable()
        {
            _manifest = AssetDatabase.LoadAssetAtPath<HighMotionDirectionManifest>(
                HighMotionDirectionManifestBuilder.AssetPath);
        }

        private void OnGUI()
        {
            _manifest = (HighMotionDirectionManifest)EditorGUILayout.ObjectField(
                "Manifest", _manifest, typeof(HighMotionDirectionManifest), false);
            if (_manifest == null) return;
            string[] ids = _manifest.Characters.Select(item => item.MemberId).ToArray();
            _characterIndex = EditorGUILayout.Popup("Character", _characterIndex, ids);
            _phaseIndex = EditorGUILayout.IntSlider(
                "Walk phase",
                _phaseIndex,
                0,
                HighMotionDirectionManifest.WalkFrameCount - 1);
            HighMotionDirectionManifest.CharacterDirectionEntry entry =
                _manifest.Characters[_characterIndex];
            EditorGUILayout.HelpBox(
                "Check the visible body direction, not the filename. Approval is stored in the import manifest.",
                MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var direction = 0; direction < HighMotionDirectionManifest.DirectionCount; direction++)
            {
                int frameIndex = _phaseIndex * HighMotionDirectionManifest.DirectionCount + direction;
                string frameName = HighMotionCharacterArtBuilder.GetFrameNames(entry.MemberId)[frameIndex];
                string path = HighMotionCharacterArtBuilder.GetFrameFolder(entry.MemberId) +
                              "/" + frameName + ".png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(AssetPreview.GetAssetPreview(sprite) ?? sprite.texture, GUILayout.Width(96f), GUILayout.Height(96f));
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(direction + " — " + frameName, EditorStyles.boldLabel);
                bool approved = EditorGUILayout.Toggle("Human visual approval", entry.VisualApproval[direction]);
                if (approved != entry.VisualApproval[direction])
                {
                    Undo.RecordObject(_manifest, "Approve character direction");
                    _manifest.SetVisualApproval(entry.MemberId, direction, approved);
                    EditorUtility.SetDirty(_manifest);
                }
                bool frameApproved = EditorGUILayout.Toggle(
                    "Phase frame approval",
                    entry.FrameVisualApproval[frameIndex]);
                if (frameApproved != entry.FrameVisualApproval[frameIndex])
                {
                    Undo.RecordObject(_manifest, "Approve character walk frame");
                    _manifest.SetFrameVisualApproval(entry.MemberId, direction, _phaseIndex, frameApproved);
                    EditorUtility.SetDirty(_manifest);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Build 4x8 Contact Sheet")) OfficeCharacterDirectionQa.BuildContactSheet();
            if (GUILayout.Button("Save approvals")) AssetDatabase.SaveAssets();
        }
    }
}
