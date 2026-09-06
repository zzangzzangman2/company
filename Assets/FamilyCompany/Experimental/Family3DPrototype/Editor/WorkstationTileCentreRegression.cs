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
            public float monitorDeskAxisError, keyboardDeskAxisError, monitorFaceNormalError;
        }
        [Serializable] private class Result
        {
            public string scope = "geometry only; gameplay and release remain separate";
            public Sample[] samples;
            public bool passed;
        }
        public static void RunBatch()
        {
            var samples = new List<Sample>();
            bool passed = true;
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
                        Vector3 panelRight = panel.TransformDirection(mesh.vertices[1] - mesh.vertices[0]);
                        Vector3 meshNormal = Vector3.Cross(Vector3.up, panelRight).normalized;
                        if (Vector3.Dot(meshNormal, -f) < 0) meshNormal = -meshNormal;
                        // In the overlay's oblique mapped coordinates the Euclidean surface normal
                        // is not the tile-forward vector. Match the actual desk-front plane instead.
                        Vector3 expectedNormal = Vector3.Cross(Vector3.up, r).normalized;
                        if (Vector3.Dot(expectedNormal, -f) < 0) expectedNormal = -expectedNormal;
                        Transform top = desk.transform.Find("Desk_Top");
                        Transform body = desk.transform.Find("Crt_Body");
                        Transform keyboard = desk.transform.Find("Keyboard");
                        var sample = new Sample { basis=basis, turn=turn,
                            chairError=Vector3.Distance(centre, desk.ChairGroundWorld),
                            stemError=Vector3.Distance(centre, stem),
                            keyboardLateral=Vector3.Cross(key, f).magnitude,
                            screenLateral=Vector3.Cross(screen, f).magnitude,
                            screenNormalError=Vector3.Angle(meshNormal, expectedNormal),
                            monitorDeskAxisError=BoxAxisError(body, top),
                            keyboardDeskAxisError=BoxAxisError(keyboard, top),
                            monitorFaceNormalError=FaceNormalError(body.GetComponent<MeshFilter>().sharedMesh) };
                        samples.Add(sample);
                        if (sample.chairError > 0.0001f || sample.stemError > 0.0001f ||
                            sample.keyboardLateral > 0.0001f || sample.screenLateral > 0.0001f ||
                            sample.screenNormalError > 0.1f || sample.monitorDeskAxisError > 0.1f ||
                            sample.keyboardDeskAxisError > 0.1f || sample.monitorFaceNormalError > 0.1f)
                            passed = false;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                string output = "Artifacts/WorkstationTileCentre";
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == "-workstationGeometryOutput") output = args[i + 1];
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "geometry.json"),
                    JsonUtility.ToJson(new Result { samples=samples.ToArray(), passed=passed }, true));
                if (!passed) throw new InvalidOperationException("Desk/monitor/key axes or CRT face normals failed; see geometry.json.");
                Debug.Log("WORKSTATION_TILE_CENTRE: PASS 8 rotated physical-geometry cases");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static float BoxAxisError(Transform part, Transform desk)
        {
            Vector3[] p = part.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3[] d = desk.GetComponent<MeshFilter>().sharedMesh.vertices;
            return Mathf.Max(
                Vector3.Angle(part.TransformVector(p[1] - p[0]), desk.TransformVector(d[1] - d[0])),
                Vector3.Angle(part.TransformVector(p[2] - p[1]), desk.TransformVector(d[2] - d[1])));
        }

        private static float FaceNormalError(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            int[] triangles = mesh.triangles;
            float maximum = 0f;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 face = Vector3.Cross(vertices[triangles[t + 1]] - vertices[triangles[t]],
                    vertices[triangles[t + 2]] - vertices[triangles[t]]).normalized;
                for (int k = 0; k < 3; k++)
                    maximum = Mathf.Max(maximum, Vector3.Angle(face, normals[triangles[t + k]]));
            }
            return maximum;
        }
    }
}
