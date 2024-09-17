using CalamityMod.Events;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.SkyEffects
{
    internal class BrutalSkeletronSky : CustomSky, ILoader
    {
        internal static string name => "CWRMod:BrutalSkeletronSky";
        private bool active;
        private float intensity;
        private float maxIntensity = 0.6f;
        private PGBolt[] bolts;
        private int ticksUntilNextBolt;
        private UnifiedRandom random = new UnifiedRandom();

        void ILoader.Load() {
            if (CWRUtils.isServer) {
                return;
            }
            SkyManager.Instance[name] = this;
            Filters.Scene[name] = new Filter(new ScreenShaderData("FilterMiniTower").UseColor(0.15f, 0.1f, 0.1f).UseOpacity(0.3f), EffectPriority.High);
        }


        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
            bolts = new PGBolt[300];
            random = new UnifiedRandom();
            for (int i = 0; i < bolts.Length; i++) {
                bolts[i] = new PGBolt {
                    IsAlive = false
                };
            }
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            Rectangle rectangle = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            if (BossRushEvent.BossRushActive) {
                rectangle = new Rectangle(0, 0, Main.screenWidth * 2, Main.screenHeight * 2);
            }
            spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.Placeholder2), rectangle, Color.Black * intensity);

            float scale = Math.Min(1f, (Main.screenPosition.Y - 1000f) / 1000f);
            Vector2 value3 = Main.screenPosition + new Vector2(Main.screenWidth >> 1, Main.screenHeight >> 1);
            Rectangle rectangle2 = new Rectangle(-1000, -1000, 4000, 4000);
            for (int i = 0; i < bolts.Length; i++) {
                bolts[i].Draw(spriteBatch, rectangle2, value3, intensity, scale, minDepth, maxDepth);
            }
        }

        public override bool IsActive() {
            return active || intensity > 0;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override void Update(GameTime gameTime) {
            if (NPC.AnyNPCs(NPCID.SkeletronPrime)) {
                if (intensity < 0.3f) {
                    intensity += 0.005f;
                }
            }
            else {
                intensity -= 0.005f;
                if (intensity < 0) {
                    Deactivate();
                }
            }

            if (ticksUntilNextBolt <= 0) {
                ticksUntilNextBolt = random.Next(10, 30);
                foreach (PGBolt pGBolt in bolts) {
                    if (pGBolt.IsAlive) {
                        continue;
                    }
                    pGBolt.IsAlive = true;
                    pGBolt.Position.X = random.NextFloat() * (Main.maxTilesX * 16f + 4000f) - 2000f;
                    pGBolt.Position.Y = random.NextFloat() * 1800f;
                    pGBolt.Depth = random.NextFloat() * 8f + 2f;
                    pGBolt.Life = 30;
                }
            }
            ticksUntilNextBolt--;
            foreach (PGBolt pGBolt in bolts) {
                if (!pGBolt.IsAlive) {
                    continue;
                }
                pGBolt.Life--;
                if (pGBolt.Life <= 0) {
                    pGBolt.IsAlive = false;
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            return inColor;
        }
    }
}
