namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public static class EntityConfigEntExt {

        [INLINE(256)]
        public static T ReadStatic<T>(this in Ent ent) where T : unmanaged, IComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.ReadStatic<T>();
            }

            return StaticTypes<T>.defaultValue;

        }

        [INLINE(256)]
        public static bool HasStatic<T>(this in Ent ent) where T : unmanaged, IComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.HasStatic<T>();
            }

            return false;

        }

    }

}