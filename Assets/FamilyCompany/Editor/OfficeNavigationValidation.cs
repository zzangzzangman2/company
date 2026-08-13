using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeNavigationValidation
    {
        [MenuItem("Family Company/Validate Office Navigation")]
        public static void Run()
        {
            var report = OfficeNavigationRegressionSuite.Run(128);
            var sceneReport = ValidateCanonicalScene();
            Debug.Log(
                "OFFICE_NAVIGATION_VALIDATION: PASS | " +
                $"seeds={report.Seeds} | paths={report.Paths} | replans={report.Replans} | " +
                $"segments={report.SegmentChecks} | oracleSegments={report.OracleSegmentChecks} | " +
                $"counterexamples={report.CounterexampleChecks} | facingPresentation={report.FacingPresentationChecks} | " +
                $"gaitPresentation={report.GaitPresentationChecks} | " +
                $"collisionSlides={report.CollisionSlideChecks} | motionPartitions={report.MotionPartitionChecks} | " +
                $"trafficPermutations={report.TrafficPermutationChecks} | maxStretch={report.MaximumStretch:F3} | " +
                $"maxExpanded={report.MaximumExpandedNodes}/{FamilyCompany.Simulation.Navigation.OfficeNavigationLimits.MaxExpandedNodes} | " +
                $"deadlockTicks={report.DeadlockTicks} | {sceneReport}");
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

        private static string ValidateCanonicalScene()
        {
            const string scenePath = "Assets/FamilyCompany/Scenes/Prototype01.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var office = GameObject.Find("FAMILY OFFICE V0.2");
            if (office == null) throw new InvalidOperationException("Canonical office root is missing.");
            var runtimeObject = new GameObject("Office Navigation Validation (Transient)");
            runtimeObject.transform.SetParent(office.transform, false);
            var world = runtimeObject.AddComponent<Presentation.Unity.OfficeNavigationWorld>();
            try
            {
                world.ConfigureRuntime(office.transform);
                if (!world.IsReady)
                    throw new InvalidOperationException(
                        "Office navigation world did not build: " + world.FailureReason);
                if (world.ObstacleCount < 19)
                    throw new InvalidOperationException(
                        $"Expected collider and renderer footprints, found only {world.ObstacleCount} obstacles.");
                var agents = UnityEngine.Object.FindObjectsByType<Presentation.Unity.OfficeWorkerAgent>(FindObjectsSortMode.None)
                    .OrderBy(item => item.AgentId, StringComparer.Ordinal)
                    .ToArray();
                var destinations = UnityEngine.Object.FindObjectsByType<Presentation.Unity.OfficeWaypoint>(FindObjectsSortMode.None)
                    .Where(item => item.Activity != Presentation.Unity.OfficeActivity.Walking)
                    .OrderBy(item => item.WaypointId, StringComparer.Ordinal)
                    .ToArray();
                if (agents.Length < 3 || destinations.Length < 6)
                    throw new InvalidOperationException(
                        $"Incomplete navigation scene: agents={agents.Length}, destinations={destinations.Length}.");
                var pathCount = 0;
                var maximumExpanded = 0;
                foreach (var agent in agents)
                {
                    var controller = agent.GetComponent<CharacterController>();
                    var radius = controller == null ? 0.30f : controller.radius;
                    foreach (var destination in destinations)
                    {
                        if (!world.TryFindPath(
                                agent.transform.position,
                                destination.transform.position,
                                radius,
                                out var path))
                            throw new InvalidOperationException(
                                $"No canonical path: {agent.AgentId}->{destination.WaypointId}.");
                        if (!world.IsPathCollisionFree(path, radius))
                            throw new InvalidOperationException(
                                $"Canonical path overlaps inflated footprint: {agent.AgentId}->{destination.WaypointId}.");
                        if (path.StartProjected || path.GoalProjected)
                            throw new InvalidOperationException(
                                $"Canonical semantic path projected an endpoint: {agent.AgentId}->{destination.WaypointId}.");
                        maximumExpanded = Math.Max(maximumExpanded, path.ExpandedNodes);
                        pathCount++;
                    }
                }

                return $"sceneObstacles={world.ObstacleCount} | scenePaths={pathCount} | sceneMaxExpanded={maximumExpanded}";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeObject);
            }
        }
    }
}
