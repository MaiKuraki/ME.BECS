namespace ME.BECS.FogOfWar {
    
    using Views;
    using UnityEngine;
    using Unity.Mathematics;

    public class FogOfWarView : EntityView {

        private static readonly int fogTex = Shader.PropertyToID("_FogTex");
        private static readonly int resolution = Shader.PropertyToID("_HeightResolution");
        
        private static readonly int inverseMvp = Shader.PropertyToID("_InverseMVP");
        private static readonly int pos = Shader.PropertyToID("_CamPos");
        private static readonly int @params = Shader.PropertyToID("_Params");

        public Material material;
        private float2 worldSize;
        private Vector3 offset;

        protected override void OnInitialize(in EntRO ent) {
            
            var fowSystem = ent.World.GetSystem<CreateSystem>();
            var system = ent.World.GetSystem<CreateTextureSystem>();
            var heightResolution = fowSystem.resolution;
            this.material.SetTexture(fogTex, system.GetTexture());
            this.material.SetFloat(resolution, heightResolution);

            this.worldSize = fowSystem.mapSize;

        }

        protected override void OnUpdate(in EntRO ent, float dt) {
            
            var fowSystem = ent.World.GetSystem<CreateSystem>();
            var system = ent.World.GetSystem<CreateTextureSystem>();
            var visualWorld = ent.World.GetSystem<UpdateTextureSystem>().GetVisualWorld();
            this.material.SetTexture(fogTex, system.GetTexture());

            var camera = visualWorld.Camera.GetAspect<CameraAspect>();
            var proj = (Matrix4x4)camera.projectionMatrix;
            var cam = (Matrix4x4)camera.worldToCameraMatrix;
            var inverseMVP = (proj * cam).inverse;
            //var inverseMVP = math.inverse(math.mul(camera.projectionMatrix, camera.worldToCameraMatrix));

            var invScaleX = 1f / this.worldSize.x;
            var invScaleY = 1f / this.worldSize.y;
            var x = this.offset.x - this.worldSize.x * 0.5f;
            var y = this.offset.z - this.worldSize.y * 0.5f;
            var camPos3d = camera.ent.GetAspect<ME.BECS.Transforms.TransformAspect>().position;
            var camPos = new float4(camPos3d.xyz, 0f);
            if (QualitySettings.antiAliasing > 0) {
                RuntimePlatform pl = Application.platform;
                if (pl == RuntimePlatform.WindowsEditor ||
                    pl == RuntimePlatform.WindowsPlayer ||
                    pl == RuntimePlatform.WebGLPlayer) {
                    camPos.w = 1f;
                }
            }
            
            var p = new Vector4(-x * invScaleX, -y * invScaleY, invScaleX, 0f);
            this.material.SetTexture(fogTex, system.GetTexture());
            var heightResolution = fowSystem.resolution;
            this.material.SetFloat(resolution, heightResolution);
            this.material.SetMatrix(inverseMvp, inverseMVP);
            this.material.SetVector(pos, camPos);
            this.material.SetVector(@params, p);
            
        }

    }
    
}