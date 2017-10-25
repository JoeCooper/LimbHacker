using System.Collections.Generic;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
    public static class ThreadSafeHack
    {
        public enum TriangleAssignment
        {
            Alfa, Bravo, Split
        };

        enum Shape
        {
            None,
            Triangle = 3,
            Quad = 4
        }

        static readonly CollectionPool<ArrayBuilder<Vector3>, Vector3> vectorThreeBuilderPool = new CollectionPool<ArrayBuilder<Vector3>, Vector3>(32, (instance) => instance.Capacity, (cap) => new ArrayBuilder<Vector3>(cap));
        static readonly CollectionPool<ArrayBuilder<Vector2>, Vector2> vectorTwoBuilderPool = new CollectionPool<ArrayBuilder<Vector2>, Vector2>(32, (instance) => instance.Capacity, (cap) => new ArrayBuilder<Vector2>(cap));
        static readonly CollectionPool<ArrayBuilder<BoneWeight>, BoneWeight> weightBuilderPool = new CollectionPool<ArrayBuilder<BoneWeight>, BoneWeight>(32, (instance) => instance.Capacity, (cap) => new ArrayBuilder<BoneWeight>(cap));
        static readonly CollectionPool<List<string>, string> stringBuilderPool = new CollectionPool<List<string>, string>(32, (instance) => instance.Capacity, (cap) => new List<string>(cap));

        static readonly CollectionPool<ArrayBuilder<int>, int> intBuilderPool = new CollectionPool<ArrayBuilder<int>, int>(32, (instance) => instance.Capacity, (cap) => new ArrayBuilder<int>(cap));
        static readonly CollectionPool<ArrayBuilder<SplitAction>, SplitAction> splitActionBuilderPool = new CollectionPool<ArrayBuilder<SplitAction>, SplitAction>(32, (instance) => instance.Capacity, (cap) => new ArrayBuilder<SplitAction>(cap));

        static readonly ArrayPool<SplitAction> splitActionArrayPool = new ArrayPool<SplitAction>(32);
        static readonly ArrayPool<Shape> shapeArrayPool = new ArrayPool<Shape>(32);
        static readonly ArrayPool<Vector2> vectorTwoPool = new ArrayPool<Vector2>(32);
        static readonly ArrayPool<Vector3> vectorThreePool = new ArrayPool<Vector3>(32);
        static readonly ArrayPool<BoneWeight> weightsPool = new ArrayPool<BoneWeight>(32);
        static readonly ArrayPool<int> intArrayPool = new ArrayPool<int>(32);
        static readonly ArrayPool<float> floatArrayPool = new ArrayPool<float>(32);
        static readonly ArrayPool<bool> boolPool = new ArrayPool<bool>(32);
        static readonly ArrayPool<TriangleAssignment> triangleAssignmentArrayPool = new ArrayPool<TriangleAssignment>(32);

        // This is a little different as indices are -not- retained. This is how much we need to allocate for each resultant mesh,
        //compared to the original. I have set it assume that resultant meshes may be up to 90% the complexity of originals because
        //a highly uneven slice (a common occurrence) will result in this.
        const float factorOfSafetyIndices = 0.9f;

        public static void Slice(object _jobState)
        {
            try
            {
                var jobState = (JobState)_jobState;
                Slice(jobState);
            }
            catch (System.InvalidCastException)
            {
                Debug.LogFormat("ThreadSafeSlice called with wrong kind of state object: {0}", _jobState);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public static void Slice(JobState jobState)
        {
            var disposables = new List<System.IDisposable>();

            try
            {
                var jobSpec = jobState.Specification;
                var metadataByNodeName = jobSpec.NodeMetadata;

                var alfaBuilder = new List<MeshSnapshot>();
                var bravoBuilder = new List<MeshSnapshot>();

                foreach (var snapshot in jobSpec.MeshSnapshots)
                {
                    var severedJointKey = jobSpec.JointName;

                    bool[] severedByBoneIndex;
                    bool[] mandatoryByBoneIndex;

                    disposables.Add(boolPool.Get(snapshot.boneMetadata.Length, false, out severedByBoneIndex));
                    disposables.Add(boolPool.Get(snapshot.boneMetadata.Length, false, out mandatoryByBoneIndex));
                    
                    var boneIndexByName = new Dictionary<string, int>();
                    
                    for (int i = 0; i < snapshot.boneMetadata.Length; i++)
                    {
                        var metadata = snapshot.boneMetadata[i];
                        boneIndexByName[metadata.NameInViewGraph] = metadata.Index;
                        severedByBoneIndex[metadata.Index] = metadataByNodeName[metadata.NameInViewGraph].IsConsideredSevered;
                    }

                    var plane = Vector4.zero;

                    bool willSliceThisMesh = boneIndexByName.ContainsKey(severedJointKey);

                    if (willSliceThisMesh)
                    {
                        //We need to create a slice plane in local space. We're going to do that by using the bind poses
                        //from the SEVERED limb, its PARENT and its CHILDREN to create a position and normal.

                        var severedJointIndex = boneIndexByName[severedJointKey];

                        var severedJointMatrix = snapshot.boneMetadata[severedJointIndex].BindPose.inverse;

                        var severedJointParentMatrix = Matrix4x4.identity;

                        for (int i = 0; i < boneIndexByName.Count; i++)
                        {
                            mandatoryByBoneIndex[i] = false;
                        }

                        NodeMetadata metadata;
                        if (metadataByNodeName.TryGetValue(severedJointKey, out metadata))
                        {
                            var severedJointParentKey = metadata.ParentKey;

                            if (boneIndexByName.ContainsKey(severedJointParentKey))
                            {
                                int severedJointParentIndex = boneIndexByName[severedJointParentKey];

                                severedJointParentMatrix = snapshot.boneMetadata[severedJointParentIndex].BindPose.inverse;

                                mandatoryByBoneIndex[boneIndexByName[severedJointParentKey]] = true;
                            }
                        }

                        VectorAccumulator meanChildPosition = new VectorAccumulator();

                        if (jobSpec.RootTipProgression > 0f)
                        {
                            mandatoryByBoneIndex[boneIndexByName[jobSpec.JointName]] = true;

                            List<string> childKeys;
                            using (stringBuilderPool.Get(metadataByNodeName.Count, out childKeys))
                            {
                                foreach (KeyValuePair<string, NodeMetadata> kvp in metadataByNodeName)
                                {
                                    if (kvp.Value.ParentKey == severedJointKey)
                                    {
                                        childKeys.Add(kvp.Key);
                                    }
                                }

                                ArrayBuilder<int> childIndices;
                                using (intBuilderPool.Get(childKeys.Count, out childIndices))
                                {
                                    foreach (string key in childKeys)
                                    {
                                        int childIndex;

                                        if (boneIndexByName.TryGetValue(key, out childIndex))
                                        {
                                            childIndices.Add(childIndex);
                                        }
                                    }

                                    for (int i = 0; i < childIndices.Count; i++)
                                    {
                                        var index = childIndices[i];

                                        var childMatrix = snapshot.boneMetadata[index].BindPose.inverse;

                                        var childPosition = childMatrix.MultiplyPoint3x4(Vector3.zero);

                                        meanChildPosition.Add(childPosition);
                                    }
                                }
                            }
                        }

                        var position0 = severedJointParentMatrix.MultiplyPoint3x4(Vector3.zero);
                        var position1 = severedJointMatrix.MultiplyPoint3x4(Vector3.zero);
                        var position2 = meanChildPosition.Mean;

                        var deltaParent = position0 - position1;
                        var deltaChildren = position1 - position2;

                        var position = Vector3.Lerp(position1, position2, jobSpec.RootTipProgression);

                        var normalFromParentToChild = -Vector3.Lerp(deltaParent, deltaChildren, jobSpec.RootTipProgression).normalized;

                        if (jobSpec.TiltPlane.HasValue)
                        {
                            var fromWorldToLocalSpaceOfBone = jobSpec.NodeMetadata[severedJointKey].WorldToLocalMatrix;

                            var v = jobSpec.TiltPlane.Value;
                            v = fromWorldToLocalSpaceOfBone.MultiplyVector(v);
                            v = severedJointMatrix.MultiplyVector(v);
                            v.Normalize();

                            if (Vector3.Dot(v, normalFromParentToChild) < 0f)
                            {
                                v = -v;
                            }

                            plane = ClampNormalToBicone(v, normalFromParentToChild, 30f);
                        }
                        else
                        {
                            plane = normalFromParentToChild;
                        }

                        plane.w = -(plane.x * position.x + plane.y * position.y + plane.z * position.z);
                    }

                    //We're going to create two new tentative meshes which contain ALL original vertices in order,
                    //plus room for new vertices. Not all of these copied vertices will be addressed, but copying them
                    //over eliminates the need to remove doubles and do an On^2 search.

                    var submeshCount = snapshot.indices.Length;
                    
                    var alfaIndicesBySubmesh = new ArrayBuilder<int>[submeshCount];
                    var bravoIndicesBySubmesh = new ArrayBuilder<int>[submeshCount];
                    
                    TriangleAssignment[] sidePlanes;
                    disposables.Add(triangleAssignmentArrayPool.Get(snapshot.vertices.Length, false, out sidePlanes));
                    {
                        var count = snapshot.vertices.Length;

                        bool[] whollySeveredByVertexIndex;
                        bool[] severableByVertexIndex;
                        bool[] mandatoryByVertexIndex;

                        using (boolPool.Get(count, false, out whollySeveredByVertexIndex))
                        using (boolPool.Get(count, false, out severableByVertexIndex))
                        using (boolPool.Get(count, false, out mandatoryByVertexIndex))
                        {
                            const float minimumWeightForRelevance = 0.1f;

                            for (int i = 0; i < count; i++)
                            {
                                BoneWeight weight = snapshot.boneWeights[i];

                                bool whollySevered = true;
                                bool severable = false;
                                bool mandatory = false;

                                int[] indices = { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 };
                                float[] scalarWeights = { weight.weight0, weight.weight1, weight.weight2, weight.weight3 };

                                for (int j = 0; j < 4; j++)
                                {
                                    if (scalarWeights[j] > minimumWeightForRelevance)
                                    {
                                        int index = indices[j];
                                        bool _severable = severedByBoneIndex[index];
                                        bool _mandatory = mandatoryByBoneIndex[index];
                                        whollySevered &= _severable;
                                        severable |= _severable;
                                        mandatory |= _mandatory;
                                    }
                                }

                                whollySeveredByVertexIndex[i] = whollySevered;
                                severableByVertexIndex[i] = severable;
                                mandatoryByVertexIndex[i] = mandatory;
                            }

                            for (int i = 0; i < count; i++)
                            {
                                if (willSliceThisMesh && mandatoryByVertexIndex[i])
                                    sidePlanes[i] = GetSidePlane(ref snapshot.vertices[i], ref plane);
                                else if (whollySeveredByVertexIndex[i])
                                    sidePlanes[i] = TriangleAssignment.Alfa;
                                else if (willSliceThisMesh && severableByVertexIndex[i])
                                    sidePlanes[i] = GetSidePlane(ref snapshot.vertices[i], ref plane);
                                else
                                    sidePlanes[i] = TriangleAssignment.Bravo;
                            }
                        }
                    }

                    ArrayBuilder<int> alfaInfill;
                    ArrayBuilder<int> bravoInfill;

                    for (int j = 0; j < submeshCount; j++)
                    {
                        int initialCapacityIndices = Mathf.RoundToInt((float)snapshot.indices[j].Length * factorOfSafetyIndices);

                        var alfaIndicesBuilderBundle = intBuilderPool.Get(initialCapacityIndices);
                        disposables.Add(alfaIndicesBuilderBundle);

                        var bravoIndicesBuilderBundle = intBuilderPool.Get(initialCapacityIndices);
                        disposables.Add(bravoIndicesBuilderBundle);

                        alfaIndicesBySubmesh[j] = alfaIndicesBuilderBundle.Object;
                        bravoIndicesBySubmesh[j] = bravoIndicesBuilderBundle.Object;
                    }

                    if (snapshot.infillIndex.HasValue)
                    {
                        alfaInfill = alfaIndicesBySubmesh[snapshot.infillIndex.Value];
                        bravoInfill = bravoIndicesBySubmesh[snapshot.infillIndex.Value];
                    }
                    else if (jobSpec.InfillMaterial != null)
                    {
                        disposables.Add(intBuilderPool.Get(1024, out alfaInfill));
                        disposables.Add(intBuilderPool.Get(1024, out bravoInfill));
                    }
                    else
                    {
                        throw new System.Exception();
                    }

                    ArrayBuilder<Vector3> verticesBuilder, normalsBuilder;
                    ArrayBuilder<Vector2> coordsBuilder;
                    ArrayBuilder<BoneWeight> weightsBuilder;

                    var builderCapacity = (snapshot.vertices.Length * 3) / 2;

                    disposables.Add(vectorThreeBuilderPool.Get(builderCapacity, out verticesBuilder));
                    disposables.Add(vectorThreeBuilderPool.Get(builderCapacity, out normalsBuilder));
                    disposables.Add(vectorTwoBuilderPool.Get(builderCapacity, out coordsBuilder));
                    disposables.Add(weightBuilderPool.Get(builderCapacity, out weightsBuilder));

                    verticesBuilder.AddArray(snapshot.vertices);
                    normalsBuilder.AddArray(snapshot.normals);
                    coordsBuilder.AddArray(snapshot.coords);
                    weightsBuilder.AddArray(snapshot.boneWeights);

                    for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                    {
                        var initialCapacityIndices = Mathf.RoundToInt((float)snapshot.indices[submeshIndex].Length * factorOfSafetyIndices);

                        var rawSourceIndices = snapshot.indices[submeshIndex];

                        var alfaIndicesBuilder = alfaIndicesBySubmesh[submeshIndex];
                        var bravoIndicesBuilder = bravoIndicesBySubmesh[submeshIndex];

                        var pendingSplitsBuilderBundle = intBuilderPool.Get(initialCapacityIndices);
                        disposables.Add(pendingSplitsBuilderBundle);

                        var splitPending = pendingSplitsBuilderBundle.Object;
                        splitPending.Clear();
                        
                        var triangleBuffer = new int[3];

                        for (int i = 0; i < rawSourceIndices.Length;)
                        {
                            triangleBuffer[0] = rawSourceIndices[i++];
                            triangleBuffer[1] = rawSourceIndices[i++];
                            triangleBuffer[2] = rawSourceIndices[i++];

                            // compute the side of the plane each vertex is on
                            var r1 = sidePlanes[triangleBuffer[0]];
                            var r2 = sidePlanes[triangleBuffer[1]];
                            var r3 = sidePlanes[triangleBuffer[2]];

                            if (r1 == r2 && r1 == r3) // if all three vertices are on the same side of the plane.
                            {
                                if (r1 == TriangleAssignment.Alfa) // if all three are in front of the plane, then copy to the 'front' output triangle.
                                {
                                    alfaIndicesBuilder.AddArray(triangleBuffer);
                                }
                                else
                                {
                                    bravoIndicesBuilder.AddArray(triangleBuffer);
                                }
                            }
                            else if(willSliceThisMesh)
                            {
                                splitPending.AddArray(triangleBuffer);
                            }
                        }

                        if (willSliceThisMesh)
                        {
                            var doInfill = jobSpec.InfillMaterial != null;
                            
                            //Now we're going to do the decision making pass. This is where we assess the side figures and produce actions...

                            int inputTriangleCount = splitPending.Count / 3;

                            //A good action count estimate can avoid reallocations.
                            //We expect exactly five actions per triangle.
                            int estimatedSplitActionCount = inputTriangleCount * 5;

                            ArrayBuilder<SplitAction> splitActionsBuilder;
                            disposables.Add(splitActionBuilderPool.Get(estimatedSplitActionCount, out splitActionsBuilder));
                            
                            Shape[] alfaShapes, bravoShapes;

                            disposables.Add(shapeArrayPool.Get(inputTriangleCount, false, out alfaShapes));
                            disposables.Add(shapeArrayPool.Get(inputTriangleCount, false, out bravoShapes));
                            
                            using (var _pointClassifications = floatArrayPool.Get(splitPending.length, false))
                            {
                                var pointClassifications = _pointClassifications.Object;
                                
                                for (int i = 0; i < splitPending.length; i++)
                                {
                                    pointClassifications[i] = ClassifyPoint(ref plane, ref snapshot.vertices[splitPending.array[i]]);
                                }
                                
                                var sides = new float[3];

                                for (var i = 0; i < splitPending.length; i += 3)
                                {
                                    triangleBuffer[0] = splitPending.array[i];
                                    triangleBuffer[1] = splitPending.array[i + 1];
                                    triangleBuffer[2] = splitPending.array[i + 2];

                                    sides[0] = pointClassifications[i];
                                    sides[1] = pointClassifications[i + 1];
                                    sides[2] = pointClassifications[i + 2];

                                    var indexA = 2;

                                    var alfaVertexCount = 0;
                                    var bravoVertexCount = 0;

                                    for (int indexB = 0; indexB < 3; indexB++)
                                    {
                                        var sideA = sides[indexA];
                                        var sideB = sides[indexB];

                                        if (sideB > 0f)
                                        {
                                            if (sideA < 0f)
                                            {
                                                //Find intersection between A, B. Add to BOTH
                                                splitActionsBuilder.Add(new SplitAction(triangleBuffer[indexA], triangleBuffer[indexB]));
                                                alfaVertexCount++;
                                                bravoVertexCount++;
                                            }
                                            //Add B to FRONT.
                                            splitActionsBuilder.Add(new SplitAction(true, false, triangleBuffer[indexB]));
                                            alfaVertexCount++;
                                        }
                                        else if (sideB < 0f)
                                        {
                                            if (sideA > 0f)
                                            {
                                                //Find intersection between A, B. Add to BOTH
                                                splitActionsBuilder.Add(new SplitAction(triangleBuffer[indexA], triangleBuffer[indexB]));
                                                alfaVertexCount++;
                                                bravoVertexCount++;
                                            }
                                            //Add B to BACK.
                                            splitActionsBuilder.Add(new SplitAction(false, true, triangleBuffer[indexB]));
                                            bravoVertexCount++;
                                        }
                                        else
                                        {
                                            //Add B to BOTH.
                                            splitActionsBuilder.Add(new SplitAction(true, true, triangleBuffer[indexB]));
                                            alfaVertexCount++;
                                            bravoVertexCount++;
                                        }

                                        indexA = indexB;
                                    }
                                    
                                    var j = i / 3; //This is the triangle counter.

                                    alfaShapes[j] = (Shape)alfaVertexCount;
                                    bravoShapes[j] = (Shape)bravoVertexCount;
                                }
                            }

                            // We're going to iterate through the splits only several times, so let's
                            //find the subset once now.
                            // Since these are STRUCTs, this is going to COPY the array content. The
                            //intersectionInverseRelation table made below helps us put it back into the
                            //main array before we use it.

                            var intersectionCount = 0;

                            for (int i = 0; i < splitActionsBuilder.length; i++)
                            {
                                var sa = splitActionsBuilder.array[i];
                                if ((sa.flags & SplitAction.INTERSECT) == SplitAction.INTERSECT)
                                    intersectionCount++;
                            }

                            SplitAction[] intersectionActions;
                            int[] intersectionInverseRelation;

                            disposables.Add(splitActionArrayPool.Get(intersectionCount, false, out intersectionActions));
                            disposables.Add(intArrayPool.Get(intersectionCount, false, out intersectionInverseRelation));

                            {
                                int j = 0;
                                for (int i = 0; i < splitActionsBuilder.length; i++)
                                {
                                    SplitAction sa = splitActionsBuilder.array[i];
                                    if ((sa.flags & SplitAction.INTERSECT) == SplitAction.INTERSECT)
                                    {
                                        intersectionActions[j] = sa;
                                        intersectionInverseRelation[j] = i;
                                        j++;
                                    }
                                }
                            }

                            // Next, we're going to find out which splitActions replicate the work of other split actions.
                            //A given SA replicates another if and only if it _both_ calls for an intersection _and_ has
                            //the same two parent indices (index0 and index1). This is because all intersections are called
                            //with the same other parameters, so any case with an index0 and index1 matching will yield the
                            //same results.
                            // Only caveat is that two given splitActions might as the source indices in reverse order, so
                            //we'll arbitrarily decide that "greater first" or something is the correct order. Flipping this
                            //order has no consequence until after the intersection is found (at which point flipping the order
                            //necessitates converting intersection i to 1-i to flip it as well.)
                            // We can assume that every SA has at most 1 correlation. For a given SA, we'll search the list
                            //UP TO its own index and, if we find one, we'll take the other's index and put it into the CLONE OF
                            //slot.
                            // So if we had a set like AFDBAK, than when the _latter_ A comes up for assessment, it'll find
                            //the _first_ A (with an index of 0) and set the latter A's cloneOf figure to 0. This way we know
                            //any latter As are a clone of the first A.

                            for (int i = 0; i < intersectionCount; i++)
                            {
                                SplitAction a = intersectionActions[i];

                                //Ensure that the index0, index1 figures are all in the same order.
                                //(We'll do this as we walk the list.)
                                if (a.index0 > a.index1)
                                {
                                    int j = a.index0;
                                    a.index0 = a.index1;
                                    a.index1 = j;
                                }

                                //Only latters clone formers, so we don't need to search up to and past the self.
                                for (int j = 0; j < i; j++)
                                {
                                    SplitAction b = intersectionActions[j];

                                    bool match = a.index0 == b.index0 && a.index1 == b.index1;

                                    if (match)
                                    {
                                        a.cloneOf = j;
                                    }
                                }

                                intersectionActions[i] = a;
                            }

                            //Next, we want to perform all INTERSECTIONS. Any action which has an intersection needs to have that, like, done.
                            
                            for (int i = 0; i < intersectionCount; i++)
                            {
                                SplitAction sa = intersectionActions[i];

                                if (sa.cloneOf == SplitAction.nullIndex)
                                {
                                    var p1 = snapshot.vertices[sa.index1];
                                    var p2 = snapshot.vertices[sa.index0];

                                    var distanceToPoint = p1.x * plane.x + p1.y * plane.y + p1.z * plane.z + plane.w;

                                    var dir = p2 - p1;

                                    var dot1 = dir.x * plane.x + dir.y * plane.y + dir.z * plane.z;
                                    var dot2 = distanceToPoint - plane.w;

                                    sa.intersectionResult = -(plane.w + dot2) / dot1;

                                    intersectionActions[i] = sa;
                                }

                            }

                            // Let's create a table that relates an INTERSECTION index to a GEOMETRY index with an offset of 0 (for example
                            //to refer to our newVertices or to the transformedVertices or whatever; internal use.)
                            // We can also set up our realIndex figures in the same go.

                            int newIndexStartsAt = verticesBuilder.Count;
                            int uniqueVertexCount = 0;
                            int[] localIndexByIntersection;
                            disposables.Add(intArrayPool.Get(intersectionCount, false, out localIndexByIntersection));
                            {
                                int currentLocalIndex = 0;
                                for (int i = 0; i < intersectionCount; i++)
                                {
                                    var sa = intersectionActions[i];

                                    int j;

                                    if (sa.cloneOf == SplitAction.nullIndex)
                                    {
                                        j = currentLocalIndex++;
                                    }
                                    else
                                    {
                                        //This assumes that the widget that we are a clone of already has its localIndexByIntersection assigned.
                                        //We assume this because above – where we seek for clones – we only look behind for cloned elements.
                                        j = localIndexByIntersection[sa.cloneOf];
                                    }

                                    sa.realIndex = newIndexStartsAt + j;

                                    localIndexByIntersection[i] = j;

                                    intersectionActions[i] = sa;
                                }
                                uniqueVertexCount = currentLocalIndex;
                            }

                            //Let's figure out how much geometry we might have.
                            //The infill geometry is a pair of clones of this geometry, but with different NORMALS and UVs. (Each set has different normals.)

                            var numberOfVerticesAdded = uniqueVertexCount * (doInfill ? 3 : 1);

                            //In this ACTION pass we'll act upon intersections by fetching both referred vertices and LERPing as appropriate.
                            //The resultant indices will be written out over the index0 figures.

                            Vector3[] newVertices, newNormals;
                            Vector2[] newUVs;
                            BoneWeight[] newWeights;

                            disposables.Add(vectorThreePool.Get(numberOfVerticesAdded, false, out newVertices));
                            disposables.Add(vectorTwoPool.Get(numberOfVerticesAdded, false, out newUVs));
                            disposables.Add(vectorThreePool.Get(numberOfVerticesAdded, false, out newNormals));
                            disposables.Add(weightsPool.Get(numberOfVerticesAdded, false, out newWeights));
                            
                            int currentNewIndex = 0;
                            for (int i = 0; i < intersectionCount; i++)
                            {
                                var sa = intersectionActions[i];
                                if (sa.cloneOf == SplitAction.nullIndex)
                                {
                                    var v = verticesBuilder.array[sa.index0];
                                    var v2 = verticesBuilder.array[sa.index1];
                                    newVertices[currentNewIndex] = Vector3.Lerp(v2, v, sa.intersectionResult);

                                    var uv = coordsBuilder.array[sa.index0];
                                    var uv2 = coordsBuilder.array[sa.index1];
                                    newUVs[currentNewIndex] = Vector2.Lerp(uv2, uv, sa.intersectionResult);
                                    
                                    var n = normalsBuilder.array[sa.index0];
                                    var n2 = normalsBuilder.array[sa.index1];
                                    newNormals[currentNewIndex] = Vector3.Lerp(n2, n, sa.intersectionResult);

                                    BoneWeight bw;
                                    if (sidePlanes[sa.index0] == TriangleAssignment.Alfa)
                                    {
                                        bw = weightsBuilder.array[sa.index0];
                                    }
                                    else
                                    {
                                        bw = weightsBuilder.array[sa.index1];
                                    }
                                    newWeights[currentNewIndex] = bw;

                                    currentNewIndex++;
                                }
                            }

                            Debug.Assert(currentNewIndex == uniqueVertexCount);
                            
                            //All the polygon triangulation algorithms depend on having a 2D polygon. We also need the slice plane's
                            //geometry in two-space to map the UVs.

                            //NOTE that as we only need this data to analyze polygon geometry for triangulation, we can TRANSFORM (scale, translate, rotate)
                            //these figures any way we like, as long as they retain the same relative geometry. So we're going to perform ops on this
                            //data to create the UVs by scaling it around, and we'll feed the same data to the triangulator.

                            //Our's exists in three-space, but is essentially flat... So we can transform it onto a flat coordinate system.
                            //The first three figures of our plane four-vector describe the normal to the plane, so if we can create
                            //a transformation matrix from that normal to the up normal, we can transform the vertices for observation.
                            //We don't need to transform them back; we simply refer to the original vertex coordinates by their index,
                            //which (as this is an ordered set) will match the indices of coorisponding transformed vertices.

                            //This vector-vector transformation comes from Benjamin Zhu at SGI, pulled from a 1992
                            //forum posting here: http://steve.hollasch.net/cgindex/math/rotvecs.html

                            /*	"A somewhat "nasty" way to solve this problem:

                                Let V1 = [ x1, y1, z1 ], V2 = [ x2, y2, z2 ]. Assume V1 and V2 are already normalized.

                                    V3 = normalize(cross(V1, V2)). (the normalization here is mandatory.)
                                    V4 = cross(V3, V1).

                                         [ V1 ]
                                    M1 = [ V4 ]
                                         [ V3 ]

                                    cos = dot(V2, V1), sin = dot(V2, V4)

                                         [ cos   sin    0 ]
                                    M2 = [ -sin  cos    0 ]
                                         [ 0     0      1 ]

                                The sought transformation matrix is just M1^-1 * M2 * M1. This might well be a standard-text solution."

                                -Ben Zhu, SGI, 1992
                             */

                            Vector2[] transformedVertices = new Vector2[0];
                            int infillFrontOffset = 0, infillBackOffset = 0;

                            if (doInfill)
                            {
                                disposables.Add(vectorTwoPool.Get(uniqueVertexCount, false, out transformedVertices));

                                //Based on the algorithm described above, this will create a matrix permitting us
                                //to multiply a given vertex yielding a vertex transformed to an XY plane (where Z is
                                //undefined.)

                                // Based on the algorithm described above, this will create a matrix permitting us
                                //to multiply a given vertex yielding a vertex transformed to an XY plane (where Z is
                                //undefined.)
                                // This algorithm cannot work if we're already in that plane. We know if we're already
                                //in that plane if X and Y are both zero and Z is nonzero.
                                bool canUseSimplifiedTransform = Mathf.Approximately(plane.x, 0f) && Mathf.Approximately(plane.y, 0f);
                                if (!canUseSimplifiedTransform)
                                {
                                    Matrix4x4 flattenTransform;

                                    var v1 = Vector3.forward;
                                    var v2 = new Vector3(plane.x, plane.y, plane.z).normalized;
                                    var v3 = Vector3.Cross(v1, v2).normalized;
                                    var v4 = Vector3.Cross(v3, v1);

                                    var cos = Vector3.Dot(v2, v1);
                                    var sin = Vector3.Dot(v2, v4);

                                    Matrix4x4 m1 = Matrix4x4.identity;
                                    m1.SetRow(0, (Vector4)v1);
                                    m1.SetRow(1, (Vector4)v4);
                                    m1.SetRow(2, (Vector4)v3);

                                    Matrix4x4 m1i = m1.inverse;

                                    Matrix4x4 m2 = Matrix4x4.identity;
                                    m2.SetRow(0, new Vector4(cos, sin, 0, 0));
                                    m2.SetRow(1, new Vector4(-sin, cos, 0, 0));

                                    flattenTransform = m1i * m2 * m1;

                                    for (int i = 0; i < uniqueVertexCount; i++)
                                    {
                                        transformedVertices[i] = flattenTransform.MultiplyPoint3x4(newVertices[i]);
                                    }
                                }
                                else
                                {
                                    var sign = Mathf.Sign(plane.z);
                                    for (int i = 0; i < uniqueVertexCount; i++)
                                    {
                                        transformedVertices[i] = new Vector2(newVertices[i].x, sign * newVertices[i].y);
                                    }
                                }

                                // We want to normalize the entire transformed vertices. To do this, we find the largest
                                //floats in either (by abs). Then we scale. Of course, this normalizes us to figures
                                //in the range of [-1f,1f] (not necessarily extending all the way on both sides), and
                                //what we need are figures between 0f and 1f (not necessarily filling, but necessarily
                                //not spilling.) So we'll shift it here.
                                {
                                    float x = 0f, y = 0f;

                                    for (int i = 0; i < uniqueVertexCount; i++)
                                    {
                                        Vector2 v = transformedVertices[i];

                                        v.x = Mathf.Abs(v.x);
                                        v.y = Mathf.Abs(v.y);

                                        if (v.x > x) x = v.x;
                                        if (v.y > y) y = v.y;
                                    }

                                    //We would use 1f/x, 1f/y but we also want to scale everything to half (and perform an offset) as
                                    //described above.
                                    x = 0.5f / x;
                                    y = 0.5f / y;

                                    var r = new Rect(0, 0, 1f, 1f);
                                    
                                    for (int i = 0; i < uniqueVertexCount; i++)
                                    {
                                        var v = transformedVertices[i];
                                        v.x *= x;
                                        v.y *= y;
                                        v.x += 0.5f;
                                        v.y += 0.5f;
                                        v.x *= r.width;
                                        v.y *= r.height;
                                        v.x += r.x;
                                        v.y += r.y;
                                        transformedVertices[i] = v;
                                    }
                                }

                                //Now let's build the geometry for the two slice in-fills.
                                //One is for the front side, and the other for the back side. Each has differing normals.

                                infillFrontOffset = uniqueVertexCount;
                                infillBackOffset = uniqueVertexCount * 2;

                                //The geometry is identical...

                                System.Array.Copy(newVertices, 0, newVertices, infillFrontOffset, uniqueVertexCount);
                                System.Array.Copy(newVertices, 0, newVertices, infillBackOffset, uniqueVertexCount);

                                System.Array.Copy(newWeights, 0, newWeights, infillFrontOffset, uniqueVertexCount);
                                System.Array.Copy(newWeights, 0, newWeights, infillBackOffset, uniqueVertexCount);

                                System.Array.Copy(transformedVertices, 0, newUVs, infillFrontOffset, uniqueVertexCount);
                                System.Array.Copy(transformedVertices, 0, newUVs, infillBackOffset, uniqueVertexCount);

                                Vector3 infillFrontNormal = ((Vector3)plane) * -1f;
                                infillFrontNormal.Normalize();

                                for (int i = infillFrontOffset; i < infillBackOffset; i++)
                                    newNormals[i] = infillFrontNormal;

                                Vector3 infillBackNormal = (Vector3)plane;
                                infillBackNormal.Normalize();

                                for (int i = infillBackOffset; i < numberOfVerticesAdded; i++)
                                    newNormals[i] = infillBackNormal;
                            }
                            
                            //Note that here we refer to split actions again, so let's copy back the updated splitActions.
                            for (int i = 0; i < intersectionCount; i++)
                            {
                                int j = intersectionInverseRelation[i];
                                splitActionsBuilder.array[j] = intersectionActions[i];
                            }

                            DirectTransferStage(splitActionsBuilder, alfaShapes, inputTriangleCount, SplitAction.TO_ALFA, alfaIndicesBuilder);
                            DirectTransferStage(splitActionsBuilder, bravoShapes, inputTriangleCount, SplitAction.TO_BRAVO, bravoIndicesBuilder);

                            //Let's add this shiznit in!

                            verticesBuilder.AddArray(newVertices, numberOfVerticesAdded);
                            normalsBuilder.AddArray(newNormals, numberOfVerticesAdded);
                            coordsBuilder.AddArray(newUVs, numberOfVerticesAdded);
                            weightsBuilder.AddArray(newWeights, numberOfVerticesAdded);

                            //Now we need to fill in the slice hole. There are TWO infillers; the Sloppy and Meticulous.

                            //The sloppy infiller will find a point in the middle of all slice vertices and produce a triangle fan.
                            //It can work fast, but will have issues with non-roundish cross sections or cross sections with multiple holes.

                            //The meticulous infill can distinguish between polygons and accurately fill multiple holes, but is more sensitive to
                            //geometrical oddities. It may fail when slicing certain joints because of the way that not all geometry is sliced.
                            //It is transferred from Turbo Slicer, where it is a key part of the product, but it is not most appropriate here.
                            //Nevertheless, it is here in case it is needed.

                            if (doInfill && jobSpec.InfillMode == InfillMode.Sloppy)
                            {
                                var centerVertex = new VectorAccumulator();
                                var centerUV = new VectorAccumulator();
                                var centerNormal = new VectorAccumulator();
                                
                                var weightsByBone = new Dictionary<int, float>();

                                int sliceVertexCount = numberOfVerticesAdded / 3;

                                for (int i = 0; i < sliceVertexCount; i++)
                                {
                                    centerVertex.Add(newVertices[i]);
                                    centerUV.Add(newUVs[i]);
                                    centerNormal.Add(newNormals[i]);

                                    BoneWeight bw = newWeights[i];

                                    if (weightsByBone.ContainsKey(bw.boneIndex0))
                                        weightsByBone[bw.boneIndex0] += bw.weight0;
                                    else
                                        weightsByBone[bw.boneIndex0] = bw.weight0;

                                    /*if(weightsByBone.ContainsKey(bw.boneIndex1))
                                        weightsByBone[bw.boneIndex1] += bw.weight1;
                                    else
                                        weightsByBone[bw.boneIndex1] = bw.weight1;

                                    if(weightsByBone.ContainsKey(bw.boneIndex2))
                                        weightsByBone[bw.boneIndex2] += bw.weight2;
                                    else
                                        weightsByBone[bw.boneIndex2] = bw.weight2;

                                    if(weightsByBone.ContainsKey(bw.boneIndex3))
                                        weightsByBone[bw.boneIndex3] += bw.weight3;
                                    else
                                        weightsByBone[bw.boneIndex3] = bw.weight3;*/
                                }

                                var orderedWeights = new List<KeyValuePair<int, float>>(weightsByBone);

                                orderedWeights.Sort((firstPair, nextPair) =>
                                {
                                    return -firstPair.Value.CompareTo(nextPair.Value);
                                }
                                );

                                var centerWeight = new BoneWeight();
                                var weightNormalizer = Vector4.zero;

                                if (orderedWeights.Count > 0)
                                {
                                    centerWeight.boneIndex0 = orderedWeights[0].Key;
                                    weightNormalizer.x = 1f;
                                }

                                weightNormalizer.Normalize();

                                centerWeight.weight0 = weightNormalizer.x;
                                centerWeight.weight1 = weightNormalizer.y;
                                centerWeight.weight2 = weightNormalizer.z;
                                centerWeight.weight3 = weightNormalizer.w;

                                int centerIndex = verticesBuilder.Count;

                                verticesBuilder.Add(centerVertex.Mean);
                                coordsBuilder.Add(centerUV.Mean);
                                normalsBuilder.Add(centerNormal.Mean);
                                weightsBuilder.Add(centerWeight);
                            
                                var transformedCenter = Vector2.zero;
                                for(int i = 0; i < uniqueVertexCount; i++)
                                {
                                    transformedCenter += transformedVertices[i];
                                }
                                transformedCenter /= uniqueVertexCount;

                                var angleByIndex = new Dictionary<int, float>();
                                for (int i = 0; i < uniqueVertexCount; i++)
                                {
                                    Vector2 delta = transformedVertices[i] - transformedCenter;
                                    angleByIndex[i] = Mathf.Atan2(delta.y, delta.x);
                                }

                                var orderedVertices = new List<KeyValuePair<int, float>>(angleByIndex);

                                orderedVertices.Sort((firstPair, nextPair) =>
                                {
                                    return firstPair.Value.CompareTo(nextPair.Value);
                                }
                                );

                                for (var i = 0; i < orderedVertices.Count; i++)
                                {
                                    var atEnd = i == orderedVertices.Count - 1;
                                    var iNext = atEnd ? 0 : i + 1;

                                    var index0 = orderedVertices[i].Key;
                                    var index1 = orderedVertices[iNext].Key;

                                    var frontInfillIndices = new [] { centerIndex, index1 + infillFrontOffset + newIndexStartsAt, index0 + infillFrontOffset + newIndexStartsAt };
                                    alfaInfill.AddArray(frontInfillIndices);

                                    var backInfillIndices = new [] { centerIndex, index0 + infillBackOffset + newIndexStartsAt, index1 + infillBackOffset + newIndexStartsAt };
                                    bravoInfill.AddArray(backInfillIndices);
                                }
                            }
                            else if (doInfill && jobSpec.InfillMode == InfillMode.Meticulous)
                            {
                                ArrayBuilder<int> polygonBuilder = null;
                                var allPolys = new List<ArrayBuilder<int>>();

                                using (var _availabilityBuffer = boolPool.Get(intersectionCount, false))
                                using (var _sqrMags = floatArrayPool.Get(uniqueVertexCount, false))
                                {
                                    var availabilityBuffer = _availabilityBuffer.Object;
                                    for (int i = 0; i < intersectionCount; i++)
                                    {
                                        availabilityBuffer[i] = true;
                                    }

                                    var sqrMags = _sqrMags.Object;

                                    for (int i = 0; i < uniqueVertexCount; i++)
                                    {
                                        sqrMags[i] = newVertices[i].sqrMagnitude;
                                    }

                                    var availabilityCount = intersectionCount;

                                    int? seekingFor = null;

                                    while (seekingFor.HasValue || availabilityCount > 0)
                                    {
                                        const int NotFound = -1;
                                        var recountAvailability = false;

                                        if (seekingFor.HasValue)
                                        {
                                            var seekingForLocalIndex = localIndexByIntersection[seekingFor.Value];

                                            //It would seem we could use the 2D transformed vertices but I want the
                                            //least manipulated figures.
                                            var seekingForValue = newVertices[seekingForLocalIndex];
                                            var seekingForValueX = (double)seekingForValue.x;
                                            var seekingForValueY = (double)seekingForValue.y;
                                            var seekingForValueZ = (double)seekingForValue.z;

                                            var loopStartIndex = polygonBuilder.array[0];

                                            var bestMatchIndex = NotFound;
                                            var bestMatchDelta = double.MaxValue;

                                            for (int i = 0; i < intersectionCount; i++)
                                            {
                                                var isAvailable = i == loopStartIndex || availabilityBuffer[i];
                                                if (isAvailable)
                                                {
                                                    var candidateLocalIndex = localIndexByIntersection[i];

                                                    //The quickest way to show they match is if they have matching vertex index.
                                                    if (candidateLocalIndex == seekingForLocalIndex)
                                                    {
                                                        bestMatchIndex = i;
                                                        bestMatchDelta = 0.0;
                                                    }
                                                    else
                                                    {
                                                        //Otherwise, let's just check if it's closer than the current best candidate.

                                                        var candidateValue = newVertices[candidateLocalIndex];

                                                        var candidateX = (double)candidateValue.x;
                                                        var candidateY = (double)candidateValue.y;
                                                        var candidateZ = (double)candidateValue.z;
                                                        var dx = seekingForValueX - candidateX;
                                                        var dy = seekingForValueY - candidateY;
                                                        var dz = seekingForValueZ - candidateZ;
                                                        var candidateDelta = dx * dx + dy * dy + dz * dz;
                                                        if (candidateDelta < bestMatchDelta)
                                                        {
                                                            bestMatchIndex = i;
                                                            bestMatchDelta = candidateDelta;
                                                        }
                                                    }
                                                }
                                            }

                                            if (bestMatchIndex == NotFound)
                                            {
                                                //Fail; drop the current polygon.
                                                seekingFor = null;
                                                polygonBuilder = null;
                                            }
                                            else if (bestMatchIndex == loopStartIndex)
                                            {
                                                //Loop complete; consume the current polygon.
                                                seekingFor = null;

                                                if (polygonBuilder.Count >= 3)
                                                {
                                                    allPolys.Add(polygonBuilder);
                                                }
                                            }
                                            else
                                            {
                                                int partnerByParent = bestMatchIndex % 2 == 1 ? bestMatchIndex - 1 : bestMatchIndex + 1;
                                                availabilityBuffer[bestMatchIndex] = false;
                                                availabilityBuffer[partnerByParent] = false;
                                                recountAvailability = true;
                                                seekingFor = partnerByParent;

                                                bool isDegenerate;

                                                //Before we add this to the polygon let's check if it's the same as the last one.

                                                var bestMatchLocalIndex = localIndexByIntersection[bestMatchIndex];
                                                var lastAddedLocalIndex = localIndexByIntersection[polygonBuilder.array[polygonBuilder.Count - 1]];

                                                //The cheapest way to spot a match is if they have the same index in the vertex array.
                                                if (bestMatchLocalIndex != lastAddedLocalIndex)
                                                {
                                                    const float ePositive = 1.0f / 65536.0f;
                                                    const float eMinus = -1.0f / 65536.0f;

                                                    var sqrMagDelta = sqrMags[bestMatchLocalIndex] - sqrMags[lastAddedLocalIndex];

                                                    //The cheapest way to show they _don't_ match is to see if their square magnitudes differ.
                                                    if (sqrMagDelta < ePositive && sqrMagDelta > eMinus)
                                                    {

                                                        //If all else fails, check the different on a dimension by dimension basis.
                                                        //Note that we can use the two dimensional "transformed vertices" set to do this
                                                        //since we only care about degerency with respect to the infill, which is done in
                                                        //two dimensional space.

                                                        var alfa = transformedVertices[bestMatchLocalIndex];
                                                        var bravo = transformedVertices[lastAddedLocalIndex];
                                                        var delta = alfa - bravo;

                                                        isDegenerate = delta.x > eMinus && delta.y > eMinus && delta.x < ePositive && delta.y < ePositive;
                                                    }
                                                    else
                                                    {
                                                        isDegenerate = false;
                                                    }
                                                }
                                                else
                                                {
                                                    isDegenerate = true;
                                                }

                                                if (!isDegenerate)
                                                {
                                                    polygonBuilder.Add(bestMatchIndex);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            //Here we're going to try to start a loop. Find any unclaimed index and go from there.

                                            var loopStartIndex = NotFound;

                                            for (int i = 0; i < intersectionCount; i++)
                                            {
                                                if (availabilityBuffer[i])
                                                {
                                                    loopStartIndex = i;
                                                    break;
                                                }
                                            }

                                            if (loopStartIndex != NotFound)
                                            {
                                                int partnerByParent = loopStartIndex % 2 == 1 ? loopStartIndex - 1 : loopStartIndex + 1;
                                                availabilityBuffer[loopStartIndex] = false;
                                                availabilityBuffer[partnerByParent] = false;
                                                recountAvailability = true;
                                                seekingFor = partnerByParent;
                                                disposables.Add(intBuilderPool.Get(availabilityCount, out polygonBuilder));
                                                polygonBuilder.Add(loopStartIndex);
                                            }
                                        }

                                        if (recountAvailability)
                                        {
                                            availabilityCount = 0;
                                            for (int i = 0; i < intersectionCount; i++)
                                            {
                                                if (availabilityBuffer[i])
                                                {
                                                    availabilityCount++;
                                                }
                                            }
                                        }
                                    }
                                }

                                for (var polyIndex = 0; polyIndex < allPolys.Count; polyIndex++)
                                {
                                    var intersectionIndicesForThisPolygon = allPolys[polyIndex];

                                    var triangleBufferSize = Triangulation.GetArraySize(intersectionIndicesForThisPolygon.Count);

                                    using (var _geometryForThisPolygon = vectorTwoPool.Get(intersectionIndicesForThisPolygon.Count, false))
                                    using (var _triangleBuffer = intArrayPool.Get(triangleBufferSize, true))
                                    {
                                        var geometryForThisPolygon = _geometryForThisPolygon.Object;

                                        for (int i = 0; i < intersectionIndicesForThisPolygon.Count; i++)
                                        {
                                            int j = localIndexByIntersection[intersectionIndicesForThisPolygon.array[i]];
                                            geometryForThisPolygon[i] = transformedVertices[j];
                                        }

                                        var triangulationBuffer = _triangleBuffer.Object;

                                        if (Triangulation.Triangulate(geometryForThisPolygon, intersectionIndicesForThisPolygon.Count, triangulationBuffer))
                                        {
                                            using (var _alfa = intArrayPool.Get(triangleBufferSize, false))
                                            using (var _bravo = intArrayPool.Get(triangleBufferSize, false))
                                            {

                                                var alfa = _alfa.Object;
                                                var bravo = _bravo.Object;

                                                for (var i = 0; i < triangleBufferSize; i++)
                                                {
                                                    var intersection = intersectionIndicesForThisPolygon.array[triangulationBuffer[i]];
                                                    var local = localIndexByIntersection[intersection];
                                                    alfa[i] = local + infillFrontOffset + newIndexStartsAt;
                                                    bravo[i] = local + infillBackOffset + newIndexStartsAt;
                                                }

                                                //Invert the winding on the alfa side
                                                for (int i = 0; i < triangleBufferSize; i += 3)
                                                {
                                                    var j = alfa[i];
                                                    alfa[i] = alfa[i + 2];
                                                    alfa[i + 2] = j;
                                                }

                                                alfaInfill.AddArray(alfa, triangleBufferSize);
                                                bravoInfill.AddArray(bravo, triangleBufferSize);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    MeshSnapshot penultimateSnapshot;

                    if (jobSpec.InfillMaterial != null && !snapshot.infillIndex.HasValue)
                    {
                        var oldLength = snapshot.materials.Length;
                        var newLength = oldLength + 1;

                        var materialArray = new Material[newLength];
                        System.Array.Copy(snapshot.materials, materialArray, oldLength);
                        materialArray[newLength - 1] = jobSpec.InfillMaterial;

                        ArrayBuilder<int>[] indexArray;

                        indexArray = new ArrayBuilder<int>[newLength];
                        System.Array.Copy(alfaIndicesBySubmesh, indexArray, oldLength);
                        indexArray[newLength - 1] = alfaInfill;
                        alfaIndicesBySubmesh = indexArray;

                        indexArray = new ArrayBuilder<int>[newLength];
                        System.Array.Copy(bravoIndicesBySubmesh, indexArray, oldLength);
                        indexArray[newLength - 1] = bravoInfill;
                        bravoIndicesBySubmesh = indexArray;

                        submeshCount++;

                        penultimateSnapshot = snapshot.WithMaterials(materialArray);
                    }
                    else
                    {
                        penultimateSnapshot = snapshot;
                    }
                    
                    var alfaSnapshot = penultimateSnapshot.EmbraceAndExtend(verticesBuilder, normalsBuilder, coordsBuilder, weightsBuilder, alfaIndicesBySubmesh);
                    var bravoSnapshot = penultimateSnapshot.EmbraceAndExtend(verticesBuilder, normalsBuilder, coordsBuilder, weightsBuilder, bravoIndicesBySubmesh);
                    
                    alfaBuilder.Add(alfaSnapshot);
                    bravoBuilder.Add(bravoSnapshot);
                }

				Vector4 planeInWorldSpace;
				Vector3 focalPointInWorldSpace;

				{
					var severedJointKey = jobSpec.JointName;

					VectorAccumulator meanChildPosition = new VectorAccumulator();

					if (jobSpec.RootTipProgression > 0f)
					{
						foreach (KeyValuePair<string, NodeMetadata> kvp in metadataByNodeName)
						{
							if (kvp.Value.ParentKey == severedJointKey)
							{
								meanChildPosition.Add(kvp.Value.LocalPosition);
							}
						}
					}

					var severedJointMetadata = metadataByNodeName[severedJointKey];

					var severedJointMatrix = severedJointMetadata.WorldToLocalMatrix.inverse;

					NodeMetadata parentJointMetadata;

					Vector3 position0, position1;

					position1 = severedJointMatrix.MultiplyPoint3x4(Vector3.zero);

					if (metadataByNodeName.TryGetValue(severedJointMetadata.ParentKey, out parentJointMetadata))
					{
						var severedJointParentMatrix = parentJointMetadata.WorldToLocalMatrix.inverse;
						position0 = severedJointParentMatrix.MultiplyPoint3x4(Vector3.zero);
					}
					else {
						position0 = position1;
					}

					var position2 = severedJointMatrix.MultiplyPoint3x4(meanChildPosition.Mean);

					var deltaParent = position0 - position1;
					var deltaChildren = position1 - position2;

					focalPointInWorldSpace = Vector3.Lerp(position1, position2, jobSpec.RootTipProgression);

					var normalFromParentToChild = -Vector3.Lerp(deltaParent, deltaChildren, jobSpec.RootTipProgression).normalized;

					if (jobSpec.TiltPlane.HasValue)
					{
						var fromWorldToLocalSpaceOfBone = jobSpec.NodeMetadata[severedJointKey].WorldToLocalMatrix;

						var v = jobSpec.TiltPlane.Value;
						v = fromWorldToLocalSpaceOfBone.MultiplyVector(v);
						v = severedJointMatrix.MultiplyVector(v);
						v.Normalize();

						if (Vector3.Dot(v, normalFromParentToChild) < 0f)
						{
							v = -v;
						}

						planeInWorldSpace = ClampNormalToBicone(v, normalFromParentToChild, 30f);
					}
					else
					{
						planeInWorldSpace = normalFromParentToChild;
					}

					planeInWorldSpace.w = -(planeInWorldSpace.x * focalPointInWorldSpace.x + planeInWorldSpace.y * focalPointInWorldSpace.y + planeInWorldSpace.z * focalPointInWorldSpace.z);
				}

				jobState.Yield = new JobYield(jobSpec, planeInWorldSpace, focalPointInWorldSpace, alfaBuilder, bravoBuilder);
            }
            catch (System.Exception ex)
            {
                jobState.Exception = ex;
            }
            finally
            {
                for (int i = 0; i < disposables.Count; i++)
                {
                    disposables[i].Dispose();
                }
            }
        }

        private static void DirectTransferStage(ArrayBuilder<SplitAction> splitActionsBuilder, Shape[] shapes, int inputTriangleCount, int flag, ArrayBuilder<int> indexBuilder)
        {
            using (var _newIndexBuilder = intBuilderPool.Get(0))
            {
                var newIndexBuilder = _newIndexBuilder.Object;

                for (int i = 0; i < splitActionsBuilder.length; i++)
                {
                    var sa = splitActionsBuilder.array[i];
                    if ((sa.flags & flag) == flag)
                    {
                        newIndexBuilder.Add(sa.realIndex);
                    }
                }

                //Now we need to triangulate sets of quads.
                //We recorded earlier whether we're looking at triangles or quads – in order. So we have a pattern like TTQTTQQTTTQ, and
                //we can expect these vertices to match up perfectly to what the above section of code dumped out.

                int startIndex = 0;

                int[] _indices3 = new int[3];
                int[] _indices4 = new int[6];

                for (int i = 0; i < inputTriangleCount; i++)
                {
                    var s = shapes[i];
                    switch (s)
                    {
                        case Shape.Triangle:
                            _indices3[0] = newIndexBuilder.array[startIndex];
                            _indices3[1] = newIndexBuilder.array[startIndex + 1];
                            _indices3[2] = newIndexBuilder.array[startIndex + 2];
                            indexBuilder.AddArray(_indices3);
                            startIndex += 3;
                            break;
                        case Shape.Quad:
                            _indices4[0] = newIndexBuilder.array[startIndex];
                            _indices4[1] = newIndexBuilder.array[startIndex + 1];
                            _indices4[2] = newIndexBuilder.array[startIndex + 3];
                            _indices4[3] = newIndexBuilder.array[startIndex + 1];
                            _indices4[4] = newIndexBuilder.array[startIndex + 2];
                            _indices4[5] = newIndexBuilder.array[startIndex + 3];
                            indexBuilder.AddArray(_indices4);
                            startIndex += 4;
                            break;
                    }
                }
            }
        }
        
        public static MeshSnapshot EmbraceAndExtend(this MeshSnapshot source,
            ArrayBuilder<Vector3> newVertices, ArrayBuilder<Vector3> newNormals, ArrayBuilder<Vector2> newCoords, ArrayBuilder<BoneWeight> newWeights, ArrayBuilder<int>[] newIndicesBySubmesh)
        {
            const int Unassigned = -1;

            var vertexCount = newVertices.length;
            var submeshCount = newIndicesBySubmesh.Length;

            int[] transferTable;
            using (intArrayPool.Get(vertexCount, false, out transferTable))
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    transferTable[i] = Unassigned;
                }

                var targetIndex = 0;

                var targetIndexArrays = new int[submeshCount][];

                for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    var sourceIndices = newIndicesBySubmesh[submeshIndex];
                    var targetIndices = targetIndexArrays[submeshIndex] = new int[sourceIndices.length];

                    for (int i = 0; i < sourceIndices.length; i++)
                    {
                        int requestedVertex = sourceIndices.array[i];

                        int j = transferTable[requestedVertex];

                        if (j == Unassigned)
                        {
                            j = targetIndex;
                            transferTable[requestedVertex] = j;
                            targetIndex++;
                        }

                        targetIndices[i] = j;
                    }
                }

                var newVertexCount = targetIndex;

                var targetVertices = new Vector3[newVertexCount];
                var targetCoords = new Vector2[newVertexCount];
                var targetNormals = new Vector3[newVertexCount];
                var boneWeights = new BoneWeight[newVertexCount];

                for (int i = 0; i < vertexCount; i++)
                {
                    int j = transferTable[i];
                    if (j != Unassigned)
                    {
                        targetVertices[j] = newVertices.array[i];
                        targetCoords[j] = newCoords.array[i];
                        targetNormals[j] = newNormals.array[i];
                        boneWeights[j] = newWeights.array[i];
                    }
                }

                Vector4[] targetTangents;

                if (source.tangents.Length > 0)
                {
                    //This code assumes that the new geometry is a proper superset of the old geometry.
                    //But there is a catch; while new geometry is provided, new tangents are not.
                    //So we source some tangents from the source data, but over that limit, we have to
                    //generate them.

                    targetTangents = new Vector4[newVertexCount];

                    var newGeometryStartsFrom = source.vertices.Length;

                    for (int i = 0; i < newGeometryStartsFrom; i++)
                    {
                        int j = transferTable[i];
                        if (j != Unassigned)
                        {
                            targetTangents[j] = source.tangents[i];
                        }
                    }

                    //Based on code here:
                    //http://www.cs.upc.edu/~virtual/G/1.%20Teoria/06.%20Textures/Tangent%20Space%20Calculation.pdf

                    Vector3[] tan1, tan2;

                    using (vectorThreePool.Get(vertexCount, true, out tan1))
                    using (vectorThreePool.Get(vertexCount, true, out tan2))
                    {
                        for (int i = 0; i < newIndicesBySubmesh.Length; i++)
                        {
                            var triangles = newIndicesBySubmesh[i];

                            for (int j = 0; j < triangles.length;)
                            {
                                var j1 = triangles.array[j++];
                                var j2 = triangles.array[j++];
                                var j3 = triangles.array[j++];

                                Debug.Assert(j1 != j2 && j1 != j3);

                                var isRelevant = j1 >= newGeometryStartsFrom || j2 >= newGeometryStartsFrom || j3 >= newGeometryStartsFrom;

                                if (isRelevant)
                                {
                                    var v1 = newVertices.array[j1];
                                    var v2 = newVertices.array[j2];
                                    var v3 = newVertices.array[j3];

                                    //We have to test for degeneracy.
                                    var e1 = v1 - v2;
                                    var e2 = v1 - v3;
                                    const float epsilon = 1.0f / 65536.0f;
                                    if (e1.sqrMagnitude > epsilon && e2.sqrMagnitude > epsilon)
                                    {
                                        var w1 = newCoords.array[j1];
                                        var w2 = newCoords.array[j2];
                                        var w3 = newCoords.array[j3];

                                        var x1 = v2.x - v1.x;
                                        var x2 = v3.x - v1.x;
                                        var y1 = v2.y - v1.y;
                                        var y2 = v3.y - v1.y;
                                        var z1 = v2.z - v1.z;
                                        var z2 = v3.z - v1.z;

                                        var s1 = w2.x - w1.x;
                                        var s2 = w3.x - w1.x;
                                        var t1 = w2.y - w1.y;
                                        var t2 = w3.y - w1.y;

                                        var r = 1.0f / (s1 * t2 - s2 * t1);

                                        var sX = (t2 * x1 - t1 * x2) * r;
                                        var sY = (t2 * y1 - t1 * y2) * r;
                                        var sZ = (t2 * z1 - t1 * z2) * r;

                                        var tX = (s1 * x2 - s2 * x1) * r;
                                        var tY = (s1 * y2 - s2 * y1) * r;
                                        var tZ = (s1 * z2 - s2 * z1) * r;

                                        var tan1j1 = tan1[j1];
                                        var tan1j2 = tan1[j2];
                                        var tan1j3 = tan1[j3];
                                        var tan2j1 = tan2[j1];
                                        var tan2j2 = tan2[j2];
                                        var tan2j3 = tan2[j3];

                                        tan1j1.x += sX;
                                        tan1j1.y += sY;
                                        tan1j1.z += sZ;
                                        tan1j2.x += sX;
                                        tan1j2.y += sY;
                                        tan1j2.z += sZ;
                                        tan1j3.x += sX;
                                        tan1j3.y += sY;
                                        tan1j3.z += sZ;

                                        tan2j1.x += tX;
                                        tan2j1.y += tY;
                                        tan2j1.z += tZ;
                                        tan2j2.x += tX;
                                        tan2j2.y += tY;
                                        tan2j2.z += tZ;
                                        tan2j3.x += tX;
                                        tan2j3.y += tY;
                                        tan2j3.z += tZ;

                                        tan1[j1] = tan1j1;
                                        tan1[j2] = tan1j2;
                                        tan1[j3] = tan1j3;
                                        tan2[j1] = tan2j1;
                                        tan2[j2] = tan2j2;
                                        tan2[j3] = tan2j3;
                                    }
                                }
                            }
                        }

                        for (int i = newGeometryStartsFrom; i < vertexCount; i++)
                        {
                            int j = transferTable[i];
                            if (j != Unassigned)
                            {
                                var n = newNormals.array[i];
                                var t = tan1[i];

                                // Gram-Schmidt orthogonalize
                                Vector3.OrthoNormalize(ref n, ref t);

                                targetTangents[j].x = t.x;
                                targetTangents[j].y = t.y;
                                targetTangents[j].z = t.z;

                                // Calculate handedness
                                targetTangents[j].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f) ? -1.0f : 1.0f;
                            }
                        }
                    }
                }
                else
                {
                    targetTangents = new Vector4[0];
                }                

                return new MeshSnapshot(source.key, targetVertices, targetNormals, targetCoords, targetTangents, boneWeights, source.materials, source.boneMetadata, source.infillIndex, targetIndexArrays);
            }
        }

        public static Vector3 ClampNormalToBicone(Vector3 input, Vector3 axis, float maximumDegrees)
        {
            var minimumDotProduct = Mathf.Cos(maximumDegrees * Mathf.Deg2Rad);

            var dotProduct = Vector3.Dot(input, axis);

            var result = input;

            if (Mathf.Abs(dotProduct) < minimumDotProduct)
            {
                var sign = Mathf.Sign(dotProduct);

                var differenceBetweenNowAndIdeal = minimumDotProduct - Mathf.Abs(dotProduct);

                var repairativeContribution = axis * differenceBetweenNowAndIdeal * sign;

                var currentCorrective = 1f;

                var lowCorrective = 1f;
                var highCorrective = 100f;

                var iterations = 16;

                while (iterations > 0)
                {
                    result = (input + repairativeContribution * currentCorrective).normalized;

                    float dp = Mathf.Abs(Vector3.Dot(result, axis));

                    if (dp > minimumDotProduct)
                    {
                        highCorrective = currentCorrective;
                        currentCorrective = (currentCorrective + lowCorrective) / 2f;
                    }
                    else if (dp < minimumDotProduct)
                    {
                        lowCorrective = currentCorrective;
                        currentCorrective = (currentCorrective + highCorrective) / 2f;
                    }

                    iterations--;
                }
            }

            return result;
        }

        public static float ClassifyPoint(ref Vector4 plane, ref Vector3 p)
        {
            return p.x * plane.x + p.y * plane.y + p.z * plane.z + plane.w;
        }

        public static TriangleAssignment GetSidePlane(ref Vector3 p, ref Vector4 plane)
        {
            double d = DistanceToPoint(ref p, ref plane);

            if ((d + float.Epsilon) > 0)
                return TriangleAssignment.Alfa; // it is 'in front' within the provided epsilon value.

            return TriangleAssignment.Bravo;
        }

        public static float DistanceToPoint(ref Vector3 p, ref Vector4 plane)
        {
            float d = p.x * plane.x + p.y * plane.y + p.z * plane.z + plane.w;
            return d;
        }
    }
}