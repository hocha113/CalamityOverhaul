using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】熔岩套「熔核喷发」（A 档）：材质=地底熔浆。<br/>
    /// ①命中积攒熔压，满 10 层后下一击自目标脚下裂地喷出三股熔浆柱（先裂纹预兆再喷发；
    /// 逐口向下 8 格探实心地面锚喷口，飞行目标在其正下方地面喷发）
    /// ②喷发后 4 秒进入「余温」，期间你的攻击附带烈焰③受击泄压崩 3 层，高攒高崩。<br/>
    /// 原版套装奖励保留，神赋是叠加层；层数与余温是攻击方端本地量，
    /// 跨端可见的部分是熔浆柱实体；柱体分相演出（蓄压 14 帧→喷发 26 帧），两端有收口
    /// </summary>
    internal class GsMoltenArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.MoltenHelmet];

        public override int BodyID => ItemID.MoltenBreastplate;

        public override int LegsID => ItemID.MoltenGreaves;

        protected override string EndowLineFallback =>
            "Core Eruption: strikes build magma pressure; at 10 stacks the next strike erupts three lava geysers underfoot, and your attacks ignite foes for 4 seconds after";

        //熔浆色板
        internal static readonly Color CharShell = new(70, 36, 28);
        internal static readonly Color MeltOrange = new(255, 120, 40);
        internal static readonly Color WhiteHot = new(255, 230, 180);

        /// <summary>喷发所需熔压层数</summary>
        private const int FullCharge = 10;

        /// <summary>余温窗口帧长（4 秒）</summary>
        private const int AfterheatFrames = 240;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //余温到期自然冷却（余温只在攻击方端存在）
            if (state.EndowFlag && Main.GameUpdateCount >= state.EndowTimer) {
                state.EndowFlag = false;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //余温体感：余烬绕身（个人读数）
            if (state.EndowFlag) {
                Lighting.AddLight(player.Center, MeltOrange.ToVector3() * 0.2f);
                if (Main.rand.NextBool(7)) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(14f, 20f),
                        DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.4f)), 100, default, Main.rand.NextFloat(1f, 1.5f));
                    d.noGravity = true;
                }
                return;
            }
            //满压就绪：热浪蒸腾（个人读数，层数只在攻击方端存在）
            if (state.EndowCharge >= FullCharge) {
                Lighting.AddLight(player.Center, MeltOrange.ToVector3() * 0.3f);
                if (Main.rand.NextBool(5)) {
                    Dust d = Dust.NewDustPerfect(player.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), 0f),
                        DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(1f, 2.2f)), 80, default, Main.rand.NextFloat(1.2f, 1.8f));
                    d.noGravity = true;
                }
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //熔浆柱自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsMoltenGeyserProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            //余温联动：窗口内每次命中附带烈焰
            if (state.EndowFlag && Main.GameUpdateCount < state.EndowTimer) {
                target.AddBuff(BuffID.OnFire, 120);
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), 80, default, 1.2f);
                    d.noGravity = true;
                }
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //满压喷发：目标脚下裂开三道熔核喷口，随后进入余温
            state.EndowCharge = 0;
            state.EndowFlag = true;
            state.EndowTimer = Main.GameUpdateCount + AfterheatFrames;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.8f, Pitch = -0.4f }, target.Bottom);
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Bottom, Vector2.Zero, MeltOrange, 0.5f)
                    ?.Configure(0.15f, 1f, 16);
            }
            //柱伤按触发伤害三成折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int geyserDamage = Math.Clamp((int)(damageDone * 0.30f), 10, 140);
                for (int i = -1; i <= 1; i++) {
                    //逐口探地（Pumpkin 式）：向下 8 格找实心地面锚喷口，飞行目标在其正下方地面喷发；
                    //脚下无地才保留原空中落位
                    Vector2 vent = new(target.Center.X + i * 70f, target.Bottom.Y);
                    GsArmorTerrainProbe.TryFindGroundBelow(vent, 8, out float groundY);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithMoltenEndow"),
                        new Vector2(vent.X, groundY),
                        Vector2.Zero,
                        ModContent.ProjectileType<GsMoltenGeyserProj>(), geyserDamage, 3f, player.whoAmI);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击泄压崩三层，攒得高崩得狠
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 3);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Lava, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 0, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 熔核喷口：地底熔浆的两相演出。前 14 帧裂纹蓄压（无判定：地缝辉光、余烬上飘、闷雷）；
    /// 后 26 帧喷发（锥形熔浆柱拔地而起：暗壳压边/熔橙柱体/白热窄芯，柱顶熔冠收口、
    /// 柱底熔池辉光，熔滴回落），宽度有生命周期，末段整体消散
    /// </summary>
    internal class GsMoltenGeyserProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>蓄压相帧数</summary>
        private const int TelegraphFrames = 14;

        /// <summary>喷发相帧数</summary>
        private const int EruptFrames = 26;

        /// <summary>柱体全高（像素）</summary>
        private const float ColumnHeight = 150f;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>喷发进度 0..1（蓄压期为 0）</summary>
        private float EruptT => MathHelper.Clamp((Life - TelegraphFrames) / EruptFrames, 0f, 1f);

        /// <summary>柱高包络：快升缓顶</summary>
        private float HeightT => MathF.Sin(MathHelper.Clamp(EruptT * 1.8f, 0f, 1f) * MathHelper.PiOver2);

        /// <summary>末段消散系数</summary>
        private float EndFade => 1f - MathHelper.Clamp((EruptT - 0.72f) / 0.28f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TelegraphFrames + EruptFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //蓄压相：地缝余烬聚拢上飘，无判定
            if (Life <= TelegraphFrames) {
                if (!Main.dedServ && Life % 2 == 0) {
                    Dust d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-18f, 18f), 0f),
                        DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), 100, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Bottom, GsMoltenArmor.MeltOrange.ToVector3() * (0.25f * Life / TelegraphFrames));
                return;
            }

            //喷发瞬间：撑起柱形判定（保底锚住柱底）、喷响、熔浆迸溅
            if (Life == TelegraphFrames + 1) {
                Vector2 bottom = Projectile.Bottom;
                Projectile.Resize(36, (int)ColumnHeight);
                Projectile.Bottom = bottom;
                Projectile.friendly = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.65f, Pitch = -0.15f, MaxInstances = 3 }, bottom);
                    for (int i = 0; i < 10; i++) {
                        Dust d = Dust.NewDustPerfect(bottom, DustID.Lava,
                            new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(4f, 9f)), 0, default, Main.rand.NextFloat(1f, 1.6f));
                        d.noGravity = false;
                    }
                }
            }

            //喷发相：柱顶熔滴回落 + 柱身火星
            if (!Main.dedServ && EndFade > 0.1f) {
                if (Life % 3 == 0) {
                    Vector2 top = Projectile.Bottom - new Vector2(0f, ColumnHeight * HeightT);
                    Dust d = Dust.NewDustPerfect(top + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                        DustID.Lava, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)), 0, default, Main.rand.NextFloat(0.9f, 1.3f));
                    d.noGravity = false;
                }
                if (Life % 4 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Bottom - new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(0f, ColumnHeight * HeightT * 0.8f)),
                        new Vector2(0f, -Main.rand.NextFloat(1f, 3f)),
                        Main.rand.NextBool() ? GsMoltenArmor.MeltOrange : GsMoltenArmor.WhiteHot,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 18));
                }
            }
            Lighting.AddLight(Projectile.Center, GsMoltenArmor.MeltOrange.ToVector3() * (0.7f * HeightT * EndFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Lava,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
        }

        //==================== 绘制：裂纹预兆 → 锥形熔浆柱（暗壳/熔橙/白热三层 + 两端收口） ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (tex == null || glow == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 bottom = Projectile.Bottom - Main.screenPosition;

            //蓄压相：地缝辉光横贴，随进度增亮微颤
            if (Life <= TelegraphFrames) {
                float tp = Life / (float)TelegraphFrames;
                float crackFlicker = 1f + MathF.Sin(Life * 1.1f + Seed * 5f) * 0.15f;
                Vector2 crackScale = new(56f / tex.Width * (0.4f + 0.6f * tp) * crackFlicker, 10f / tex.Height);
                Main.EntitySpriteDraw(tex, bottom, null, GsMoltenArmor.CharShell * (0.8f * tp),
                    0f, origin, crackScale * 1.3f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, bottom, null, (GsMoltenArmor.MeltOrange with { A = 0 }) * (0.7f * tp),
                    0f, origin, crackScale, SpriteEffects.None, 0);
                return false;
            }

            float h = ColumnHeight * HeightT;
            float fade = EndFade;
            //柱底熔池辉光（收口之一：源头有名字的答案）
            Main.EntitySpriteDraw(glow, bottom, null, (GsMoltenArmor.MeltOrange with { A = 0 }) * (0.75f * fade),
                0f, glow.Size() * 0.5f, new Vector2(1.4f, 0.5f) * (0.8f + 0.2f * HeightT), SpriteEffects.None, 0);

            //柱身：三段叠升、上窄下宽（锥形收口），宽度随喷发呼吸
            Span<(float heightAt, float widthMul)> stacks = [(0.22f, 1f), (0.55f, 0.8f), (0.85f, 0.55f)];
            for (int layer = 0; layer < 3; layer++) {
                //layer 0=暗壳压边(真alpha暗色遮底) 1=熔橙柱体 2=白热窄芯
                foreach ((float heightAt, float widthMul) in stacks) {
                    float flicker = 1f + MathF.Sin(Life * 0.7f + Seed * 4f + heightAt * 9f) * 0.12f;
                    float baseW = layer switch { 0 => 38f, 1 => 26f, _ => 12f };
                    float segH = h * 0.62f;
                    Vector2 at = bottom - new Vector2(0f, h * heightAt);
                    Vector2 scale = new(baseW * widthMul * flicker / tex.Width, segH / tex.Height);
                    Color c = layer switch {
                        0 => GsMoltenArmor.CharShell * (0.8f * fade),
                        1 => (GsMoltenArmor.MeltOrange with { A = 0 }) * (0.85f * fade),
                        _ => (GsMoltenArmor.WhiteHot with { A = 0 }) * (0.7f * fade),
                    };
                    Main.EntitySpriteDraw(tex, at, null, c, 0f, origin, scale, SpriteEffects.None, 0);
                }
            }

            //柱顶熔冠（收口之二：末端有名字的答案），随高度爬升
            Vector2 crown = bottom - new Vector2(0f, h);
            float crownPulse = 1f + MathF.Sin(Life * 0.9f + Seed * 6f) * 0.15f;
            Main.EntitySpriteDraw(tex, crown, null, (GsMoltenArmor.MeltOrange with { A = 0 }) * (0.8f * fade),
                0f, origin, new Vector2(22f, 16f) * crownPulse / tex.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, crown, null, (GsMoltenArmor.WhiteHot with { A = 0 }) * (0.6f * fade),
                0f, origin, new Vector2(11f, 8f) * crownPulse / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
