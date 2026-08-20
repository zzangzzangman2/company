using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PlayerBakedWalkV2CatalogBuilder
    {
        private const string Root = "Assets/Resources/FamilyCompany/PlayerBakedWalkV2";
        private const string CatalogPath = Root + "/PlayerBakedWalkCatalogV2.asset";
        private const string StaticQaReceiptPath = Root + "/PlayerBakedWalkStaticQaV2.json";

        [Serializable]
        private sealed class StaticQaReceipt
        {
            public string contract = "FC-PLAYER-BAKED-WALK-V2-STATIC-QA";
            public int directions = PlayerBakedWalkCatalogV2.DirectionCount;
            public int poses = PlayerBakedWalkCatalogV2.DirectionCount * PlayerBakedWalkCatalogV2.PoseCount;
            public int waistGapViolations;
            public int detachedAlphaViolations;
            public string validationProfile = PlayerBakedWalkV2Validation.PaperDollValidationProfile;
            public float directionMedianHeightDeltaPercent;
            public string catalogSourceReceiptSha256 = string.Empty;
        }

        [MenuItem("Family Company/Art/Build Player Baked Walk Catalog V2")]
        public static void Run()
        {
            var rows = new PlayerBakedWalkDirectionV2[PlayerBakedWalkCatalogV2.DirectionCount];
            string[] receiptPaths = new string[PlayerBakedWalkCatalogV2.DirectionCount];
            string declaredValidationProfile = string.Empty;
            for (var direction = 0; direction < rows.Length; direction++)
            {
                string name = PlayerBakedWalkCatalogV2.DirectionNames[direction];
                string receiptPath = Root + "/source-receipt-" + name + ".json";
                receiptPaths[direction] = receiptPath;
                if (!File.Exists(receiptPath))
                    throw new FileNotFoundException(
                        "All eight approved direction receipts are required before catalog promotion.",
                        receiptPath);
                PlayerBakedWalkV2BakeReceipt receipt =
                    JsonUtility.FromJson<PlayerBakedWalkV2BakeReceipt>(File.ReadAllText(receiptPath));
                string profile = string.IsNullOrWhiteSpace(receipt.validationProfile)
                    ? PlayerBakedWalkV2Validation.PaperDollValidationProfile
                    : receipt.validationProfile;
                if (string.IsNullOrEmpty(declaredValidationProfile)) declaredValidationProfile = profile;
                if (!string.Equals(declaredValidationProfile, profile, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Player baked walk catalog cannot mix validation profiles.");
                PlayerBakedWalkV2Validation.ValidateReceiptUsingDeclaredProfile(receipt, direction);
                PlayerBakedWalkV2BakePoseReceipt[] poses = receipt.poses.OrderBy(value => value.pose).ToArray();
                var sprites = new Sprite[PlayerBakedWalkCatalogV2.PoseCount];
                var support = new PlayerWalkSupportLegV2[PlayerBakedWalkCatalogV2.PoseCount];
                var left = new Vector2[PlayerBakedWalkCatalogV2.PoseCount];
                var right = new Vector2[PlayerBakedWalkCatalogV2.PoseCount];
                var pelvis = new Vector2[PlayerBakedWalkCatalogV2.PoseCount];
                for (var pose = 0; pose < poses.Length; pose++)
                {
                    sprites[pose] = AssetDatabase.LoadAssetAtPath<Sprite>(poses[pose].spritePath);
                    support[pose] = pose < 4 ? PlayerWalkSupportLegV2.Left : PlayerWalkSupportLegV2.Right;
                    left[pose] = poses[pose].leftFootAnchorPx;
                    right[pose] = poses[pose].rightFootAnchorPx;
                    pelvis[pose] = poses[pose].pelvisAnchorPx;
                }
                rows[direction] = new PlayerBakedWalkDirectionV2();
                rows[direction].Configure(
                    direction,
                    name,
                    new Vector2Int(receipt.canvasWidth, receipt.canvasHeight),
                    receipt.pixelsPerUnit,
                    sprites,
                    support,
                    left,
                    right,
                    pelvis);
            }

            string aggregateSha = AggregateSha(receiptPaths);
            PlayerBakedWalkCatalogV2 catalog = AssetDatabase.LoadAssetAtPath<PlayerBakedWalkCatalogV2>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PlayerBakedWalkCatalogV2>();
                catalog.Configure(rows, aggregateSha, OfficeLocomotionGaitRules.DefaultStrideLength);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            else
            {
                catalog.Configure(rows, aggregateSha, OfficeLocomotionGaitRules.DefaultStrideLength);
                EditorUtility.SetDirty(catalog);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
            PlayerBakedWalkV2Validation.ValidateCatalog(catalog);
            float directionMedianHeightDelta = string.Equals(
                declaredValidationProfile,
                PlayerBakedWalkV2Validation.HumanoidValidationProfile,
                StringComparison.Ordinal)
                ? PlayerBakedWalkV2Validation.ValidateHumanoidDirectionSet(Root)
                : 0f;
            var staticQa = new StaticQaReceipt
            {
                validationProfile = declaredValidationProfile,
                directionMedianHeightDeltaPercent = directionMedianHeightDelta,
                catalogSourceReceiptSha256 = aggregateSha
            };
            File.WriteAllText(StaticQaReceiptPath, JsonUtility.ToJson(staticQa, true));
            AssetDatabase.ImportAsset(StaticQaReceiptPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("PLAYER_BAKED_WALK_V2_CATALOG_BUILD: PASS | directions=8 poses=64");
        }

        private static string AggregateSha(string[] paths)
        {
            using SHA256 sha = SHA256.Create();
            byte[] payload = paths
                .OrderBy(path => path, StringComparer.Ordinal)
                .SelectMany(File.ReadAllBytes)
                .ToArray();
            return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", string.Empty);
        }
    }
}
