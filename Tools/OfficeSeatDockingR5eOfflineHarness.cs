using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal static class OfficeSeatDockingR5eOfflineHarness
{
    private const string TransactionSource =
        "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeSeatDockingTransaction.cs";
    private const string AgentSource =
        "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeAgent.cs";
    private const string WorldSource =
        "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeWorld.cs";
    private const string AnimatorSource =
        "Assets/FamilyCompany/Presentation.Unity/DirectionalSpriteAnimator.cs";
    private const string RuntimeQaSource =
        "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/Qa/OfficeSeatDockingR5ePlayerQa.cs";
    private const string RuntimeContractSource =
        "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/Qa/OfficeSeatDockingR5eRuntimeQaContract.cs";
    private const string TraceWriterSource =
        "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeSeatDockingR5eTraceWriter.cs";
    private const string ScenarioCatalogSource =
        "Assets/FamilyCompany/Presentation.Unity/Resources/OfficeSeatDockingR5eScenarioCatalog.json";
    private const string ScenarioParserSource =
        "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/Qa/OfficeSeatDockingR5eScenarioCatalog.cs";
    private const string StaticAggregatorSource =
        "Assets/FamilyCompany/Editor/OfficeSeatDockingR5eStaticValidation.cs";
    private const string ProductionFixtureSource =
        "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/Qa/OfficeSeatDockingR5eProductionStaticFixture.cs";
    private const string PostProcessorSource = "Tools/OfficeSeatDockingR5ePostProcessor.cs";
    private const string MaskAnalyzerSource = "Tools/OfficeSeatDockingR5eMaskAnalyzer.cs";
    private const string StaticSelfTestSource = "Tools/Invoke-OfficeSeatDockingR5eStaticSelfTest.ps1";
    private const string PostProcessEntrypointSource = "Tools/Invoke-OfficeSeatDockingR5ePostProcess.ps1";

    private static readonly SchemaContract[] Schemas =
    {
        new SchemaContract("TransitionHeader", "seat-transition-events-r5e.csv", 253),
        new SchemaContract("SeatedSessionHeader", "seat-session-samples-r5e.csv", 110),
        new SchemaContract("LocomotionAdapterHeader", "locomotion-step-adapter-r5e.csv", 74),
        new SchemaContract("DecodedFrameHeader", "classic-docking-r5e-decoded-frame-oracle.csv", 118),
        new SchemaContract("HumanReviewHeader", "classic-docking-r5e-human-visual-review.csv", 20)
    };

    public static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args.Length == 0 ? Directory.GetCurrentDirectory() : args[0]);
            string transactionText = Read(root, TransactionSource);
            Dictionary<string, string[]> headers = ValidateSchemas(transactionText);
            ValidateCapacities(transactionText);
            ValidateStaticSourceContracts(root);
            ValidateLifecycleFixtures();
            ValidateSeatedPairFixtures();
            ValidateAtomicPublishFixtures();
            ValidateFailClosedHeaderFixtures(headers);
            if (args.Length > 1)
                ValidateTraceDirectory(Path.GetFullPath(args[1]), headers);
            Console.WriteLine(
                "OFFICE_SEAT_DOCKING_R5E_OFFLINE: PASS " +
                "schemas=253/110/74/118/20 transitionCapacity=512 " +
                "seatedCapacity=49152 locomotionCapacity=24576 visualCapacity=2048 " +
                "legacyModelNegativeFixtures=20 actualPacketNegativeFixtures=5 " +
                "scenarioCases=158 legacyClipOracle=unused");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("OFFICE_SEAT_DOCKING_R5E_OFFLINE: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static Dictionary<string, string[]> ValidateSchemas(string source)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (SchemaContract schema in Schemas)
        {
            string header = ExtractLiteral(source, schema.ConstantName);
            string[] columns = header.Split(',');
            Require(columns.Length == schema.ExpectedColumns,
                schema.ConstantName + " count " + columns.Length + " != " + schema.ExpectedColumns);
            Require(columns.All(value => value.Length > 0), schema.ConstantName + " has an empty column");
            Require(columns.Distinct(StringComparer.Ordinal).Count() == columns.Length,
                schema.ConstantName + " contains duplicate columns");
            result.Add(schema.ConstantName, columns);
        }

        RequireColumns(result["TransitionHeader"],
            "schemaVersion", "runId", "tick", "frame", "actorId", "seatId",
            "transactionId", "event", "transitionKind", "locomotionSample",
            "seatedSessionId", "movementHandoffId", "locomotionTraceRowId",
            "gcAllocBytes", "frameMs", "floorValid", "staticOverlap", "chairOverlap",
            "commitSucceeded", "rollbackSucceeded", "defaultOnlyFieldMask");
        RequireColumns(result["SeatedSessionHeader"],
            "samplePhase", "actorStepOrdinal", "runtimeTick", "seatedSessionId",
            "expectedPreClearSampleCount", "observedPreClearSampleCount",
            "expectedPostClearSampleCount", "observedPostClearSampleCount",
            "clearMaskedViolationCount", "producerValid", "overflowCount");
        RequireColumns(result["LocomotionAdapterHeader"],
            "routeGenerationId", "movementHandoffId", "expectedMoving", "observedMoving",
            "firstWalk", "quantizedVelocityFacing", "renderedFacing", "forwardDot",
            "renderJoinValid", "acceptedTraceOneToOneValid", "overflowCount");
        RequireColumns(result["DecodedFrameHeader"],
            "sourceFrameSha256", "sourceFrameIdentityValid", "frameJoinValid",
            "actualFurnitureMaskValid", "expectedFrameSampleCount", "observedFrameSampleCount",
            "standWhileMovingProducerValid", "footOnChairProducerValid",
            "descendRiseProducerValid", "bodyPopProducerValid",
            "chairDeskPenetrationProducerValid", "ghostProducerValid",
            "doubleBodyProducerValid", "headTeleportProducerValid", "defaultOnlyMask");
        RequireColumns(result["HumanReviewHeader"],
            "cleanVideoSha256", "annotatedVideoSha256", "decodedOracleSha256",
            "normalScale", "pass");
        return result;
    }

    private static void ValidateCapacities(string source)
    {
        int transition = ExtractIntConstant(source, "TransitionCapacityPerActor");
        int seated = ExtractIntConstant(source, "SeatedCapacityPerActor");
        int locomotion = ExtractIntConstant(source, "LocomotionCapacityPerActor");
        int visual = ExtractIntConstant(source, "VisualCapacityPerActor");
        int lifecycle = ExtractIntConstant(source, "MaximumLifecycleEventRowsPerTransaction");
        Require(transition == 512 && seated == 49152 && locomotion == 24576 && visual == 2048,
            "fixed capacities differ from R5e");
        Require(lifecycle == 5, "lifecycle rows per transaction must be five");
        Require(64 * lifecycle <= transition, "transition capacity proof failed");
        Require(24576 * 2 <= seated, "seated PreClear/PostClear capacity proof failed");
        Require(12288 * 2 <= locomotion, "step/render locomotion capacity proof failed");
        Require(30 * 60 <= visual, "visual metadata capacity proof failed");
    }

    private static void ValidateStaticSourceContracts(string root)
    {
        string world = Read(root, WorldSource);
        AssertOrdered(world,
            "actor.BeginPresentationFrame();",
            "actor.ConsumeVisibleMotionDelta(unscaledDeltaTime);",
            "OfficeNavigationMotionIntegrator.CalculateStepCount(actorDelta);",
            "step >= actorSteps) continue;",
            "OfficeNavigationMotionIntegrator.ResolveStepDelta(",
            "_traceCoordinator.TryBeginActorStep(",
            "actor.BeginR5eRuntimeStep(",
            "actor.TickRuntime(stepDelta);",
            "R5eAgentStepSnapshot preClear",
            "actor.AppendObservedPreClear(",
            "actor.ClearInactiveVisibleMotionDebt();",
            "R5eAgentStepSnapshot postClear",
            "actor.FinalizeR5eRuntimeStepPostClear(",
            "_traceCoordinator.TryPreflightRender(actor)",
            "_traceCoordinator.BeginRenderFrame(actor, Time.frameCount);",
            "actor.TickPresentation(_frameMotionDeltas[index]);",
            "_traceCoordinator.AppendRenderAdapter(actor, Time.frameCount);",
            "_depthSorter.Apply(actors);");

        string agent = Read(root, AgentSource);
        string dispatchEpilogue = ExtractMethod(agent, "private void SealR5eRuntimeStepDispatch()");
        Require(!dispatchEpilogue.Contains("_currentVelocity =", StringComparison.Ordinal) &&
                !dispatchEpilogue.Contains("_visibleMotionDebtSeconds =", StringComparison.Ordinal) &&
                !dispatchEpilogue.Contains("RebaseTileMotionAfterAtomicPlacement", StringComparison.Ordinal),
            "TickRuntime epilogue masks an immediate PreClear stationary violation");
        string stationaryFrame = ExtractMethod(
            agent,
            "internal void PrepareR5eStationaryFrameAfterAcceptedMotionBudget()");
        Require(stationaryFrame.Contains("IsR5eSeatedPostState", StringComparison.Ordinal) &&
                stationaryFrame.Contains("_r5eExitTurnPending", StringComparison.Ordinal) &&
                stationaryFrame.Contains("_visibleFrameMovementBudgetWorld = 0f", StringComparison.Ordinal),
            "seated/turn-in-place render budget is not zeroed before the PreClear sample");
        string seating = ExtractMethod(agent, "private void TickSeating(float deltaTime)");
        Require(CountOccurrences(
                    seating,
                    "PrepareR5eStationaryFrameAfterAcceptedMotionBudget();") == 3,
            "seated/blocked-exit/turn-in-place paths do not all zero their stationary budget");
        Require(seating.Contains("TryPublishR5eAtomicSeat();", StringComparison.Ordinal),
            "atomic entry is not selected");
        Require(seating.Contains("TryPublishR5eAtomicExit();", StringComparison.Ordinal),
            "atomic exit is not selected");
        Require(!seating.Contains(".BeginSitDown(", StringComparison.Ordinal),
            "deprecated SitDown clip remains selectable");
        Require(!seating.Contains(".BeginStandUp(", StringComparison.Ordinal),
            "deprecated StandUp clip remains selectable");
        Require(!seating.Contains("TickSeatEgressDismount(", StringComparison.Ordinal),
            "pelvis egress tween remains selectable");
        AssertOrdered(agent,
            "public void TickRuntime(float deltaTime)",
            "TickRuntimeDispatch(deltaTime);",
            "finally",
            "SealR5eRuntimeStepDispatch();");
        Require(agent.Contains("_r5eLastClosedSeatedSessionId", StringComparison.Ordinal),
            "exit lifecycle loses seated-session join identity");
        Require(agent.Contains("FirstWalk", StringComparison.Ordinal) &&
                agent.Contains("_r5eRuntimeTick > _r5eTurnCompleteTick", StringComparison.Ordinal),
            "FirstWalk is not later than TurnComplete");
        Require(agent.Contains("CommitPreparedAtomicActorPlacement", StringComparison.Ordinal) &&
                agent.Contains("RebaseAfterAtomicPlacement", StringComparison.Ordinal),
            "same-tick canonical placement/rebase is absent");
        Require(agent.Contains("R5eAtomicAgentSnapshot", StringComparison.Ordinal) &&
                agent.Contains("RollbackPreparedAtomicActorPlacement", StringComparison.Ordinal) &&
                agent.Contains("RollbackPreparedOccupy", StringComparison.Ordinal) &&
                agent.Contains("RollbackPreparedRelease", StringComparison.Ordinal) &&
                agent.Contains("RestoreAtomicReservationScope", StringComparison.Ordinal),
            "production atomic publish does not expose an exact rollback journal");

        string animator = Read(root, AnimatorSource);
        Require(animator.Contains("EnterCompletedSeatedWorkAfterAtomicPlacement", StringComparison.Ordinal),
            "additive completed seated API is absent");
        Require(animator.Contains("LeaveCompletedSeatedWorkAfterAtomicPlacement", StringComparison.Ordinal),
            "additive completed standing API is absent");
        Require(!agent.Contains("OfficeSeatingTransitionPlayerQa", StringComparison.Ordinal),
            "legacy 4/6/4 Player QA is referenced by R5e runtime");

        string contract = Read(root, RuntimeContractSource);
        Require(contract.Contains("-familyCompanyChairR5eQa", StringComparison.Ordinal) &&
                contract.Contains("-familyCompanyChairR5eVisualQa", StringComparison.Ordinal) &&
                contract.Contains("-familyCompanyChairR5e4xQa", StringComparison.Ordinal) &&
                contract.Contains("-familyCompanyChairR5eQaArtifacts", StringComparison.Ordinal),
            "frozen Release runner flags are incomplete");
        string runner = Read(root, RuntimeQaSource);
        Require(runner.Contains("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal) &&
                runner.Contains("GameplayMeasureBegin", StringComparison.Ordinal) &&
                runner.Contains("FlushPostWindow", StringComparison.Ordinal) &&
                runner.Contains("frameOver50MsCount", StringComparison.Ordinal) &&
                !runner.Contains("OfficeSeatingTransitionPlayerQa", StringComparison.Ordinal) &&
                runner.Contains("RunScenarioCase(_scenarioPlan.Cases[index])", StringComparison.Ordinal) &&
                 runner.Contains("RunContentionScenario", StringComparison.Ordinal) &&
                 runner.Contains("QaArmR5eFault", StringComparison.Ordinal) &&
                 runner.Contains("QaInvalidateNextAtomicVersion", StringComparison.Ordinal) &&
                 runner.Contains("OfficeRuntimeTraceArchive", StringComparison.Ordinal) &&
                 runner.Contains("TryCaptureFurnitureTransformAggregate", StringComparison.Ordinal) &&
                 runner.Contains("gameplayAllocationFrameCount", StringComparison.Ordinal) &&
                 !runner.Contains("_scenarioCoordinators", StringComparison.Ordinal) &&
                 runner.Contains("PENDING_POSTPROCESS", StringComparison.Ordinal),
            "atomic Release observer/visual runner contract is incomplete or stale");
        Require(!runner.Contains("if (!_visualOwner) yield break;", StringComparison.Ordinal) &&
                runner.Contains("yield return RunVisualCycle();", StringComparison.Ordinal),
            "observer flag installs an inert zero-denominator runner");
        string writer = Read(root, TraceWriterSource);
        Require(writer.Contains("Post-window only serializer", StringComparison.Ordinal) &&
                writer.Contains("seat-transition-events-r5e.csv", StringComparison.Ordinal) &&
                writer.Contains("seat-session-samples-r5e.csv", StringComparison.Ordinal) &&
                writer.Contains("locomotion-step-adapter-r5e.csv", StringComparison.Ordinal),
            "post-window trace serializer is incomplete");
        string aggregator = Read(root, StaticAggregatorSource);
        Require(aggregator.Contains(
                    "FamilyCompany.Editor.OfficeSeatDockingR5eStaticValidation.Run",
                    StringComparison.Ordinal) &&
                aggregator.Contains("legacyClipOracle=unused", StringComparison.Ordinal),
            "single G1 static aggregator entrypoint is missing");
        string catalog = Read(root, ScenarioCatalogSource);
        Require(catalog.Contains("\"baseMatrixCaseCount\": 128", StringComparison.Ordinal) &&
                catalog.Contains("r5e-all-exits-blocked", StringComparison.Ordinal) &&
                catalog.Contains("contentionPermutations", StringComparison.Ordinal) &&
                catalog.Contains("58193017", StringComparison.Ordinal),
            "deterministic 4x4x8/contention/blocked/seeded catalog is incomplete");
        string parser = Read(root, ScenarioParserSource);
        Require(parser.Contains("ParseAndValidate", StringComparison.Ordinal) &&
                parser.Contains("ExpectedSha256", StringComparison.Ordinal) &&
                parser.Contains("BaseCaseCount = 128", StringComparison.Ordinal) &&
                parser.Contains("TotalCaseCount = 158", StringComparison.Ordinal) &&
                parser.Contains("CreateLayoutForCase", StringComparison.Ordinal),
            "scenario catalog is not fail-closed typed production input");
        string expectedCatalogSha = Regex.Match(
            parser,
            "ExpectedSha256\\s*=\\s*\"(?<sha>[0-9A-F]{64})\"").Groups["sha"].Value;
        Require(expectedCatalogSha.Length == 64 &&
                string.Equals(expectedCatalogSha, Sha256File(Path.Combine(root, ScenarioCatalogSource)),
                    StringComparison.OrdinalIgnoreCase),
            "scenario catalog file does not match the compiled fail-closed hash");
        Require(writer.Contains("observedAfter.ProducerValid", StringComparison.Ordinal) &&
                writer.Contains("observedBefore.Chair.Hash != observedAfter.Chair.Hash", StringComparison.Ordinal) &&
                writer.Contains("row.Observation.AllocationBytes", StringComparison.Ordinal) &&
                writer.Contains("TransactionSnapshotHash", StringComparison.Ordinal) &&
                writer.Contains("row.RenderTrace.ActualDisplacement", StringComparison.Ordinal) &&
                writer.Contains("PENDING", StringComparison.Ordinal),
            "trace writer uses default declarations instead of live producer values/PENDING visual state");

        string productionFixture = Read(root, ProductionFixtureSource);
        Require(productionFixture.Contains(
                    "OfficeSeatDockingR5eScenarioCatalog.ParseAndValidateJson", StringComparison.Ordinal) &&
                productionFixture.Contains("for (var index = 0; index < plan.Cases.Length; index++)",
                    StringComparison.Ordinal) &&
                productionFixture.Contains("TryPrepareProductionTransactionFixturePlacement",
                    StringComparison.Ordinal) &&
                productionFixture.Contains("CommitPreparedAtomicActorPlacement", StringComparison.Ordinal) &&
                productionFixture.Contains("RollbackPreparedAtomicActorPlacement", StringComparison.Ordinal) &&
                productionFixture.Contains("OfficeSeatDockingAtomicPublishPrimitive.TryPublish",
                    StringComparison.Ordinal) &&
                productionFixture.Contains("OfficeSeatDockingR5eTraceWriter.WriteProductionStaticFixture",
                    StringComparison.Ordinal),
            "executable 158-case production transaction/writer fixture is absent");
        Require(agent.Contains("OfficeSeatDockingAtomicPublishPrimitive.TryPublish", StringComparison.Ordinal) &&
                CountOccurrences(agent, "OfficeSeatDockingAtomicPublishPrimitive.TryPublish") == 2,
            "live entry/exit do not share the source-compiled atomic primitive with the fixture");
        string postProcessor = Read(root, PostProcessorSource);
        Require(postProcessor.Contains("status=PENDING_POSTPROCESS", StringComparison.Ordinal) &&
                postProcessor.Contains("ProbeVideo", StringComparison.Ordinal) &&
                postProcessor.Contains("expectedFrameSampleCount", StringComparison.Ordinal) &&
                postProcessor.Contains("chair-r5e-static-fixture-complete.marker", StringComparison.Ordinal) &&
                postProcessor.Contains("chair-r5e-complete.marker", StringComparison.Ordinal),
            "deterministic decoded/human completion producer is absent");
        string maskAnalyzer = Read(root, MaskAnalyzerSource);
        Require(maskAnalyzer.Contains("ReadPgm", StringComparison.Ordinal) &&
                maskAnalyzer.Contains("Intersection", StringComparison.Ordinal) &&
                maskAnalyzer.Contains("CountLargeComponents", StringComparison.Ordinal) &&
                maskAnalyzer.Contains("source-frame identity mismatch", StringComparison.Ordinal) &&
                maskAnalyzer.Contains("mask-analyzer-complete.marker", StringComparison.Ordinal),
            "decoded semantic-mask producer is absent or default-only");
        string staticSelfTest = Read(root, StaticSelfTestSource);
        Require(staticSelfTest.Contains("OfficeSeatDockingR5eProductionFixtureRunner", StringComparison.Ordinal) &&
                staticSelfTest.Contains("OfficeSeatDockingR5ePostProcessor", StringComparison.Ordinal) &&
                staticSelfTest.Contains("-TraceDirectory $productionFixtureTrace", StringComparison.Ordinal) &&
                staticSelfTest.Contains("SCENARIO_NEGATIVE", StringComparison.Ordinal) &&
                staticSelfTest.Contains("MASK_NEGATIVE", StringComparison.Ordinal) &&
                staticSelfTest.Contains("POSTPROCESS_NEGATIVE", StringComparison.Ordinal),
            "static gate does not execute production writer/parser/postprocessor roundtrip");
        string postProcessEntrypoint = Read(root, PostProcessEntrypointSource);
        Require(postProcessEntrypoint.Contains("OfficeSeatDockingR5eMaskAnalyzer", StringComparison.Ordinal) &&
                postProcessEntrypoint.Contains("OfficeSeatDockingR5ePostProcessor", StringComparison.Ordinal) &&
                postProcessEntrypoint.Contains("process0", StringComparison.Ordinal) &&
                postProcessEntrypoint.Contains("POSTPROCESS_ENTRYPOINT: PASS", StringComparison.Ordinal),
            "offline postprocess gate has no deterministic executable entrypoint");
    }

    private static void ValidateLifecycleFixtures()
    {
        RequireLifecycle(new[] { "Prepare", "Commit", "Rebase" }, "Entry", true);
        RequireLifecycle(new[] { "Prepare", "Commit", "Rebase", "TurnComplete", "FirstWalk" }, "Exit", true);
        RequireLifecycle(new[] { "Prepare", "Rollback" }, "Entry", false);
        RequireLifecycle(new[] { "Prepare", "Rollback" }, "Exit", false);
        ExpectFailure(() => RequireLifecycle(new[] { "Prepare", "Commit" }, "Entry", true), "missing rebase");
        ExpectFailure(() => RequireLifecycle(new[] { "Prepare", "Rebase", "Commit" }, "Entry", true), "reordered entry");
        ExpectFailure(() => RequireLifecycle(new[] { "Prepare", "Rollback", "Commit" }, "Exit", false), "event after terminal");
        ExpectFailure(() => RequireLifecycle(new[] { "Prepare", "Commit", "Rebase", "FirstWalk", "TurnComplete" }, "Exit", true), "walk before turn");
    }

    private static void ValidateSeatedPairFixtures()
    {
        RequireSeatedPair(new[] { "PreClear", "PostClear" }, 0f, 0f);
        ExpectFailure(() => RequireSeatedPair(new[] { "PostClear" }, 0f, 0f), "missing preclear");
        ExpectFailure(() => RequireSeatedPair(new[] { "PreClear", "PostClear" }, 0.5f, 0f), "clear masked mutation");
    }

    private static void ValidateAtomicPublishFixtures()
    {
        var before = new AtomicModel("Working", "occupied", "seat-a", 10, 4, 2, 3, 7);
        AtomicModel blocked = PublishExit(before, floorValid: false, staticOverlap: true,
            chairVersion: 10, commitVersion: 10, faultPoint: 0);
        Require(before.Equals(blocked), "all-blocked exit is not an exact seated no-op");

        AtomicModel mutated = PublishExit(before, floorValid: true, staticOverlap: false,
            chairVersion: 10, commitVersion: 11, faultPoint: 0);
        Require(before.Equals(mutated), "version mismatch changed actor/transaction state");

        AtomicModel after = PublishExit(before, floorValid: true, staticOverlap: false,
            chairVersion: 10, commitVersion: 10, faultPoint: 0);
        Require(after.State == "LeavingSeat" && after.Claim == "released" &&
                after.Occupancy == "exit" && after.Velocity == 0 && after.Debt == 0,
            "atomic exit post-state is incomplete");
        for (int fault = 1; fault <= 6; fault++)
        {
            AtomicModel result = PublishExit(before, true, false, 10, 10, fault);
            Require(result.Equals(before), "fault fixture exposed a half-state at boundary " + fault);
        }
    }

    private static void ValidateFailClosedHeaderFixtures(Dictionary<string, string[]> headers)
    {
        string[] original = headers["TransitionHeader"];
        RequireHeader(original, 253);
        ExpectFailure(() => RequireHeader(original.Take(252).ToArray(), 253), "missing header column");
        string[] duplicate = original.ToArray();
        duplicate[252] = duplicate[251];
        ExpectFailure(() => RequireHeader(duplicate, 253), "duplicate header column");
    }

    private static void ValidateTraceDirectory(string directory, Dictionary<string, string[]> headers)
    {
        Require(Directory.Exists(directory), "trace directory does not exist: " + directory);
        bool staticProductionPacket = IsStaticProductionPacket(directory);
        IEnumerable<SchemaContract> requiredSchemas = staticProductionPacket
            ? Schemas.Take(3)
            : Schemas;
        foreach (SchemaContract schema in requiredSchemas)
        {
            string path = Path.Combine(directory, schema.FileName);
            Require(File.Exists(path), "missing trace file: " + schema.FileName);
            using var reader = new StreamReader(path, Encoding.UTF8, true);
            string first = reader.ReadLine() ?? string.Empty;
            string[] actual = ParseCsv(first).ToArray();
            string[] expected = headers[schema.ConstantName];
            Require(actual.SequenceEqual(expected, StringComparer.Ordinal),
                schema.FileName + " header/order mismatch");
            Require(reader.ReadLine() != null, schema.FileName + " has zero observed rows");
        }
        ValidateTransitionCsv(Path.Combine(directory, Schemas[0].FileName), headers["TransitionHeader"]);
        ValidateSeatedCsv(Path.Combine(directory, Schemas[1].FileName), headers["SeatedSessionHeader"]);
        ValidateLocomotionCsv(Path.Combine(directory, Schemas[2].FileName), headers["LocomotionAdapterHeader"]);
        ValidateVisualMetadataCsv(
            Path.Combine(directory, "visual-capture-metadata-r5e.csv"),
            !staticProductionPacket);
        ValidateScenarioResultsCsv(Path.Combine(directory, "chair-r5e-scenario-results.csv"));
        if (staticProductionPacket)
        {
            ValidateDecodedCsv(Path.Combine(directory, Schemas[3].FileName), headers["DecodedFrameHeader"]);
            ValidateHumanCsv(Path.Combine(directory, Schemas[4].FileName), headers["HumanReviewHeader"]);
            ValidateStaticProductionEnvelope(directory);
            ValidateActualPacketNegativeFixtures(directory, headers);
            return;
        }
        ValidateDecodedCsv(Path.Combine(directory, Schemas[3].FileName), headers["DecodedFrameHeader"]);
        ValidateHumanCsv(Path.Combine(directory, Schemas[4].FileName), headers["HumanReviewHeader"]);
        ValidateRuntimeEnvelope(directory);
    }

    private static bool IsStaticProductionPacket(string directory)
    {
        string path = Path.Combine(directory, "chair-r5e-runtime-result.txt");
        return File.Exists(path) && File.ReadAllText(path).Contains(
            "fixtureKind=production-static",
            StringComparison.Ordinal);
    }

    private static void ValidateStaticProductionEnvelope(string directory)
    {
        string path = Path.Combine(directory, "chair-r5e-runtime-result.txt");
        Require(File.Exists(path), "production static result missing");
        string text = File.ReadAllText(path);
        Require(text.Contains("status=PENDING_POSTPROCESS", StringComparison.Ordinal) &&
                text.Contains("fixtureKind=production-static", StringComparison.Ordinal) &&
                text.Contains("scenarioExpected=158", StringComparison.Ordinal) &&
                text.Contains("scenarioObserved=158", StringComparison.Ordinal) &&
                text.Contains("legacyClipOracle=unused", StringComparison.Ordinal),
            "production static packet escaped PENDING or lost coverage identity");
        string marker = Path.Combine(directory, "chair-r5e-static-fixture-complete.marker");
        Require(File.Exists(marker) && File.ReadAllText(marker).Contains(
                "complete=true", StringComparison.Ordinal),
            "static postprocessor completion marker missing");
    }

    private static void ValidateActualPacketNegativeFixtures(
        string directory,
        Dictionary<string, string[]> headers)
    {
        string scratch = Path.Combine(
            Path.GetTempPath(),
            "chair-r5e-actual-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string transition = Path.Combine(directory, Schemas[0].FileName);
            string seated = Path.Combine(directory, Schemas[1].FileName);
            string locomotion = Path.Combine(directory, Schemas[2].FileName);

            string missingColumn = Path.Combine(scratch, "missing-column.csv");
            string[] transitionLines = File.ReadAllLines(transition);
            transitionLines[0] = string.Join(",", ParseCsv(transitionLines[0]).Take(252));
            File.WriteAllLines(missingColumn, transitionLines, new UTF8Encoding(false));
            ExpectFailureContains(
                () => ValidateTransitionCsv(missingColumn, headers["TransitionHeader"]),
                "header/order mismatch",
                "actual transition missing column");

            string missingWalk = Path.Combine(scratch, "missing-first-walk.csv");
            File.WriteAllLines(
                missingWalk,
                File.ReadAllLines(transition)
                    .Where(line => !line.Contains(",FirstWalk,", StringComparison.Ordinal)),
                new UTF8Encoding(false));
            ExpectFailureContains(
                () => ValidateTransitionCsv(missingWalk, headers["TransitionHeader"]),
                "lifecycle mismatch",
                "actual transition missing FirstWalk");

            string rollbackMismatch = Path.Combine(scratch, "rollback-hash.csv");
            MutateFirstRow(
                transition,
                rollbackMismatch,
                headers["TransitionHeader"],
                row => Value(row, "event") == "Rollback",
                "actorTransactionSnapshotHashAfter",
                "18446744073709551614");
            ExpectFailureContains(
                () => ValidateTransitionCsv(rollbackMismatch, headers["TransitionHeader"]),
                "rollback was not byte-equivalent",
                "actual rollback snapshot hash");

            string clearMasked = Path.Combine(scratch, "clear-masked.csv");
            MutateFirstRow(
                seated,
                clearMasked,
                headers["SeatedSessionHeader"],
                row => Value(row, "rowKind") == "Sample" && Value(row, "samplePhase") == "PreClear",
                "visibleMotionDebtSeconds",
                "0.25");
            ExpectFailureContains(
                () => ValidateSeatedCsv(clearMasked, headers["SeatedSessionHeader"]),
                "seated stationary invariant",
                "actual pre-clear debt");

            string duplicateRender = Path.Combine(scratch, "duplicate-render.csv");
            string[] locomotionLines = File.ReadAllLines(locomotion);
            string renderLine = locomotionLines.First(line => line.Contains(",Render,", StringComparison.Ordinal));
            File.WriteAllLines(
                duplicateRender,
                locomotionLines.Concat(new[] { renderLine }),
                new UTF8Encoding(false));
            ExpectFailureContains(
                () => ValidateLocomotionCsv(duplicateRender, headers["LocomotionAdapterHeader"]),
                "render row missing/duplicated",
                "actual duplicate render");
        }
        finally
        {
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }
    }

    private static void MutateFirstRow(
        string source,
        string target,
        string[] header,
        Func<Dictionary<string, string>, bool> predicate,
        string column,
        string replacement)
    {
        string[] lines = File.ReadAllLines(source);
        int columnIndex = Array.IndexOf(header, column);
        Require(columnIndex >= 0, "negative mutation column missing: " + column);
        bool changed = false;
        for (var index = 1; index < lines.Length; index++)
        {
            List<string> values = ParseCsv(lines[index]);
            Require(values.Count == header.Length, "negative source row width mismatch");
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var item = 0; item < header.Length; item++) row.Add(header[item], values[item]);
            if (changed || !predicate(row)) continue;
            values[columnIndex] = replacement;
            lines[index] = string.Join(",", values.Select(EscapeCsv));
            changed = true;
        }
        Require(changed, "negative mutation target row missing: " + column);
        File.WriteAllLines(target, lines, new UTF8Encoding(false));
    }

    private static void ValidateTransitionCsv(string path, string[] header)
    {
        List<Dictionary<string, string>> rows = ReadRows(path, header);
        Require(rows.All(row => NonZero(row, "runId") && NonZero(row, "transactionId") &&
                                Value(row, "actorId").Length > 0), "transition stable IDs missing");
        Require(rows.All(row => IsFalse(row, "locomotionSample")),
            "transition event entered locomotion sample denominator");
        Require(rows.All(row => IsTrue(row, "producerCoverageValid") &&
                                ParseInt(row, "droppedRowCount") == 0 &&
                                ParseInt(row, "overflowCount") == 0 &&
                                IsFalse(row, "overflowed") &&
                                ParseLong(row, "gcAllocBytes") == 0 &&
                                ParseLong(row, "traceProducerAllocBytes") == 0 &&
                                ParseInt(row, "forbiddenColliderCount") == 0 &&
                                ParseInt(row, "forbiddenCollider2DCount") == 0 &&
                                ParseInt(row, "forbiddenRigidbodyCount") == 0 &&
                                ParseInt(row, "forbiddenRigidbody2DCount") == 0 &&
                                ParseInt(row, "forbiddenNavMeshAgentCount") == 0 &&
                                IsFalse(row, "observedChairMutation")),
            "transition producer reported allocation/overflow/physics/chair mutation");
        foreach (Dictionary<string, string> row in rows)
        {
            string eventKind = Value(row, "event");
            if (eventKind == "Commit" || eventKind == "Rebase")
            {
                Require(IsTrue(row, "commitSucceeded") && IsTrue(row, "floorValid") &&
                        IsFalse(row, "staticOverlap") && IsFalse(row, "chairOverlap"),
                    "committed atomic placement did not occupy validated clear floor");
                string[] zeroAfter =
                {
                    "velocityAfterX", "velocityAfterY", "motionDebtAfterX", "motionDebtAfterY",
                    "movementBudgetAfter", "actualDisplacementAfterX", "actualDisplacementAfterY",
                    "semanticDisplacementAfterX", "semanticDisplacementAfterY",
                    "accumulatedDisplacementAfterX", "accumulatedDisplacementAfterY",
                    "gaitDistanceAfter", "gaitPhaseAfter"
                };
                Require(zeroAfter.All(column => AbsFloat(row, column) <= 0.000001f) &&
                        ParseInt(row, "walkFrameAfter") == 0,
                    "atomic placement leaked locomotion/gait/debt state");
            }
            if (eventKind == "Rollback")
                Require(Value(row, "actorTransactionSnapshotHashBefore") ==
                        Value(row, "actorTransactionSnapshotHashAfter") &&
                        IsTrue(row, "rollbackSucceeded") && IsFalse(row, "commitSucceeded"),
                    "rollback was not byte-equivalent for actor transaction state");
            if (eventKind == "TurnComplete")
                Require(AbsFloat(row, "turnDisplacement") <= 0.000001f &&
                        AbsFloat(row, "actualDisplacementAfterX") <= 0.000001f &&
                        AbsFloat(row, "actualDisplacementAfterY") <= 0.000001f,
                    "turn-in-place translated the actor");
        }
        foreach (IGrouping<string, Dictionary<string, string>> group in rows.GroupBy(
                     row => Value(row, "runId") + "|" + Value(row, "actorId") + "|" + Value(row, "transactionId")))
        {
            string kind = Value(group.First(), "transitionKind");
            string[] events = group.Select(row => Value(row, "event")).ToArray();
            bool success = events.Contains("Commit", StringComparer.Ordinal);
            RequireLifecycle(events, kind, success);
        }
    }

    private static void ValidateSeatedCsv(string path, string[] header)
    {
        List<Dictionary<string, string>> allRows = ReadRows(path, header);
        List<Dictionary<string, string>> rows = allRows
            .Where(row => Value(row, "rowKind") == "Sample").ToList();
        Require(rows.Count > 0, "seated sample coverage is zero");
        Require(rows.All(row => NonZero(row, "runId") && NonZero(row, "actorStepOrdinal") &&
                                NonZero(row, "runtimeTick") && NonZero(row, "seatedSessionId") &&
                                IsTrue(row, "producerValid") && IsTrue(row, "aggregateUpdated") &&
                                IsFalse(row, "locomotionSample")),
            "seated producer/default-only evidence invalid");

        string[] stationaryScalars =
        {
            "currentVelocityX", "currentVelocityY", "desiredVelocityX", "desiredVelocityY",
            "visibleMotionDebtSeconds", "movementBudgetWorld",
            "actualDisplacementX", "actualDisplacementY",
            "semanticDisplacementX", "semanticDisplacementY",
            "accumulatedDisplacementX", "accumulatedDisplacementY",
            "gaitDistance", "gaitPhase"
        };
        foreach (Dictionary<string, string> row in rows)
        {
            Require(stationaryScalars.All(column => AbsFloat(row, column) <= 0.000001f),
                "seated stationary invariant was nonzero before/post clear");
            Require(ParseInt(row, "walkFrame") == 0, "seated gait frame was nonzero");
            RequirePositionsEqual(row, "logicalRoot", "visualRoot", 0.000001f);
            RequirePositionsEqual(row, "logicalRoot", "visualBaseline", 0.000001f);
            RequirePositionsEqual(row, "logicalRoot", "previousLogical", 0.000001f);
            RequirePositionsEqual(row, "logicalRoot", "previousVisual", 0.000001f);
            RequirePositionsEqual(row, "logicalRoot", "previousWorld", 0.000001f);
            RequirePositionsEqual(row, "logicalRoot", "previousRendered", 0.000001f);
            RequirePositionsEqual(row, "logicalRoot", "occupancyPosition", 0.000001f);
        }

        foreach (IGrouping<string, Dictionary<string, string>> group in rows.GroupBy(
                     row => Value(row, "runId") + "|" + Value(row, "actorId") + "|" + Value(row, "actorStepOrdinal")))
        {
            Dictionary<string, string>[] ordered = group.ToArray();
            RequireSeatedPair(ordered.Select(row => Value(row, "samplePhase")).ToArray(),
                AbsFloat(ordered[0], "visibleMotionDebtSeconds"),
                AbsFloat(ordered[1], "visibleMotionDebtSeconds"));
        }

        List<Dictionary<string, string>> summaries = allRows
            .Where(row => Value(row, "rowKind") == "Summary").ToList();
        Require(summaries.Count > 0, "seated aggregate summary coverage is zero");
        foreach (Dictionary<string, string> row in summaries)
        {
            Require(ParseInt(row, "expectedPreClearSampleCount") > 0 &&
                    ParseInt(row, "expectedPreClearSampleCount") == ParseInt(row, "observedPreClearSampleCount") &&
                    ParseInt(row, "expectedPostClearSampleCount") > 0 &&
                    ParseInt(row, "expectedPostClearSampleCount") == ParseInt(row, "observedPostClearSampleCount") &&
                    ParseInt(row, "expectedStepPairCount") > 0 &&
                    ParseInt(row, "expectedStepPairCount") == ParseInt(row, "observedStepPairCount"),
                "seated expected/observed coverage mismatch");
            string[] zeroCounters =
            {
                "missingPhasePairCount", "duplicatePhasePairCount", "sequenceGapCount",
                "droppedRowCount", "overflowCount", "violationCount", "clearMaskedViolationCount"
            };
            Require(zeroCounters.All(column => ParseInt(row, column) == 0),
                "seated aggregate reported a missing/dropped/overflow/invariant failure");
        }
    }

    private static void RequirePositionsEqual(
        Dictionary<string, string> row,
        string left,
        string right,
        float epsilon)
    {
        Require(Math.Abs(Float(row, left + "X") - Float(row, right + "X")) <= epsilon &&
                Math.Abs(Float(row, left + "Y") - Float(row, right + "Y")) <= epsilon,
            left + " and " + right + " diverged after atomic rebase");
    }

    private static void ValidateLocomotionCsv(string path, string[] header)
    {
        List<Dictionary<string, string>> rows = ReadRows(path, header);
        Require(rows.Count > 0, "locomotion adapter coverage is zero");
        Require(rows.All(row => IsTrue(row, "producerValid") &&
                                ParseInt(row, "droppedRowCount") == 0 &&
                                ParseInt(row, "overflowCount") == 0),
            "locomotion producer invalid/dropped/overflowed");
        foreach (Dictionary<string, string> row in rows.Where(IsActuallyMoving))
        {
            Require(Value(row, "renderedFacing") == Value(row, "quantizedVelocityFacing"),
                "moving facing mismatch");
            Require(Float(row, "forwardDot") >= 0.92f, "moving forward dot below 0.92");
            Require(NonZero(row, "routeGenerationId"), "moving route ID missing");
            Require(ParseInt(row, "wrongFacingCount") == 0 &&
                    ParseInt(row, "strafeCount") == 0 &&
                    ParseInt(row, "frontFacingLateralCount") == 0 &&
                    ParseInt(row, "backwardLookingCount") == 0,
                "moving row reported wrong-facing/strafe/backward presentation");
        }
        foreach (Dictionary<string, string> row in rows.Where(row => Value(row, "rowKind") == "Step"))
        {
            Require(NonZero(row, "actorStepOrdinal") && NonZero(row, "runtimeTick"),
                "step adapter stable IDs missing");
            if (!IsTrue(row, "atomicPlacement"))
                Require(Math.Sqrt(
                            Float(row, "rootDeltaX") * Float(row, "rootDeltaX") +
                            Float(row, "rootDeltaY") * Float(row, "rootDeltaY")) <= 0.099001f,
                    "accepted per-step displacement exceeded e36875 bound");
            if (IsTrue(row, "expectedMoving"))
                Require(IsTrue(row, "observedMoving") && NonZero(row, "movementHandoffId"),
                    "expected moving step was missing its actual producer/handoff");
        }

        foreach (IGrouping<string, Dictionary<string, string>> group in rows.GroupBy(
                     row => Value(row, "runId") + "|" + Value(row, "scenarioId") + "|" +
                            Value(row, "actorId") + "|" + Value(row, "frame")))
        {
            Dictionary<string, string>[] render = group
                .Where(row => Value(row, "rowKind") == "Render").ToArray();
            Require(render.Length == 1, "accepted render row missing/duplicated for actor/frame");
            Dictionary<string, string> accepted = render[0];
            Require(IsTrue(accepted, "renderJoinValid") &&
                    IsTrue(accepted, "renderDisplacementMatchesStepSum") &&
                    IsTrue(accepted, "acceptedTraceOneToOneValid") &&
                    ParseInt(accepted, "duplicateRenderedTraceCount") == 0,
                "render adapter did not join accepted presentation one-to-one");
            float sumX = group.Where(row => Value(row, "rowKind") == "Step" &&
                                           !IsTrue(row, "atomicPlacement"))
                .Sum(row => Float(row, "rootDeltaX"));
            float sumY = group.Where(row => Value(row, "rowKind") == "Step" &&
                                           !IsTrue(row, "atomicPlacement"))
                .Sum(row => Float(row, "rootDeltaY"));
            Require(Math.Abs(sumX - Float(accepted, "renderActualDisplacementX")) <= 0.000001f &&
                    Math.Abs(sumY - Float(accepted, "renderActualDisplacementY")) <= 0.000001f,
                "accepted presentation displacement was not produced by the joined actual steps");
        }

        foreach (IGrouping<string, Dictionary<string, string>> actor in rows.GroupBy(
                     row => Value(row, "runId") + "|" + Value(row, "scenarioId") + "|" + Value(row, "actorId")))
        {
            Dictionary<string, string> final = actor.Last();
            Require(ParseInt(final, "expectedMovingCount") == ParseInt(final, "observedMovingCount") &&
                    ParseInt(final, "missingMovingCount") == 0 &&
                    ParseInt(final, "expectedRenderedTraceCount") ==
                    ParseInt(final, "observedRenderedTraceCount") &&
                    ParseInt(final, "missingRenderedTraceCount") == 0,
                "locomotion expected/observed coverage mismatch");
        }
    }

    private static bool IsActuallyMoving(Dictionary<string, string> row) =>
        Value(row, "rowKind") == "Render" ? IsTrue(row, "renderIsMoving") : IsTrue(row, "observedMoving");

    private static void ValidateVisualMetadataCsv(string path, bool requireReady)
    {
        Require(File.Exists(path), "visual metadata producer file missing");
        string[] expected =
        {
            "schemaVersion", "runId", "scenarioId", "frame", "runtimeTick", "actorId",
            "transactionId", "seatedSessionId", "cleanFrameObserved",
            "evidenceAtlasObserved", "postProcessStatus"
        };
        List<Dictionary<string, string>> rows = ReadRows(path, expected);
        Require(rows.Count > 0 && rows.All(row => NonZero(row, "runId") &&
                                                 NonZero(row, "scenarioId") &&
                                                 Value(row, "actorId").Length > 0 &&
                                                 (Value(row, "postProcessStatus") == "PENDING" ||
                                                  Value(row, "postProcessStatus") == "READY")),
            "visual capture producer hook is unreachable/default-only");
        if (requireReady)
            Require(rows.All(row => IsTrue(row, "cleanFrameObserved") &&
                                    IsTrue(row, "evidenceAtlasObserved") &&
                                    Value(row, "postProcessStatus") == "READY"),
                "runtime PASS retained PENDING/unobserved visual metadata");
    }

    private static void ValidateScenarioResultsCsv(string path)
    {
        Require(File.Exists(path), "production scenario result file missing");
        string[] header =
            { "schemaVersion", "scenarioId", "caseId", "kind", "terminalObserved", "passed", "detail" };
        List<Dictionary<string, string>> rows = ReadRows(path, header);
        Require(rows.Count == 158, "production scenario coverage count is not 158");
        Require(rows.Select(row => Value(row, "scenarioId")).Distinct(StringComparer.Ordinal).Count() == 158 &&
                rows.Select(row => Value(row, "caseId")).Distinct(StringComparer.Ordinal).Count() == 158,
            "production scenario IDs/case IDs are duplicated");
        Require(rows.All(row => IsTrue(row, "terminalObserved") && IsTrue(row, "passed") &&
                                Value(row, "detail").Length > 0),
            "production scenario terminal/oracle failed");
        Require(rows.Count(row => Value(row, "kind") == "BaseMatrix") == 128 &&
                rows.Any(row => Value(row, "kind") == "AllExitsBlocked") &&
                rows.Any(row => Value(row, "kind") == "Contention") &&
                rows.Any(row => Value(row, "kind") == "FaultEntry") &&
                rows.Any(row => Value(row, "kind") == "FaultExit") &&
                rows.Any(row => Value(row, "kind") == "VersionEntry") &&
                rows.Any(row => Value(row, "kind") == "VersionExit"),
            "production scenario family coverage is incomplete");
    }

    private static void ValidateDecodedCsv(string path, string[] header)
    {
        List<Dictionary<string, string>> rows = ReadRows(path, header)
            .Where(row => Value(row, "rowKind") == "ActorDirectionSummary").ToList();
        Require(rows.Count > 0, "decoded summary coverage is zero");
        string[] producers =
        {
            "standWhileMoving", "footOnChair", "descendRise", "bodyPop",
            "chairDeskPenetration", "ghost", "doubleBody", "headTeleport"
        };
        foreach (Dictionary<string, string> row in rows)
        {
            Require(IsTrue(row, "sourceFrameIdentityValid") && IsTrue(row, "frameJoinValid"),
                "decoded source/join invalid");
            Require(ParseInt(row, "expectedFrameSampleCount") > 0 &&
                    ParseInt(row, "expectedFrameSampleCount") == ParseInt(row, "observedFrameSampleCount"),
                "decoded expected/observed coverage mismatch");
            Require(ParseInt(row, "defaultOnlyMask") == 0, "decoded default-only mask nonzero");
            foreach (string producer in producers)
                Require(IsTrue(row, producer + "ProducerValid") &&
                        ParseInt(row, producer + "SampleCount") > 0 &&
                        ParseInt(row, producer + "Count") == 0,
                    producer + " oracle invalid or uncovered");
        }
    }

    private static void ValidateHumanCsv(string path, string[] header)
    {
        List<Dictionary<string, string>> rows = ReadRows(path, header);
        Require(rows.Count > 0 && rows.All(row => IsTrue(row, "normalScale") && IsTrue(row, "pass")),
            "normal-scale human visual gate missing or failed");
        Require(rows.All(row => Value(row, "cleanVideoSha256").Length == 64 &&
                                Value(row, "annotatedVideoSha256").Length == 64 &&
                                Value(row, "decodedOracleSha256").Length == 64),
            "human review artifact identity missing");
    }

    private static void ValidateRuntimeEnvelope(string directory)
    {
        string marker = Path.Combine(directory, "chair-r5e-complete.marker");
        string resultPath = Path.Combine(directory, "chair-r5e-runtime-result.txt");
        string boundariesPath = Path.Combine(directory, "chair-r5e-startup-boundaries.csv");
        string framesPath = Path.Combine(directory, "chair-r5e-performance-frames.csv");
        string manifestPath = Path.Combine(directory, "chair-r5e-runtime-artifact-manifest.tsv");
        Require(File.Exists(marker) && File.ReadAllText(marker).Contains("complete=true", StringComparison.Ordinal),
            "runtime completion marker missing/partial");
        Require(File.Exists(resultPath), "runtime result missing");
        Dictionary<string, string> result = File.ReadAllLines(resultPath)
            .Where(line => line.Contains('='))
            .Select(line => line.Split(new[] { '=' }, 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        Require(result.TryGetValue("status", out string status) && status == "PASS",
            "runtime result did not close PASS");
        Require(result.TryGetValue("legacyClipOracle", out string legacy) && legacy == "unused",
            "legacy clip oracle was used");
        Require(result.TryGetValue("scenarioCatalogSha256", out string catalogHash) && catalogHash.Length == 64,
            "scenario catalog identity missing");

        Require(File.Exists(boundariesPath), "startup boundary trace missing");
        string[] boundaryRows = File.ReadAllLines(boundariesPath).Skip(1).ToArray();
        string[] expected =
            { "ProcessStart", "SessionStart", "RuntimeReady", "PreloadComplete", "GameplayMeasureBegin", "GameplayMeasureEnd" };
        int cursor = -1;
        foreach (string name in expected)
        {
            int found = Array.FindIndex(boundaryRows, cursor + 1,
                row => row.StartsWith(name + ",", StringComparison.Ordinal));
            Require(found > cursor, "startup/gameplay boundary missing or reordered: " + name);
            cursor = found;
        }

        Require(File.Exists(framesPath), "performance frame trace missing");
        string[] frameLines = File.ReadAllLines(framesPath);
        Require(frameLines.Length > 1, "performance frame denominator is zero");
        string[] frameHeader = ParseCsv(frameLines[0]).ToArray();
        RequireColumns(frameHeader, "frame", "frame_ms", "gc_alloc_bytes", "mono_used_bytes",
            "active_body_sprites", "actor_collider_count", "actor_collider2d_count",
            "actor_rigidbody_count", "actor_rigidbody2d_count", "actor_navmeshagent_count",
            "furniture_transform_hash", "furniture_count", "furniture_snapshot_valid",
            "furniture_transform_mutation", "floor_invalid_count", "static_overlap_count",
            "dynamic_overlap_count", "collision_violation_count", "legacy_sit_frame_mask",
            "legacy_stand_frame_mask", "maximum_stuck_seconds", "maximum_seated_debt",
            "maximum_seated_velocity", "maximum_seated_displacement");
        var frameRows = new List<Dictionary<string, string>>();
        for (var index = 1; index < frameLines.Length; index++)
        {
            List<string> values = ParseCsv(frameLines[index]);
            Require(values.Count == frameHeader.Length, "performance frame row width mismatch");
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var column = 0; column < frameHeader.Length; column++) row.Add(frameHeader[column], values[column]);
            frameRows.Add(row);
        }
        float[] milliseconds = frameRows.Select(row => Float(row, "frame_ms")).OrderBy(value => value).ToArray();
        Require(milliseconds.All(value => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f),
            "performance frame contains invalid numeric data");
        float maximum = milliseconds[milliseconds.Length - 1];
        float p95 = milliseconds[(int)Math.Ceiling(milliseconds.Length * 0.95) - 1];
        int timeScale = result.TryGetValue("timeScale", out string scaleText)
            ? int.Parse(scaleText, CultureInfo.InvariantCulture)
            : 0;
        float p95Limit = timeScale == 4 ? 30.788f : 21.594f;
        Require(timeScale == 1 || timeScale == 4, "runtime timeScale identity invalid");
        Require(maximum < 50f && milliseconds.Count(value => value >= 50f) == 0,
            "gameplay >=50ms frame observed");
        Require(p95 <= p95Limit, "p95 " + p95 + " exceeds " + p95Limit);
        Require(frameRows.All(row => ParseInt(row, "active_body_sprites") == 4),
            "active body sprite count changed from four");
        Require(frameRows.All(row => ParseLong(row, "gc_alloc_bytes") == 0),
            "gameplay allocation observed");
        Require(frameRows.All(row => IsTrue(row, "furniture_snapshot_valid") &&
                                     IsFalse(row, "furniture_transform_mutation") &&
                                     NonZero(row, "furniture_transform_hash") &&
                                     ParseInt(row, "furniture_count") > 0),
            "furniture transform producer missing or mutation observed");
        string[] runtimeZeroCounters =
        {
            "floor_invalid_count", "static_overlap_count", "dynamic_overlap_count",
            "collision_violation_count", "legacy_sit_frame_mask", "legacy_stand_frame_mask"
        };
        Require(frameRows.All(row => runtimeZeroCounters.All(column => ParseInt(row, column) == 0) &&
                                     AbsFloat(row, "maximum_stuck_seconds") <= 0.000001f &&
                                     AbsFloat(row, "maximum_seated_debt") <= 0.000001f &&
                                     AbsFloat(row, "maximum_seated_velocity") <= 0.000001f &&
                                     AbsFloat(row, "maximum_seated_displacement") <= 0.000001f),
            "runtime collision/debt/legacy transition invariant failed");
        string[] forbidden =
        {
            "actor_collider_count", "actor_collider2d_count", "actor_rigidbody_count",
            "actor_rigidbody2d_count", "actor_navmeshagent_count"
        };
        Require(frameRows.All(row => forbidden.All(column => ParseInt(row, column) == 0)),
            "forbidden actor physics/navigation component observed");

        Require(File.Exists(manifestPath), "runtime payload manifest missing");
        foreach (string line in File.ReadAllLines(manifestPath).Skip(1))
        {
            string[] parts = line.Split('\t');
            Require(parts.Length == 3, "runtime manifest row malformed");
            string payload = Path.Combine(directory, parts[0]);
            Require(File.Exists(payload), "runtime manifest payload missing: " + parts[0]);
            Require(new FileInfo(payload).Length == long.Parse(parts[1], CultureInfo.InvariantCulture),
                "runtime manifest length mismatch: " + parts[0]);
            Require(string.Equals(Sha256File(payload), parts[2], StringComparison.OrdinalIgnoreCase),
                "runtime manifest hash mismatch: " + parts[0]);
        }

        string[] cleanVideos = Directory.GetFiles(directory, "*clean*.mp4");
        string[] annotatedVideos = Directory.GetFiles(directory, "*annotated*.mp4");
        Require(cleanVideos.Any(path => new FileInfo(path).Length > 1024),
            "normal-scale clean MP4 missing/empty");
        Require(annotatedVideos.Any(path => new FileInfo(path).Length > 1024),
            "normal-scale annotated MP4 missing/empty");
        Require(Directory.GetFiles(directory, "*ffprobe*.json").Length > 0,
            "decoded MP4 ffprobe identity missing");
    }

    private static void RequireLifecycle(string[] events, string kind, bool success)
    {
        string[] expected = !success
            ? new[] { "Prepare", "Rollback" }
            : kind == "Entry"
                ? new[] { "Prepare", "Commit", "Rebase" }
                : new[] { "Prepare", "Commit", "Rebase", "TurnComplete", "FirstWalk" };
        Require(events.SequenceEqual(expected, StringComparer.Ordinal),
            kind + " lifecycle mismatch: " + string.Join("->", events));
    }

    private static void RequireSeatedPair(string[] phases, float preClearMagnitude, float postClearMagnitude)
    {
        Require(phases.Length == 2 && phases[0] == "PreClear" && phases[1] == "PostClear",
            "seated phase pair missing/duplicate/reordered");
        Require(preClearMagnitude <= 0.000001f, "seated invariant already nonzero before clear");
        Require(!(preClearMagnitude > 0.000001f && postClearMagnitude <= 0.000001f),
            "ClearInactiveVisibleMotionDebt masked a seated violation");
    }

    private static AtomicModel PublishExit(
        AtomicModel before,
        bool floorValid,
        bool staticOverlap,
        int chairVersion,
        int commitVersion,
        int faultPoint)
    {
        if (!floorValid || staticOverlap || chairVersion != commitVersion || faultPoint != 0)
            return before;
        return new AtomicModel("LeavingSeat", "released", "exit", commitVersion, 0, 0, 0, 0);
    }

    private static List<Dictionary<string, string>> ReadRows(string path, string[] header)
    {
        var result = new List<Dictionary<string, string>>();
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        string first = reader.ReadLine() ?? string.Empty;
        Require(ParseCsv(first).SequenceEqual(header, StringComparer.Ordinal),
            Path.GetFileName(path) + " header/order mismatch");
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            List<string> values = ParseCsv(line);
            Require(values.Count == header.Length, Path.GetFileName(path) + " row width mismatch");
            var row = new Dictionary<string, string>(header.Length, StringComparer.Ordinal);
            for (int index = 0; index < header.Length; index++) row.Add(header[index], values[index]);
            result.Add(row);
        }
        return result;
    }

    private static List<string> ParseCsv(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char current = line[index];
            if (current == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (current == ',' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else value.Append(current);
        }
        Require(!quoted, "unterminated CSV quote");
        values.Add(value.ToString());
        return values;
    }

    private static string EscapeCsv(string value)
    {
        string text = value ?? string.Empty;
        return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
            ? text
            : "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static string ExtractLiteral(string source, string constantName)
    {
        Match declaration = Regex.Match(source,
            "public\\s+const\\s+string\\s+" + Regex.Escape(constantName) + "\\s*=\\s*(?<body>[\\s\\S]*?);");
        Require(declaration.Success, "schema constant missing: " + constantName);
        var builder = new StringBuilder();
        foreach (Match literal in Regex.Matches(declaration.Groups["body"].Value,
                     "\"(?<value>(?:[^\"\\\\]|\\\\.)*)\""))
            builder.Append(Regex.Unescape(literal.Groups["value"].Value));
        Require(builder.Length > 0, "schema constant has no literal content: " + constantName);
        return builder.ToString();
    }

    private static int ExtractIntConstant(string source, string constantName)
    {
        Match match = Regex.Match(source,
            "public\\s+const\\s+int\\s+" + Regex.Escape(constantName) + "\\s*=\\s*(?<value>[0-9]+)\\s*;");
        Require(match.Success, "capacity constant missing: " + constantName);
        return int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
    }

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Require(start >= 0, "method signature missing: " + signature);
        int open = source.IndexOf('{', start);
        Require(open >= 0, "method body missing: " + signature);
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return source.Substring(start, index - start + 1);
        }
        throw new InvalidOperationException("unterminated method: " + signature);
    }

    private static void AssertOrdered(string text, params string[] tokens)
    {
        int cursor = -1;
        foreach (string token in tokens)
        {
            int index = text.IndexOf(token, cursor + 1, StringComparison.Ordinal);
            Require(index >= 0, "ordered source token missing: " + token);
            Require(index > cursor, "source token out of order: " + token);
            cursor = index;
        }
    }

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int cursor = 0;
        while ((cursor = text.IndexOf(token, cursor, StringComparison.Ordinal)) >= 0)
        {
            count++;
            cursor += token.Length;
        }
        return count;
    }

    private static void RequireColumns(string[] actual, params string[] required)
    {
        foreach (string column in required)
            Require(actual.Contains(column, StringComparer.Ordinal), "required column missing: " + column);
    }

    private static void RequireHeader(string[] columns, int expected)
    {
        Require(columns.Length == expected, "header count mismatch");
        Require(columns.Distinct(StringComparer.Ordinal).Count() == columns.Length, "duplicate header");
    }

    private static string Read(string root, string relative)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Require(File.Exists(path), "source file missing: " + path);
        return File.ReadAllText(path);
    }

    private static string Sha256File(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
    }

    private static string Value(Dictionary<string, string> row, string name) =>
        row.TryGetValue(name, out string value) ? value : throw new InvalidOperationException("column missing: " + name);

    private static bool IsTrue(Dictionary<string, string> row, string name) =>
        string.Equals(Value(row, name), "true", StringComparison.OrdinalIgnoreCase) || Value(row, name) == "1";

    private static bool IsFalse(Dictionary<string, string> row, string name) =>
        string.Equals(Value(row, name), "false", StringComparison.OrdinalIgnoreCase) || Value(row, name) == "0";

    private static bool NonZero(Dictionary<string, string> row, string name) =>
        ulong.TryParse(Value(row, name), NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) && value != 0;

    private static int ParseInt(Dictionary<string, string> row, string name) =>
        int.Parse(Value(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static long ParseLong(Dictionary<string, string> row, string name) =>
        long.Parse(Value(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static float Float(Dictionary<string, string> row, string name) =>
        float.Parse(Value(row, name), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static float AbsFloat(Dictionary<string, string> row, string name) => Math.Abs(Float(row, name));

    private static void ExpectFailure(Action action, string name)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("negative fixture unexpectedly passed: " + name);
    }

    private static void ExpectFailureContains(Action action, string expected, string name)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            if (exception.Message.Contains(expected, StringComparison.Ordinal)) return;
            throw new InvalidOperationException(
                "negative fixture failed at the wrong oracle: " + name +
                " expected='" + expected + "' actual='" + exception.Message + "'",
                exception);
        }
        throw new InvalidOperationException("negative fixture unexpectedly passed: " + name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private readonly struct SchemaContract
    {
        public SchemaContract(string constantName, string fileName, int expectedColumns)
        {
            ConstantName = constantName;
            FileName = fileName;
            ExpectedColumns = expectedColumns;
        }

        public string ConstantName { get; }
        public string FileName { get; }
        public int ExpectedColumns { get; }
    }

    private readonly struct AtomicModel : IEquatable<AtomicModel>
    {
        public AtomicModel(string state, string claim, string occupancy, int version,
            int velocity, int debt, int budget, int gait)
        {
            State = state;
            Claim = claim;
            Occupancy = occupancy;
            Version = version;
            Velocity = velocity;
            Debt = debt;
            Budget = budget;
            Gait = gait;
        }

        public string State { get; }
        public string Claim { get; }
        public string Occupancy { get; }
        public int Version { get; }
        public int Velocity { get; }
        public int Debt { get; }
        public int Budget { get; }
        public int Gait { get; }

        public bool Equals(AtomicModel other) =>
            State == other.State && Claim == other.Claim && Occupancy == other.Occupancy &&
            Version == other.Version && Velocity == other.Velocity && Debt == other.Debt &&
            Budget == other.Budget && Gait == other.Gait;
    }
}
