namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Units;
    using Unity.Mathematics;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Look at target system")]
    public struct LookAtSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<TransformAspect, UnitAspect> {

            public float dt;
            public BuildGraphSystem buildGraphSystem;

            public void Execute(in JobInfo jobInfo, ref TransformAspect tr, ref UnitAspect unit) {

                var lookAtComponent = unit.ent.Read<UnitLookAtComponent>();
                var pos = tr.GetWorldMatrixPosition();
                var dir = lookAtComponent.target - pos;
                
                this.buildGraphSystem.ReadHeights().GetHeight(pos, out var unitNormal);
                var rot = tr.rotation;
                var toRot = quaternion.LookRotation(dir, unitNormal);
                var targetRot = toRot;
                var maxDegreesDelta = this.dt * unit.readRotationSpeed;
                var qAngle = math.angle(rot, toRot);
                if (qAngle != 0f) {
                    toRot = math.slerp(rot, toRot, math.min(1.0f, maxDegreesDelta / qAngle));
                }
                tr.rotation = toRot;
                if (math.angle(tr.rotation, targetRot) <= 0.01f) {
                    unit.ent.Remove<UnitLookAtComponent>();
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().With<UnitLookAtComponent>().Without<PathFollowComponent>().Without<IsUnitStaticComponent>().Schedule<Job, TransformAspect, UnitAspect>(new Job() {
                dt = context.deltaTime,
                buildGraphSystem = context.world.GetSystem<BuildGraphSystem>(),
            });
            context.SetDependency(dependsOn);

        }

    }

}