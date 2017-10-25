using UnityEngine;
using System.Collections.Generic;

namespace NobleMuffins.LimbHacker.Guts
{
    public class LimbHackerAgent : MonoBehaviour
    {
        public Mesh[] preloadMeshes;
        public WorkerThreadMode workerThreadMode;
        
        private readonly HashSet<JobState> jobStates = new HashSet<JobState>();
        private readonly List<JobState> jobStateQueue = new List<JobState>();
        private readonly ICollection<JobState> jobStateRemovalQueue = new List<JobState>();

        private readonly IDictionary<int, MeshSnapshot> preloadedMeshes = new Dictionary<int, MeshSnapshot>();

        private static LimbHackerAgent _instance;
        public static LimbHackerAgent instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject();
                    _instance = go.AddComponent<LimbHackerAgent>();
                }
                return _instance;
            }
        }

        public void Awake()
        {
            #if UNITY_WEBGL
			if (workerThreadMode == WorkerThreadMode.Asynchronous) {
				Debug.LogWarning ("Turbo Slicer will run synchronously because WebGL does not support threads.", this);
				workerThreadMode = WorkerThreadMode.Synchronous;
			}
            #endif

            if (preloadMeshes != null)
            {
                for (int i = 0; i < preloadMeshes.Length; i++)
                {
                    var mesh = preloadMeshes[i];
                    var indices = new int[mesh.subMeshCount][];
                    for (int j = 0; j < mesh.subMeshCount; j++)
                    {
                        indices[j] = mesh.GetIndices(j);
                    }
                    
                    //Note that this is NOT a usable mesh snapshot. It will need to be combined with live data at runtime.
                    var rom = new MeshSnapshot(null, mesh.vertices, mesh.normals, mesh.uv, mesh.tangents, mesh.boneWeights, new Material[0], new BoneMetadata[0], null, indices);
                    preloadedMeshes[mesh.GetInstanceID()] = rom;
                }
            }
        }

        // Use this for initialization
        void Start()
        {
            _instance = this;
        }

        //These buffers are used in ConsumeJobYield. This method is executed only on the event dispatch thread and therefore will not be clobbered.
        readonly Dictionary<string, bool> bonePresenceAlfaBuffer = new Dictionary<string, bool>();
        readonly Dictionary<string, bool> bonePresenceBravoBuffer = new Dictionary<string, bool>();

        void ConsumeJobYield(JobState jobState)
        {
            if (jobState.HasException)
            {
                Debug.LogException(jobState.Exception);
            }
            else
            {
                var jobSpecification = jobState.Specification;
                var jobYield = jobState.Yield;
                
                bonePresenceAlfaBuffer.Clear();
                bonePresenceBravoBuffer.Clear();

                foreach (var kvp in jobSpecification.NodeMetadata)
                {
                    bonePresenceAlfaBuffer[kvp.Key] = kvp.Value.IsConsideredSevered;
                    bonePresenceBravoBuffer[kvp.Key] = !kvp.Value.IsConsideredSevered;
                }
                
                var originalSubjectTransform = jobSpecification.Subject.transform;

                bool useAlternateForFront, useAlternateForBack;

                if (jobSpecification.Hackable.alternatePrefab == null)
                {
                    useAlternateForFront = false;
                    useAlternateForBack = false;
                }
                else
                {
                    useAlternateForFront = jobSpecification.Hackable.cloneAlternate(bonePresenceAlfaBuffer);
                    useAlternateForBack = jobSpecification.Hackable.cloneAlternate(bonePresenceBravoBuffer);
                }

                GameObject alfaObject, bravoObject;

                var backIsNew = useAlternateForBack;

                if (backIsNew)
                {
                    var backSource = useAlternateForBack ? jobSpecification.Hackable.alternatePrefab : jobSpecification.Subject;
                    bravoObject = (GameObject)Instantiate(backSource);
                    bravoObject.name = string.Format("{0} (Bravo)", jobSpecification.Subject);
                }
                else
                    bravoObject = jobSpecification.Subject;

                var alfaSource = useAlternateForFront ? jobSpecification.Hackable.alternatePrefab : jobSpecification.Subject;
                alfaObject = (GameObject)Instantiate(alfaSource);

                HandleHierarchy(alfaObject.transform, bonePresenceAlfaBuffer, jobSpecification.NodeMetadata);
                HandleHierarchy(bravoObject.transform, bonePresenceBravoBuffer, jobSpecification.NodeMetadata);

                var parent = originalSubjectTransform.parent;

                var position = originalSubjectTransform.localPosition;
                var scale = originalSubjectTransform.localScale;

                var rotation = originalSubjectTransform.localRotation;

                alfaObject.transform.parent = parent;
                alfaObject.transform.localPosition = position;
                alfaObject.transform.localScale = scale;

                alfaObject.transform.localRotation = rotation;

                alfaObject.layer = jobSpecification.Subject.layer;

                alfaObject.name = string.Format("{0} (Alfa)", jobSpecification.Subject);

                if (backIsNew)
                {
                    bravoObject.transform.parent = parent;
                    bravoObject.transform.localPosition = position;
                    bravoObject.transform.localScale = scale;

                    bravoObject.transform.localRotation = rotation;

                    bravoObject.layer = jobSpecification.Subject.layer;
                }

                ApplySnapshotsToRoot(alfaObject, jobYield.Alfa);
                ApplySnapshotsToRoot(bravoObject, jobYield.Bravo);
                                                
                var results = new GameObject[] {
                    alfaObject, bravoObject
                };

				jobSpecification.Hackable.handleSlice(results, jobState.Yield.PlaneInWorldSpace, jobState.Yield.FocalPointInWorldSpace);

				if(backIsNew) {
                    Destroy(jobSpecification.Subject);
				}
            }
        }
        
        //These buffers are used in HandleHierarchy. This method is executed only on the event dispatch thread and therefore will not be clobbered.
        readonly ICollection<Transform> boneBuffer = new HashSet<Transform>();
        readonly ICollection<GameObject> rendererHolderBuffer = new HashSet<GameObject>();
        readonly List<Transform> childrenBuffer = new List<Transform>();

        private void HandleHierarchy(Transform root, Dictionary<string, bool> bonePresenceByName, IDictionary<string, NodeMetadata> originalsByName)
        {
            boneBuffer.Clear();

            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>();

            rendererHolderBuffer.Clear();

            foreach (var smr in smrs)
            {
                rendererHolderBuffer.Add(smr.gameObject);

                var _bones = smr.bones;
                
                for(int i = 0; i < _bones.Length; i++)
                {
                    var bone = _bones[i];

                    boneBuffer.Add(bone);

                    // Hierarchies often have transforms between bones and the root that are not
                    // part of the bones collection pulled from the SMR. However if we turn these
                    // intermediaries off, the ragdoll will not work. For the purposes of this
                    // procedure, we're going to treat these AS bones.

                    boneBuffer.Add(bone.parent);
                }
            }
            
            childrenBuffer.Clear();
            if (childrenBuffer.Capacity < bonePresenceByName.Count)
            {
                childrenBuffer.Capacity = bonePresenceByName.Count;
            }

            ConcatenateHierarchy(root, childrenBuffer);

            for(int i = 0; i < childrenBuffer.Count; i++)
            {
                var t = childrenBuffer[i];
                var go = t.gameObject;
                var thisIsTheSkinnedMeshRenderer = rendererHolderBuffer.Contains(go);
                var shouldBePresent = true;

                var presenceKeySource = t;
                do
                {
                    string presenceKey = presenceKeySource.name;
                    if (bonePresenceByName.ContainsKey(presenceKey))
                    {
                        shouldBePresent = bonePresenceByName[presenceKey];
                        break;
                    }
                    else
                    {
                        presenceKeySource = presenceKeySource.parent;
                    }
                }
                while (childrenBuffer.Contains(presenceKeySource));

                NodeMetadata sourceMetadata;
                if(originalsByName.TryGetValue(t.name, out sourceMetadata))
                {
                    t.localPosition = sourceMetadata.LocalPosition;
                    t.localRotation = sourceMetadata.LocalRotation;
                    t.localScale = sourceMetadata.LocalScale;

                    shouldBePresent &= sourceMetadata.IsActive;
                }

                bool isBone = boneBuffer.Contains(t);

                if (!shouldBePresent && isBone)
                {
                    var c = t.GetComponent<Collider>();
                    if (c != null)
                    {
                        c.enabled = shouldBePresent;
                    }

                    var r = t.GetComponent<Rigidbody>();
                    if (r != null)
                    {
                        r.mass = float.Epsilon;
                    }
                }
                else
                {
                    shouldBePresent |= thisIsTheSkinnedMeshRenderer;

                    go.SetActive(shouldBePresent || thisIsTheSkinnedMeshRenderer);
                }
            }
        }

        private ICollection<Transform> GetConcatenatedHierarchy(Transform t)
        {
            var children = new HashSet<Transform>() as ICollection<Transform>;
            ConcatenateHierarchy(t, children);
            return children;
        }

        static void ConcatenateHierarchy(Transform root, ICollection<Transform> resultBuffer)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                resultBuffer.Add(child);
                ConcatenateHierarchy(child, resultBuffer);
            }
        }

        static void ApplySnapshotsToRoot(GameObject root, IEnumerable<MeshSnapshot> snapshots)
        {
            var skinnedMeshRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var snapshot in snapshots)
            {
                for (int i = 0; i < skinnedMeshRenderers.Length; i++)
                {
                    if (skinnedMeshRenderers[i].name.Equals(snapshot.key))
                    {
                        var skinnedMeshRenderer = skinnedMeshRenderers[i];

                        if (snapshot.vertices.Length > 0)
                        {
                            var bindPoses = new Matrix4x4[snapshot.boneMetadata.Length];

                            for (int j = 0; j < bindPoses.Length; j++)
                            {
                                bindPoses[j] = snapshot.boneMetadata[j].BindPose;
                            }

                            //Note that we do not explicitly call recalculate bounds because (as per the manual) this is implicit in an
                            //assignment to vertices whenever the vertex count changes from zero to non-zero.

                            var mesh = new Mesh();

                            skinnedMeshRenderer.materials = snapshot.materials;
                            
                            mesh.vertices = snapshot.vertices;
                            mesh.normals = snapshot.normals;
                            mesh.uv = snapshot.coords;
                            mesh.boneWeights = snapshot.boneWeights;
                            mesh.tangents = snapshot.tangents;
                            mesh.subMeshCount = snapshot.indices.Length;
                            mesh.bindposes = bindPoses;

                            for (int j = 0; j < snapshot.indices.Length; j++)
                            {
                                mesh.SetTriangles(snapshot.indices[j], j, false);
                            }

							mesh.UploadMeshData(true);

							skinnedMeshRenderer.sharedMesh = mesh;
                        }
                        else
                        {
                            DestroyImmediate(skinnedMeshRenderer);
                        }
                        break;
                    }
                }
            }
            
            var forwardPassAgent = root.GetComponent<ForwardPassAgent>();

            if (forwardPassAgent == null)
                forwardPassAgent = root.AddComponent<ForwardPassAgent>();

            forwardPassAgent.Snapshot = snapshots;
        }

        public static bool DetermineSlice(Hackable hackable, Vector3 pointInWorldSpace, ref string boneName, ref float rootTipProgression)
        {
            const int nothing = -1;

            var severables = hackable.severables;

            var indexByObject = new Dictionary<Transform, int>();
            for (var i = 0; i < severables.Length; i++)
            {
                indexByObject[severables[i]] = i;
            }

            var severablesInThreeSpace = new Vector3[severables.Length];
            for (var i = 0; i < severables.Length; i++)
            {
                severablesInThreeSpace[i] = severables[i].position;
            }

            var deltas = new Vector3[severables.Length];
            for (var i = 0; i < severables.Length; i++)
            {
                deltas[i] = severablesInThreeSpace[i] - pointInWorldSpace;
            }

            var mags = new float[severables.Length];
            for (var i = 0; i < severables.Length; i++)
            {
                mags[i] = deltas[i].magnitude;
            }

            var indexOfNearestThing = nothing;
            var distanceToNearestThing = float.PositiveInfinity;
            for (var i = 0; i < severables.Length; i++)
            {
                if (mags[i] < distanceToNearestThing)
                {
                    indexOfNearestThing = i;
                    distanceToNearestThing = mags[i];
                }
            }

            if (indexOfNearestThing != nothing)
            {
                var nearestThing = severables[indexOfNearestThing];

                if (indexByObject.ContainsKey(nearestThing.parent))
                {
                    var parentIndex = indexByObject[nearestThing.parent];

                    var hereDelta = severablesInThreeSpace[indexOfNearestThing] - severablesInThreeSpace[parentIndex];

                    var touchDelta = pointInWorldSpace - severablesInThreeSpace[parentIndex];

                    //If the touch is closer to the parent than the severable is, than it's between them.
                    //We'll use that and then use the root tip progression to slice just the right spot.
                    if (touchDelta.magnitude < hereDelta.magnitude)
                    {
                        indexOfNearestThing = parentIndex;
                        nearestThing = severables[indexOfNearestThing];
                    }
                }

                var childIndices = new List<int>();

                for (var i = 0; i < severables.Length; i++)
                {
                    var candidate = severables[i];

                    if (candidate.parent == nearestThing)
                    {
                        childIndices.Add(i);
                    }
                }

                rootTipProgression = 0f;

                if (childIndices.Count > 0)
                {
                    var aggregatedChildPositions = Vector3.zero;

                    foreach (var i in childIndices)
                    {
                        aggregatedChildPositions += severablesInThreeSpace[i];
                    }

                    var meanChildPosition = aggregatedChildPositions / childIndices.Count;

                    var alfa = (pointInWorldSpace - nearestThing.position).sqrMagnitude;
                    var bravo = (pointInWorldSpace - meanChildPosition).sqrMagnitude;

                    rootTipProgression = Mathf.Clamp(alfa / bravo, 0.0f, 0.99f);
                }

                boneName = nearestThing.name;

                return true;
            }
            else
            {
                return false;
            }
        }

        public void SeverByJoint(GameObject subject, string jointName, float rootTipProgression, Vector3? planeNormal)
        {
            //Sanity check: are we already slicing this?
            foreach (var extantState in jobStates)
            {
                if (ReferenceEquals(extantState.Specification.Subject, subject))
                {
                    //Debug.LogErrorFormat("Turbo Slicer was asked to slice '{0}' but this target is already enqueued.", subject.name);
                    return;
                }
            }

            rootTipProgression = Mathf.Clamp01(rootTipProgression);

            //These here are in local space because they're only used to copy to the resultant meshes; they're not used
            //to transform the vertices. We expect a world-space slice input.

            Hackable hackable = null;

            {
                var hackables = subject.GetComponentsInChildren<Hackable>();

                if (hackables.Length > 0)
                {
                    if (hackables.Length > 1)
                    {
                        Debug.LogWarning("Limb Hacker found multiple slice configurations on object '" + subject.name + "'! Behavior is undefined.");
                    }

                    hackable = hackables[0];
                }
                else
                {
                    Debug.LogWarning("Limb Hacker found no slice configuration on object '" + subject.name + "'.");
                    return;
                }
            }
            
            //We need information about which BONES are getting severed.
            var metadataByNodeName = new Dictionary<string, NodeMetadata>();
            {
                var childTransformByName = new Dictionary<string, Transform>();
                var parentKeyByKey = new Dictionary<string, string>();

                foreach (Transform t in GetConcatenatedHierarchy(subject.transform))
                {
                    childTransformByName[t.name] = t;

                    var parent = t.parent;

                    if (t == subject.transform)
                        parent = null;

                    parentKeyByKey[t.name] = parent == null ? null : parent.name;
                }

                var severedByChildName = new Dictionary<string, bool>();
                {
                    foreach (string childName in childTransformByName.Keys)
                    {
                        severedByChildName[childName] = childName == jointName;
                    }

                    bool changesMade;
                    do
                    {
                        changesMade = false;

                        foreach (string childKey in childTransformByName.Keys)
                        {
                            bool severed = severedByChildName[childKey];

                            if (severed)
                                continue;

                            string parentKey = parentKeyByKey[childKey];

                            bool parentSevered;

                            if (severedByChildName.TryGetValue(parentKey, out parentSevered) == false)
                                continue;

                            if (parentSevered)
                            {
                                severedByChildName[childKey] = true;

                                changesMade = true;
                            }
                        }
                    }
                    while (changesMade);
                }

                foreach (var kvp in severedByChildName)
                {
                    var t = childTransformByName[kvp.Key];
                    var isConsideredSevered = kvp.Value;
                    metadataByNodeName[kvp.Key] = new NodeMetadata(t, isConsideredSevered);
                }
            }

            IEnumerable<MeshSnapshot> snapshots;
            var forwardPassAgent = subject.GetComponent<ForwardPassAgent>();
            if(forwardPassAgent == null)
            {
                var snapshotBuilder = new List<MeshSnapshot>();
                var skinnedMeshRenderers = subject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in skinnedMeshRenderers)
                {
                    var mesh = smr.sharedMesh;
                    
                    var boneMetadata = new BoneMetadata[smr.bones.Length];

                    var bones = smr.bones;
                    var bindPoses = mesh.bindposes;

                    for (int i = 0; i < boneMetadata.Length; i++)
                    {
                        boneMetadata[i] = new BoneMetadata(i, bones[i].name, bindPoses[i]);
                    }

                    int? infillIndex = null;
                    if (hackable.infillMaterial != null)
                    {
                        var mats = smr.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (hackable.infillMaterial == mats[i])
                            {
                                infillIndex = i;
                                break;
                            }
                        }
                    }

                    MeshSnapshot snapshot;

                    MeshSnapshot preloadedFragment;
                    if(preloadedMeshes.TryGetValue(mesh.GetInstanceID(), out preloadedFragment))
                    {
                        //The preloaded fragments are missing data which is particular to the SMR. We'll combine it with such data here.

                        snapshot = preloadedFragment.WithKey(smr.name).WithMaterials(smr.sharedMaterials).WithBoneMetadata(boneMetadata).WithInfillIndex(infillIndex);
                    }
                    else
                    {
                        var indices = new int[mesh.subMeshCount][];
                        for (int i = 0; i < mesh.subMeshCount; i++)
                        {
                            indices[i] = mesh.GetIndices(i);
                        }

                        snapshot = new MeshSnapshot(
                            smr.name,
                            mesh.vertices,
                            mesh.normals,
                            mesh.uv,
                            mesh.tangents,
                            mesh.boneWeights,
                            smr.sharedMaterials,
                            boneMetadata,
                            infillIndex,
                            indices);
                    }

                    snapshotBuilder.Add(snapshot);
                }
                snapshots = snapshotBuilder;
            }
            else
            {
                snapshots = forwardPassAgent.Snapshot;
            }
           
            var jobSpec = new JobSpecification(subject, hackable, snapshots, metadataByNodeName, hackable.infillMaterial, jointName, rootTipProgression, planeNormal, hackable.infillMode, true);
            var jobState = new JobState(jobSpec);

            try
            {
                switch (workerThreadMode)
                {
                    case WorkerThreadMode.Asynchronous:
                        jobStates.Add(jobState);
                        #if NETFX_CORE && !UNITY_EDITOR
                        System.Threading.Tasks.Task.Factory.StartNew(ThreadSafeHack.Slice, jobState);
                        #else
                        System.Threading.ThreadPool.QueueUserWorkItem(ThreadSafeHack.Slice, jobState);
                        #endif
                        break;
                    case WorkerThreadMode.Synchronous:
                        ThreadSafeHack.Slice(jobState);
                        if (jobState.HasYield)
                        {
                            ConsumeJobYield(jobState);
                        }
                        else if (jobState.HasException)
                        {
                            throw jobState.Exception;
                        }
                        break;
                    default:
                        throw new System.NotImplementedException();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, subject);
            }
        }

        void Update()
        {
            jobStateRemovalQueue.Clear();
            jobStateQueue.Clear();
            jobStateQueue.AddRange(jobStates);
            foreach (var jobState in jobStateQueue)
            {
                if (jobState.IsDone)
                {
                    try
                    {
                        if (jobState.HasYield)
                        {
                            ConsumeJobYield(jobState);
                        }
                        else if (jobState.HasException)
                        {
                            throw jobState.Exception;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogException(ex, jobState.Specification.Subject);

                    }
                    finally
                    {
                        jobStateRemovalQueue.Add(jobState);
                    }
                }
            }
            jobStates.ExceptWith(jobStateRemovalQueue);
        }
    }
}