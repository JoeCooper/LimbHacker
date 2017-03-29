using System.Collections.Generic;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public class MeshSnapshot
	{
        public static readonly Vector4[] EmptyTangents = new Vector4[0];

		public MeshSnapshot (string key,
            Vector3[] vertices, Vector3[] normals, Vector2[] coords, Vector4[] tangents, BoneWeight[] boneWeights,
            Material[] materials,
            BoneMetadata[] boneMetadata,
            int? infillIndex, int[][] indices)
		{
            this.key = key;
            this.vertices = vertices;
			this.normals = normals;
			this.coords = coords;
			this.tangents = tangents;
            this.materials = materials;
            this.boneMetadata = boneMetadata;
            this.boneWeights = boneWeights;
            this.infillIndex = infillIndex;
            this.indices = indices;
        }

        public readonly string key;
		public readonly Vector3[] vertices;
		public readonly Vector3[] normals;
		public readonly Vector2[] coords;
		public readonly Vector4[] tangents;
        public readonly int? infillIndex;
        public readonly Material[] materials;
        public readonly BoneMetadata[] boneMetadata;
        public readonly BoneWeight[] boneWeights;

		public readonly int[][] indices;

        public MeshSnapshot WithKey(string figure)
        {
            return new MeshSnapshot(figure, vertices, normals, coords, tangents, boneWeights, materials, boneMetadata, infillIndex, indices);
        }

        public MeshSnapshot WithInfillIndex(int? infillIndex)
        {
            return new MeshSnapshot(key, vertices, normals, coords, tangents, boneWeights, materials, boneMetadata, infillIndex, indices);
        }
        
        public MeshSnapshot WithBoneMetadata(BoneMetadata[] figure)
        {
            return new MeshSnapshot(key, vertices, normals, coords, tangents, boneWeights, materials, figure, infillIndex, indices);
        }

        public MeshSnapshot WithBoneWeights(BoneWeight[] figure)
        {
            return new MeshSnapshot(key, vertices, normals, coords, tangents, figure, materials, boneMetadata, infillIndex, indices);
        }

        public MeshSnapshot WithMaterials(Material[] figure)
        {
            return new MeshSnapshot(key, vertices, normals, coords, tangents, boneWeights, figure, boneMetadata, infillIndex, indices);
        }

        public MeshSnapshot WithVertices(Vector3[] figure) {
			return new MeshSnapshot(key, figure, normals, coords, tangents, boneWeights, materials, boneMetadata, infillIndex, indices);
		}

		public MeshSnapshot WithNormals(Vector3[] figure) {
			return new MeshSnapshot(key, vertices, figure, coords, tangents, boneWeights, materials, boneMetadata, infillIndex, indices);
		}

		public MeshSnapshot WithCoords(Vector2[] figure) {
			return new MeshSnapshot(key, vertices, normals, figure, tangents, boneWeights, materials, boneMetadata, infillIndex, indices);
		}
        
		public MeshSnapshot WithTangents(Vector4[] figure) {
			return new MeshSnapshot(key, vertices, normals, coords, figure, boneWeights, materials, boneMetadata, infillIndex, indices);
		}

        public MeshSnapshot WithIndices(int[][] figure)
        {
            return new MeshSnapshot(key, vertices, normals, coords, tangents, boneWeights, materials, boneMetadata, infillIndex, figure);
        }
	}
}