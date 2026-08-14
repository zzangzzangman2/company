using System;
using System.Collections;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.Qa
{
    public sealed class FastQaPlayerStartupProbe : MonoBehaviour
    {
        private const string Argument = "-familyCompanyFastQaStartupProbe";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallIfRequested()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Argument) < 0) return;
            new GameObject("~FastQaPlayerStartupProbe").AddComponent<FastQaPlayerStartupProbe>();
        }

        private IEnumerator Start()
        {
            yield return null;
            Debug.Log(
                "FAST_QA_PLAYER_STARTUP: PASS | " +
                $"unity={Application.unityVersion} graphics={SystemInfo.graphicsDeviceType} " +
                $"device={SystemInfo.graphicsDeviceName} frame={Time.frameCount}");
            yield return null;
            Application.Quit(0);
        }
    }
}
