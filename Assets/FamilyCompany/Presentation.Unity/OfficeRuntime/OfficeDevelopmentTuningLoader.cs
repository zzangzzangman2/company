using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FamilyCompany.Simulation.Navigation;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [DefaultExecutionOrder(-20000)]
    public sealed class OfficeDevelopmentTuningLoader : MonoBehaviour
    {
        public const string Flag = "-familyCompanyDevSettings";
        private string path;
        private string lastInput;
        private float nextPoll;

        [Serializable]
        private sealed class Input
        {
            public int schemaVersion;
            public float moveSpeed = float.NaN, strideOfficeUnits = float.NaN, phaseOffsetCycles = float.NaN;
            public float playerFootOffsetX = float.NaN, playerFootOffsetZ = float.NaN;
            public float fatherFootOffsetX = float.NaN, fatherFootOffsetZ = float.NaN;
            public long workstationPriceWon = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => OfficeDevelopmentTuningSession.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0) return;
            // The ordinary Release player cannot enable experimental settings through a flag.
            bool qaPlayer = Path.GetFileName(Application.dataPath) == "FamilyCompany_FastQa_Data";
            if ((!Application.isEditor && !Debug.isDebugBuild && !qaPlayer) || index + 1 >= args.Length)
            { Debug.LogWarning("FAMILY_DEV_SETTINGS: ignored outside Editor/Development/FastQa player."); return; }
            string inputPath = Path.GetFullPath(args[index + 1]);
            var host = new GameObject("~OfficeDevelopmentTuningLoader");
            DontDestroyOnLoad(host);
            var loader = host.AddComponent<OfficeDevelopmentTuningLoader>();
            loader.path = inputPath;
            loader.Poll();
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + 0.5f;
            Poll();
        }

        private void Poll()
        {
            try
            {
                if (!File.Exists(path))
                {
                    if (lastInput != null) { OfficeDevelopmentTuningSession.Clear(); lastInput = null; }
                    return;
                }
                if (new FileInfo(path).Length > 16384) throw new InvalidDataException("JSON exceeds 16 KiB.");
                string json = File.ReadAllText(path);
                if (json == lastInput) return;
                lastInput = json;
                var names = new HashSet<string>(StringComparer.Ordinal)
                { "schemaVersion", "moveSpeed", "strideOfficeUnits", "phaseOffsetCycles", "playerFootOffsetX",
                  "playerFootOffsetZ", "fatherFootOffsetX", "fatherFootOffsetZ", "workstationPriceWon" };
                foreach (Match match in Regex.Matches(json, "\"([^\"]+)\"\\s*:"))
                    if (!names.Remove(match.Groups[1].Value)) throw new InvalidDataException("Unknown or duplicate setting.");
                if (names.Count != 0) throw new InvalidDataException("Missing developer setting.");
                var input = new Input();
                JsonUtility.FromJsonOverwrite(json, input);
                if (input.schemaVersion != 1) throw new InvalidDataException("Unsupported settings schema.");
                var snapshot = new OfficeDevelopmentTuning(input.moveSpeed, input.strideOfficeUnits,
                    input.phaseOffsetCycles, input.playerFootOffsetX, input.playerFootOffsetZ,
                    input.fatherFootOffsetX, input.fatherFootOffsetZ, input.workstationPriceWon);
                OfficeDevelopmentTuningSession.Apply(snapshot);
                Debug.Log("FAMILY_DEV_SETTINGS: APPLIED revision=" + OfficeDevelopmentTuningSession.Revision +
                    " speed=" + snapshot.MoveSpeed + " stride=" + snapshot.Stride + " setPrice=" + snapshot.WorkstationPriceWon);
            }
            catch (Exception e)
            {
                // Partial/invalid edits retain the entire last valid snapshot, never half-apply.
                Debug.LogWarning("FAMILY_DEV_SETTINGS: REJECTED; keeping last valid settings: " + e.Message);
            }
        }
    }
}
