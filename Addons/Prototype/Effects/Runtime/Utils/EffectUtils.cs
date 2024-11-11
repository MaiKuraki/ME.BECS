namespace ME.BECS.Effects {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using Unity.Mathematics;
    using ME.BECS.Players;

    public static class EffectUtils {

        [INLINE(256)]
        public static EffectAspect CreateEffect(in float3 position, in EffectConfig effect, in JobInfo jobInfo = default) {
            return CreateEffect(in position, quaternion.identity, in effect, in jobInfo);
        }

        [INLINE(256)]
        public static EffectAspect CreateEffect(in float3 position, in quaternion rotation, in EffectConfig effect, in JobInfo jobInfo = default, in PlayerAspect owner = default) {

            var ent = Ent.New(in jobInfo);
            effect.config.Apply(ent);
            if (owner.ent != default) {
                ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            }
            var tr = ent.GetOrCreateAspect<TransformAspect>();
            tr.position = position;
            tr.rotation = rotation;
            ent.Destroy(effect.lifetime);
            return ent.GetOrCreateAspect<EffectAspect>();

        }

    }

}