using InnoVault.GameSystem;
using InnoVault.PRT;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Common.DyeEffectHandle;

namespace CalamityOverhaul.Common
{
    internal class DyeEffectHandle
    {
        /// <summary>
        /// 染色体数据
        /// </summary>
        public static ArmorShaderData DyeShaderData { get; internal set; } = null;
        public static bool IsDyeDustEffectActive { get; internal set; } = false;
    }

    internal class DyeGlobalDust : GlobalDust
    {
        public override void OnSpawn(Dust dust) {
            if (!IsDyeDustEffectActive || DyeShaderData == null) {
                return;
            }
            dust.shader = DyeShaderData;
        }
    }

    internal class DyeGlobalPRT : GlobalPRT
    {
        public override void OnSpawn(BasePRT prt) {
            if (!IsDyeDustEffectActive || DyeShaderData == null) {
                return;
            }
            prt.shader = DyeShaderData;
        }
    }

    internal class DyeGlobalProjectile : ProjOverride
    {
        public static bool IsUpdate { get; private set; } = false;
        public override int TargetID => -1;//设置为-1，这样让这个修改节点运行在所有弹幕之上
        public override bool AI() {
            IsDyeDustEffectActive = IsUpdate = true;
            int dyeItemID = projectile.CWR().DyeItemID;
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }

        public override void PostAI() {
            IsDyeDustEffectActive = IsUpdate = false;
            DyeShaderData = null;
        }
    }

    internal class DyeGlobalNPC : NPCOverride
    {
        public static bool IsUpdate { get; private set; } = false;
        public override int TargetID => -1;//设置为-1，这样让这个修改节点运行在所有NPC之上
        public override bool AI() {
            IsDyeDustEffectActive = IsUpdate = true;
            int dyeItemID = npc.CWR().DyeItemID;
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }
        public override void PostAI() {
            IsDyeDustEffectActive = IsUpdate = false;
            DyeShaderData = null;
        }
    }

    internal class DyeGlobalItem : ItemOverride
    {
        public static bool IsShootUpdate { get; private set; } = false;
        public override int TargetID => -1;//设置为-1，这样让这个修改节点运行在所有物品之上
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (item.type == ItemID.None) {
                return null;
            }
            IsDyeDustEffectActive = IsShootUpdate = true;
            int dyeItemID = item.CWR().DyeItemID;
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return null;
        }

