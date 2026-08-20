using System;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Tombstone for the rejected 3D protagonist experiment.
    /// The entry points remain so old commands fail clearly without writing candidate or production files.
    /// </summary>
    public static class PlayerWalkHumanoidPromotion
    {
        public const string CandidateRoot =
            "Assets/Resources/FamilyCompany/PlayerBakedWalkHumanoidV2Candidate";
        public const string ProductionRoot =
            "Assets/Resources/FamilyCompany/PlayerBakedWalkV2";

        private const string DisabledMessage =
            "PLAYER_WALK_HUMANOID_PIPELINE_DISABLED: The rejected 3D protagonist pipeline " +
            "cannot bake, promote, build, or run. Continue the 2D east six-pose workflow in " +
            "Docs/HOME_PC_WALK_CHECKPOINT_2026-08-20.md.";

        [MenuItem("Family Company/Art/Rejected Research/Humanoid Walk Promotion (Disabled)")]
        public static void Run()
        {
            throw new InvalidOperationException(DisabledMessage);
        }

        public static void RunFromCommandLine()
        {
            ExitDisabled();
        }

        public static void RunFullPipelineFromCommandLine()
        {
            ExitDisabled();
        }

        private static void ExitDisabled()
        {
            Debug.LogError(DisabledMessage);
            EditorApplication.Exit(1);
        }
    }
}
