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
    /// <summary>
    /// Opt-in hidden D3D11 Player gate. Observe normal coordinator-driven movement first; then
    /// exercise the real build controller's preview/confirmation without injecting native input.
    /// This is NOT a native-pointer acceptance test or approval of final Mother/Sister assets.
    /// </summary>
    public sealed class Family3DOpeningShopQa : MonoBehaviour
    {
        public const string Flag = "-familyCompanyOpeningShopQa";
        public const string ArtifactsArgument = "-familyCompanyOpeningShopArtifacts";
        private string directory;
        private readonly StringBuilder receipt = new StringBuilder();
        private readonly List<string> runtimeErrors = new List<string>();

        private void Awake() => Application.logMessageReceived += RecordRuntimeError;
        private void OnDestroy() => Application.logMessageReceived -= RecordRuntimeError;
        private void RecordRuntimeError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                runtimeErrors.Add(condition);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains(Flag)) return;
            var host = new GameObject("~Family3DOpeningShopQa");
            DontDestroyOnLoad(host);
            host.AddComponent<Family3DOpeningShopQa>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            AudioListener.volume = 0f;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, ArtifactsArgument);
            directory = index >= 0 && index + 1 < args.Length ? args[index + 1] :
                Path.Combine(Application.persistentDataPath, "Family3DOpeningShopQa");
            Directory.CreateDirectory(directory);
            StartCoroutine(RunGuarded());
        }

        private IEnumerator RunGuarded()
        {
            IEnumerator run = Run();
            while (true)
            {
                object next;
                try
                {
                    if (!run.MoveNext()) yield break;
                    next = run.Current;
                }
                catch (Exception exception)
                {
                    Finish(false, exception.ToString());
                    yield break;
                }
                yield return next;
            }
        }

        private IEnumerator Run()
        {
            var bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            Require(bootstrap != null, "bootstrap missing");
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.SetWorldTimeScaleNow(1f);
            float deadline = Time.realtimeSinceStartup + 30f;
            StarterOfficeRuntimeBootstrap runtime;
            do
            {
                runtime = FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (runtime != null && runtime.IsReady && runtime.Actors.Count == 4 &&
                    Family3DProductionPresenter.Instance != null && Family3DProductionPresenter.Instance.IsBound)
                    break;
                Require(Time.realtimeSinceStartup < deadline, "four 3D actors did not bind");
                yield return null;
            } while (true);

            var state = bootstrap.State;
            Require(state.Company.CashWon == 5_000_000 && state.OfficeGrid.SeatSlots.Count == 0 &&
                state.OfficeFurnitureInventory.Instances.Count == 0, "fresh game must be unfurnished with five million won");
            Require(!state.OfficeGrid.Furniture.Any(item =>
                OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable == true), "free editable furniture present");
            AssertFourBodies(runtime);
            Require(runtime.Actors.All(actor => !actor.IsPresentationAway), "all four must be inside on founding morning");
            receipt.AppendLine("initialTime=" + state.Time.Now.ToString("s"));
            receipt.AppendLine("initialCash=5000000 initialSeats=0 initialInventory=0 familyIds=" +
                string.Join(",", runtime.Actors.Select(actor => actor.AgentId)));

            var ids = runtime.Actors.Select(actor => actor.AgentId).ToArray();
            var previous = runtime.Actors.ToDictionary(actor => actor.AgentId, actor => actor.Position);
            var travel = ids.ToDictionary(id => id, id => 0f);
            var pathFrames = ids.ToDictionary(id => id, id => 0);
            var directions = ids.ToDictionary(id => id, id => new HashSet<string>());
            var progressAnchor = new Dictionary<string, Vector2>(previous);
            var progressAt = ids.ToDictionary(id => id, id => 0f);
            var maximumStall = ids.ToDictionary(id => id, id => 0f);
            var samples = new StringBuilder("seconds,gameTime,member,x,y,gaitDistance,phase,destination,direction,reservationBlocker,movementBlocker\n");
            runtime.World.Occupancy.ResetMetrics();
            float start = Time.realtimeSinceStartup;
            float nextSample = 0f;
            float nextCapture = 0f;
            int captures = 0;
            bool fitOnly = Environment.GetCommandLineArgs().Contains("-familyCompanyChairFitQa");
            while (!fitOnly && Time.realtimeSinceStartup - start < 60f)
            {
                float elapsed = Time.realtimeSinceStartup - start;
                foreach (OfficeRuntimeAgent actor in runtime.Actors)
                {
                    float distance = Vector2.Distance(previous[actor.AgentId], actor.Position);
                    travel[actor.AgentId] += distance;
                    previous[actor.AgentId] = actor.Position;
                    if (actor.ActiveDestinationCell.HasValue) pathFrames[actor.AgentId]++;
                    if (distance > 0.0001f) directions[actor.AgentId].Add(actor.CurrentDirection.ToString());
                    string actorId = actor.AgentId;
                    if (actor.Phase != OfficeRuntimeAgentPhase.Navigating ||
                        Vector2.Distance(progressAnchor[actorId], actor.Position) >= 0.02f)
                    {
                        progressAnchor[actorId] = actor.Position;
                        progressAt[actorId] = elapsed;
                    }
                    maximumStall[actorId] = Mathf.Max(maximumStall[actorId], elapsed - progressAt[actorId]);
                    Require(!actor.IsPresentationAway, "actor left initial observation: " + actor.AgentId);
                    if (elapsed >= nextSample)
                        samples.AppendLine(string.Join(",", F(elapsed), state.Time.Now.ToString("HH:mm"), actor.AgentId,
                            F(actor.Position.x), F(actor.Position.y), F(actor.GaitDistance), actor.Phase.ToString(),
                            actor.ActiveDestinationCell.ToString().Replace(',', ':'), actor.CurrentDirection.ToString(),
                            actor.LastReservationBlocker.Replace(',', ':'), actor.LastMovementBlocker.Replace(',', ':')));
                }
                if (elapsed >= nextSample) nextSample += 0.5f;
                if (elapsed >= nextCapture)
                {
                    Capture("wander-" + captures.ToString("D3") + ".png");
                    captures++;
                    nextCapture += 2f;
                }
                AssertNoPenetration(runtime);
                yield return null;
            }
            File.WriteAllText(Path.Combine(directory, "normal-wander.csv"), samples.ToString());
            foreach (string id in ids)
            {
                if (fitOnly) continue;
                receipt.AppendLine(id + " maximumNavigatingNoProgressSeconds=" + F(maximumStall[id]));
                Require(maximumStall[id] < 8f, "normal actor deadlocked for eight seconds: " + id);
                Require(travel[id] > 1f && pathFrames[id] > 10 && directions[id].Count >= 2,
                    "normal coordinator did not produce independent movement: " + id);
                receipt.AppendLine(id + " normalTravel=" + F(travel[id]) + " destinationFrames=" + pathFrames[id] +
                    " directions=" + string.Join("/", directions[id]));
            }
            AssertFourBodies(runtime);
            receipt.AppendLine("normalObservationSeconds=" + (fitOnly ? "0 (fit-only, wander NOT TESTED)" : "60") + " teleports=0 injectedRoutes=0 injectedClockMinutes=0 " +
                "staticViolations=0 interactionViolations=0 agentPenetrations=0 finalTime=" + state.Time.Now.ToString("s"));

            var editor = FindFirstObjectByType<OfficeLayoutEditModeController>();
            Require(editor != null, "build controller missing");
            Require(editor.Open(out string failure), failure);
            Require(OfficeFurnitureCatalog.ShopOffers.Count() == 1, "shop must have one offer");
            Require(!editor.BeginPurchaseForPlayerQa(OfficeGridLayouts.SwivelChairKind, out _) &&
                !editor.BeginPurchaseForPlayerQa(OfficeGridLayouts.WaterDispenserKind, out _), "unapproved offer was exposed");
            var cells = new[] { new OfficeGridCoordinate(3, 3), new OfficeGridCoordinate(9, 3),
                new OfficeGridCoordinate(9, 9), new OfficeGridCoordinate(3, 9) };
            FieldInfo rotation = typeof(OfficeLayoutEditModeController).GetField("_previewRotation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo confirm = typeof(OfficeLayoutEditModeController).GetMethod("ConfirmPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(rotation != null && confirm != null, "controller test seam missing");
            for (int turn = 0; turn < 4; turn++)
            {
                Require(editor.BeginPurchaseForPlayerQa(OfficeGridLayouts.DeskWithPcKind, out failure), failure);
                rotation.SetValue(editor, (OfficeFurnitureFacing)turn);
                Require(editor.LockPreviewOriginForPlayerQa(cells[turn], out failure), failure);
                for (int frame = 0; frame < 5; frame++) yield return null;
                Require(editor.PreviewValidForPlayerQa && editor.PreviewFootprintMarkerCountForPlayerQa == 3,
                    "valid three-tile set preview missing: " + turn);
                Capture("shop-preview-" + turn + ".png");
                yield return new WaitForSecondsRealtime(0.25f);
                // Same confirmation method as the UI; intentionally not reported as a native click.
                confirm.Invoke(editor, null);
                deadline = Time.realtimeSinceStartup + 15f;
                do
                {
                    yield return null;
                    Require(Time.realtimeSinceStartup < deadline, "layout did not rebind after purchase: " + turn);
                } while (!runtime.IsReady || !Family3DProductionPresenter.Instance.IsBound ||
                         Family3DProductionPresenter.Instance.WorkstationCount != turn + 1);
                Require(state.Company.CashWon == 5_000_000 - 400_000 * (turn + 1) &&
                    state.OfficeGrid.SeatSlots.Count == turn + 1 &&
                    state.OfficeFurnitureInventory.Instances.Count == 2 * (turn + 1), "purchase mutation mismatch: " + turn);
                AssertFourBodies(runtime);
                receipt.AppendLine("purchase=" + (turn + 1) + " facing=" + (OfficeFurnitureFacing)turn +
                    " cash=" + state.Company.CashWon + " previewCells=3 seats=" + state.OfficeGrid.SeatSlots.Count);
            }
            Require(ids.All(id => state.OfficeGrid.SeatSlots.Any(seat => seat.SeatId == "seat_" + id)),
                "family seats are not independent");
            Require(Family3DProductionPresenter.Instance.WorkstationCount == 4, "four V31 sets did not bind");
            Require(editor.DiagnosticStateMutationCount == 4 && editor.DiagnosticPointerCommitCount == 0,
                "confirmation count mismatch (programmatic only)");
            long cashBeforeInvalid = state.Company.CashWon;
            Require(editor.BeginPurchaseForPlayerQa(OfficeGridLayouts.DeskWithPcKind, out failure), failure);
            Require(editor.LockPreviewOriginForPlayerQa(cells[0], out failure), failure);
            for (int frame = 0; frame < 5; frame++) yield return null;
            Require(!editor.PreviewValidForPlayerQa, "overlapping set preview was accepted");
            Capture("shop-overlap-rejected.png");
            yield return new WaitForSecondsRealtime(0.25f);
            confirm.Invoke(editor, null);
            Require(state.Company.CashWon == cashBeforeInvalid && editor.DiagnosticStateMutationCount == 4,
                "invalid confirmation charged cash or mutated layout");
            editor.Close();
            for (int frame = 0; frame < 5; frame++) yield return null;
            if (Environment.GetCommandLineArgs().Contains("-familyCompanyAutonomyTraceQa"))
            {
                var attendanceFailures = new List<string>();
                // An idle manually controlled Player cannot satisfy the old simultaneous-four-work
                // assertion. Collect normal intent/path/blocker evidence without declaring work PASS.
                deadline = Time.realtimeSinceStartup + 100f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    AssertNoPenetration(runtime);
                    yield return null;
                }
                Capture("normal-autonomy-end.png");
                if (Environment.GetCommandLineArgs().Contains("-familyCompanyNextDayAutonomyQa"))
                {
                    // Only skip the unobserved afternoon/night. Do not alter actors, routes,
                    // individual due times or the normal 08:50 -> 09:20 attendance window.
                    DateTime evening = state.Time.Now.Date.AddHours(17).AddMinutes(50);
                    bootstrap.AdvanceTimeNow((long)(evening - state.Time.Now).TotalMinutes);
                    bootstrap.SetWorldTimeScaleNow(4f);
                    deadline = Time.realtimeSinceStartup + 120f;
                    while (runtime.Actors.Any(actor => !actor.IsPresentationAway))
                    {
                        Require(Time.realtimeSinceStartup < deadline, "normal departure timeout");
                        AssertNoPenetration(runtime);
                        yield return null;
                    }
                    DateTime morning = state.Time.Now.Date.AddDays(1).AddHours(8).AddMinutes(50);
                    bootstrap.AdvanceTimeNow((long)(morning - state.Time.Now).TotalMinutes);
                    bootstrap.SetWorldTimeScaleNow(1f);
                    var released = new Dictionary<string, DateTime>();
                    var seated = new HashSet<string>();
                    bool checkedNineFour = false;
                    deadline = Time.realtimeSinceStartup + 70f;
                    while (state.Time.Now < morning.Date.AddHours(9).AddMinutes(20))
                    {
                        Require(Time.realtimeSinceStartup < deadline, "normal morning clock timeout");
                        AssertNoPenetration(runtime);
                        foreach (var actor in runtime.Actors)
                        {
                            if (!actor.IsPresentationAway && !released.ContainsKey(actor.AgentId))
                                released.Add(actor.AgentId, state.Time.Now);
                            if (actor.Phase == OfficeRuntimeAgentPhase.Working && actor.AttendanceSeatArrivalCount == 1)
                                seated.Add(actor.AgentId);
                        }
                        if (!checkedNineFour && state.Time.Now >= morning.Date.AddHours(9).AddMinutes(4))
                        {
                            checkedNineFour = true;
                            foreach (var actor in runtime.Actors)
                                if (actor.IsPresentationAway ||
                                    !(actor.HasActiveVisibleMotionIntent || actor.AttendanceSeatArrivalCount == 1))
                                    attendanceFailures.Add("09:04 missing live arrival/seat: " + actor.AgentId);
                        }
                        yield return null;
                    }
                    foreach (string member in new[] { "player", "older_sister", "father", "mother" })
                    {
                        int order = Array.IndexOf(new[] { "player", "older_sister", "father", "mother" }, member);
                        DateTime due = morning.Date.AddHours(9).AddMinutes(order);
                        bool appeared = released.TryGetValue(member, out DateTime actual);
                        if (!appeared || actual != due)
                            attendanceFailures.Add("staggered release mismatch: " + member);
                        if (!seated.Contains(member))
                            attendanceFailures.Add("normal attendance never reached Working: " + member);
                        receipt.AppendLine("nextDay=" + member + " due=" + due.ToString("s") +
                            " released=" + (appeared ? actual.ToString("s") : "NOT_OBSERVED") +
                            " seatedNormally=" + seated.Contains(member));
                    }
                    Capture("next-day-normal-seated.png");
                    receipt.AppendLine("nextDayClockSetupJump=afternoon-night-only nextDayObservedClock=1x nativePointer=false routeInjection=false");
                    receipt.AppendLine("nextDayAttendanceGatePassed=" + (attendanceFailures.Count == 0));
                    foreach (string failure in attendanceFailures) receipt.AppendLine("attendanceFailure=" + failure);
                }
                File.WriteAllText(Path.Combine(directory, "normal-autonomy-observed.txt"),
                    "OBSERVED, NOT A WORK/RELEASE PASS\n" + receipt +
                    "normalClock=true actorControl=false routeInjection=false poseInjection=false nativePointer=false\n" +
                    "runtimeErrors=" + runtimeErrors.Count);
                // Keep collecting the complete normal morning after a timing failure. The failed
                // gate stays failed (exit 1); later arrivals are evidence, not a relaxed PASS.
                Application.Quit(runtimeErrors.Count == 0 && attendanceFailures.Count == 0 ? 0 : 1);
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("-familyCompanyChairFitQa"))
            {
                RunChairFit(runtime);
                receipt.AppendLine("chairFitOnly=true poseInjection=true nativePointer=false normalFourWorking=NOT_TESTED");
                Finish(true, receipt.ToString());
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains(Family3DManualGameplayObserver.BackgroundFlag))
            {
                // Let the normal coordinator dock all four actors; never inject a seat or pose.
                deadline = Time.realtimeSinceStartup + 100f;
                while (!runtime.Actors.All(actor => actor.Phase == OfficeRuntimeAgentPhase.Working))
                {
                    Require(Time.realtimeSinceStartup < deadline, "four actors did not work normally");
                    AssertNoPenetration(runtime);
                    yield return null;
                }
                float settle = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < settle) { AssertNoPenetration(runtime); yield return null; }
                receipt.AppendLine("normalFourWorking=true routeInjection=false poseInjection=false");
            }
            Capture("four-family-four-workstations.png");
            yield return new WaitForSecondsRealtime(1f);
            Require(File.Exists(Path.Combine(directory, "wander-000.png")) &&
                File.Exists(Path.Combine(directory, "four-family-four-workstations.png")), "D3D11 screenshot missing");
            receipt.AppendLine("fourSetsCash=3400000 seats=4 inventory=8 onlyShopOffer=V31 threeTilePreview=PASS overlapNoCharge=PASS");
            receipt.AppendLine("capture=explicitD3D11CameraStack IMGUI=NOT_CAPTURED nativePointer=NOT_TESTED " +
                "finalMotherSisterAssets=NOT_PRESENT temporaryPresentationOnly=true");
            Finish(true, receipt.ToString());
        }

        private void RunChairFit(StarterOfficeRuntimeBootstrap runtime)
        {
            var presenter = Family3DProductionPresenter.Instance;
            presenter.enabled = false;
            var desks = presenter.GetComponentsInChildren<Family3DWorkstation>().OrderBy(d => d.WorkstationSetId).ToArray();
            var hosts = runtime.Actors.Select(actor => GameObject.Find(
                OfficeFamily3DVisualRoster.ProductionName(actor.AgentId) + "ProductionHost")).ToArray();
            // Hide pixels only. Deactivating/re-enabling a host rebuilds its animation graph
            // and remeasures bounds mid-pose, invalidating the production standing-height input.
            foreach (GameObject host in hosts)
                foreach (Renderer renderer in host.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = true;
            bool sweep = Environment.GetCommandLineArgs().Contains("-familyCompanyChairFitSweep");
            var metrics = new StringBuilder("member,seat,hipClearance,kneeTarget,handError,maxHandError,leftKnee,rightKnee,chairPenetrations,cushion,upperLeg,torso,rail,stem,leanDegrees\n");
            bool passed = true;
            foreach (string member in new[] { "player", "father" })
            {
                GameObject host = hosts.Single(item => item.name ==
                    OfficeFamily3DVisualRoster.ProductionName(member) + "ProductionHost");
                foreach (Renderer renderer in host.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = false;
                var body = host.GetComponent<Family3DWalkActor>();
                var avatar = host.GetComponentInChildren<Animator>();
                foreach (var desk in (sweep ? desks.Take(1) : desks))
                {
                  foreach (Vector2 fitting in (sweep
                    ? from clearance in new[] { 0.113f, 0.123f, 0.133f, 0.143f }
                      from knee in new[] { 95f, 100f, 105f, 110f } select new Vector2(clearance, knee)
                    : new[] { new Vector2(0.113f, 95f) }))
                  {
                    Vector3 root = desk.SeatGroundWorld;
                    body.TickSeatedDeskWork(0d, root, desk.SeatedRotationWorld, 1f, false);
                    var pose = body.ReadPoseSnapshot();
                    root.y = desk.CushionWorldY + fitting.x * pose.standingHeight - pose.hipsLocal.y;
                    root += desk.SeatedBodyForwardWorld * (0.07f * pose.standingHeight);
                    host.transform.position = root;
                    body.AlignSeatedDeskLimbs(desk.KeyboardWorld, desk.SeatedBodyForwardWorld, 0, 1, 0, false, fitting.y);
                    pose = body.ReadPoseSnapshot();
                    Vector3 hands = (avatar.GetBoneTransform(HumanBodyBones.LeftHand).position +
                                     avatar.GetBoneTransform(HumanBodyBones.RightHand).position) * 0.5f;
                    Vector3 expected = desk.KeyboardWorld + Vector3.up * (0.022f * body.StandingHeight) -
                                       desk.SeatedBodyForwardWorld * (0.035f * body.StandingHeight);
                    float error = Vector3.Distance(hands, expected);
                    Vector3 handRight = Vector3.Cross(Vector3.up, desk.SeatedBodyForwardWorld).normalized * (0.12f * body.StandingHeight);
                    float maxHandError = Mathf.Max(
                        Vector3.Distance(avatar.GetBoneTransform(HumanBodyBones.LeftHand).position, expected - handRight),
                        Vector3.Distance(avatar.GetBoneTransform(HumanBodyBones.RightHand).position, expected + handRight));
                    float left = Vector3.Angle(pose.leftHipWorld - pose.leftKneeWorld, pose.leftFootWorld - pose.leftKneeWorld);
                    float right = Vector3.Angle(pose.rightHipWorld - pose.rightKneeWorld, pose.rightFootWorld - pose.rightKneeWorld);
                    var vertices = new List<Vector3>(); var regions = new List<Family3DWalkActor.SeatedSkinRegion>();
                    body.CollectCurrentWorldSkinVertices(vertices, regions);
                    var penetration = desk.MeasureChairSkinPenetration(vertices, regions);
                    metrics.AppendLine(string.Join(",", member, desk.WorkstationSetId, F(fitting.x), F(fitting.y), F(error), F(maxHandError), F(left), F(right),
                        penetration.totalPenetratingVertexCount, penetration.cushionVertexCount,
                        penetration.cushionUpperLegVertexCount, penetration.cushionPelvisOrTorsoVertexCount,
                        penetration.lumbarVertexCount, penetration.stemVertexCount, F(body.LastSeatedTorsoLeanDegrees)));
                    if (!sweep) Capture("chair-fit-" + member + "-" + desk.WorkstationSetId + ".png");
                    passed &= sweep || maxHandError <= 0.02f * body.StandingHeight && left >= 80 && left <= 140 &&
                              right >= 80 && right <= 140 && penetration.totalPenetratingVertexCount == 0;
                  }
                }
                foreach (Renderer renderer in host.GetComponentsInChildren<Renderer>()) renderer.forceRenderingOff = true;
            }
            File.WriteAllText(Path.Combine(directory, "chair-fit.csv"), metrics.ToString());
            if (sweep) receipt.AppendLine("parameterSweepOnly=true productionPose=NOT_VALIDATED");
            Require(passed, "chair pose fit failed; inspect chair-fit.csv (not normal navigation)");
        }

        private static void AssertFourBodies(StarterOfficeRuntimeBootstrap runtime)
        {
            var presenter = Family3DProductionPresenter.Instance;
            Require(presenter != null && presenter.IsBound && presenter.BoundCharacterCount == 4,
                "four 3D bindings required");
            Require(presenter.VisibleLegacyCharacterRendererCount == 0 &&
                presenter.VisibleLegacyWorkstationRendererCount == 0, "legacy sprite pixels visible");
            var renderers = new HashSet<int>();
            foreach (OfficeRuntimeAgent actor in runtime.Actors)
            {
                GameObject host = GameObject.Find(OfficeFamily3DVisualRoster.ProductionName(actor.AgentId) + "ProductionHost");
                Require(host != null && host.activeInHierarchy, "3D host missing: " + actor.AgentId);
                var mesh = host.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var avatar = host.GetComponentInChildren<Animator>(true);
                Require(mesh.Length == 1 && mesh[0].enabled && !mesh[0].forceRenderingOff &&
                    renderers.Add(mesh[0].GetInstanceID()) && avatar != null && avatar.avatar != null &&
                    avatar.avatar.isValid && avatar.avatar.isHuman, "independent complete Humanoid missing: " + actor.AgentId);
            }
        }

        private static void AssertNoPenetration(StarterOfficeRuntimeBootstrap runtime)
        {
            var occupancy = runtime.World.Occupancy;
            Require(occupancy.StaticViolationCount == 0 && occupancy.InteractionViolationCount == 0 &&
                occupancy.AgentPenetrationCount == 0, "normal movement penetration detected " +
                "static=" + occupancy.StaticViolationCount + " interaction=" + occupancy.InteractionViolationCount +
                " agents=" + occupancy.AgentPenetrationCount + " | " + string.Join(" | ", runtime.Actors.Select(actor =>
                    actor.AgentId + ":" + actor.Phase + ":" + actor.Position + ":seat=" + actor.ActiveSeatId + ":" +
                    occupancy.DescribeMoveBlocker(actor.AgentId, actor.Position, actor.Position, actor.AgentRadius, actor.ActiveSeatId))));
        }

        private void Capture(string name)
            => CaptureCameraStack(Path.Combine(directory, name));

        internal static void CaptureCameraStack(string path)
        {
            // Batchmode has no presented swap chain: ScreenCapture yields black images. Render
            // the actual office + actual 3D overlay to one target; IMGUI is explicitly excluded.
            Camera source = Camera.main;
            Camera overlay = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SingleOrDefault(camera => camera.name == "Family3DProductionOverlayCamera");
            Require(source != null && overlay != null, "office camera stack missing");
            const int width = 1280;
            const int height = 720;
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousOverlay = overlay.targetTexture;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var host = new GameObject("OpeningShopCapture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Camera camera = host.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.aspect = width / (float)height;
                camera.enabled = false;
                camera.targetTexture = target;
                camera.Render();
                overlay.targetTexture = target;
                overlay.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                pixels.Apply(false, false);
                Require(pixels.GetPixels32().Count(pixel => pixel.r > 24 || pixel.g > 24 || pixel.b > 24) > width * height / 4,
                    "blank D3D11 capture rejected");
                File.WriteAllBytes(path, Path.GetExtension(path) == ".tga" ? pixels.EncodeToTGA() : pixels.EncodeToPNG());
            }
            finally
            {
                overlay.targetTexture = previousOverlay;
                RenderTexture.active = previous;
                Destroy(host);
                Destroy(pixels);
                Destroy(target);
            }
        }
        private static string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
        private static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
        }

        private void Finish(bool pass, string detail)
        {
            if (pass && runtimeErrors.Count > 0)
            {
                pass = false;
                detail += "\nruntimeErrors=" + string.Join(" | ", runtimeErrors);
            }
            if (pass) detail += "\nruntimeErrors=0";
            string result = "FAMILY_3D_OPENING_SHOP_QA: " + (pass ? "PASS" : "FAIL") + "\n" + detail;
            File.WriteAllText(Path.Combine(directory, "opening-shop-final.txt"), result);
            if (pass) Debug.Log(result); else Debug.LogError(result);
            Application.Quit(pass ? 0 : 1);
        }
    }
}
