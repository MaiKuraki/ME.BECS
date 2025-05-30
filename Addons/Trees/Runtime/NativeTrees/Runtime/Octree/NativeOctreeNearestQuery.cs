#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;

// https://bartvandesande.nl
// https://github.com/bartofzo
// ReSharper disable StaticMemberInGenericType

namespace NativeTrees {
    
    using ME.BECS;

    public partial struct NativeOctree<T> : INativeDisposable {

        /*
         * Note: while it's possible to have implemented this as an enumerator to support foreach, I decided against it
         * because the query cache couldn't be reused and for consistency with the range query (which would seriously suffer from
         * performance if implemented as an enumerator)
         */

        /// <summary>
        /// Perform a nearest neighbour query. 
        /// </summary>
        /// <param name="point">Point to get nearest neighbours for</param>
        /// <param name="maxDistance">Maximum distance to look</param>
        /// <param name="visitor">Handler for when a neighbour is encountered</param>
        /// <param name="distanceSquaredProvider">Provide a calculation for the distance</param>
        /// <typeparam name="U">Handler type for when a neighbour is encountered</typeparam>
        /// <typeparam name="V">Provide a calculation for the distance</typeparam>
        /// <remarks>Allocates native containers. To prevent reallocating for every query, create a <see cref="NearestNeighbourCache"/> struct
        /// and re-use it.</remarks>
        public void Nearest<U, V>(float3 point, tfloat minDistanceSqr, tfloat maxDistanceSqr, ref U visitor, V distanceSquaredProvider = default)
            where U : struct, IOctreeNearestVisitor<T>
            where V : struct, IOctreeDistanceProvider<T> {
            var query = new NearestNeighbourCache(Allocator.Temp);
            query.Nearest(ref this, point, minDistanceSqr, maxDistanceSqr, ref visitor, distanceSquaredProvider);
            query.Dispose();
        }

        /// <summary>
        /// Struct to perform an N-nearest neighbour query on the tree.
        /// </summary>
        /// <remarks>Implemented as a struct because this type of query requires the use of some extra native containers.
        /// You can cache this struct and re-use it to circumvent the extra cost associated with allocating the internal containers.</remarks>
        public struct NearestNeighbourCache : INativeDisposable {

            // objects and nodes are stored in a separate list, as benchmarking turned out,
            // putting everything in one big struct was much, much slower because of the large struct size
            // we want to keep the struct in the minheap as small as possibly as many comparisons and swaps take place there
            private NativeList<ObjWrapper> objList;
            private NativeList<NodeWrapper> nodeList;
            private NativeMinHeap<DistanceAndIndexWrapper, NearestComp> minHeap;

            public NearestNeighbourCache(Allocator allocator) : this(0, allocator) { }

            public NearestNeighbourCache(int initialCapacity, Allocator allocator) {
                this.nodeList = new NativeList<NodeWrapper>(initialCapacity, allocator);
                this.objList = new NativeList<ObjWrapper>(initialCapacity, allocator);
                this.minHeap = new NativeMinHeap<DistanceAndIndexWrapper, NearestComp>(default, allocator);
            }

            public void Dispose() {
                this.objList.Dispose();
                this.nodeList.Dispose();
                this.minHeap.Dispose();
            }

            public JobHandle Dispose(JobHandle inputDeps) {
                return JobHandle.CombineDependencies(this.objList.Dispose(inputDeps), this.nodeList.Dispose(inputDeps), this.minHeap.Dispose(inputDeps));
            }

            /// <summary>
            /// Perform a nearest neighbour query. 
            /// </summary>
            /// <param name="octree">Octree to perform the query on</param>
            /// <param name="point">Point to get nearest neighbours for</param>
            /// <param name="minDistanceSqr">Minimum distance to look</param>
            /// <param name="maxDistanceSqr">Maximum distance to look</param>
            /// <param name="visitor">Handler for when a neighbour is encountered</param>
            /// <param name="distanceSquaredProvider">Provide a calculation for the distance</param>
            /// <typeparam name="U">Handler type for when a neighbour is encountered</typeparam>
            /// <typeparam name="V">Provide a calculation for the distance</typeparam>
            public void Nearest<U, V>(ref NativeOctree<T> octree, float3 point, tfloat minDistanceSqr, tfloat maxDistanceSqr, ref U visitor, V distanceSquaredProvider = default)
                where U : struct, IOctreeNearestVisitor<T>
                where V : struct, IOctreeDistanceProvider<T> {
                // reference for the method used:
                // https://stackoverflow.com/questions/41306122/nearest-neighbor-search-in-octree
                // - add root to priority queue
                // - pop queue, if it's an object, it's the closest one, if it's a node, add it's children to the queue
                // - repeat

                this.minHeap.Clear();
                this.nodeList.Clear();
                this.objList.Clear();

                var root = new NodeWrapper(
                    1,
                    0,
                    0,
                    new ExtentsBounds(octree.boundsCenter, octree.boundsExtents));

                // Add our first octants to the heap
                this.NearestNodeNext(
                    ref octree,
                    point,
                    ref root,
                    minDistanceSqr,
                    maxDistanceSqr,
                    0);

                while (this.minHeap.TryPop(out var nearestWrapper)) {
                    if (nearestWrapper.isNode) {
                        this.NearestNode(
                            ref octree,
                            point,
                            distanceAndIndexWrapper: nearestWrapper,
                            minDistanceSquared: minDistanceSqr,
                            maxDistanceSquared: maxDistanceSqr,
                            distanceProvider: distanceSquaredProvider);
                    } else {
                        var item = this.objList[nearestWrapper.objIndex];
                        if (minDistanceSqr > 0f && math.distancesq(item.bounds.Center, point) <= minDistanceSqr) continue;
                        if (visitor.OnVisit(item.obj, item.bounds) == false) {
                            break;
                        }
                    }
                }
            }

