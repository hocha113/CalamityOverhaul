using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 波尼鞭处决「脊骨突刺」：目标脚下窜出骨刺柱（主段 1.6x + 轻击飞），
    /// 顶端迸裂补一段（ai[0] 传二段伤害 0.6x），总账 2.2x。<br/>
    /// 生成位置由方案找地后传入，柱体从地基向上生长
    /// </summary>
    internal class GsWhipBoneSpireProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        private const int OmenFrames = 6;      //地面预兆
        private const int RiseFrames = 8;      //窜刺段（主伤窗）
        private const int CrackFrames = 4;     //顶端迸裂段（二段窗）
        private const int LifeFrames = 30;
        private const float SpireHeight = 132f;
        private const float SpireWidth = 46f;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        /// <summary>窜出进度 0~1</summary>
        private float RiseT => MathHelper.Clamp((Elapsed - OmenFrames) / (float)RiseFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? CanDamage()
            => Elapsed >= OmenFrames && Elapsed < OmenFrames + RiseFrames + CrackFrames ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //窜刺段：柱体实高矩形；迸裂段：顶端圆域
            if (Elapsed < OmenFrames + RiseFrames) {
                float h = SpireHeight * RiseT;
                Rectangle spire = new((int)(Projectile.Center.X - SpireWidth * 0.5f),
                    (int)(Projectile.Center.Y - h), (int)SpireWidth, (int)MathF.Max(h, 8f));
                return targetHitbox.Intersects(spire);
            }
            Vector2 tip = Projectile.Center - new Vector2(0f, SpireHeight);
            return targetHitbox.Intersects(Utils.CenteredRectangle(tip, new Vector2(140f)));
        }

        public override void AI() {
            int elapsed = Elapsed;
            if (elapsed == OmenFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.9f, Pitch = -0.35f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            }
            //迸裂段起点：伤害切二段口径（damage 是本端结算量，各端同式演化）
            if (elapsed == OmenFrames + RiseFrames) {
                Projectile.damage = Math.Max(1, (int)Projectile.ai[0]);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath2 with { Volume = 0.6f, Pitch = 0.2f },
                        Projectile.Center - new Vector2(0f, SpireHeight));
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //主段从脚下顶起：击退向上带
            if (Elapsed < OmenFrames + RiseFrames) {
                modifiers.Knockback += 2f;
            }
        }

        /// <summary>柱体一笔：原版骨头贴图自地基向上拉伸到当前柱高（lightColor 着色），尾段渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            if (elapsed < OmenFrames) {
                return false;
            }
            float fade = elapsed >= OmenFrames + RiseFrames + CrackFrames
                ? 1f - (elapsed - OmenFrames - RiseFrames - CrackFrames) / (float)(LifeFrames - OmenFrames - RiseFrames - CrackFrames)
                : 1f;
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float h = MathF.Max(SpireHeight * RiseT, 8f);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                new Vector2(tex.Width * 0.5f, tex.Height), new Vector2(SpireWidth / tex.Width, h / tex.Height),
                SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 波尼鞭「连骨振」：踩拍挥击单次挥中三敌时，鞭梢对本挥未中之敌
    /// 追加一记 60px 横扫余振（0.6x）。排除表由方案在生成后填充，
    /// 只服务 owner 端命中判定，无需过线
    /// </summary>
    internal class GsWhipBoneEchoProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        private const int LifeFrames = 14;
        private readonly HashSet<int> excludedNPCs = [];

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        /// <summary>登记本挥已中目标（owner 端生成后立即调用）</summary>
        internal void CaptureExclusions(HashSet<int> hitNPCs) {
            excludedNPCs.Clear();
            foreach (int who in hitNPCs) {
                excludedNPCs.Add(who);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Elapsed >= 2 && Elapsed < 7 ? null : false;

        public override bool? CanHitNPC(NPC target)
            => excludedNPCs.Contains(target.whoAmI) ? false : null;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center, new Vector2(120f)));

        public override void AI() {
            if (Elapsed == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        /// <summary>范围提示：原版气泡贴图按判定框缩放画一笔（lightColor 着色），随寿命渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fade = 1f - Elapsed / (float)LifeFrames;
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, Projectile.width / (float)tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
