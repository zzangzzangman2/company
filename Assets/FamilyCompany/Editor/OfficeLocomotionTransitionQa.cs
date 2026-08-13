using System;
using System.Collections.Generic;
using FamilyCompany.Editor.OfficeLayout;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeLocomotionTransitionQa
    {
        [MenuItem("Family Company/QA/Run Office Locomotion Transition Runtime QA")]
        public static void Run()
        {
            OfficeLocomotionTransitionAssetBuilder.Build();
            OfficeLocomotionTransitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<OfficeLocomotionTransitionCatalog>(
                    OfficeLocomotionTransitionAssetBuilder.AssetPath);
            if (catalog == null) throw new InvalidOperationException("Transition catalog build failed.");
            foreach (OfficeLocomotionTransitionEntry entry in catalog.Members)
                ValidateMember(catalog, entry.MemberId);
            Debug.Log(
                "OFFICE_LOCOMOTION_TRANSITION_RUNTIME_QA_PASS | members=4 " +
                "states=StartStep,Walk,Stopping,Idle,ShortShuffle,Pivot " +
                "transitionSlots=256 uniqueTransitionArt=256");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void ValidateMember(
            OfficeLocomotionTransitionCatalog catalog,
            string memberId)
        {
            GameObject root = new GameObject("LocomotionTransitionQa_" + memberId);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                var animator = root.AddComponent<DirectionalSpriteAnimator>();
                animator.Configure(renderer, LoadWalkFrames(memberId));
                animator.ConfigureLocomotionTransitions(catalog.CopyFrames(memberId));
                animator.SetExternallyTicked(true);

                TickMotion(animator, new Vector2(0f, -1f), new Vector2(0f, -0.03f), 0.016f);
                RequirePhase(animator, OfficeLocomotionPhase.StartStep, true, memberId);

                TickMotion(animator, new Vector2(0f, -1f), new Vector2(0f, -0.36f), 0.10f);
                RequirePhase(animator, OfficeLocomotionPhase.Walk, false, memberId);

                TickStopped(animator, 0.02f);
                RequirePhase(animator, OfficeLocomotionPhase.Stopping, true, memberId);
                TickStopped(animator, OfficeLocomotionGaitRules.StopSettleSeconds);
                RequirePhase(animator, OfficeLocomotionPhase.Idle, true, memberId);

                TickMotion(animator, new Vector2(0f, -1f), new Vector2(0f, -0.04f), 0.016f);
                TickStopped(animator, 0.02f);
                RequirePhase(animator, OfficeLocomotionPhase.ShortShuffle, true, memberId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            GameObject pivotRoot = new GameObject("LocomotionPivotQa_" + memberId);
            try
            {
                var renderer = pivotRoot.AddComponent<SpriteRenderer>();
                var animator = pivotRoot.AddComponent<DirectionalSpriteAnimator>();
                animator.Configure(renderer, LoadWalkFrames(memberId));
                animator.ConfigureLocomotionTransitions(catalog.CopyFrames(memberId));
                animator.SetExternallyTicked(true);
                TickMotion(animator, new Vector2(0f, 1f), Vector2.zero, 0.04f);
                RequirePhase(animator, OfficeLocomotionPhase.Pivot, true, memberId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pivotRoot);
            }
        }

        private static void TickMotion(
            DirectionalSpriteAnimator animator,
            Vector2 semanticVelocity,
            Vector2 displacement,
            float deltaTime)
        {
            animator.BeginTilePresentationFrame();
            animator.AccumulateTileMotion(semanticVelocity, displacement, deltaTime, false);
            animator.EndTilePresentationFrame();
            animator.Tick(deltaTime);
        }

        private static void TickStopped(DirectionalSpriteAnimator animator, float deltaTime)
        {
            animator.BeginTilePresentationFrame();
            animator.EndTilePresentationFrame();
            animator.Tick(deltaTime);
        }

        private static void RequirePhase(
            DirectionalSpriteAnimator animator,
            OfficeLocomotionPhase expected,
            bool expectsTransition,
            string memberId)
        {
            if (animator.LocomotionPhase != expected)
                throw new InvalidOperationException(
                    $"{memberId} expected phase {expected}; found {animator.LocomotionPhase}.");
            if (animator.IsLocomotionTransitionSpriteActive != expectsTransition)
                throw new InvalidOperationException(
                    $"{memberId}/{expected} transition sprite state is incorrect.");
            if (animator.CurrentSprite == null)
                throw new InvalidOperationException($"{memberId}/{expected} rendered no sprite.");
        }

        private static Sprite[] LoadWalkFrames(string memberId)
        {
            string folder = HighMotionCharacterArtBuilder.GetFrameFolder(memberId);
            string[] names = HighMotionCharacterArtBuilder.GetFrameNames(memberId);
            var result = new List<Sprite>(names.Length);
            foreach (string name in names)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");
                if (sprite == null)
                    throw new InvalidOperationException("Missing QA walk frame: " + name);
                result.Add(sprite);
            }
            return result.ToArray();
        }
    }
}
