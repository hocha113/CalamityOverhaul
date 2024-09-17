using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Particles.Core
{
    internal class PRTLoader : ModSystem, ICWRLoader
    {
        internal static Dictionary<Type, int> ParticleTypesDic;
        internal static Dictionary<int, Texture2D> ParticleIDToTexturesDic;
        internal static List<BaseParticle> CWRParticleCoreInds;
        private static List<BaseParticle> particles;
        private static List<BaseParticle> particlesToKill;
        private static List<BaseParticle> batched_AlphaBlend_DRK;
        private static List<BaseParticle> batched_NonPremultiplied_DRK;
        private static List<BaseParticle> batched_AdditiveBlend_DRK;

        public static int GetParticlesCount() => particles.Count;
        public static int GetParticlesCount(int fxType) {
            int num = 0;
            foreach (var particle in particles) {
                if (particle.Type == fxType) {
                    num++;
                }
            }
            return num;
        }
        public static int GetParticlesCount(Vector2 targetPos, float maxFindDistance) {
            int num = 0;
            foreach (var particle in particles) {
                if (particle.Position.Distance(targetPos) <= maxFindDistance) {
                    num++;
                }
            }
            return num;
        }
        public static int GetParticlesCount(Vector2 targetPos, float maxFindDistance, int fxType) {
            int num = 0;
            foreach (var particle in particles) {
                if (particle.Position.Distance(targetPos) <= maxFindDistance && particle.Type == fxType) {
                    num++;
                }
            }
            return num;
        }

        public override void PostUpdateEverything() => Update();
        public static void CWRDrawForegroundParticles(Terraria.On_Main.orig_DrawInfernoRings orig, Main self) {
            DrawAll(Main.spriteBatch);
            orig(self);
        }

        public override void Load() {
            particles = [];
            particlesToKill = [];
            ParticleTypesDic = [];
            ParticleIDToTexturesDic = [];
            CWRParticleCoreInds = [];

            batched_AlphaBlend_DRK = [];
            batched_NonPremultiplied_DRK = [];
            batched_AdditiveBlend_DRK = [];

            CWRParticleCoreInds = CWRUtils.HanderSubclass<BaseParticle>(false);
            foreach (var particleType in CWRParticleCoreInds) {
                Type type = particleType.GetType();
                int ID = ParticleTypesDic.Count;
                ParticleTypesDic[type] = ID;
            }

            On_Main.DrawInfernoRings += CWRDrawForegroundParticles;
        }

        void ICWRLoader.LoadAsset() {
            foreach (var prt in CWRParticleCoreInds) {
                Type type = prt.GetType();
                string texturePath = type.Namespace.Replace('.', '/') + "/" + type.Name;
                if (prt.Texture != "") {
                    texturePath = prt.Texture;
                }
                ParticleIDToTexturesDic[ParticleTypesDic[type]] = CWRUtils.GetT2DValue(texturePath);
            }
        }

        public override void Unload() {
            particles = null;
            particlesToKill = null;
            ParticleTypesDic = null;
            ParticleIDToTexturesDic = null;
            CWRParticleCoreInds = null;
            batched_AlphaBlend_DRK = null;
            batched_NonPremultiplied_DRK = null;
            batched_AdditiveBlend_DRK = null;

            On_Main.DrawInfernoRings -= CWRDrawForegroundParticles;
        }

        public static int GetParticleType<T>() where T : BaseParticle => ParticleTypesDic[typeof(T)];

        public static int GetParticleType(Type sType) => ParticleTypesDic[sType];

        /// <summary>
        /// 生成提供给世界的粒子实例。如果达到颗粒限值，但该颗粒被标记为重要，它将尝试替换不重要的颗粒
        /// </summary>
        public static void AddParticle(BaseParticle particle) {
            if (Main.gamePaused || Main.dedServ || particles == null) {
                return;
            }
            if (particles.Count >= CWRConstant.MaxParticleCount && !particle.Important) {
                return;
            }

            particles.Add(particle);
            particle.Type = GetParticleType(particle.GetType());
            particle.SetPRT();
        }

        /// <summary>
        /// 使用指定的属性初始化并添加一个新粒子到粒子系统中
        /// </summary>
        /// <param name="particle">要初始化和添加的粒子实例</param>
        /// <param name="position">粒子在二维空间中的初始位置</param>
        /// <param name="velocity">粒子的初始速度向量</param>
        /// <param name="color">粒子的颜色，默认为默认颜色</param>
        /// <param name="scale">粒子的缩放比例，默认为1</param>
        /// <param name="ai0">粒子的自定义属性 ai0，默认为0</param>
        /// <param name="ai1">粒子的自定义属性 ai1，默认为0</param>
        /// <param name="ai2">粒子的自定义属性 ai2，默认为0</param>
        public static void NewParticle(BaseParticle particle, Vector2 position, Vector2 velocity
            , Color color = default, float scale = 1f, int ai0 = 0, int ai1 = 0, int ai2 = 0) {
            particle.Position = position;
            particle.Velocity = velocity;
            particle.Scale = scale;
            particle.Color = color;
            particle.ai[0] = ai0;
            particle.ai[1] = ai1;
            particle.ai[2] = ai2;
            AddParticle(particle);
        }

        public static void Update() {
            if (Main.dedServ) {//不要在服务器上更新逻辑
                return;
            }

            foreach (BaseParticle particle in particles) {
                if (particle == null) {
                    continue;
                }
                UpdateParticleVelocity(particle);
                UpdateParticleTime(particle);
                particle.AI();
            }
            ParticleGarbageCollection(ref particles);
            particles.RemoveAll(particle => particle.Time >= particle.Lifetime && particle.SetLifetime || particlesToKill.Contains(particle));
            particlesToKill.Clear();
        }

        public static void ParticleGarbageCollection(ref List<BaseParticle> particles) {
            bool isGC(BaseParticle p) => p.Time >= p.Lifetime && p.SetLifetime || particlesToKill.Contains(p);
            particles.RemoveAll(isGC);
        }
        public static void UpdateParticleVelocity(BaseParticle particle) => particle.Position += particle.Velocity;
        public static void UpdateParticleTime(BaseParticle particle) => particle.Time++;
        public static void RemoveParticle(BaseParticle particle) => particlesToKill.Add(particle);

        public static void DrawAll(SpriteBatch sb) {
            if (particles.Count == 0) {
                return;
            }

            sb.End();
            var rasterizer = Main.Rasterizer;
            rasterizer.ScissorTestEnable = true;
            Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
            Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            foreach (BaseParticle particle in particles) {
                if (particle == null) {
                    continue;
                }
                if (particle.UseAdditiveBlend) {
                    batched_AdditiveBlend_DRK.Add(particle);
                }
                else if (particle.UseHalfTransparency) {
                    batched_NonPremultiplied_DRK.Add(particle);
                }
                else {
                    batched_AlphaBlend_DRK.Add(particle);
                }
            }

            if (batched_AlphaBlend_DRK.Count > 0) {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                void defaultDraw(BaseParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f,
                        particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (BaseParticle particle in batched_AlphaBlend_DRK) {
                    if (particle.UseCustomDraw) {
                        particle.CustomDraw(sb);
                    }
                    else {
                        defaultDraw(particle);
                    }
                }
                sb.End();
            }


            if (batched_NonPremultiplied_DRK.Count > 0) {
                rasterizer = Main.Rasterizer;
                rasterizer.ScissorTestEnable = true;
                Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
                Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
                sb.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.Default, rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                void defaultDraw(BaseParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f, particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (BaseParticle particle in batched_NonPremultiplied_DRK) {
                    if (particle.UseCustomDraw)
                        particle.CustomDraw(sb);
                    else {
                        defaultDraw(particle);
                    }
                }
                sb.End();
            }

            if (batched_AdditiveBlend_DRK.Count > 0) {
                rasterizer = Main.Rasterizer;
                rasterizer.ScissorTestEnable = true;
                Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
                Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.Default, rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                void defaultDraw(BaseParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f, particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (BaseParticle particle in batched_AdditiveBlend_DRK) {
                    if (particle.UseCustomDraw) {
                        particle.CustomDraw(sb);
                    }
                    else {
                        defaultDraw(particle);
                    }
                }
                sb.End();
            }

            batched_AlphaBlend_DRK.Clear();
            batched_NonPremultiplied_DRK.Clear();
            batched_AdditiveBlend_DRK.Clear();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }

        /// <summary>
        /// 给出可用粒子槽的数量。当一次需要多个粒子来制作效果，并且不希望由于缺乏粒子槽而只绘制一半时非常有用
        /// </summary>
        /// <returns></returns>
        public static int FreeSpacesAvailable() {
            return Main.dedServ || particles == null ? 0 : CWRConstant.MaxParticleCount - particles.Count();
        }
    }
}
