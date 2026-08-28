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
    /// 【神赋·盔甲】锡套「锡鸣共振」：材质=锡钟敲响后扩散的声波环。<br/>
    /// ①命中积攒共鸣，满 5 层后下一击自目标敲响锡鸣，同点荡开三环②三环错拍扩张
    /// （各延迟 8 帧），只有环带扫过处才判定③被声波扫中的敌人震聋致乱（混乱 90 帧）
    /// ④受击走音崩落 2 层锡屑。<br/>
    /// 原版套装奖励（+2 防御）保留，神赋是叠加层；层数是攻击方端本地量，
    /// 就绪银鸣只对佩戴者自己可见（个人读数），跨端可见的部分是声波环实体
    /// </summary>
    internal class GsTinArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.TinHelmet];

        public override int BodyID => ItemID.TinChainmail;

        public override int LegsID => ItemID.TinGreaves;

        protected override string EndowLineFallback =>
            "Tin Resonance: strikes build resonance; at 5 stacks the next strike rings out three sonic rings that batter and bewilder foes";

        //锡钟声波色板
        internal static readonly Color TinSilver = new(196, 202, 208);
        internal static readonly Color PaleCyan = new(170, 220, 225);
        internal static readonly Color ChimeWhite = new(240, 248, 250);

        /// <summary>敲响所需共鸣层数</summary>
        private const int FullCharge = 5;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //就绪态：银亮微光绕身轻颤（个人读数，层数只在攻击方端存在）
            Lighting.AddLight(player.Center, PaleCyan.ToVector3() * 0.16f);
            if (Main.rand.NextBool(9)) {
                Vector2 at = player.Center + Main.rand.NextVector2CircularEdge(20f, 26f);
                PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.6f)),
                    ChimeWhite, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(14, 0.65f);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //声波环自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsTinChimeRingProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //敲响：满层后这一击自目标荡开三环
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.55f, Pitch = 0.6f }, target.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, ChimeWhite, 0.5f)
                    ?.Configure(0.15f, 1f, 14);
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.Tin,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 80, default, Main.rand.NextFloat(0.9f, 1.3f));
                    d.noGravity = true;
                }
            }
            //proc 弹幕 owner 侧生成；每环伤害按触发伤害 15% 折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int ringDamage = Math.Clamp((int)(damageDone * 0.15f), 5, 70);
                for (int i = 0; i < 3; i++) {
                    //ai[1] = 错拍序号，各自延迟 ai[1]*8 帧后才开始扩张与判定
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithTinEndow"),
                        target.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsTinChimeRingProj>(), ringDamage, 0.5f, player.whoAmI,
                        0f, i);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击走音崩落两层共鸣
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Tin, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 锡鸣声波环：锡钟荡出的一圈可闻声压，不是贴图圆片。ai[1]=错拍序号（延迟 ai[1]*8 帧），
    /// 扩张半径 r = 150*(1-(1-t)^2)（缓出减速），Colliding 只认 [r-26, r+26] 环带；
    /// 三层同心叠色（银外缘/淡青主体/白亮内缘），alpha 随扩张衰减，延迟期画待振小光点，
    /// 命中震聋致乱（混乱 90 帧）
    /// </summary>
    internal class GsTinChimeRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";

        /// <summary>扩张帧长</summary>
        private const int ExpandFrames = 34;

        /// <summary>环带判定半宽（px）</summary>
        private const float BandHalfWidth = 26f;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>错拍延迟帧数（ai[1] = 0/1/2 序号）</summary>
        private int Delay => (int)(Projectile.ai[1] * 8f);

        /// <summary>扩张进度 0..1（延迟期为 0）</summary>
        private float ExpandT => MathHelper.Clamp((Life - Delay) / (float)ExpandFrames, 0f, 1f);

        /// <summary>当前环半径：缓出减速，荡开快收尾慢</summary>
        private float Radius {
            get {
                float t = ExpandT;
                return 150f * (1f - (1f - t) * (1f - t));
            }
        }

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //起振一瞬：各环错拍轻鸣，音高逐环抬升
            if (!Main.dedServ && (int)Life == Delay + 1) {
                SoundEngine.PlaySound(SoundID.Item35 with {
                    Volume = 0.3f, Pitch = 0.5f + Projectile.ai[1] * 0.15f, MaxInstances = 3
                }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, GsTinArmor.PaleCyan.ToVector3() * (0.2f * (1f - ExpandT) * VisualFade));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //延迟期不判定；扩张期只认环带扫过处
            float t = ExpandT;
            if (t <= 0f) {
                return false;
            }
            float dist = targetHitbox.Center.ToVector2().Distance(Projectile.Center);
            float r = Radius;
            return dist >= r - BandHalfWidth && dist <= r + BandHalfWidth;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //震聋致乱
            target.AddBuff(BuffID.Confused, 90);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.25f, Pitch = 0.8f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Tin,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 80, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        //==================== 绘制：三层同心声波环 + 延迟期待振小光点 ====================

        public override bool PreDraw(ref Color lightColor) {
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float t = ExpandT;

            //延迟期：待振小光点，确定性相位呼吸
            if (t <= 0f) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow == null) {
                    return false;
                }
                float breathe = 1f + MathF.Sin(Life * 0.55f + Seed * 5f) * 0.2f;
                Main.EntitySpriteDraw(glow, pos, null, (GsTinArmor.PaleCyan with { A = 0 }) * (0.55f * fade),
                    0f, glow.Size() * 0.5f, 0.14f * breathe, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, (GsTinArmor.ChimeWhite with { A = 0 }) * (0.5f * fade),
                    0f, glow.Size() * 0.5f, 0.07f * breathe, SpriteEffects.None, 0);
                return false;
            }

            Texture2D tex = CWRAsset.DiffusionCircle?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;
            //按半径折算贴图缩放，alpha 随扩张衰减
            float scale = Radius * 2f / tex.Width;
            float ebb = (1f - t) * fade;

            //银外缘略大
            Main.EntitySpriteDraw(tex, pos, null, (GsTinArmor.TinSilver with { A = 0 }) * (0.7f * ebb),
                0f, origin, scale * 1.06f, SpriteEffects.None, 0);
            //淡青主体
            Main.EntitySpriteDraw(tex, pos, null, (GsTinArmor.PaleCyan with { A = 0 }) * ebb,
                0f, origin, scale, SpriteEffects.None, 0);
            //白亮内缘略小
            Main.EntitySpriteDraw(tex, pos, null, (GsTinArmor.ChimeWhite with { A = 0 }) * (0.8f * ebb),
                0f, origin, scale * 0.94f, SpriteEffects.None, 0);
            return false;
        }
    }
}
