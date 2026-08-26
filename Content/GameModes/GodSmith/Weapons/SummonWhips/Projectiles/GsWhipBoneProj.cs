using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
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
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal static readonly Color BoneBright = new(255, 250, 236);
        internal static readonly Color BoneMain = new(226, 222, 200);
        internal static readonly Color BoneDeep = new(140, 132, 108);

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
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(
                            Projectile.Center - new Vector2(0f, SpireHeight),
                            Main.rand.NextVector2Circular(4.5f, 3.5f) - Vector2.UnitY * 2f,
                            BoneMain, Main.rand.NextFloat(0.5f, 0.8f));
                    }
                }
            }
            //窜刺期骨屑贴地迸溅
            if (elapsed >= OmenFrames && elapsed < OmenFrames + RiseFrames && !VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    Dust d = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-SpireWidth, SpireWidth) * 0.5f, 0f),
                        DustID.Bone, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 5f)),
                        0, default, 1.1f);
                    d.noGravity = false;
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //主段从脚下顶起：击退向上带
            if (Elapsed < OmenFrames + RiseFrames) {
                modifiers.Knockback += 2f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D edge = CWRUtils.GetT2DAsset(CWRConstant.Masking + "CrescentEdge01")?.Value;
            Texture2D jag = CWRUtils.GetT2DAsset(CWRConstant.Masking + "HitJagged01")?.Value;
            if (edge == null || jag == null) {
                return false;
            }
            int elapsed = Elapsed;
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            float seed = Projectile.identity * 0.47f;
            if (elapsed < OmenFrames) {
                //预兆：地基骨白微光渐亮
                float g = elapsed / (float)OmenFrames;
                Main.EntitySpriteDraw(jag, basePos, null, BoneMain with { A = 0 } * (0.45f * g),
                    seed, jag.Size() * 0.5f, 0.35f * g + 0.1f, SpriteEffects.None, 0);
                return false;
            }
            //柱体：三片骨白竖弧交错、微开叉堆成刺柱；整体渐隐在尾段
            float fade = elapsed >= OmenFrames + RiseFrames + CrackFrames
                ? 1f - (elapsed - OmenFrames - RiseFrames - CrackFrames) / (float)(LifeFrames - OmenFrames - RiseFrames - CrackFrames)
                : 1f;
            float rise = RiseT;
            for (int i = 0; i < 3; i++) {
                float lean = (i - 1) * 0.16f;
                float hScale = (0.55f + 0.5f * rise) * (1f - MathF.Abs(lean) * 0.7f);
                Vector2 tipOffset = new(lean * 46f, -SpireHeight * rise * (1f - MathF.Abs(lean) * 0.35f) * 0.5f);
                Main.EntitySpriteDraw(edge, basePos + tipOffset, null,
                    (i == 1 ? BoneBright : BoneMain) with { A = 0 } * (0.8f * fade),
                    -MathHelper.PiOver2 + lean, edge.Size() * 0.5f,
                    new Vector2(hScale * 1.1f, 0.5f), SpriteEffects.None, 0);
            }
            //迸裂闪
            if (elapsed >= OmenFrames + RiseFrames && elapsed < OmenFrames + RiseFrames + CrackFrames + 4) {
                float f = 1f - (elapsed - OmenFrames - RiseFrames) / (float)(CrackFrames + 4);
                Main.EntitySpriteDraw(jag, basePos - new Vector2(0f, SpireHeight * rise), null,
                    BoneBright with { A = 0 } * (0.85f * f), -seed,
                    jag.Size() * 0.5f, 0.5f * f + 0.12f, SpriteEffects.None, 0);
            }
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
        public override string Texture => CWRConstant.VaultPlaceholder;

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
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center,
                        Main.rand.NextVector2Circular(5f, 3f),
                        GsWhipBoneSpireProj.BoneMain, Main.rand.NextFloat(0.4f, 0.7f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D edge = CWRUtils.GetT2DAsset(CWRConstant.Masking + "CrescentEdge01")?.Value;
            if (edge == null) {
                return false;
            }
            //一道骨白横弧快扫后渐隐
            float t = Elapsed / (float)LifeFrames;
            float fade = 1f - t;
            float sweep = MathHelper.Lerp(-0.7f, 0.7f, MathF.Min(1f, t * 2.2f));
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(edge, pos, null,
                GsWhipBoneSpireProj.BoneBright with { A = 0 } * (0.75f * fade),
                sweep, edge.Size() * 0.5f, new Vector2(1.15f, 0.7f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(edge, pos, null,
                GsWhipBoneSpireProj.BoneMain with { A = 0 } * (0.45f * fade),
                sweep - 0.25f, edge.Size() * 0.5f, new Vector2(0.9f, 0.55f), SpriteEffects.None, 0);
            return false;
        }
    }
}
