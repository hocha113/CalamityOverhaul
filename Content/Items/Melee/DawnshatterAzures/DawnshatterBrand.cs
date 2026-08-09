using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>
    /// 破晓斩痕:重击(终结拍/突刺/下砸)命中的目标身上留一道横贯金线,从暗到亮 12t 后沿线爆出日芒<br/>
    /// 无伤害纯演出,owner 端 OnHitNPC 生成,弹幕自然同步到各端<br/>
    /// ai[0]=目标 NPC index ai[1]=斩线角(命中时的枪向)
    /// </summary>
    internal class DawnshatterBrand : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> StreakTex = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        private const int LifeFrames = 20;
        /// 亮满爆发帧
        private const int BurstTick = 12;

        private static readonly Color DawnGold = new(255, 210, 110);
        private static readonly Color DawnDeep = new(214, 72, 26);

        private int Timer => LifeFrames - Projectile.timeLeft;
        /// 斩线半长,锚定时按目标体型定
        private float halfLen = 60f;
        private bool burstFired;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>重击留痕,只在 owner 端调用;弹幕自带生成包同步到各端</summary>
        internal static void Strike(Player owner, NPC target, Vector2 slashVec) {
            if (owner == null || target == null || Main.myPlayer != owner.whoAmI) {
                return;
            }
            Projectile.NewProjectile(owner.GetSource_Misc("CWR_DawnshatterBrand"), target.Center, Vector2.Zero
                , ModContent.ProjectileType<DawnshatterBrand>(), 0, 0f, owner.whoAmI
                , ai0: target.whoAmI, ai1: slashVec.SafeNormalize(Vector2.UnitX).ToRotation());
        }

        public override void AI() {
            int idx = (int)Projectile.ai[0];
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC npc = Main.npc[idx];
                if (npc.active) {
                    Projectile.Center = npc.Center;
                    halfLen = MathF.Max(48f, MathF.Max(npc.width, npc.height) * 0.72f);
                }
            }

            //亮满一瞬:金线沿线爆出日芒余烬,痕迹由生转爆
            if (!burstFired && Timer >= BurstTick) {
                burstFired = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
                    Vector2 lineDir = Projectile.ai[1].ToRotationVector2();
                    Vector2 outward = lineDir.RotatedBy(MathHelper.PiOver2);
                    for (int i = 0; i < 9; i++) {
                        float along = Main.rand.NextFloat(-1f, 1f);
                        float side = Main.rand.NextBool() ? 1f : -1f;
                        PRTLoader.NewParticle<PRT_DawnEmber>(Projectile.Center + lineDir * along * halfLen
                            , outward * side * Main.rand.NextFloat(2f, 6f) + lineDir * along * 2.5f
                            , default, Main.rand.NextFloat(0.8f, 1.3f)).Configure(Main.rand.Next(14, 24));
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1.1f, 0.7f, 0.3f)
                * MathHelper.Clamp(Timer / (float)BurstTick, 0f, 1f) * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = StreakTex?.Value;
            Texture2D glow = GlowTex?.Value;
            if (streak == null || glow == null) {
                return false;
            }
            float t = Timer;
            //从暗到亮的破晓斜坡,爆发后快退
            float birth = MathHelper.Clamp(t / BurstTick, 0f, 1f);
            float fade = t <= BurstTick
                ? MathF.Pow(birth, 1.6f)
                : 1f - (t - BurstTick) / (float)(LifeFrames - BurstTick);
            if (fade <= 0.02f) {
                return false;
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.ai[1] + MathHelper.PiOver2;
            //亮度爬升同时颜色从深红烧到金,线宽收细:破晓是"光出线"不是"贴纸变亮"
            Color col = Color.Lerp(DawnDeep, DawnGold, birth) with { A = 0 };
            float lineLen = halfLen * 2f * (0.55f + 0.45f * birth);
            float lineWidth = MathHelper.Lerp(0.5f, 0.24f, birth) + (t > BurstTick ? 0.1f : 0f);

            Main.EntitySpriteDraw(streak, pos, null, col * (0.9f * fade), rot, streak.Size() * 0.5f
                , new Vector2(lineWidth, lineLen / streak.Height), SpriteEffects.None, 0);

            //两端点日芒,亮满前只有微光
            Vector2 lineDir = Projectile.ai[1].ToRotationVector2();
            float tipK = MathF.Pow(birth, 2.2f) * fade;
            Color tip = new Color(255, 240, 200) with { A = 0 } * (0.8f * tipK);
            for (int side = -1; side <= 1; side += 2) {
                Main.EntitySpriteDraw(glow, pos + lineDir * side * lineLen * 0.5f, null, tip
                    , 0f, glow.Size() * 0.5f, 0.14f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
