using System;

namespace FamilyCompany.Presentation.Unity
{
    public enum OfficeSoundscapeZone
    {
        Unknown = 0,
        Inside = 1,
        Outside = 2
    }

    public enum OfficeSoundscapeStage
    {
        None = 0,
        Walking = 1,
        Reception = 2,
        Work = 3,
        Printing = 4,
        Meeting = 5,
        Break = 6,
        Outside = 7
    }

    [Flags]
    public enum OfficeSoundCue
    {
        None = 0,
        DoorOpen = 1 << 0,
        DoorClose = 1 << 1,
        Paper = 1 << 2,
        ContractAmbient = 1 << 3
    }

    /// <summary>
    /// Unity-free input for the office soundscape transition rules.
    /// A default observation has an Unknown zone and intentionally produces no cue.
    /// </summary>
    public readonly struct OfficeSoundscapeObservation
    {
        public OfficeSoundscapeObservation(
            OfficeSoundscapeZone zone,
            OfficeSoundscapeStage stage,
            bool isContractTask,
            string taskId,
            string targetId)
        {
            Zone = zone;
            Stage = stage;
            IsContractTask = isContractTask;
            TaskId = taskId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
        }

        public OfficeSoundscapeZone Zone { get; }
        public OfficeSoundscapeStage Stage { get; }
        public bool IsContractTask { get; }
        public string TaskId { get; }
        public string TargetId { get; }
    }

    /// <summary>
    /// Pure transition rules. This type depends only on System and can be compiled
    /// and exercised without loading Unity.
    /// </summary>
    public static class OfficeSoundscapeTransitionRules
    {
        private const OfficeSoundCue DoorSequence = OfficeSoundCue.DoorOpen | OfficeSoundCue.DoorClose;

        public static OfficeSoundCue Resolve(
            OfficeSoundscapeObservation previous,
            OfficeSoundscapeObservation current)
        {
            if (previous.Zone == OfficeSoundscapeZone.Unknown ||
                current.Zone == OfficeSoundscapeZone.Unknown)
            {
                return OfficeSoundCue.None;
            }

            if (previous.Zone != current.Zone)
            {
                return DoorSequence;
            }

            if (current.Zone == OfficeSoundscapeZone.Outside || !IsArrival(previous, current))
            {
                return OfficeSoundCue.None;
            }

            var cues = OfficeSoundCue.None;
            if (current.Stage == OfficeSoundscapeStage.Printing)
            {
                cues |= OfficeSoundCue.Paper;
            }

            if (current.IsContractTask &&
                (current.Stage == OfficeSoundscapeStage.Work ||
                 current.Stage == OfficeSoundscapeStage.Meeting))
            {
                cues |= OfficeSoundCue.ContractAmbient;
            }

            return cues;
        }

        private static bool IsArrival(
            OfficeSoundscapeObservation previous,
            OfficeSoundscapeObservation current)
        {
            if (!IsAudibleDestination(current.Stage))
            {
                return false;
            }

            if (previous.Stage == OfficeSoundscapeStage.Walking || previous.Stage != current.Stage)
            {
                return true;
            }

            return !string.Equals(previous.TaskId, current.TaskId, StringComparison.Ordinal) ||
                   !string.Equals(previous.TargetId, current.TargetId, StringComparison.Ordinal);
        }

        private static bool IsAudibleDestination(OfficeSoundscapeStage stage)
        {
            return stage == OfficeSoundscapeStage.Work ||
                   stage == OfficeSoundscapeStage.Printing ||
                   stage == OfficeSoundscapeStage.Meeting;
        }
    }
}