        public override void On_PostShoot(Item item, int whoAmI, int weaponDamage) {
            IsDyeDustEffectActive = IsShootUpdate = false;
            DyeShaderData = null;
        }
    }

    internal class DyePlayer : PlayerOverride
    {
        public static bool IsMeleeEffectUpdate { get; private set; } = false;
        public override int TargetItemID => ItemID.None;//设置为None，这样让这个修改节点运行在所有物品之上
        public override bool On_PreEmitUseVisuals(Item item, ref Rectangle itemRectangle) {
            if (item.type == ItemID.None) {
                return true;
            }
            IsDyeDustEffectActive = IsMeleeEffectUpdate = true;
            int dyeItemID = item.CWR().DyeItemID;
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }

        public override void On_PostEmitUseVisuals(Item item, ref Rectangle itemRectangle) {
            IsDyeDustEffectActive = IsMeleeEffectUpdate = false;
            DyeShaderData = null;
        }
    }

    internal class DyeHitEffect : ModSystem
    {
        public static bool IsHitEffectUpdate { get; private set; } = false;
        private delegate void On_OnHitNPCWithProj_Delegate(Projectile proj, NPC target, in NPC.HitInfo hit, int damageDone);
        public override void Load() {//这个钩子应该在后续的更新里移动到InnoVault中，而不是在应用模组里挂载
            var method = typeof(CombinedHooks).GetMethod("OnHitNPCWithProj", BindingFlags.Public | BindingFlags.Static);
            VaultHook.Add(method, OnHitNPCWithProjHook);
        }

        private static void OnHitNPCWithProjHook(On_OnHitNPCWithProj_Delegate orig, Projectile proj, NPC target, in NPC.HitInfo hit, int damageDone) {
            IsDyeDustEffectActive = IsHitEffectUpdate = true;
            int dyeItemID = 0;
            if (proj.Alives()) {
                dyeItemID = proj.CWR().DyeItemID;
            }
            if (dyeItemID == 0 && target.Alives()) {
                dyeItemID = target.CWR().DyeItemID;
            }
            if (dyeItemID > 0) {
                DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            orig.Invoke(proj, target, hit, damageDone);
            IsDyeDustEffectActive = IsHitEffectUpdate = false;
            DyeShaderData = null;
        }
    }
    /*
    [CWRJITEnabled]
    internal class DyeCalParticles : ModSystem
    {
        //使用Dictionary存储粒子对应的着色器数据
        private readonly static Dictionary<Particle, ArmorShaderData> ParticleShaderDatas = [];
        //使用HashSet存储所有受着色器影响的粒子，以获得O(1)的添加、删除和包含检查性能
        private readonly static HashSet<Particle> shaderParticles = [];
        //使用HashSet存储待移除的粒子，同样为了性能
        private readonly static HashSet<Particle> particlesToKill = [];

        //为不同的混合模式定义一个枚举，便于分组
        private enum ParticleDrawMode
        {
            AlphaBlend,
            NonPremultiplied,
            Additive
        }

        public override void Load() {
            var method = typeof(GeneralParticleHandler).GetMethod("DrawAllParticles", BindingFlags.Public | BindingFlags.Static);
            VaultHook.Add(method, OnDrawAllParticlesHook);
            method = typeof(GeneralParticleHandler).GetMethod("SpawnParticle", BindingFlags.Public | BindingFlags.Static);
            VaultHook.Add(method, OnSpawnParticleHook);
            method = typeof(GeneralParticleHandler).GetMethod("RemoveParticle", BindingFlags.Public | BindingFlags.Static);
            VaultHook.Add(method, OnRemoveParticleHook);
        }

        /// <summary>
        /// 根据粒子的属性判断其应该使用的混合模式
        /// </summary>
        private static ParticleDrawMode GetParticleDrawMode(Particle particle) {
            if (particle.UseAdditiveBlend) {
                return ParticleDrawMode.Additive;
            }
            if (particle.UseHalfTransparency) {
                return ParticleDrawMode.NonPremultiplied;
            }
            return ParticleDrawMode.AlphaBlend;
        }

        /// <summary>
        /// 初始化所有粒子相关的列表
        /// </summary>
        private static void InitializeWorld() {
            //确保所有与世界相关的列表是全新的
            ParticleShaderDatas.Clear();
            shaderParticles.Clear();
            particlesToKill.Clear();
        }

        public override void OnWorldLoad() => InitializeWorld();

        public override void OnWorldUnload() => InitializeWorld();

        public override void PostUpdateEverything() {
            if (VaultUtils.isServer) {
                return;
            }

            if (shaderParticles.Count == 0) {
                return;
            }

            //更新所有自定义粒子的状态
            foreach (var particle in shaderParticles) {
                //跳过已标记为待移除的粒子
                if (particle == null || particlesToKill.Contains(particle)) {
                    continue;
                }

                particle.Position += particle.Velocity;
                particle.Time++;
                particle.Update();

                //如果粒子生命周期结束，则标记它
                if (particle.Time >= particle.Lifetime && particle.SetLifetime) {
                    particlesToKill.Add(particle);
                }
            }

            //如果没有粒子需要移除，则提前返回
            if (particlesToKill.Count == 0) {
                return;
            }

            //高效地移除所有被标记的粒子
            foreach (var particle in particlesToKill) {
                shaderParticles.Remove(particle);
                ParticleShaderDatas.Remove(particle);
            }

            //清空待移除列表，为下一帧做准备
            particlesToKill.Clear();
        }

        private static void OnSpawnParticleHook(Action<Particle> orig, Particle particle) {
            if (VaultUtils.isServer || Main.gamePaused) {
                return;
            }

            //当染料效果激活时，将粒子添加到自定义管理器中
            if (IsDyeDustEffectActive && DyeShaderData != null) {
                shaderParticles.Add(particle);
                ParticleShaderDatas.Add(particle, DyeShaderData);
                return;
            }

            orig.Invoke(particle);
        }

        private static void OnRemoveParticleHook(Action<Particle> orig, Particle particle) {
            if (VaultUtils.isServer) {
                return;
            }

            //如果粒子管理器中，则将其标记为待移除
            //HashSet.Contains是O(1)操作，性能很高
            if (shaderParticles.Contains(particle)) {
                particlesToKill.Add(particle);
                return;
            }

            orig.Invoke(particle);
        }

        private static void OnDrawAllParticlesHook(Action<SpriteBatch> orig, SpriteBatch spriteBatch) {
            //先执行原始的粒子绘制逻辑
            orig.Invoke(spriteBatch);

            //如果没有自定义粒子需要绘制，则直接返回
            if (shaderParticles.Count == 0) {
                return;
            }

            spriteBatch.End();

            //创建一个启用了裁剪测试的光栅化状态，用于后续所有绘制
            var scissorTestRasterizer = new RasterizerState {
                CullMode = CullMode.None,
                ScissorTestEnable = true
            };
            Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            //参考PRTLoader.HanderHasShaderPRTDrawList的实现
            //第一级分组：按着色器实例分组，这是最高代价的状态切换
            var groupedByShader = ParticleShaderDatas
                .Where(kvp => kvp.Key != null && kvp.Key.UseCustomDraw) //过滤掉无效或不可绘制的粒子
                .GroupBy(kvp => kvp.Value); //kvp.Value是ArmorShaderData

            foreach (var shaderGroup in groupedByShader) {
                ArmorShaderData currentShader = shaderGroup.Key;

                //第二级分组：在每个着色器组内部，再按绘制模式分组
                var groupedByDrawMode = shaderGroup.GroupBy(kvp => GetParticleDrawMode(kvp.Key));

                foreach (var drawModeGroup in groupedByDrawMode) {
                    ParticleDrawMode currentDrawMode = drawModeGroup.Key;

                    //根据当前组的绘制模式，设置渲染状态并开始绘制
                    //注意：使用shader时，SpriteSortMode需要设置为Immediate，以确保shader效果能立即应用
                    switch (currentDrawMode) {
                        case ParticleDrawMode.AlphaBlend:
                            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState
                                , DepthStencilState.None, scissorTestRasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                            break;
                        case ParticleDrawMode.NonPremultiplied:
                            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.PointClamp
                                , DepthStencilState.Default, scissorTestRasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                            break;
                        case ParticleDrawMode.Additive:
                            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.PointClamp
                                , DepthStencilState.Default, scissorTestRasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                            break;
                    }

                    //应用当前组的着色器
                    currentShader?.Apply(null);

                    //绘制所有属于这个子组（相同shader和相同drawMode）的粒子
                    foreach (var kvp in drawModeGroup) {
                        kvp.Key.CustomDraw(spriteBatch);
                    }

                    spriteBatch.End();
                }
            }

            //恢复游戏默认的SpriteBatch状态，以便后续的游戏UI等内容能够正确绘制
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }
    }
    */
}
