#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using ME.BECS.Jobs;
    using System.Runtime.InteropServices;
    using ME.BECS.Transforms;
    using Unity.Jobs;
    using static Cuts;

    [ComponentGroup(typeof(OctreeComponentGroup))]
    public struct OctreeBoxObject : IConfigComponent, IConfigInitialize {

        public tfloat sizeX;
        public tfloat sizeY;
        public tfloat height;
        public int treeIndex;
        
        public void OnInitialize(in Ent ent) {

            var qt = ent.Set<OctreeAspect>();
            qt.treeIndex = this.treeIndex;
            qt.SetAsRectWithSize(this.sizeX, this.sizeY);
            qt.SetHeight(this.height);

        }

    }

    [ComponentGroup(typeof(OctreeComponentGroup))]
    [StructLayout(LayoutKind.Explicit)]
    public struct OctreeElement : IComponent {

        [FieldOffset(0)]
        public tfloat radius;
        [FieldOffset(0)]
        public tfloat sizeX;
        [FieldOffset(4)]
        public int treeIndex;
        [FieldOffset(8)]
        public byte ignoreY;

    }

    [ComponentGroup(typeof(OctreeComponentGroup))]
    public struct OctreeElementRect : IComponent {

        public tfloat sizeY;
        
    }

    [ComponentGroup(typeof(OctreeComponentGroup))]
    public struct OctreeHeightComponent : IComponent {
        
        public tfloat height;
        
    }

    [EditorComment("Used by OctreeInsertSystem to filter entities by treeIndex")]
    public struct OctreeAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<OctreeElement> octreeElementPtr;
        public AspectDataPtr<OctreeElementRect> octreeRectPtr;
        public AspectDataPtr<OctreeHeightComponent> octreeHeightPtr;

        public readonly ref OctreeElement octreeElement => ref this.octreeElementPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly OctreeElement readOctreeElement => ref this.octreeElementPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref int treeIndex => ref this.octreeElement.treeIndex;
        public readonly ref readonly int readTreeIndex => ref this.readOctreeElement.treeIndex;
        public readonly bool isRect => this.ent.Has<OctreeElementRect>();
        public readonly bool hasHeight => this.ent.Has<OctreeHeightComponent>();
        public readonly float2 rectSize => new float2(this.readOctreeElement.sizeX, this.octreeRectPtr.Read(this.ent.id, this.ent.gen).sizeY);
        public readonly tfloat height => this.octreeHeightPtr.Read(this.ent.id, this.ent.gen).height;

        public readonly void SetHeight(tfloat height) {
            this.ent.Set(new OctreeHeightComponent() {
                height = height,
            });
        }
        
        public readonly void SetAsRectWithSize(tfloat sizeX, tfloat sizeY) {
            ref var rect = ref this.octreeRectPtr.Get(this.ent.id, this.ent.gen);
            rect.sizeY = sizeY;
            this.octreeElement.sizeX = sizeX;
        }

    }
    
    [BURST]
    public unsafe struct OctreeInsertSystem : IAwake, IUpdate, IDestroy, IDrawGizmos {
        
        public static OctreeInsertSystem Default => new OctreeInsertSystem() {
            mapSize = new float3(200f, 200f, 200f),
        };

        public float3 mapPosition;
        public float3 mapSize;
        
        private UnsafeList<safe_ptr> trees;
        public readonly uint treesCount => (uint)this.trees.Length;
        private ushort worldId;

        [BURST]
        public struct CollectRectJob : IJobForAspects<OctreeAspect, TransformAspect> {
            
            public UnsafeList<safe_ptr> trees;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref OctreeAspect aspect, ref TransformAspect tr) {
                
                var tree = (safe_ptr<NativeTrees.NativeOctree<Ent>>)this.trees[aspect.treeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition();
                if (aspect.readOctreeElement.ignoreY == 1) pos.y = 0f;
                tfloat height = 0f;
                if (ent.TryRead(out OctreeHeightComponent heightComponent) == true) {
                    height = heightComponent.height;
                }
                var size = aspect.rectSize;
                var halfSize = new float3(size.x * 0.5f, height * 0.5f, size.y * 0.5f);
                var rot = tr.rotation;
                tree.ptr->Add(tr.ent, new NativeTrees.AABB(pos - halfSize, pos + halfSize, rot));
                
            }

        }

        [BURST]
        public struct CollectJob : IJobForAspects<OctreeAspect, TransformAspect> {
            
            public UnsafeList<safe_ptr> trees;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref OctreeAspect aspect, ref TransformAspect tr) {
                
                var tree = (safe_ptr<NativeTrees.NativeOctree<Ent>>)this.trees[aspect.treeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition();
                if (aspect.readOctreeElement.ignoreY == 1) pos.y = 0f;
                var radius = aspect.readOctreeElement.radius;
                var rot = tr.rotation;
                tree.ptr->Add(tr.ent, new NativeTrees.AABB(pos - radius, pos + radius, rot));
                
            }

        }

        [BURST]
        public struct ApplyJob : Unity.Jobs.IJobParallelFor {

            public UnsafeList<safe_ptr> trees;
            
            public void Execute(int index) {

                var tree = (safe_ptr<NativeTrees.NativeOctree<Ent>>)this.trees[index];
                tree.ptr->Rebuild();
                
            }

        }
        
        [BURST]
        public struct ClearJob : Unity.Jobs.IJobParallelFor {

            public UnsafeList<safe_ptr> trees;

            public void Execute(int index) {

                var item = (safe_ptr<NativeTrees.NativeOctree<Ent>>)this.trees[index];
                item.ptr->Clear();
                
            }

        }
        
        [INLINE(256)]
        public readonly safe_ptr<NativeTrees.NativeOctree<Ent>> GetTree(int treeIndex) {

            return (safe_ptr<NativeTrees.NativeOctree<Ent>>)this.trees[treeIndex];

        }

        [INLINE(256)]
        public int AddTree() {

            var size = new NativeTrees.AABB(this.mapPosition, this.mapPosition + this.mapSize, quaternion.identity);
            this.trees.Add((safe_ptr)_make(new NativeTrees.NativeOctree<Ent>(size, WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator)));
            return this.trees.Length - 1;

        }

        public void OnAwake(ref SystemContext context) {

            this.worldId = context.world.id;
            this.trees = new UnsafeList<safe_ptr>(10, WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator);
            
        }

        public void OnUpdate(ref SystemContext context) {

            var clearJob = new ClearJob() {
                trees = this.trees,
            };
            var clearJobHandle = clearJob.Schedule(this.trees.Length, 1, context.dependsOn);
            
            var handle = context.Query(clearJobHandle).Without<OctreeElementRect>().AsParallel().AsUnsafe().Schedule<CollectJob, OctreeAspect, TransformAspect>(new CollectJob() {
                trees = this.trees,
            });
            
            var handleRect = context.Query(clearJobHandle).With<OctreeElementRect>().AsParallel().AsUnsafe().Schedule<CollectRectJob, OctreeAspect, TransformAspect>(new CollectRectJob() {
                trees = this.trees,
            });

            var job = new ApplyJob() {
                trees = this.trees,
            };
            var resultHandle = job.Schedule(this.trees.Length, 1, JobHandle.CombineDependencies(handle, handleRect));
            //var resultHandle = handle;
            context.SetDependency(resultHandle);

        }

        public void OnDestroy(ref SystemContext context) {

            for (int i = 0; i < this.trees.Length; ++i) {
                var item = (safe_ptr<NativeTrees.NativeOctree<Ent>>)this.trees[i];
                item.ptr->Dispose();
                _free(item);
            }

            this.trees.Dispose();

        }

        public readonly void FillNearest<T>(ref OctreeQueryAspect query, in TransformAspect tr, in T subFilter = default) where T : struct, IOctreeSubFilter<Ent> {
            
            if (tr.IsCalculated == false) return;
            
            var q = query.readQuery;
            if (q.updatePerTick > 0 && (query.ent.World.CurrentTick + query.ent.id) % q.updatePerTick == 0) return;
            
            var marker = new Unity.Profiling.ProfilerMarker("Prepare");
            marker.Begin();
            var worldPos = tr.GetWorldMatrixPosition();
            var worldRot = tr.GetWorldMatrixRotation();
            var sector = new MathSector(worldPos, worldRot, query.readQuery.sector);
            var ent = tr.ent;
            marker.End();
            
            // clean up results
            marker = new Unity.Profiling.ProfilerMarker("Prepare:Clear");
            marker.Begin();
            if (query.readResults.results.IsCreated == true) query.results.results.Clear();
            marker.End();
            marker = new Unity.Profiling.ProfilerMarker("Prepare:Alloc");
            marker.Begin();
            if (query.readResults.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, q.nearestCount > 0u ? q.nearestCount : 1u);
            marker.End();

            if (q.nearestCount == 1u) {
                var nearest = this.GetNearestFirst(q.treeMask, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf, q.ignoreY, q.ignoreSorting, in subFilter);
                if (nearest.IsAlive() == true) query.results.results.Add(nearest);
            } else {
                this.GetNearest(q.treeMask, q.nearestCount, ref query.results.results, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf, q.ignoreY, q.ignoreSorting, in subFilter);
            }
            
        }
        
        public readonly Ent GetNearestFirst(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, tfloat minRangeSqr = default,
                                            tfloat rangeSqr = default, bool ignoreSelf = default, bool ignoreY = default, bool ignoreSorting = false) {
            return this.GetNearestFirst(mask, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreY, ignoreSorting, new AlwaysTrueOctreeSubFilter());
        }

        public readonly Ent GetNearestFirst<T>(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, tfloat minRangeSqr = default, tfloat rangeSqr = default, bool ignoreSelf = default, bool ignoreY = default, bool ignoreSorting = default, in T subFilter = default) where T : struct, IOctreeSubFilter<Ent> {

            const uint nearestCount = 1u;
            var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(this.treesCount, Constants.ALLOCATOR_TEMP);
            // for each tree
            for (int i = 0; i < this.treesCount; ++i) {
                if ((mask & (1 << i)) == 0) {
                    continue;
                }
                ref var tree = ref *this.GetTree(i).ptr;
                {
                    var visitor = new OctreeNearestAABBVisitor<Ent, T>() {
                        subFilter = subFilter,
                        sector = sector,
                        ignoreSelf = ignoreSelf,
                        ignore = selfEnt,
                    };
                    var marker = new Unity.Profiling.ProfilerMarker("tree::NearestFirst");
                    marker.Begin();
                    tree.Nearest(worldPos, minRangeSqr, rangeSqr, ref visitor, new AABBDistanceSquaredProvider<Ent>() { ignoreY = ignoreY });
                    if (visitor.found == true) {
                        if (ignoreSorting == true) {
                            marker.End();
                            return visitor.nearest;
                        }
                        heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(visitor.nearest, math.lengthsq(worldPos - visitor.nearest.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                    }
                    marker.End();
                }
            }
            
            if (ignoreSorting == false) {
                var max = math.min(nearestCount, heap.Count);
                if (max > 0u) return heap[heap.Pop()].data;
            }

            return default;

        }

        public readonly void GetNearest(int mask, ushort nearestCount, ref ListAuto<Ent> results, in Ent selfEnt, in float3 worldPos, in MathSector sector, tfloat minRangeSqr, tfloat rangeSqr, bool ignoreSelf, bool ignoreY, bool ignoreSorting) {
            this.GetNearest(mask, nearestCount, ref results, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreY, ignoreSorting, new AlwaysTrueOctreeSubFilter());
        }

        public readonly void GetNearest<T>(int mask, ushort nearestCount, ref ListAuto<Ent> results, in Ent selfEnt, in float3 worldPos, in MathSector sector, tfloat minRangeSqr, tfloat rangeSqr, bool ignoreSelf, bool ignoreY, bool ignoreSorting, in T subFilter = default) where T : struct, IOctreeSubFilter<Ent> {
            
            var bitsCount = math.countbits(mask);
            if (nearestCount > 0u) {

                var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(nearestCount * this.treesCount, Constants.ALLOCATOR_TEMP);
                var resultsTemp = new UnsafeHashSet<Ent>(nearestCount, Constants.ALLOCATOR_TEMP);
                // for each tree
                for (int i = 0; i < this.treesCount; ++i) {
                    if ((mask & (1 << i)) == 0) {
                        continue;
                    }
                    ref var tree = ref *this.GetTree(i).ptr;
                    {
                        resultsTemp.Clear();
                        var visitor = new OctreeKNearestAABBVisitor<Ent, T>() {
                            subFilter = subFilter,
                            sector = sector,
                            results = resultsTemp,
                            max = nearestCount,
                            ignoreSelf = ignoreSelf,
                            ignore = selfEnt,
                        };
                        var marker = new Unity.Profiling.ProfilerMarker("tree::Nearest");
                        marker.Begin();
                        tree.Nearest(worldPos, minRangeSqr, rangeSqr, ref visitor, new AABBDistanceSquaredProvider<Ent>() { ignoreY = ignoreY });
                        if (ignoreSorting == true) {
                            var markerResults = new Unity.Profiling.ProfilerMarker("Fill Results (Unsorted)");
                            markerResults.Begin();
                            foreach (var item in visitor.results) {
                                results.Add(item);
                            }
                            markerResults.End();
                        } else {
                            var markerResults = new Unity.Profiling.ProfilerMarker("Fill Results (Sorted)");
                            markerResults.Begin();
                            foreach (var item in visitor.results) {
                                heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(item, math.lengthsq(worldPos - item.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                            }
                            markerResults.End();
                        }
                        marker.End();
                    }
                    if (bitsCount == 1) break;
                }
                resultsTemp.Dispose();

                if (ignoreSorting == false) {
                    var max = math.min((uint)nearestCount, heap.Count);
                    results.EnsureCapacity(max);
                    for (uint i = 0u; i < max; ++i) {
                        results.Add(heap[heap.Pop()].data);
                    }
                }

            } else {
                
                // select all units
                var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(this.treesCount, Constants.ALLOCATOR_TEMP);
                var resultsTemp = new UnsafeHashSet<Ent>((int)this.treesCount, Constants.ALLOCATOR_TEMP);
                // for each tree
                for (int i = 0; i < this.treesCount; ++i) {
                    if ((mask & (1 << i)) == 0) {
                        continue;
                    }
                    ref var tree = ref *this.GetTree(i).ptr;
                    {
                        resultsTemp.Clear();
                        var visitor = new RangeAABBUniqueVisitor<Ent, T>() {
                            subFilter = subFilter,
                            sector = sector,
                            results = resultsTemp,
                            rangeSqr = rangeSqr,
                            max = nearestCount,
                            ignoreSelf = ignoreSelf,
                            ignore = selfEnt,
                        };
                        var range = math.sqrt(rangeSqr);
                        var marker = new Unity.Profiling.ProfilerMarker("tree::Range");
                        marker.Begin();
                        var bounds = new NativeTrees.AABB(worldPos - range, worldPos + range, quaternion.identity);
                        if (ignoreY == true) {
                            bounds.min.y = tfloat.MinValue;
                            bounds.max.y = tfloat.MaxValue;
                        }
                        tree.Range(bounds, ref visitor);
                        if (ignoreSorting == true) {
                            var markerResults = new Unity.Profiling.ProfilerMarker("Fill Results (Unsorted)");
                            markerResults.Begin();
                            results.EnsureCapacity(results.Count + (uint)visitor.results.Count);
                            foreach (var item in visitor.results) {
                                results.Add(item);
                            }
                            markerResults.End();
                        } else {
                            var markerResults = new Unity.Profiling.ProfilerMarker("Fill Results (Sorted)");
                            markerResults.Begin();
                            heap.EnsureCapacity((uint)visitor.results.Count);
                            foreach (var item in visitor.results) {
                                heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(item, math.lengthsq(worldPos - item.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                            }
                            markerResults.End();
                        }
                        marker.End();
                    }
                    if (bitsCount == 1) break;
                }
                resultsTemp.Dispose();

                if (ignoreSorting == false) {
                    results.EnsureCapacity(heap.Count);
                    for (uint i = 0u; i < heap.Count; ++i) {
                        results.Add(heap[heap.Pop()].data);
                    }
                }

            }

        }

        public void OnDrawGizmos(ref SystemContext context) {
            UnityEngine.Gizmos.color = UnityEngine.Color.green;
            for (int i = 0; i < this.treesCount; ++i) {
                var tree = this.GetTree(i);
                tree.ptr->DrawGizmos();
            }
        }

    }

}
