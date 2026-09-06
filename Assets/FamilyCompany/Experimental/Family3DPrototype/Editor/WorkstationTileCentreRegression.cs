using System;
using System.Collections.Generic;
using System.IO;
using FamilyCompany.Runtime.Character3D;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    // Independent geometry assertions; not a gameplay or release approval.
    public static class WorkstationTileCentreRegression
    {
        [Serializable] private class Sample
        {
            public int basis, turn;
            public float chairError, stemError, keyboardLateral, screenLateral, screenNormalError;
        }
        [Serializable] private class Result
        {
            public string scope = "geometry only; gameplay and release remain separate";
            public Sample[] samples;
        }
        public static void RunBatch()
        {
            var samples = new List<Sample>();
            try
            {
                for (int basis = 0; basis < 2; basis++)
                for (int turn = 0; turn < 4; turn++)
                {
                    Vector3 r = basis == 0 ? Vector3.right : new Vector3(0.81649658f, 0, 0.57735027f);
                    Vector3 f = basis == 0 ? Vector3.forward : new Vector3(-0.81649658f, 0, 0.57735027f);
                    for (int i = 0; i < turn; i++) { Vector3 old = r; r = -f; f = old; }
                    float tile = basis == 0 ? 1.07996454f : 0.93485f;
                    float h = basis == 0 ? 1.53951067f : Family3DProductionPresenter.PlayerApprovedTargetHeight;
                    var root = new GameObject("TileCentreOracle");
                    Vector3 centre = new Vector3(3.7f, 0, -2.1f);
                    try
                    {
                        var desk = Family3DWorkstation.Create(root.transform, 30, "oracle", centre, r, f,
                            centre + (-0.5f * r + f) * tile, 2 * tile, tile,
                            centre + new Vector3(30, 40, -50), h, 0, 0, true);
                        Vector3 stem = desk.transform.Find("Chair_SwivelPivot/Chair_Stem").position;
                        stem.y = 0;
                        Vector3 key = desk.KeyboardWorld - centre; key.y = 0;
                        Vector3 screen = desk.MonitorWorld - centre; screen.y = 0;
                        Transform panel = desk.transform.Find("Crt_Screen");
                        Mesh mesh = panel.GetComponent<MeshFilter>().sharedMesh;
                        Vector3 meshNormal = panel.TransformDirection(Vector3.Cross(
                            mesh.vertices[4] - mesh.vertices[0], mesh.vertices[1] - mesh.vertices[0])).normalized;
                        if (Vector3.Dot(meshNormal, -f) < 0) meshNormal = -meshNormal;
                        var sample = new Sample { basis=basis, turn=turn,
                            chairError=Vector3.Distance(centre, desk.ChairGroundWorld),
                            stemError=Vector3.Distance(centre, stem),
                            keyboardLateral=Vector3.Cross(key, f).magnitude,
                            screenLateral=Vector3.Cross(screen, f).magnitude,
                            screenNormalError=Vector3.Angle(meshNormal, -f) };
                        samples.Add(sample);
                        if (sample.chairError > 0.0001f || sample.stemError > 0.0001f ||
                            sample.keyboardLateral > 0.0001f || sample.screenLateral > 0.0001f ||
                            sample.screenNormalError > 0.1f)
                            throw new InvalidOperationException(JsonUtility.ToJson(sample));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                Directory.CreateDirectory("Artifacts/WorkstationTileCentre");
                File.WriteAllText("Artifacts/WorkstationTileCentre/geometry.json",
                    JsonUtility.ToJson(new Result { samples=samples.ToArray() }, true));
                Debug.Log("WORKSTATION_TILE_CENTRE: PASS 8 rotated physical-geometry cases");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
