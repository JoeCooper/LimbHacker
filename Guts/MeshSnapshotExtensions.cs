using System;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public static class MeshSnapshotExtensions
	{
		static readonly ArrayPool<int> intArrayPool = new ArrayPool<int>(4);
		static readonly ArrayPool<Vector3> vectorThreePool = new ArrayPool<Vector3>(4);

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
	}
}
