using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Runtime.Character3D
{
    // Diagnostic only. No route injection, teleport, pose edit or fixed capture clock. Capture
    // every rendered frame at its measured delta so a variable-rate recording cannot fake gait.
    [DefaultExecutionOrder(20000)]
    public sealed class Family3DOpeningWalkAudit : MonoBehaviour
    {
        public const string Flag = "-familyCompanyOpeningWalkAudit";
        private const int Width = 1280, Height = 720;
        private static readonly FieldInfo PathField = typeof(OfficeRuntimeAgent).GetField(
            "_path", BindingFlags.Instance | BindingFlags.NonPublic);
        private StarterOfficeRuntimeBootstrap runtime;
        private Camera source, overlay;
        private string directory;
        private float start;
        private bool recording;
        private int frame;
        private readonly List<string> errors = new List<string>();
        private readonly Dictionary<string, GameObject> hosts = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Mesh> baked = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, float> lowest = new Dictionary<string, float>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly StringBuilder csv = new StringBuilder(
            "frame,seconds,dt,member,cellX,cellY,gridX,gridY,rootX,rootY,footMidX,footMidY,footMidErrorPx," +
            "leftGroundX,leftGroundY,rightGroundX,rightGroundY,leftActualX,leftActualY,rightActualX,rightActualY," +
            "leftWorldX,leftWorldY,leftWorldZ,rightWorldX,rightWorldY,rightWorldZ,rootWorldX,rootWorldZ," +
            "yaw,phase,footLead,leftContact,rightContact,gaitDistance,displacement,pathErrorPx,lowestMeshY," +
            "direction,destinationX,destinationY,pathCount,hostCount\n");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains(Flag)) return;
            var host = new GameObject("~Family3DOpeningWalkAudit");
            DontDestroyOnLoad(host);
            host.AddComponent<Family3DOpeningWalkAudit>();
        }

        private void Awake() => Application.logMessageReceived += LogError;
        private void LogError(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors.Add(condition);
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-familyCompanyOpeningWalkArtifacts");
            directory = index >= 0 && index + 1 < args.Length ? args[index + 1] :
                Path.Combine(Application.persistentDataPath, "OpeningWalkAudit");
            Directory.CreateDirectory(Path.Combine(directory, "frames"));
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            AudioListener.volume = 0f;
            var bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null) { Finish(false, "bootstrap missing"); yield break; }
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.SetWorldTimeScaleNow(1f);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                runtime = FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (runtime != null && runtime.IsReady && Family3DProductionPresenter.Instance != null &&
                    Family3DProductionPresenter.Instance.IsBound) break;
                yield return null;
            }
            if (runtime == null || !runtime.IsReady || !Family3DProductionPresenter.Instance.IsBound)
            { Finish(false, "runtime not ready"); yield break; }
            source = Camera.main;
            overlay = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(camera => camera.name == "Family3DProductionOverlayCamera");
            foreach (OfficeRuntimeAgent actor in runtime.Actors)
            {
                hosts.Add(actor.AgentId, GameObject.Find(OfficeFamily3DVisualRoster.ProductionName(actor.AgentId) + "ProductionHost"));
                baked.Add(actor.AgentId, new Mesh());
                lowest.Add(actor.AgentId, 0f);
            }
            var p = runtime.World.Presenter;
            Vector2 origin = Pixel(source, p.CellCenterWorld(new OfficeGridCoordinate(0, 0)));
            Vector2 x = Pixel(source, p.CellCenterWorld(new OfficeGridCoordinate(1, 0))) - origin;
            Vector2 y = Pixel(source, p.CellCenterWorld(new OfficeGridCoordinate(0, 1))) - origin;
            File.WriteAllText(Path.Combine(directory, "projection.csv"),
                "originX,originY,basisXX,basisXY,basisYX,basisYY\n" + string.Join(",", F(origin.x), F(origin.y), F(x.x), F(x.y), F(y.x), F(y.y)));
            runtime.World.Occupancy.ResetMetrics();
            start = Time.realtimeSinceStartup;
            recording = true;
        }

        private void LateUpdate()
        {
            if (!recording) return;
            try
            {
                float elapsed = Time.realtimeSinceStartup - start;
                if (elapsed >= 24f) { Finish(true, "capture complete; visual/tile acceptance is a separate analysis"); return; }
                foreach (OfficeRuntimeAgent actor in runtime.Actors) Sample(actor, elapsed);
                Family3DOpeningShopQa.CaptureCameraStack(Path.Combine(directory, "frames", "frame-" + frame.ToString("D4") + ".tga"));
                frame++;
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private void Sample(OfficeRuntimeAgent actor, float elapsed)
        {
            var host = hosts[actor.AgentId];
            var walk = host.GetComponent<Family3DWalkActor>();
            var pose = walk.ReadPoseSnapshot();
            Vector3 root = ProductionGround(actor.Position);
            Vector3 foot = (pose.leftFootWorld + pose.rightFootWorld) * 0.5f;
            foot.y = 0;
            Vector2 rootPx = Pixel(overlay, root), footPx = Pixel(overlay, foot);
            var leftGround = pose.leftFootWorld; leftGround.y = 0;
            var rightGround = pose.rightFootWorld; rightGround.y = 0;
            Vector2 lg = Pixel(overlay, leftGround), rg = Pixel(overlay, rightGround);
            Vector2 la = Pixel(overlay, pose.leftFootWorld), ra = Pixel(overlay, pose.rightFootWorld);
            var presenter = runtime.World.Presenter;
            Vector2 basisX = presenter.CellBasisXWorld(), basisY = presenter.CellBasisYWorld();
            Vector2 offset = actor.Position - (Vector2)presenter.CellCenterWorld(new OfficeGridCoordinate(0, 0));
            float determinant = basisX.x * basisY.y - basisX.y * basisY.x;
            float gx = (offset.x * basisY.y - offset.y * basisY.x) / determinant;
            float gy = (basisX.x * offset.y - basisX.y * offset.x) / determinant;
            var path = (List<OfficeGridCoordinate>)PathField.GetValue(actor);
            float pathError = -1f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = Pixel(source, presenter.CellCenterWorld(path[i - 1]));
                Vector2 b = Pixel(source, presenter.CellCenterWorld(path[i]));
                float distance = SegmentDistance(rootPx, a, b);
                if (pathError < 0 || distance < pathError) pathError = distance;
            }
            if (frame % 6 == 0)
            {
                var skin = host.GetComponentInChildren<SkinnedMeshRenderer>();
                skin.BakeMesh(baked[actor.AgentId]);
                baked[actor.AgentId].GetVertices(vertices);
                float minimum = float.PositiveInfinity;
                foreach (Vector3 vertex in vertices) minimum = Mathf.Min(minimum, skin.transform.TransformPoint(vertex).y);
                lowest[actor.AgentId] = minimum;
            }
            var destination = actor.ActiveDestinationCell;
            csv.AppendLine(string.Join(",", frame.ToString(), F(elapsed), F(Time.deltaTime), actor.AgentId,
                actor.CurrentCell.X, actor.CurrentCell.Y, F(gx), F(gy), F(rootPx.x), F(rootPx.y), F(footPx.x), F(footPx.y),
                F(Vector2.Distance(rootPx, footPx)), F(lg.x), F(lg.y), F(rg.x), F(rg.y), F(la.x), F(la.y), F(ra.x), F(ra.y),
                F(pose.leftFootWorld.x), F(pose.leftFootWorld.y), F(pose.leftFootWorld.z),
                F(pose.rightFootWorld.x), F(pose.rightFootWorld.y), F(pose.rightFootWorld.z), F(root.x), F(root.z),
                F(host.transform.eulerAngles.y), F(pose.motionPhase01), F(pose.footLead), pose.leftFootPlanted ? "1" : "0",
                pose.rightFootPlanted ? "1" : "0", F(actor.GaitDistance), F(actor.LastActualDisplacement.magnitude), F(pathError),
                F(lowest[actor.AgentId]), actor.CurrentDirection, destination?.X ?? -1, destination?.Y ?? -1, path.Count,
                Family3DProductionPresenter.Instance.BoundCharacterCount));
        }

        private Vector3 ProductionGround(Vector2 point)
        {
            Vector3 viewport = source.WorldToViewportPoint(new Vector3(point.x, point.y, 0));
            Ray ray = overlay.ViewportPointToRay(viewport);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) throw new InvalidOperationException("ground ray missing");
            return ray.GetPoint(distance);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t = d.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0;
            return Vector2.Distance(p, a + d * t);
        }
        private static Vector2 Pixel(Camera camera, Vector3 point)
        {
            Vector3 p = camera.WorldToViewportPoint(point);
            return new Vector2(p.x * Width, (1f - p.y) * Height);
        }
        private static string F(float value) => value.ToString("F6", CultureInfo.InvariantCulture);
        private void Finish(bool captured, string detail)
        {
            recording = false;
            File.WriteAllText(Path.Combine(directory, "walk-trace.csv"), csv.ToString());
            bool success = captured && frame >= 100 && errors.Count == 0;
            var occupancy = runtime?.World?.Occupancy;
            string result = "OPENING_WALK_AUDIT: " + (success ? "CAPTURED" : "FAIL") + "\n" + detail +
                "\nframes=" + frame + " forcedRoutes=0 teleports=0 fixedCaptureClock=false runtimeErrors=" + errors.Count +
                "\nstatic=" + occupancy?.StaticViolationCount + " interaction=" + occupancy?.InteractionViolationCount +
                " agentPenetration=" + occupancy?.AgentPenetrationCount + "\n" + string.Join("\n", errors);
            File.WriteAllText(Path.Combine(directory, "audit-capture.txt"), result);
            Debug.Log(result);
            Application.Quit(success ? 0 : 1);
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= LogError;
            foreach (Mesh mesh in baked.Values) Destroy(mesh);
        }
    }
}
