#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace NativeTrees {

    public struct OctreeRaycastHit<T> {

        public float3 point;
        public T obj;

    }

    public struct OctreeRaycastHitMinNode<T> : ME.BECS.NativeCollections.IMinHeapNode {

        public OctreeRaycastHit<T> data;
        public tfloat cost;

        public sfloat ExpectedCost => this.cost;
        public int Next { get; set; }

    }

}