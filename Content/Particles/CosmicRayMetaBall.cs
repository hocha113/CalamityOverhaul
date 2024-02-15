using CalamityMod.Graphics.Metaballs;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework;
using System.Linq;

namespace CalamityOverhaul.Content.Particles
{
    internal class CosmicRayMetaBall : CWRMetaball
    {
        public class CosmicParticle
        {
            public float Size;

            public float Rot;

            public float Leng;

            public float Weig;

            public Vector2 Velocity;

            public Vector2 Center;

            public CosmicParticle(Vector2 center, Vector2 velocity, float size, float rot, float leng, float weig) {
                Center = center;
                Velocity = velocity;
                Size = size;
                Rot = rot;
                Leng = leng;
                Weig = weig;
            }

            public void Update() {
                Center += Velocity;
                Velocity *= 0.96f;
                Size *= 0.91f;
            }
        }

        public static Asset<Texture2D> LayerAsset {
            get;
            private set;
        }

        public static List<CosmicParticle> Particles {
            get;
            private set;
        } = new();

        public override bool AnythingToDraw => Particles.Any();

        public override IEnumerable<Texture2D> Layers {
            get {
                yield return LayerAsset.Value;
            }
        }

        public override MetaballDrawLayer DrawContext => MetaballDrawLayer.AfterProjectiles;

        public override Color EdgeColor => CWRUtils.MultiLerpColor(Main.GameUpdateCount % 30 / 30f, Color.Blue, Color.Black);

        public override void Load() {
            if (Main.netMode == NetmodeID.Server)
                return;

            //加载图层资源
            LayerAsset = ModContent.Request<Texture2D>($"CalamityMod/Graphics/Metaballs/StreamGougeLayer", AssetRequestMode.ImmediateLoad);
        }

        public override void Update() {
            //更新所有粒子实例。一旦足够小，它们就会消失           
            for (int i = 0; i < Particles.Count; i++)
                Particles[i].Update();
            Particles.RemoveAll(p => p.Size <= 2f);
        }

        public static void AddParticle(Vector2 position, Vector2 velocity, float size, float rot, float leng, float wid) =>
            Particles.Add(new(position, velocity, size, rot, leng, wid));

        // 使纹理滚动。
        public override Vector2 CalculateManualOffsetForLayer(int layerIndex) {
            return Vector2.UnitX.RotatedBy(Main.GlobalTimeWrappedHourly * 0.6f);
        }

        public override void DrawInstances() {
            Texture2D tex = ModContent.Request<Texture2D>("CalamityOverhaul/Assets/BasicSquareCircle").Value;

            foreach (CosmicParticle particle in Particles) {
                Vector2 drawPosition = particle.Center - Main.screenPosition;
                Vector2 origin = tex.Size() * 0.5f;
                Vector2 scale = new Vector2(particle.Weig / tex.Width, 1) * particle.Size / tex.Size();
                int num = (int)(particle.Leng / tex.Height);
                Vector2 slp = new Vector2(particle.Weig / tex.Width, 1);
                Vector2 rotVr = particle.Rot.ToRotationVector2();
                for (int i = 0; i < num; i++) {
                    Main.spriteBatch.Draw(tex, drawPosition + rotVr * tex.Height * i, null, Color.White, particle.Rot, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