            private void NearestNode<V>(ref NativeOctree<T> octree, float3 point, tfloat minDistanceSquared, tfloat maxDistanceSquared,
                                        in DistanceAndIndexWrapper distanceAndIndexWrapper, V distanceProvider = default)
                where V : struct, IOctreeDistanceProvider<T> {
                ref var node = ref this.nodeList.ElementAt(distanceAndIndexWrapper.nodeIndex);
                ref var objects = ref octree.objects;

                // Leaf?
                if (node.nodeCounter <= octree.objectsPerNode || node.nodeDepth == octree.maxDepth) {
                    if (objects.TryGetFirstValue(node.nodeId, out var objWrapper, out var it)) {
                        do {
                            var objDistanceSquared = distanceProvider.DistanceSquared(point, objWrapper.obj, objWrapper.bounds);
                            if (objDistanceSquared > maxDistanceSquared) {
                                continue;
                            }

                            var objIndex = this.objList.Length;
                            this.objList.Add(objWrapper);

                            this.minHeap.Push(new DistanceAndIndexWrapper(
                                                  objDistanceSquared,
                                                  objIndex,
                                                  0,
                                                  false));

                        } while (objects.TryGetNextValue(out objWrapper, ref it));
                    }

                    return;
                }

                // Add child nodes
                this.NearestNodeNext(
                    ref octree,
                    point,
                    ref node,
                    minDistanceSquared,
                    maxDistanceSquared,
                    node.nodeDepth);
            }

            private void NearestNodeNext(ref NativeOctree<T> octree, float3 point, ref NodeWrapper nodeWrapper, tfloat minDistanceSquared, tfloat maxDistanceSquared,
                                         int parentDepth) {
                parentDepth++;
                for (var i = 0; i < 8; i++) {
                    var octantId = GetOctantId(nodeWrapper.nodeId, i);
                    if (!octree.nodes.TryGetValue(octantId, out var octantObjectCount)) {
                        continue;
                    }

                    var octantCenterExtents = ExtentsBounds.GetOctant(nodeWrapper.ExtentsBounds, i);
                    var distanceSquared = ExtentsBounds.GetBounds(octantCenterExtents).DistanceSquared(point);

                    if (distanceSquared > maxDistanceSquared) {
                        continue;
                    }

                    var nodeIndex = this.nodeList.Length;
                    this.nodeList.Add(
                        new NodeWrapper(
                            octantId,
                            parentDepth,
                            octantObjectCount,
                            octantCenterExtents));

                    this.minHeap.Push(new DistanceAndIndexWrapper(
                                          distanceSquared,
                                          0,
                                          nodeIndex,
                                          true));
                }
            }

            /// <summary>
            /// Goes in the priority queue
            /// </summary>
            private readonly struct DistanceAndIndexWrapper {

                public readonly tfloat distanceSquared;

                // There's no polymorphism with HPC#, so this is our way around that
                public readonly int objIndex;
                public readonly int nodeIndex;
                public readonly bool isNode;

                public DistanceAndIndexWrapper(tfloat distanceSquared, int objIndex, int nodeIndex, bool isNode) {
                    this.distanceSquared = distanceSquared;
                    this.objIndex = objIndex;
                    this.nodeIndex = nodeIndex;
                    this.isNode = isNode;
                }

            }

            private readonly struct NodeWrapper {

                public readonly uint nodeId;
                public readonly int nodeDepth;
                public readonly int nodeCounter;
                public readonly ExtentsBounds ExtentsBounds;

                public NodeWrapper(uint nodeId, int nodeDepth, int nodeCounter, in ExtentsBounds extentsBounds) {
                    this.nodeId = nodeId;
                    this.nodeDepth = nodeDepth;
                    this.nodeCounter = nodeCounter;
                    this.ExtentsBounds = extentsBounds;
                }

            }

            private struct NearestComp : IComparer<DistanceAndIndexWrapper> {

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public int Compare(DistanceAndIndexWrapper x, DistanceAndIndexWrapper y) {
                    return x.distanceSquared.CompareTo(y.distanceSquared);
                }

            }

        }

    }

}