#if UNITY_5_3_OR_NEWER
namespace FamilyCompany.Presentation.Unity
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class OfficeSoundscapeController : MonoBehaviour
    {
        private const float ScanIntervalSeconds = 0.08f;
        private const float DoorSequenceCooldownSeconds = 0.75f;
        private const float PaperCooldownSeconds = 0.55f;
        private const float AmbientCooldownSeconds = 1.25f;
        private const float DoorCloseDelaySeconds = 0.22f;

        private static OfficeSoundscapeController _instance;

        private readonly Dictionary<string, AgentRecord> _records =
            new Dictionary<string, AgentRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> _seenAgentIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _staleAgentIds = new List<string>();

        private float _scanCountdown;
        private float _doorReadyAt;
        private float _paperReadyAt;
        private float _ambientReadyAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCreated()
        {
            if (_instance != null)
            {
                return;
            }

            var gameObject = new GameObject("[FamilyCompany] Office Soundscape");
            gameObject.AddComponent<OfficeSoundscapeController>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _scanCountdown = 0f;
        }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            _scanCountdown -= Time.deltaTime;
            if (_scanCountdown > 0f)
            {
                return;
            }

            _scanCountdown = ScanIntervalSeconds;
            PollAgents();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void PollAgents()
        {
            var agents = FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None);
            Array.Sort(agents, CompareAgents);

            _seenAgentIds.Clear();
            for (var index = 0; index < agents.Length; index++)
            {
                var agent = agents[index];
                if (agent == null || !agent.isActiveAndEnabled)
                {
                    continue;
                }

                var agentId = agent.AgentId ?? string.Empty;
                if (agentId.Length == 0 || !_seenAgentIds.Add(agentId))
                {
                    continue;
                }

                var current = Observe(agent);
                if (!_records.TryGetValue(agentId, out var record) || record.Agent != agent)
                {
                    // First sight and object replacement seed state silently. This avoids
                    // replay storms after scene loads, domain reloads, or NPC recreation.
                    _records[agentId] = new AgentRecord(agent, current);
                    continue;
                }

                var cues = OfficeSoundscapeTransitionRules.Resolve(record.Observation, current);
                record.Observation = current;
                PlayCues(cues, current.Stage);
            }

            RemoveStaleRecords();
        }

        private void PlayCues(OfficeSoundCue cues, OfficeSoundscapeStage stage)
        {
            if ((cues & (OfficeSoundCue.DoorOpen | OfficeSoundCue.DoorClose)) != 0)
            {
                TryPlayDoorSequence();
            }

            if ((cues & OfficeSoundCue.Paper) != 0)
            {
                TryPlayPaper();
            }

            if ((cues & OfficeSoundCue.ContractAmbient) != 0)
            {
                TryPlayContractAmbient(stage);
            }
        }

        private void TryPlayDoorSequence()
        {
            var now = Time.unscaledTime;
            if (now < _doorReadyAt)
            {
                return;
            }

            _doorReadyAt = now + DoorSequenceCooldownSeconds;
            GameAudioCoordinator.Instance.PlaySfx("door_open", 0.28f);
            StartCoroutine(PlayDoorCloseAfterDelay());
        }

        private IEnumerator PlayDoorCloseAfterDelay()
        {
            yield return new WaitForSecondsRealtime(DoorCloseDelaySeconds);
            GameAudioCoordinator.Instance.PlaySfx("door_close", 0.26f);
        }

        private void TryPlayPaper()
        {
            var now = Time.unscaledTime;
            if (now < _paperReadyAt)
            {
                return;
            }

            _paperReadyAt = now + PaperCooldownSeconds;
            GameAudioCoordinator.Instance.PlaySfx("paper_place", 0.24f);
        }

        private void TryPlayContractAmbient(OfficeSoundscapeStage stage)
        {
            var now = Time.unscaledTime;
            if (now < _ambientReadyAt)
            {
                return;
            }

            _ambientReadyAt = now + AmbientCooldownSeconds;
            if (stage == OfficeSoundscapeStage.Meeting)
            {
                GameAudioCoordinator.Instance.PlaySfx("paper_rustle", 0.12f);
                return;
            }

            GameAudioCoordinator.Instance.PlaySfx("crt_glitch", 0.09f);
        }

        private void RemoveStaleRecords()
        {
            _staleAgentIds.Clear();
            foreach (var pair in _records)
            {
                if (!_seenAgentIds.Contains(pair.Key))
                {
                    _staleAgentIds.Add(pair.Key);
                }
            }

            for (var index = 0; index < _staleAgentIds.Count; index++)
            {
                _records.Remove(_staleAgentIds[index]);
            }
        }

        private static OfficeSoundscapeObservation Observe(OfficeWorkerAgent agent)
        {
            var activity = agent.CurrentActivity;
            var target = agent.TargetWaypoint;
            return new OfficeSoundscapeObservation(
                activity == OfficeActivity.Outside
                    ? OfficeSoundscapeZone.Outside
                    : OfficeSoundscapeZone.Inside,
                MapStage(activity),
                agent.HasAssignedTask,
                agent.AssignedTaskId,
                target != null ? target.WaypointId : string.Empty);
        }

        private static OfficeSoundscapeStage MapStage(OfficeActivity activity)
        {
            switch (activity)
            {
                case OfficeActivity.Walking: return OfficeSoundscapeStage.Walking;
                case OfficeActivity.Reception: return OfficeSoundscapeStage.Reception;
                case OfficeActivity.Work: return OfficeSoundscapeStage.Work;
                case OfficeActivity.Printing: return OfficeSoundscapeStage.Printing;
                case OfficeActivity.Meeting: return OfficeSoundscapeStage.Meeting;
                case OfficeActivity.Break: return OfficeSoundscapeStage.Break;
                case OfficeActivity.Outside: return OfficeSoundscapeStage.Outside;
                default: return OfficeSoundscapeStage.None;
            }
        }

        private static int CompareAgents(OfficeWorkerAgent left, OfficeWorkerAgent right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private sealed class AgentRecord
        {
            public AgentRecord(OfficeWorkerAgent agent, OfficeSoundscapeObservation observation)
            {
                Agent = agent;
                Observation = observation;
            }

            public OfficeWorkerAgent Agent { get; }
            public OfficeSoundscapeObservation Observation { get; set; }
        }
    }
}
#endif
