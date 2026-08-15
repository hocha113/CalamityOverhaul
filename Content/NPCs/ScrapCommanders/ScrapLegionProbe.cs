using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders
{
    /// <summary>
    /// 废钢仆从：统帅从废钢堆里拼出来的巡逻兵（原版探测怪贴图 + 锈化）。
    /// 出土上升 → 绕统帅巡逻 → 周期性朝目标点射锈脉冲。
    /// ai[0]=统帅 whoAmI，ai[1]=编队槽位
    /// </summary>
    internal class ScrapLegionProbe : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private NPC Boss => Main.npc[(int)NPC.ai[0]];
        private int Slot => (int)NPC.ai[1];
        /// <summary>出土计时（本地表现量）</summary>
        private ref float RiseTimer => ref NPC.localAI[0];
        /// <summary>开火节拍器</summary>
        private ref float FireClock => ref NPC.localAI[1];

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 34;
            NPC.height = 34;
            NPC.damage = 42;
            NPC.defense = 14;
            NPC.lifeMax = 900;
            NPC.knockBackResist = 0.4f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.value = 0;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void AI() {
            NPC boss = Boss;
            bool bossAlive = boss != null && boss.active
                && boss.type == ModContent.NPCType<ScrapCommander>();
            if (!bossAlive) {
                //统帅没了：断电坠机
                NPC.velocity.X *= 0.97f;
                NPC.velocity.Y = MathF.Min(NPC.velocity.Y + 0.4f, 12f);
                NPC.rotation += 0.2f;
                if (!VaultUtils.isClient && NPC.localAI[2]++ > 90) {
                    NPC.active = false;
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
                    }
                }
                return;
            }

            if (RiseTimer < 30f) {
                //出土上升
                RiseTimer++;
                NPC.velocity = new Vector2(0f, -3.4f);
                NPC.rotation = MathF.Sin(RiseTimer * 0.4f) * 0.2f;
                if (!Main.dedServ && (int)RiseTimer % 4 == 0) {
                    Dust dust = Dust.NewDustPerfect(NPC.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), 14f),
                        DustID.Dirt, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)),
                        90, default, Main.rand.NextFloat(0.8f, 1.3f));
                    dust.noGravity = Main.rand.NextBool();
                }
                if ((int)RiseTimer == 6) {
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, NPC.Center);
                }
                return;
            }

            //绕统帅巡逻：编队相位随时间缓转
            float ang = Main.GlobalTimeWrappedHourly * 0.8f + Slot * MathHelper.TwoPi / 3f;
            Vector2 anchor = boss.Center + ang.ToRotationVector2() * 190f;
            Vector2 to = anchor - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, to * 0.06f, 0.12f);
            if (NPC.velocity.Length() > 14f) {
                NPC.velocity = NPC.velocity.SafeNormalize(Vector2.Zero) * 14f;
            }

            //面向目标微倾
            Player target = Main.player[boss.target];
            if (target.Alives()) {
                NPC.rotation = NPC.rotation.AngleLerp(
                    (target.Center - NPC.Center).ToRotation() * 0.08f, 0.1f);

                //周期点射（槽位错拍，别齐射成墙）
                FireClock++;
                if (FireClock >= 130f + Slot * 43f) {
                    FireClock = 0f;
                    FirePulse(target);
                }
            }
        }

        private void FirePulse(Player target) {
            Vector2 aim = (target.Center + target.velocity * 8f - NPC.Center).SafeNormalize(Vector2.UnitX);
            SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.32f, Pitch = 0.5f, MaxInstances = 3 }, NPC.Center);
            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(NPC.Center + aim * 14f,
                        aim.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(3f, 6f),
                        ScrapCommander.WeldOrange * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(false, Main.rand.Next(8, 12));
                }
            }
            if (!VaultUtils.isClient) {
                int damage = (int)NPC.GetAttackDamage_ForProjectiles(26f, 22f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + aim * 14f, aim * 20f,
                    ModContent.ProjectileType<ScrapLaserPulse>(), damage, 1f, Main.myPlayer);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(12f, 12f),
                    new Vector2(hit.HitDirection * Main.rand.NextFloat(1.5f, 4f), -Main.rand.NextFloat(0.5f, 2f)),
                    ScrapCommander.WeldOrange, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(8, 14));
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(NPC.Center + Main.rand.NextVector2Circular(14f, 14f),
                        Main.rand.NextVector2Circular(5f, 5f),
                        Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(10, 18));
                }
                PRTLoader.NewParticle<PRT_GhostRainMist>(NPC.Center, new Vector2(0f, -0.3f),
                    ScrapCommander.SmokeGray, 0.8f)?.Configure(46);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.Probe);
            Texture2D tex = TextureAssets.Npc[NPCID.Probe]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.Probe];
            Rectangle frame = new(0, 0, tex.Width, frameH);
            Color tint = drawColor.MultiplyRGB(ScrapCommander.RustMul);
            spriteBatch.Draw(tex, NPC.Center - screenPos, frame, tint, NPC.rotation,
                frame.Size() * 0.5f, 1f, NPC.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

            //红目点
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                float pulse = 0.3f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + NPC.whoAmI);
                spriteBatch.Draw(glow, NPC.Center - screenPos, null,
                    new Color(255, 64, 46, 0) * pulse, 0f, glow.Size() * 0.5f,
                    new Vector2(9f * 2f / glow.Width), SpriteEffects.None, 0f);
            }
            return false;
        }

        /// <summary>统帅编队计数：场上归属该统帅的活仆从数</summary>
        internal static int CountFor(NPC boss) {
            int type = ModContent.NPCType<ScrapLegionProbe>();
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type && (int)npc.ai[0] == boss.whoAmI) {
                    count++;
                }
            }
            return count;
        }
    }
}
