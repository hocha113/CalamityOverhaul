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
    internal class CWRParticleHandler
    {
        internal static Dictionary<Type, int> ParticleTypesDic;
        internal static Dictionary<int, Texture2D> ParticleIDToTexturesDic;
        internal static List<CWRParticle> CWRParticleCoreInds;
        private static List<CWRParticle> particles;
        private static List<CWRParticle> particlesToKill;
        private static List<CWRParticle> batchedAlphaBlendParticles;
        private static List<CWRParticle> batchedNonPremultipliedParticles;
        private static List<CWRParticle> batchedAdditiveBlendParticles;

        public static int GetParticlesCount() => particles.Count;

        internal static void Load() {
            particles = new List<CWRParticle>();
            particlesToKill = new List<CWRParticle>();
            ParticleTypesDic = new Dictionary<Type, int>();
            ParticleIDToTexturesDic = new Dictionary<int, Texture2D>();
            CWRParticleCoreInds = new List<CWRParticle>();

            batchedAlphaBlendParticles = new List<CWRParticle>();
            batchedNonPremultipliedParticles = new List<CWRParticle>();
            batchedAdditiveBlendParticles = new List<CWRParticle>();

            CWRParticleCoreInds = CWRUtils.HanderSubclass<CWRParticle>(false);
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
        }

        internal static void Unload() {
            particles = null;
            particlesToKill = null;
            ParticleTypesDic = null;
            ParticleIDToTexturesDic = null;
            CWRParticleCoreInds = null;
            batchedAlphaBlendParticles = null;
            batchedNonPremultipliedParticles = null;
            batchedAdditiveBlendParticles = null;
        }

        /// <summary>
        /// 生成提供给世界的粒子实例。如果达到颗粒限值，但该颗粒被标记为重要，它将尝试替换不重要的颗粒
        /// </summary>
        public static void AddParticle(CWRParticle particle) {
            if (Main.gamePaused || Main.dedServ || particles == null) {
                return;
            }
            if (particles.Count >= CWRConstant.MaxParticleCount && !particle.Important) {
                return;
            }

            particles.Add(particle);
            particle.Type = ParticleTypesDic[particle.GetType()];
        }

        public static void Update() {
            if (Main.dedServ) {//不要在服务器上更新逻辑
                return;
            }

            foreach (CWRParticle particle in particles) {
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

        public static void ParticleGarbageCollection(ref List<CWRParticle> particles) {
            bool isGC(CWRParticle p) => p.Time >= p.Lifetime && p.SetLifetime || particlesToKill.Contains(p);
            particles.RemoveAll(isGC);
        }
        public static void UpdateParticleVelocity(CWRParticle particle) => particle.Position += particle.Velocity;
        public static void UpdateParticleTime(CWRParticle particle) => particle.Time++;
        public static void RemoveParticle(CWRParticle particle) => particlesToKill.Add(particle);

        public static void DrawAll(SpriteBatch sb) {
            if (particles.Count == 0) {
                return;
            }

            sb.End();
            var rasterizer = Main.Rasterizer;
            rasterizer.ScissorTestEnable = true;
            Main.instance.GraphicsDevice.RasterizerState.ScissorTestEnable = true;
            Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            foreach (CWRParticle particle in particles) {
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
                void defaultDraw(CWRParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f,
                        particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (CWRParticle particle in batchedAlphaBlendParticles) {
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
                void defaultDraw(CWRParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f, particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (CWRParticle particle in batchedNonPremultipliedParticles) {
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
                void defaultDraw(CWRParticle particle) {
                    Rectangle frame = ParticleIDToTexturesDic[particle.Type].Frame(1, particle.FrameVariants, 0, particle.Variant);
                    sb.Draw(ParticleIDToTexturesDic[particle.Type], particle.Position - Main.screenPosition, frame, particle.Color, particle.Rotation, frame.Size() * 0.5f, particle.Scale, SpriteEffects.None, 0f);
                }
                foreach (CWRParticle particle in batchedAdditiveBlendParticles) {
                    if (particle.UseCustomDraw)
                        particle.CustomDraw(sb);
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
