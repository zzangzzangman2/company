using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FamilyCompany.Editor
{
    // Read-only preflight. Building a release must not regenerate retired family sprite catalogs.
    public static class Office3DReleaseAssetValidation
    {
        public static void Run()
        {
            Type presenter = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("FamilyCompany.Runtime.Character3D.Family3DProductionPresenter")).First(t => t != null);
            foreach (string member in new[] { "Player", "Father" })
            {
                string Read(string suffix) => (string)presenter.GetField(member + suffix,
                    BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
                string modelPath = Read("ModelResourcePath");
                GameObject model = Resources.Load<GameObject>(modelPath);
                Texture2D albedo = Resources.Load<Texture2D>(Read("AlbedoResourcePath"));
                Material material = Resources.Load<Material>(Read("MaterialResourcePath"));
                if (model == null || albedo == null || material == null || material.shader == null)
                    throw new InvalidOperationException(member + " production 3D resource missing.");
                Animator avatar = model.GetComponentInChildren<Animator>(true);
                if (model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 1 || avatar == null ||
                    avatar.avatar == null || !avatar.avatar.isValid || !avatar.avatar.isHuman)
                    throw new InvalidOperationException(member + " must remain one complete Humanoid skinned body.");
                AnimationClip clip = Resources.LoadAll<AnimationClip>(modelPath).SingleOrDefault(c => c.name == Read("WalkClipName"));
                if (clip == null || !clip.isHumanMotion)
                    throw new InvalidOperationException(member + " approved walk clip missing.");
            }
            Debug.Log("OFFICE_3D_RELEASE_ASSETS: PASS approved Player/Father resources; no asset writes or sprite fallback");
        }
    }
}
