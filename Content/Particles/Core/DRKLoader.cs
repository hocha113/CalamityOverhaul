using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Particles.Core
{
    internal class DRKLoader : ModSystem
    {
        internal static Dictionary<Type, int> ParticleTypesDic;
        internal static Dictionary<int, Texture2D> ParticleIDToTexturesDic;
        internal static List<BaseParticle> CWRParticleCoreInds;
        private static List<BaseParticle> particles;
        private static List<BaseParticle> particlesToKill;
        private static List<BaseParticle> batchedAlphaBlendParticles;
        private static List<BaseParticle> batchedNonPremultipliedParticles;
        private static List<BaseParticle> batchedAdditiveBlendParticles;

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
            particles = new List<BaseParticle>();
            particlesToKill = new List<BaseParticle>();
            ParticleTypesDic = new Dictionary<Type, int>();
            ParticleIDToTexturesDic = new Dictionary<int, Texture2D>();
            CWRParticleCoreInds = new List<BaseParticle>();

            batchedAlphaBlendParticles = new List<BaseParticle>();
            batchedNonPremultipliedParticles = new List<BaseParticle>();
            batchedAdditiveBlendParticles = new List<BaseParticle>();

            CWRParticleCoreInds = CWRUtils.HanderSubclass<BaseParticle>(false);
            foreach (var particleType in CWRParticleCoreInds) {
                Type type = particleType.GetType();
                int ID = ParticleTypesDic.Count;
                ParticleTypesDic[type] = ID;
                string texturePath = type.Namespace.Replace('.', '/') + "/" + type.Name;
                if (particleType.Texture != "") {
                    texturePath = particleType.Texture;
                }
                ParticleIDToTexturesDic[ID] = ModContent.Request<Texture2D>(texturePath, AssetRequestMode.ImmediateLoad).Value;
            }

            On_Main.DrawInfernoRings += CWRDrawForegroundParticles;
        }

        public override void Unload() {
            particles = null;
            particlesToKill = null;
            ParticleTypesDic = null;
            ParticleIDToTexturesDic = null;
            CWRParticleCoreInds = null;
            batchedAlphaBlendParticles = null;
            batchedNonPremultipliedParticles = null;
            batchedAdditiveBlendParticles = null;

            On_Main.DrawInfernoRings -= CWRDrawForegroundParticles;
        }

        public static int GetParticleType(Type sType) {
            return ParticleTypesDic[sType];
        }

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
            particle.SetDRK();
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
                    batchedAdditiveBlendParticles.Add(particle);
                }
                else if (particle.UseHalfTransparency) {
                    batchedNonPremultipliedParticles.Add(particle);
                }
                else {
                    batchedAlphaBlendParticles.Add(particle);
                }
            }

            if (batchedAlphaBlendParticles.Count > 0) {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                void defaultDraw(BaseParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f,
                        particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (BaseParticle particle in batchedAlphaBlendParticles) {
                    if (particle.UseCustomDraw) {
                        particle.CustomDraw(sb);
                    }
                    else {
                        defaultDraw(particle);
                    }
                }
                sb.End();
            }


            if (batchedNonPremultipliedParticles.Count > 0) {
                rasterizer = Main.Rasterizer;
                rasterizer.ScissorTestEnable = true;
                Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
                Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
                sb.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.Default, rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                void defaultDraw(BaseParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f, particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (BaseParticle particle in batchedNonPremultipliedParticles) {
                    if (particle.UseCustomDraw)
                        particle.CustomDraw(sb);
                    else {
                        defaultDraw(particle);
                    }
                }
                sb.End();
            }

            if (batchedAdditiveBlendParticles.Count > 0) {
                rasterizer = Main.Rasterizer;
                rasterizer.ScissorTestEnable = true;
                Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
                Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.Default, rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                void defaultDraw(BaseParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f, particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (BaseParticle particle in batchedAdditiveBlendParticles) {
                    if (particle.UseCustomDraw) {
                        particle.CustomDraw(sb);
                    }
                    else {
                        defaultDraw(particle);
                    }
                }
                sb.End();
            }

            batchedAlphaBlendParticles.Clear();
            batchedNonPremultipliedParticles.Clear();
            batchedAdditiveBlendParticles.Clear();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }

        /// <summary>
        /// 给出可用粒子槽的数量。当一次需要多个粒子来制作效果，并且不希望由于缺乏粒子槽而只绘制一半时非常有用
        /// </summary>
        /// <returns></returns>
        public static int FreeSpacesAvailable() {
            if (Main.dedServ || particles == null) {
                return 0;
            }
            return CWRConstant.MaxParticleCount - particles.Count();
        }
    }
}
