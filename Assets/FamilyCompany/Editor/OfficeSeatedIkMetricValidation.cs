using System;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeSeatedIkMetricValidation
    {
        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void Run()
        {
            Type actorType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("FamilyCompany.Runtime.Character3D.Family3DWalkActor")).First(t=>t!=null);
            MethodInfo solve=actorType.GetMethod("ApplyTwoBoneIk",BindingFlags.NonPublic|BindingFlags.Static);
            float maximum=0f;
            for(int turn=0;turn<4;turn++) for(int sample=0;sample<8;sample++)
            {
                var root=new GameObject("IsolatedNonUniformIkMetric");
                try
                {
                    root.transform.localScale=new Vector3(0.92f,1f,0.92f);
                    root.transform.rotation=Quaternion.Euler(0,turn*90,0);
                    Transform upper=new GameObject("upper").transform; upper.SetParent(root.transform,false);
                    Transform lower=new GameObject("lower").transform; lower.SetParent(upper,false);
                    lower.localPosition=new Vector3(0,-0.3f,0); lower.localRotation=Quaternion.Euler(30,0,0);
                    Transform end=new GameObject("end").transform; end.SetParent(lower,false);
                    end.localPosition=new Vector3(0,-0.3f,0);
                    Vector3 localTarget=new Vector3(0.21f,-0.41f,0.1f+sample*0.012f);
                    Vector3 target=root.transform.TransformPoint(localTarget);
                    Vector3 pole=root.transform.TransformPoint(new Vector3(0.4f,-0.2f,0.4f));
                    solve.Invoke(null,new object[]{upper,lower,end,target,pole,1f});
                    float error=Vector3.Distance(end.position,target); maximum=Mathf.Max(maximum,error);
                    if(error>0.0001f) throw new InvalidOperationException("Non-uniform ancestor causes seated endpoint error: "+error);
                    if(Vector3.Distance(lower.localPosition,new Vector3(0,-0.3f,0))>0.000001f ||
                       Vector3.Distance(end.localPosition,new Vector3(0,-0.3f,0))>0.000001f)
                        throw new InvalidOperationException("IK must rotate bones, not translate/stretch them.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Debug.Log("SEATED_IK_METRIC: PASS cases=32 maxEndpointError="+maximum+" boneTranslationsUnchanged=true");
        }
    }
}
