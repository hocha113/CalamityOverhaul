using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class SetPosingStarm : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        //让我们疯狂起来!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        public FireParticleSet FireDrawer = null;
        private float modeings = 5000;
        private float sengs;
        private float drawTime;
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.scale = 1f;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 380;
            Projectile.alpha = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 99999;
            sengs = 0;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                foreach (var p in Main.projectile) {
                    if (!p.active) {
                        continue;
                    }
                    if (p.hostile && p.type != Type) {
                        p.timeLeft = 2;
                        p.Kill();
                    }
                }
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center + new Vector2(-1200, 0)
                    , new Vector2(53, 0), ModContent.ProjectileType<Mechanicalworm>(), Projectile.damage, 2, -1);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center + new Vector2(0, 1200)
                    , new Vector2(0, -53), ModContent.ProjectileType<Mechanicalworm>(), Projectile.damage, 2, -1);
            }

            if (Projectile.ai[0] % 15 == 0) {
                Vector2 pos = Projectile.Center;
                float rand = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i + rand;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 33; j++) {
                        BasePRT spark = new PRT_HeavenfallStar(pos, vr * (0.1f + j * 0.34f), false, 13, Main.rand.NextFloat(5.2f, 6.3f), Color.Red);
                        PRTLoader.AddParticle(spark);
                    }
                }
            }

            if (Projectile.timeLeft <= 30) {
                modeings = 1200 * (Projectile.timeLeft / 30f);
            }
            else {
                if (modeings > 1200) {
                    modeings -= 15;
                }
                foreach (var p in Main.player) {
                    if (!p.active) {
                        continue;
                    }

                    if (!CWRUtils.isServer && !p.dead && p.active) {
                        p.Calamity().infiniteFlight = true;
                    }

                    if (p.Distance(Projectile.Center) > modeings) {
                        p.AddBuff(ModContent.BuffType<EXHellfire>(), 2);
                        p.HealEffect(-1);
                    }
                }
            }

            if (Main.LocalPlayer.Distance(Projectile.Center) > modeings && Projectile.timeLeft > 30) {
                if (sengs < 1f) {
                    sengs += 0.02f;
                }
            }
            else {
                if (sengs > 0f) {
                    sengs -= 0.02f;
                }
            }

            Projectile.ai[0]++;
        }

        public override void OnKill(int timeLeft) {
            NPC boss = Main.npc[(int)Projectile.ai[1]];
            //如果ai1为3说明是正在消失，这个时候就不要再tp过来了
            if (boss.type == NPCID.SkeletronPrime && boss.active && boss.ai[1] != 3) {
                SoundEngine.PlaySound(SoundID.Item78 with { Pitch = 1.24f });

                boss.Center = Projectile.Center;
                boss.damage = 0;

                if (CalamityGlobalNPC.primeCannon != -1) {
                    if (Main.npc[CalamityGlobalNPC.primeCannon].active)
                        Main.npc[CalamityGlobalNPC.primeCannon].Center = Projectile.Center;
                }
                if (CalamityGlobalNPC.primeVice != -1) {
                    if (Main.npc[CalamityGlobalNPC.primeVice].active)
                        Main.npc[CalamityGlobalNPC.primeVice].Center = Projectile.Center;
                }
                if (CalamityGlobalNPC.primeSaw != -1) {
                    if (Main.npc[CalamityGlobalNPC.primeSaw].active)
                        Main.npc[CalamityGlobalNPC.primeSaw].Center = Projectile.Center;
                }
                if (CalamityGlobalNPC.primeLaser != -1) {
                    if (Main.npc[CalamityGlobalNPC.primeLaser].active)
                        Main.npc[CalamityGlobalNPC.primeLaser].Center = Projectile.Center;
                }
                BrutalSkeletronPrimeAI.SpawnHouengEffect(boss);
                //不 要 在 客 户 端 上 生 成 射 弹
                if (!CWRUtils.isClient) {
                    float maxProjSanShootNum = 28;
                    if (ModGanged.InfernumModeOpenState) {
                        maxProjSanShootNum += 4;
                    }
                    if (BossRushEvent.BossRushActive) {
                        maxProjSanShootNum += 4;
                    }

                    int type = ModContent.ProjectileType<Probe>();
                    for (int i = 0; i < maxProjSanShootNum; i++) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI()
                                , Projectile.Center, (MathHelper.TwoPi / maxProjSanShootNum * i).ToRotationVector2() * Main.rand.Next(3, 16)
                                , type, Projectile.damage, 0f, Main.myPlayer, 0, Main.rand.Next(30, 60));
                    }

                    //这些逻辑不可以在客户端上调用，以确保运行结果唯一且不会混乱
                    NPCOverride pCOverride = boss.CWR().NPCOverride;
                    pCOverride.ai[4] = 0;
                    pCOverride.ai[10] = 180;
                    pCOverride.NetAISend();
                }
            }
            FireDrawer = null;
        }

        public override void PostDraw(Color lightColor) {
            Texture2D mainValue = TextureAssets.Npc[NPCID.Probe].Value;
            Main.instance.LoadNPC(NPCID.Probe);
            Vector2 orig = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 33; i++) {
                float rot = MathHelper.TwoPi / 33f * i + Projectile.ai[0] * 0.03f;
                Vector2 drawPos = orig + rot.ToRotationVector2() * modeings;
                float rot2 = drawPos.To(Main.LocalPlayer.Center - Main.screenPosition).ToRotation();
                Main.EntitySpriteDraw(mainValue, drawPos, null, Color.White
                , rot2, mainValue.Size() / 2, 3, SpriteEffects.FlipHorizontally, 0);
            }
            drawTime++;
            var blackTile = TextureAssets.MagicPixel;
            var diagonalNoise = CWRUtils.GetT2DAsset("CalamityMod/ExtraTextures/GreyscaleGradients/HarshNoise");
            var maxOpacity = 1f;
            var shader = CWRUtils.GetEffectValue("PrimeHaloShader");
            shader.Parameters["colorMult"].SetValue(11);
            shader.Parameters["time"].SetValue(drawTime * 0.1f);
            shader.Parameters["radius"].SetValue(modeings + 50);
            shader.Parameters["anchorPoint"].SetValue(Projectile.Center);
            shader.Parameters["screenPosition"].SetValue(Main.screenPosition);
            shader.Parameters["screenSize"].SetValue(Main.ScreenSize.ToVector2());
            shader.Parameters["playerPosition"].SetValue(Main.LocalPlayer.Center);
            shader.Parameters["maxOpacity"].SetValue(maxOpacity);
            shader.Parameters["isVmos"].SetValue(Projectile.timeLeft <= 30);
            shader.Parameters["projTime"].SetValue(Projectile.timeLeft);
            Main.spriteBatch.GraphicsDevice.Textures[1] = diagonalNoise.Value;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap
                , DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);
            Rectangle rekt = new(Main.screenWidth / 2, Main.screenHeight / 2, Main.screenWidth, Main.screenHeight);
            //Rectangle rekt = new(Main.screenWidth / -2, Main.screenHeight / -2, Main.screenWidth * 2, Main.screenHeight * 2);
            Main.spriteBatch.Draw(blackTile.Value, rekt, null, default, 0f, blackTile.Value.Size() / 2, 0, 0f);
            //Main.spriteBatch.Draw(blackTile.Value, Vector2.Zero, new Rectangle(0, 0, 2000, 2000), default, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            Main.spriteBatch.ExitShaderRegion();

            Main.spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.Placeholder2)
                , new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Red * sengs);

            if (sengs > 0) {
                float num1 = Main.screenWidth * 0.6f;
                if (num1 < 10f) {
                    num1 = 10f;
                }

                float num2 = Main.screenWidth / 100f;
                if (num2 > 2.75f) {
                    num2 = 2.75f;
                }

                if (FireDrawer is null) {
                    FireDrawer = new FireParticleSet(int.MaxValue, 1, Color.Gold * 1.25f, Color.Red, num1, num2);
                }

                FireDrawer?.DrawSet(Main.LocalPlayer.Bottom - Vector2.UnitY * 122);
                FireDrawer?.Update();
            }
            else {
                FireDrawer = null;
            }
        }
    }
}
