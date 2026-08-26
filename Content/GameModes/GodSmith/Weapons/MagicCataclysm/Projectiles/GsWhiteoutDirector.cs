using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 暴雪法杖灾变「白灾」：锚定光标区。蓄势 40t 上空雪云聚拢；
    /// 爆发 150t 区内 400px 特大暴雪（每 3t 一支原版冰矢 ×0.5，区内非 Boss 周期冻缓）；
    /// 余韵 100t 冰晶地灾钉在区内地面（触碰 ×0.2）
    /// </summary>
    internal class GsWhiteoutDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 40;
        public override int MainTicks => 150;
        public override int AftermathTicks => 100;

        protected override int HitTickRate => 12;

        protected override float TickDamageMul => 0.2f;

        /// <summary>暴雪区半径</summary>
        private const float FieldRadius = 400f;
        /// <summary>雪云悬高</summary>
        private const float CloudHeight = 420f;
        /// <summary>余韵冰晶带半宽/半高</summary>
        private const float RimeHalfW = 200f;
        private const float RimeHalfH = 25f;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        internal static readonly Color FrostBlue = new(150, 210, 255);
        internal static readonly Color FrostPale = new(226, 244, 255);

        private static int BoltType => ContentSamples.ItemsByType[ItemID.BlizzardStaff].shoot;

        /// <summary>雪云浓度包络</summary>
        private float CloudEnvelope() {
            if (Phase == 0) {
                return VaultUtils.EaseOutQuad(Elapsed / (float)OmenTicks);
            }
            if (Phase == 1) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / 60f, 0f, 1f);
        }

        protected override void UpdateAnchor() {
            if (Phase == 2 && Projectile.localAI[2] == 0f) {
                //余韵：冰晶地灾钉在区心下方地面
                Projectile.localAI[2] = 1f;
                Projectile.localAI[0] = Projectile.Center.X;
                Projectile.localAI[1] = FindGroundY(Projectile.Center);
            }
            if (Phase == 2) {
                Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1] - RimeHalfH);
            }
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.7f, Pitch = -0.35f }, Projectile.Center);
            }
            //雪雾向云心聚拢（约 1/2 帧）
            if (!VaultUtils.isServer && t % 2 == 0) {
                Vector2 cloud = Projectile.Center + new Vector2(Main.rand.NextFloat(-FieldRadius, FieldRadius), -CloudHeight + Main.rand.NextFloat(-40f, 40f));
                PRTLoader.NewParticle<PRT_DefCryoMist>(cloud, new Vector2(0f, Main.rand.NextFloat(0.2f, 0.6f)),
                    FrostBlue * 0.7f, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(36, cloud, 60f);
            }
        }

        protected override void MainUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.9f, Pitch = 0.1f }, Projectile.Center);
            }
            //每 3t 一支冰矢自云底落下（owner 端生成）
            if (t % 3 == 0 && OwnerSide) {
                Vector2 spawn = Projectile.Center + new Vector2(Main.rand.NextFloat(-FieldRadius * 0.95f, FieldRadius * 0.95f), -CloudHeight + Main.rand.NextFloat(-30f, 30f));
                Vector2 vel = new(Main.rand.NextFloat(-2.2f, 2.2f), Main.rand.NextFloat(15f, 18f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawn, vel,
                    BoltType, ScaledDamage(0.5f), Projectile.knockBack * 0.5f, Projectile.owner);
            }
            //区内非 Boss 周期冻缓（权威端施 buff，轻控不做硬控）
            if (Authoritative && t % 20 == 0) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.boss || !npc.CanBeChasedBy()) {
                        continue;
                    }
                    if (Vector2.Distance(npc.Center, Projectile.Center) < FieldRadius) {
                        npc.AddBuff(BuffID.Chilled, 40);
                    }
                }
            }
            //区内飘雪雾（约 1/3 帧）
            if (!VaultUtils.isServer && t % 3 == 1) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-FieldRadius, FieldRadius), Main.rand.NextFloat(-CloudHeight, 40f));
                PRTLoader.NewParticle<PRT_DefFrostGlint>(pos, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(1.5f, 3f)),
                    FrostPale, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(30);
            }
        }

        protected override void AftermathUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
            }
            if (!VaultUtils.isServer && t % 8 == 0) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-RimeHalfW, RimeHalfW), -RimeHalfH);
                PRTLoader.NewParticle<PRT_DefFrostGlint>(pos, new Vector2(0f, -0.4f),
                    FrostBlue, Main.rand.NextFloat(0.25f, 0.45f))?.Configure(26);
            }
        }

        /// <summary>爆发段自身无判定（伤害在冰矢）；余韵冰晶带触碰判定</summary>
        public override bool? CanDamage() => Phase == 2 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != 2) {
                return false;
            }
            Rectangle rime = new((int)(Projectile.Center.X - RimeHalfW), (int)(Projectile.Center.Y - RimeHalfH),
                (int)(RimeHalfW * 2f), (int)(RimeHalfH * 2f + 12f));
            return rime.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = GlowTex?.Value;
            if (glow == null) {
                return false;
            }
            float env = CloudEnvelope();
            //雪云带：三团横排呼吸辉光
            if (env > 0.02f && Phase != 2) {
                for (int i = -1; i <= 1; i++) {
                    Vector2 pos = Projectile.Center + new Vector2(i * FieldRadius * 0.55f, -CloudHeight) - Main.screenPosition;
                    float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.4f + i * 1.7f + Projectile.identity * 0.37f);
                    Main.EntitySpriteDraw(glow, pos, null, FrostBlue with { A = 0 } * (0.4f * env * pulse), 0f,
                        glow.Size() * 0.5f, new Vector2(340f, 130f) / glow.Width * pulse, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, pos, null, FrostPale with { A = 0 } * (0.25f * env * pulse), 0f,
                        glow.Size() * 0.5f, new Vector2(220f, 80f) / glow.Width, SpriteEffects.None, 0);
                }
            }
            //余韵：地面冰晶簇（原版冰矢贴图斜插，identity 定相）
            if (Phase == 2) {
                int boltType = BoltType;
                Main.instance.LoadProjectile(boltType);
                Texture2D bolt = TextureAssets.Projectile[boltType].Value;
                float fade = MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / (float)AftermathTicks, 0f, 1f);
                for (int i = 0; i < 12; i++) {
                    float x = MathHelper.Lerp(-RimeHalfW, RimeHalfW, i / 11f) + (Hash01(i) - 0.5f) * 22f;
                    Vector2 pos = Projectile.Center + new Vector2(x, RimeHalfH - 8f - Hash01(i + 30) * 12f) - Main.screenPosition;
                    float rot = MathHelper.Pi + (Hash01(i + 60) - 0.5f) * 0.7f;
                    Main.EntitySpriteDraw(bolt, pos, null, FrostBlue * (0.8f * fade), rot,
                        bolt.Size() * 0.5f, 0.9f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
