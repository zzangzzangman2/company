using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.Leisure
{
    public enum LeisureAudioCueRole
    {
        Enter = 0,
        Loop = 1,
        Complete = 2
    }

    public enum LeisureAudioChannel
    {
        Bgm = 0,
        Sfx = 1
    }

    public enum LeisureAudioPace
    {
        Quiet = 0,
        Active = 1
    }

    public sealed class LeisureAudioCueDefinition
    {
        public LeisureAudioCueDefinition(
            string cueId,
            LeisureAudioCueRole role,
            LeisureAudioChannel channel,
            string clipId,
            float volumeScale,
            bool repeats,
            float transitionFadeSeconds)
        {
            if (string.IsNullOrWhiteSpace(cueId))
                throw new ArgumentException("Cue ID is required.", nameof(cueId));
            if (!Enum.IsDefined(typeof(LeisureAudioCueRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (!Enum.IsDefined(typeof(LeisureAudioChannel), channel))
                throw new ArgumentOutOfRangeException(nameof(channel));
            if (string.IsNullOrWhiteSpace(clipId))
                throw new ArgumentException("Clip ID is required.", nameof(clipId));
            if (float.IsNaN(volumeScale) || float.IsInfinity(volumeScale) ||
                volumeScale <= 0f || volumeScale > 1f)
                throw new ArgumentOutOfRangeException(nameof(volumeScale));
            if (float.IsNaN(transitionFadeSeconds) || float.IsInfinity(transitionFadeSeconds) ||
                transitionFadeSeconds < 0f || transitionFadeSeconds > 5f)
                throw new ArgumentOutOfRangeException(nameof(transitionFadeSeconds));

            CueId = cueId.Trim();
            Role = role;
            Channel = channel;
            ClipId = clipId.Trim();
            VolumeScale = volumeScale;
            Repeats = repeats;
            TransitionFadeSeconds = transitionFadeSeconds;
        }

        public string CueId { get; }
        public LeisureAudioCueRole Role { get; }
        public LeisureAudioChannel Channel { get; }
        public string ClipId { get; }
        public float VolumeScale { get; }
        public bool Repeats { get; }

        // Passed to the presentation layer when starting or replacing this cue.
        // One-shot SFX use zero; looping BGM uses a perceptual cross-fade duration.
        public float TransitionFadeSeconds { get; }
    }

    public sealed class LeisureActivityAudioDefinition
    {
        private readonly IReadOnlyList<LeisureAudioCueDefinition> _cues;

        public LeisureActivityAudioDefinition(
            string activityId,
            string sceneId,
            LeisureAudioPace pace,
            LeisureAudioCueDefinition enterCue,
            LeisureAudioCueDefinition loopCue,
            LeisureAudioCueDefinition completeCue)
        {
            if (string.IsNullOrWhiteSpace(activityId))
                throw new ArgumentException("Activity ID is required.", nameof(activityId));
            if (string.IsNullOrWhiteSpace(sceneId))
                throw new ArgumentException("Scene ID is required.", nameof(sceneId));
            if (!Enum.IsDefined(typeof(LeisureAudioPace), pace))
                throw new ArgumentOutOfRangeException(nameof(pace));

            ActivityId = activityId.Trim();
            SceneId = sceneId.Trim();
            Pace = pace;
            EnterCue = enterCue ?? throw new ArgumentNullException(nameof(enterCue));
            LoopCue = loopCue ?? throw new ArgumentNullException(nameof(loopCue));
            CompleteCue = completeCue ?? throw new ArgumentNullException(nameof(completeCue));
            _cues = Array.AsReadOnly(new[] { EnterCue, LoopCue, CompleteCue });
        }

        public string ActivityId { get; }

        // ImageGen scene assets use the same stable ID for a strict one-to-one join.
        public string SceneId { get; }

        public LeisureAudioPace Pace { get; }
        public LeisureAudioCueDefinition EnterCue { get; }
        public LeisureAudioCueDefinition LoopCue { get; }
        public LeisureAudioCueDefinition CompleteCue { get; }
        public IReadOnlyList<LeisureAudioCueDefinition> Cues => _cues;
    }

    public static class LeisureAudioCueCatalog
    {
        private static readonly IReadOnlyList<LeisureActivityAudioDefinition> Definitions =
            Array.AsReadOnly(new[]
            {
                Active(
                    "convenience_store_snack_run",
                    "door_open", 0.30f,
                    "market_portside_cafe", 0.44f, 0.75f,
                    "coins", 0.34f),
                Active(
                    "pc_bang_team_match",
                    "crt_glitch", 0.34f,
                    "action_strategy", 0.54f, 0.55f,
                    "crowd_victory", 0.45f),
                Quiet(
                    "video_tape_rental_night",
                    "crt_glitch", 0.22f,
                    "story_hesitation", 0.30f, 1.60f,
                    "book_close", 0.28f),
                Quiet(
                    "comic_book_rental_stack",
                    "book_open", 0.30f,
                    "relationship_raindrop", 0.28f, 1.60f,
                    "book_close", 0.26f),
                Quiet(
                    "neighborhood_public_bath",
                    "door_open", 0.24f,
                    "hub_verdure", 0.32f, 1.80f,
                    "door_close", 0.22f),
                Active(
                    "family_restaurant_dinner",
                    "door_open", 0.32f,
                    "market_portside_cafe", 0.46f, 0.70f,
                    "coins", 0.38f),
                Quiet(
                    "neighborhood_evening_walk",
                    "footstep_1", 0.18f,
                    "relationship_raindrop", 0.30f, 1.80f,
                    "footstep_2", 0.18f),
                Quiet(
                    "riverside_picnic",
                    "paper_rustle", 0.22f,
                    "hub_verdure", 0.34f, 1.70f,
                    "paper_place", 0.20f),
                Active(
                    "stationery_arcade_break",
                    "coins", 0.38f,
                    "casino_taisho", 0.50f, 0.55f,
                    "ui_confirm", 0.40f),
                Quiet(
                    "home_radio_snack_chat",
                    "ui_switch", 0.18f,
                    "hub_gentle_brew", 0.31f, 1.50f,
                    "ui_close", 0.16f),
                Active(
                    "family_singing_room",
                    "door_open", 0.34f,
                    "casino_taisho", 0.52f, 0.50f,
                    "crowd_victory", 0.50f),
                Active(
                    "adsl_coop_game_night",
                    "crt_glitch", 0.36f,
                    "action_strategy", 0.55f, 0.50f,
                    "ui_confirm", 0.44f)
            });

        private static readonly IReadOnlyDictionary<string, LeisureActivityAudioDefinition> ByActivityId =
            BuildIndex();

        public static IReadOnlyList<LeisureActivityAudioDefinition> All => Definitions;

        public static LeisureActivityAudioDefinition FindByActivityId(string activityId)
        {
            if (string.IsNullOrWhiteSpace(activityId))
            {
                return null;
            }

            ByActivityId.TryGetValue(activityId.Trim(), out var definition);
            return definition;
        }

        private static LeisureActivityAudioDefinition Quiet(
            string activityId,
            string enterClipId,
            float enterVolume,
            string loopClipId,
            float loopVolume,
            float loopFadeSeconds,
            string completeClipId,
            float completeVolume)
        {
            return Define(
                activityId,
                LeisureAudioPace.Quiet,
                enterClipId,
                enterVolume,
                loopClipId,
                loopVolume,
                loopFadeSeconds,
                completeClipId,
                completeVolume);
        }

        private static LeisureActivityAudioDefinition Active(
            string activityId,
            string enterClipId,
            float enterVolume,
            string loopClipId,
            float loopVolume,
            float loopFadeSeconds,
            string completeClipId,
            float completeVolume)
        {
            return Define(
                activityId,
                LeisureAudioPace.Active,
                enterClipId,
                enterVolume,
                loopClipId,
                loopVolume,
                loopFadeSeconds,
                completeClipId,
                completeVolume);
        }

        private static LeisureActivityAudioDefinition Define(
            string activityId,
            LeisureAudioPace pace,
            string enterClipId,
            float enterVolume,
            string loopClipId,
            float loopVolume,
            float loopFadeSeconds,
            string completeClipId,
            float completeVolume)
        {
            return new LeisureActivityAudioDefinition(
                activityId,
                activityId,
                pace,
                Cue(activityId, LeisureAudioCueRole.Enter, LeisureAudioChannel.Sfx, enterClipId, enterVolume, false, 0f),
                Cue(activityId, LeisureAudioCueRole.Loop, LeisureAudioChannel.Bgm, loopClipId, loopVolume, true, loopFadeSeconds),
                Cue(activityId, LeisureAudioCueRole.Complete, LeisureAudioChannel.Sfx, completeClipId, completeVolume, false, 0f));
        }

        private static LeisureAudioCueDefinition Cue(
            string activityId,
            LeisureAudioCueRole role,
            LeisureAudioChannel channel,
            string clipId,
            float volume,
            bool repeats,
            float fadeSeconds)
        {
            return new LeisureAudioCueDefinition(
                activityId + ":" + role.ToString().ToLowerInvariant(),
                role,
                channel,
                clipId,
                volume,
                repeats,
                fadeSeconds);
        }

        private static IReadOnlyDictionary<string, LeisureActivityAudioDefinition> BuildIndex()
        {
            var index = new Dictionary<string, LeisureActivityAudioDefinition>(StringComparer.Ordinal);
            for (var definitionIndex = 0; definitionIndex < Definitions.Count; definitionIndex++)
            {
                var definition = Definitions[definitionIndex];
                index.Add(definition.ActivityId, definition);
            }

            return index;
        }
    }
}
