using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>
    /// 下砸落地火柱:自地面窜出的日冕火,3t 长满→12t 伤害窗→燃尽收缩回地面<br/>
    /// ai[0]=柱高(px) ai[1]=柱向角(生成端已算好,本体不关心重力向)<br/>
    /// timer 走 SendExtraAI,远端中途入场不重播出生
    /// </summary>
    internal class DawnshatterSpike : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "TearFlame01")]
        internal static Asset<Texture2D> FlameTex = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        private const int LifeFrames = 40;
        private const int DamageWindow = 12;
        /// 出生长满用时
        private const float GrowTicks = 3f;

        private static readonly Color FlameGold = new(255, 198, 88);
        private static readonly Color FlameRed = new(244, 100, 36);
        private static readonly Color FlameSoot = new(120, 46, 26);

        private int timer;
        private bool anchored;
        private float jitterSeed;

        private float ColumnHeight => Projectile.ai[0] > 8f ? Projectile.ai[0] : 100f;
        private Vector2 ColumnDir => Projectile.ai[1].ToRotationVector2();
        private float Age => MathHelper.Clamp(timer / (float)LifeFrames, 0f, 1f);
        /// <summary>当前柱高包络:出生窜满,伤害窗后燃尽收缩</summary>
        private float HeightNow {
            get {
                float grow = VaultUtils.EaseOutCubic(MathHelper.Clamp(timer / GrowTicks, 0f, 1f));
                float decay = timer <= DamageWindow + 6 ? 1f
                    : 1f - VaultUtils.EaseOutCubic((timer - DamageWindow - 6) / (float)(LifeFrames - DamageWindow - 6)) * 0.85f;
                return ColumnHeight * grow * decay;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//每柱一敌一伤
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = LifeFrames + 4;
            Projectile.netImportant = true;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!anchored) {
                anchored = true;
                jitterSeed = Projectile.identity * 0.6180339887f % 1f * 100f;
                //timer>0 = 中途收到的同步,不重播出生喷发
                if (timer == 0 && !VaultUtils.isServer) {
                    Vector2 up = ColumnDir;
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_DawnEmber>(Projectile.Center + up * Main.rand.NextFloat(10f, 40f)
                            , up.RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 8f)
                            , default, Main.rand.NextFloat(0.8f, 1.3f)).Configure(Main.rand.Next(14, 24));
                    }
                }
            }
            timer++;
            if (timer >= LifeFrames) {
                Projectile.Kill();
                return;
            }
            Lighting.AddLight(Projectile.Center + ColumnDir * HeightNow * 0.5f
                , new Vector3(1.1f, 0.62f, 0.22f) * (1f - Age));

            //燃烧期柱身持续剥离余烬
            if (!VaultUtils.isServer && timer < DamageWindow + 10 && Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat(0.3f, 1f);
                PRTLoader.NewParticle<PRT_DawnEmber>(Projectile.Center + ColumnDir * HeightNow * along
                    , ColumnDir * Main.rand.NextFloat(1.5f, 4f), default, Main.rand.NextFloat(0.7f, 1.1f))
                    .Configure(Main.rand.Next(12, 20));
            }
        }

        public override bool? CanDamage() => timer <= DamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, Projectile.Center + ColumnDir * HeightNow, 30f, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //火柱向上顶
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 420);
            target.AddBuff(BuffID.Daybreak, 300);
            target.velocity += ColumnDir * 5f * target.knockBackResist;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_DawnEmber>(target.Center + Main.rand.NextVector2Circular(10f, 10f)
                        , ColumnDir.RotatedByRandom(0.6f) * Main.rand.NextFloat(3f, 8f)
                        , default, Main.rand.NextFloat(0.9f, 1.4f)).Configure(Main.rand.Next(16, 24));
                }
            }
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.Write((short)timer);

        public override void ReceiveExtraAI(BinaryReader reader) => timer = reader.ReadInt16();

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles
            , List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D flame = FlameTex?.Value;
            Texture2D glow = GlowTex?.Value;
            if (flame == null || !anchored) {
                return false;
            }
            float h = HeightNow;
            if (h < 6f) {
                return false;
            }
            Vector2 foot = Projectile.Center - Main.screenPosition;
            float rot = Projectile.ai[1] + MathHelper.PiOver2;
            //火的时域签名:柱身逐帧长度抖动,伤害窗后抖动加剧读作将熄
            float jitter = 0.9f + 0.14f * MathF.Sin((timer * 2.3f + jitterSeed) * 2.9f)
                * (timer > DamageWindow ? 1.6f : 1f);
            float fade = 1f - MathF.Pow(Age, 3f);
            var origin = new Vector2(flame.Width * 0.5f, flame.Height);

            //焦暗衬宽体带 A 遮挡,给亮焰实体轮廓
            Color soot = FlameSoot * (0.55f * fade);
            Main.EntitySpriteDraw(flame, foot, null, soot, rot, origin
                , new Vector2(48f / flame.Width, h * jitter * 1.08f / flame.Height), SpriteEffects.None, 0);

            //亮焰双层:红橙体+金芯,A=0 走加法
            Color body = FlameRed with { A = 0 } * (0.9f * fade);
            Main.EntitySpriteDraw(flame, foot, null, body, rot, origin
                , new Vector2(36f / flame.Width, h * jitter / flame.Height), SpriteEffects.None, 0);
            Color core = FlameGold with { A = 0 } * fade;
            Main.EntitySpriteDraw(flame, foot, null, core, rot, origin
                , new Vector2(20f / flame.Width, h * jitter * 0.92f / flame.Height), SpriteEffects.None, 0);

            //根部辉光钉地
            if (glow != null) {
                Color baseGlow = FlameGold with { A = 0 } * (0.5f * fade);
                Main.EntitySpriteDraw(glow, foot, null, baseGlow, 0f, glow.Size() * 0.5f
                    , new Vector2(0.34f, 0.16f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
