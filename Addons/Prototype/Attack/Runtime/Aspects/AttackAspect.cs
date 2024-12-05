namespace ME.BECS.Attack {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public struct AttackAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<AttackComponent> attackDataPtr;
        [QueryWith]
        public AspectDataPtr<AttackRuntimeReloadComponent> attackRuntimeReloadDataPtr;
        public AspectDataPtr<AttackRuntimeFireComponent> attackRuntimeFireDataPtr;
        public AspectDataPtr<AttackTargetComponent> targetDataPtr;

        public readonly ref AttackComponent component => ref this.attackDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly AttackComponent readComponent => ref this.attackDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref AttackRuntimeReloadComponent componentRuntimeReload => ref this.attackRuntimeReloadDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly AttackRuntimeReloadComponent readComponentRuntimeReload => ref this.attackRuntimeReloadDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref AttackRuntimeFireComponent componentRuntimeFire => ref this.attackRuntimeFireDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly AttackRuntimeFireComponent readComponentRuntimeFire => ref this.attackRuntimeFireDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref float attackRangeSqr => ref this.component.sector.rangeSqr;
        public readonly ref readonly float readAttackRangeSqr => ref this.readComponent.sector.rangeSqr;
        public readonly ref readonly float readMinAttackRangeSqr => ref this.readComponent.sector.minRangeSqr;
        public readonly ref readonly float readAttackSector => ref this.readComponent.sector.sector;
        public readonly ref readonly byte readIgnoreSelf => ref this.readComponent.ignoreSelf;
        
        public readonly Ent target => this.targetDataPtr.Read(this.ent.id, this.ent.gen).target;

        [INLINE(256)]
        public void SetTarget(Ent ent) {
            if (ent.IsAlive() == true) {
                if (this.ent.Read<AttackTargetComponent>().target != ent) {
                    this.CanFire = false;
                }
                
                this.ent.Set(new AttackTargetComponent() {
                    target = ent,
                });
            } else {
                this.ent.Remove<AttackTargetComponent>();
                this.CanFire = false;
            }
        }
        
        public readonly float ReloadProgress => this.componentRuntimeReload.reloadTimer / this.component.reloadTime;
        public readonly float FireProgress => this.componentRuntimeFire.fireTimer / this.component.fireTime;

        public bool IsReloaded {
            [INLINE(256)]
            get => this.ent.Has<ReloadedComponent>();
            [INLINE(256)]
            set {
                if (value == true) {
                    this.ent.Set(new ReloadedComponent());
                } else {
                    this.componentRuntimeReload.reloadTimer = 0f;
                    this.ent.Remove<ReloadedComponent>();
                }
            }
        }
        
        public bool CanFire {
            [INLINE(256)]
            get => this.ent.Has<CanFireComponent>();
            [INLINE(256)]
            set {
                if (value == true) {
                    this.ent.Set(new CanFireComponent());
                } else {
                    this.componentRuntimeFire.fireTimer = 0f;
                    this.ent.Remove<CanFireComponent>();
                    this.ent.SetTag<FireUsedComponent>(false);
                }
            }
        }

        [INLINE(256)]
        public bool IsFireUsed() => this.ent.Has<FireUsedComponent>();
        
        [INLINE(256)]
        public void UseFire() {
            this.ent.SetTag<FireUsedComponent>(true);
            this.ent.SetOneShot(new OnFireEvent(), OneShotType.NextTick);
        }

        [INLINE(256)]
        public uint CalculateDPS() {
            var config = this.readComponent.bulletConfig.AsUnsafeConfig();
            if (config.IsValid() == true) {
                if (config.TryRead(out ME.BECS.Bullets.BulletConfigComponent bulletConfigComponent) == true) {
                    return (uint)(bulletConfigComponent.damage / this.readComponent.fireTime);
                }
            }

            return 0u;
        }

    }

}