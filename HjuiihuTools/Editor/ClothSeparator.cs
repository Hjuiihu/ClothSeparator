using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using VRC.SDK3.Dynamics.PhysBone.Components;

public class HjuiihuClothSeparator : EditorWindow
{
    private GameObject originalClothing;
    private List<string> meshNames = new List<string>();
    private bool partialSeparation = false;
    private List<GameObject> selectedMeshes = new List<GameObject>();

    [MenuItem("HjuiihuTools/Hjuiihu Cloth Separator")]
    public static void ShowWindow()
    {
        GetWindow<HjuiihuClothSeparator>("Hjuiihu Cloth Separator");
    }

    [MenuItem("GameObject/HjuiihuTools/Separate Cloth Mesh", false, 10)]
    private static void SeparateClothMenu(MenuCommand menuCommand)
    {
        GameObject selectedObject = menuCommand.context as GameObject;
        if (selectedObject != null)
        {
            HjuiihuClothSeparator separator = ScriptableObject.CreateInstance<HjuiihuClothSeparator>();
            separator.originalClothing = selectedObject;
            separator.SeparateCloth();
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "No GameObject selected.", "OK");
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Cloth Mesh Separator Tool", EditorStyles.boldLabel);

        originalClothing = EditorGUILayout.ObjectField("Original Clothing", originalClothing, typeof(GameObject), true) as GameObject;
        partialSeparation = EditorGUILayout.Toggle("Partial Separation", partialSeparation);

        if (partialSeparation)
        {
            if (selectedMeshes == null)
            {
                selectedMeshes = new List<GameObject>();
            }

            EditorGUILayout.BeginVertical("box");
            for (int i = 0; i < selectedMeshes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GameObject previousMesh = selectedMeshes[i];
                selectedMeshes[i] = EditorGUILayout.ObjectField("Mesh " + (i + 1), selectedMeshes[i], typeof(GameObject), true) as GameObject;

                if (selectedMeshes[i] != null && previousMesh == null && i == selectedMeshes.Count - 1)
                {
                    selectedMeshes.Add(null); 
                }

                if (GUILayout.Button("-") && selectedMeshes.Count > 1)
                {
                    selectedMeshes.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add New Mesh"))
            {
                selectedMeshes.Add(null);
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Separate Cloth"))
        {
            if (originalClothing == null)
            {
                EditorUtility.DisplayDialog("Error", "Original clothing is required.", "OK");
            }
            else
            {
                SeparateCloth();
            }
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("❱•❰ Hjuiihu", EditorStyles.centeredGreyMiniLabel);
    }

   public void SeparateCloth()
{
    if (originalClothing == null)
    {
        EditorUtility.DisplayDialog("Error", "Original clothing is required.", "OK");
        return;
    }

    if (partialSeparation)
    {
        GameObject clothingCopy = Instantiate(originalClothing, originalClothing.transform.parent);
        clothingCopy.name = originalClothing.name + "_Original";
        clothingCopy.SetActive(false);
        HashSet<string> selectedMeshNames = new HashSet<string>();
        HashSet<Transform> allNecessaryBones = new HashSet<Transform>();

        foreach (var mesh in selectedMeshes)
        {
            if (mesh != null)
            {
                selectedMeshNames.Add(mesh.name);
                HashSet<Transform> necessaryBonesForMesh = GetNecessaryBonesForMesh(mesh);
                allNecessaryBones.UnionWith(necessaryBonesForMesh);
            }
        }

        HashSet<Transform> weightedPhysBoneColliderParents = GetWeightedPhysBoneColliderParents(clothingCopy, allNecessaryBones);
        allNecessaryBones.UnionWith(weightedPhysBoneColliderParents);

        PruneMeshesAndBonesForPartialSeparation(clothingCopy, allNecessaryBones, selectedMeshNames);
    }
    else
    {
        SeparateAndPruneAllMeshes();
    }
}
    private HashSet<Transform> GetWeightedPhysBoneColliderParents(GameObject clothing, HashSet<Transform> necessaryBones)
    {
        HashSet<Transform> colliderParents = new HashSet<Transform>();

        foreach (Transform bone in necessaryBones)
        {
            VRCPhysBone physBone = bone.GetComponent<VRCPhysBone>();
            if (physBone != null)
            {
                foreach (var collider in physBone.colliders)
                {
                    if (collider != null)
                    {
                        Transform parent = collider.transform;
                        if (parent is Transform)
                        {
                            while (parent != null && parent != clothing.transform)
                            {
                                colliderParents.Add(parent);
                                parent = parent.parent;
                            }
                        }
                    }
                }
            }
        }
        return colliderParents;
    }
    private HashSet<Transform> GetPhysBoneColliderParents(GameObject clothing)
    {
        HashSet<Transform> colliderParents = new HashSet<Transform>();
        var physBones = clothing.GetComponentsInChildren<VRCPhysBone>();
        foreach (var physBone in physBones)
        {
            foreach (var collider in physBone.colliders)
            {
                if (collider != null)
                {
                    Transform parent = collider.transform;
                    if (parent is Transform)
                    {
                        while (parent != null && parent != clothing.transform)
                        {
                            colliderParents.Add(parent);
                            parent = parent.parent;
                        }
                    }
                }
            }
        }
        return colliderParents;
    }

    private void PruneMeshesAndBonesForPartialSeparation(GameObject clothingCopy, HashSet<Transform> allNecessaryBones, HashSet<string> selectedMeshNames)
    {
        SkinnedMeshRenderer[] skinnedMeshRenderers = originalClothing.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
        {
            if (selectedMeshNames.Contains(skinnedMeshRenderer.gameObject.name))
            {
                PruneBonesForMesh(skinnedMeshRenderer.gameObject, allNecessaryBones);
            }
            else
            {
                DestroyImmediate(skinnedMeshRenderer.gameObject);
            }
        }
    }

    private void SeparateAndPruneAllMeshes()
{
    meshNames.Clear();

    SkinnedMeshRenderer[] skinnedMeshRenderers = originalClothing.GetComponentsInChildren<SkinnedMeshRenderer>();
    foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
    {
        meshNames.Add(skinnedMeshRenderer.gameObject.name);
    }

    foreach (string meshName in meshNames)
    {
        GameObject separatedClothing = Instantiate(originalClothing, originalClothing.transform.parent);
        separatedClothing.name = originalClothing.name + "-" + meshName;
        PruneMeshes(separatedClothing, meshName);
        PruneBones(separatedClothing, meshName);
    }
}

    private HashSet<Transform> GetNecessaryBonesForMesh(GameObject meshGameObject)
    {
        HashSet<Transform> necessaryBones = new HashSet<Transform>();
        SkinnedMeshRenderer renderer = meshGameObject.GetComponent<SkinnedMeshRenderer>();

        if (renderer != null)
        {
            BoneWeight[] boneWeights = renderer.sharedMesh.boneWeights;
            Transform[] bones = renderer.bones;

            foreach (var boneWeight in boneWeights)
            {
                AddBone(necessaryBones, bones, boneWeight.boneIndex0);
                AddBone(necessaryBones, bones, boneWeight.boneIndex1);
                AddBone(necessaryBones, bones, boneWeight.boneIndex2);
                AddBone(necessaryBones, bones, boneWeight.boneIndex3);
            }
        }

        return necessaryBones;
    }

    private void PruneBonesForMesh(GameObject meshGameObject, HashSet<Transform> allNecessaryBones)
    {
        SkinnedMeshRenderer targetRenderer = meshGameObject.GetComponent<SkinnedMeshRenderer>();
        if (targetRenderer == null) return;

        HashSet<Transform> physBoneColliders = new HashSet<Transform>();
        HashSet<Transform> physBoneChildren = new HashSet<Transform>();

        AddPhysBoneCollidersAndChildren(meshGameObject, allNecessaryBones, physBoneColliders, physBoneChildren);

        PruneUnnecessaryBones(targetRenderer.rootBone, allNecessaryBones, physBoneColliders, physBoneChildren);
    }

    private void PruneMeshes(GameObject clothing, string meshName)
    {
        SkinnedMeshRenderer[] renderers = clothing.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.gameObject.name != meshName)
            {
                DestroyImmediate(renderer.gameObject);
            }
        }
    }

    private void PruneBones(GameObject clothing, string meshName)
    {
        SkinnedMeshRenderer targetRenderer = clothing.transform.Find(meshName)?.GetComponent<SkinnedMeshRenderer>();
        if (targetRenderer == null) return;

        HashSet<Transform> necessaryBones = new HashSet<Transform>();
        HashSet<Transform> physBoneColliders = new HashSet<Transform>();
        HashSet<Transform> physBoneChildren = new HashSet<Transform>();

        BoneWeight[] boneWeights = targetRenderer.sharedMesh.boneWeights;
        Transform[] bones = targetRenderer.bones;

        foreach (var boneWeight in boneWeights)
        {
            AddBone(necessaryBones, bones, boneWeight.boneIndex0);
            AddBone(necessaryBones, bones, boneWeight.boneIndex1);
            AddBone(necessaryBones, bones, boneWeight.boneIndex2);
            AddBone(necessaryBones, bones, boneWeight.boneIndex3);
        }

        AddPhysBoneCollidersAndChildren(clothing, necessaryBones, physBoneColliders, physBoneChildren);
        PruneUnnecessaryBones(targetRenderer.rootBone, necessaryBones, physBoneColliders, physBoneChildren);
    }

    private void AddPhysBoneCollidersAndChildren(GameObject clothing, HashSet<Transform> necessaryBones, HashSet<Transform> physBoneColliders, HashSet<Transform> physBoneChildren)
    {
        var physBones = clothing.GetComponentsInChildren<VRCPhysBone>();
        foreach (var physBone in physBones)
        {
            if (necessaryBones.Contains(physBone.transform) || partialSeparation)
            {
                foreach (var collider in physBone.colliders)
                {
                    if (collider != null)
                    {
                        physBoneColliders.Add(collider.transform);
                        AddBoneAndParents(physBoneColliders, collider.transform);
                    }
                }

                AddBoneAndChildren(physBoneChildren, physBone.transform);
            }
        }
    }

    private void AddBoneAndParents(HashSet<Transform> set, Transform bone)
    {
        while (bone != null)
        {
            set.Add(bone);
            bone = bone.parent;
        }
    }

    private void AddBoneAndChildren(HashSet<Transform> set, Transform bone)
    {
        set.Add(bone);
        foreach (Transform child in bone)
        {
            AddBoneAndChildren(set, child);
        }
    }

    private void AddBone(HashSet<Transform> set, Transform[] bones, int index)
    {
        if (index >= 0 && index < bones.Length)
        {
            Transform bone = bones[index];
            while (bone != null)
            {
                set.Add(bone);
                bone = bone.parent;
            }
        }
    }

    private void PruneUnnecessaryBones(Transform currentBone, HashSet<Transform> necessaryBones, HashSet<Transform> physBoneColliders, HashSet<Transform> physBoneChildren)
    {
        for (int i = currentBone.childCount - 1; i >= 0; i--)
        {
            Transform child = currentBone.GetChild(i);
            if (!necessaryBones.Contains(child) && !physBoneColliders.Contains(child) && !physBoneChildren.Contains(child))
            {
                if (!partialSeparation || !IsChildOfVRCPhysBone(child))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            else
            {
                PruneUnnecessaryBones(child, necessaryBones, physBoneColliders, physBoneChildren);
            }
        }
    }

    private bool IsChildOfVRCPhysBone(Transform child)
    {
        var parent = child.parent;
        while (parent != null)
        {
            if (parent.GetComponent<VRCPhysBone>() != null)
                return true;
            parent = parent.parent;
        }
        return false;
    }
}
