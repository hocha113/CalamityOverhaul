using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【花瓣风暴】材质：山铜粉晶淬火的花刃。
    /// 签名：①每一挥沿刀弧撒出花瓣弹幕，先飘落打旋、再俯冲咬向近旁猎物（呼应山铜盔甲）
    /// ②终结拍舞袖回旋，整捧四瓣齐撒
    /// ③拍表走「舞袖」语汇：后摆最小、跟进最大，挥音下衬花瓣簌簌
    /// </summary>
    internal class GsOrichalcumSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.OrichalcumSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsOrichalcumSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: each swing scatters drifting petals along the arc that flutter, " +
            "then dive at nearby prey; the finisher looses a full bloom";

        //山铜粉晶色板
        internal static readonly Color BloomBright = new(255, 168, 210); //粉瓣亮
        internal static readonly Color BloomMain = new(232, 96, 160);    //山铜粉
        internal static readonly Color BloomHot = new(255, 214, 236);    //盛放粉白
        internal static readonly Color BloomDeep = new(70, 26, 52);      //深花影

        //预算账：拍均 (0.95+0.95+1.2)/3≈1.03；花瓣 2/2/4 枚 ×0.12x 追击
        //（散射后单体实取约半数 → +0.16/拍）；连段总帧 (20+19+27)=66 ≈ 原版 66 →
        //综合单体 DPS ≈ 1.03+0.16 ≈ 原版 105%~119%（瓣群散射的多目标覆盖另计），底伤不再加成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 花瓣风暴手持：三拍舞袖剑（RaiseBack 全族最小、Follow 全族最大，弧线连绵不断）。
    /// 每拍斩切爆发沿弧撒瓣，终结拍四瓣齐撒。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsOrichalcumSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.OrichalcumSword;
        protected override Color EdgeBright => GsOrichalcumSword.BloomBright;
        protected override Color BodyMain => GsOrichalcumSword.BloomMain;
        protected override Color HotAccent => GsOrichalcumSword.BloomHot;
        protected override Color DeepShadow => GsOrichalcumSword.BloomDeep;

        private bool petalsFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 舞袖顺斩：小后摆大跟进，弧线过身
            0 => new GsBroadBeat {
                Raise = 6, Hold = 1, Slash = 4, Recover = 9,
                RaiseBack = 1.5f, Follow = 1.35f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.05f,
            },
            //拍1 返袖
            1 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 9,
                RaiseBack = 1.45f, Follow = 1.4f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.12f,
            },
            //拍2 盛放回旋：跟进最深，整捧撒瓣
            _ => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 5, Recover = 13,
                RaiseBack = 1.9f, Follow = 1.6f, ReachScale = 1.12f, LeanAmp = 0.075f,
                DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = -0.08f,
            },
        };

        /// <summary>挥音下衬花瓣簌簌</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = Beat.SwingPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.32f, Pitch = 0.35f, MaxInstances = 3 }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.3f, Pitch = 0.25f }, Owner.Center);
            }
        }

        /// <summary>沿本次挥弧撒瓣：普通拍两枚、终结拍四枚，均匀铺在弧上外抛</summary>
        protected override void OnSlashBegin() {
            if (petalsFired) {
                return;
            }
            petalsFired = true;
            if (IsFinisher) {
                SetFlash(6);
            }
            int count = IsFinisher ? 4 : 2;
            int petalDamage = Math.Max(1, (int)(Projectile.damage * 0.12f));
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.Lerp(ArcStart, ArcEnd, (i + 1f) / (count + 1f));
                Vector2 dir = ang.ToRotationVector2();
                //切向外抛带一点上飘，随后进入飘落段
                Vector2 vel = dir * Main.rand.NextFloat(4.5f, 6f) + new Vector2(0f, -1.2f);
                SpawnOwnedProj(ModContent.ProjectileType<GsOrichalcumSwordPetalProj>(),
                    Hand + dir * (FullReach * 0.7f), vel, petalDamage, Projectile.knockBack * 0.2f,
                    i % 2 == 0 ? 1f : -1f, i);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (phase != PhaseSlash || !Main.rand.NextBool(2)) {
                return;
            }
            //斩切期刃面簌落粉尘
            PRTLoader.NewParticle<PRT_Light>(
                Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.4f, 1.1f)),
                Main.rand.NextBool(3) ? GsOrichalcumSword.BloomHot : GsOrichalcumSword.BloomMain,
                Main.rand.NextFloat(0.05f, 0.09f))?.Configure(11, 0.55f);
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //花瓣簌落柔响 + 粉白光雨（与金属剑的脆响区分）
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            int motes = IsFinisher ? 5 : 3;
            for (int i = 0; i < motes; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f);
                PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(10f, 10f), vel,
                    Main.rand.NextBool() ? GsOrichalcumSword.BloomBright : GsOrichalcumSword.BloomHot,
                    Main.rand.NextFloat(0.06f, 0.11f))?.Configure(13, 0.65f);
            }
        }
    }

    /// <summary>
    /// 追击花瓣：沿挥弧撒出，先 14 帧飘落打旋（轻重力+横向摇曳），
    /// 再锁定 420 像素内猎物俯冲咬去，速度随俯冲渐升；无猎物则继续飘散。
    /// 自绘叶形：两片镜像月牙拼成瓣身（粉体+亮缘）+ 粉晕垫底 + 瓣尖亮点，
    /// 张合呼吸吃 identity 种子。ai[0]=自旋方向 ai[1]=瓣序（错开摇曳相位）
    /// </summary>
    internal class GsOrichalcumSwordPetalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.OrichalcumSword");

        private ref float Life => ref Projectile.localAI[0];
        private float SpinDir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private float SwayPhase => Projectile.ai[1] * 1.7f;

        /// <summary>出生 3 帧淡入、末尾 8 帧淡出</summary>
        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;

            if (Life <= 14f) {
                //飘落段：轻重力 + 横向摇曳，像真花瓣旋落
                Projectile.velocity *= 0.94f;
                Projectile.velocity.Y += 0.05f;
                Projectile.velocity.X += MathF.Sin(Life * 0.5f + SwayPhase) * 0.16f;
                Projectile.rotation += SpinDir * 0.3f;
            }
            else {
                //俯冲段：咬向最近猎物，越追越快；无猎物继续飘
                NPC target = Projectile.Center.FindClosestNPC(420f);
                if (target != null) {
                    float chase = MathF.Min(6f + (Life - 14f) * 0.2f, 12f);
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * chase;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.11f);
                    Projectile.rotation += SpinDir * 0.42f;
                }
                else {
                    Projectile.velocity *= 0.97f;
                    Projectile.velocity.Y += 0.04f;
                    Projectile.velocity.X += MathF.Sin(Life * 0.45f + SwayPhase) * 0.12f;
                    Projectile.rotation += SpinDir * 0.26f;
                }
            }

            Lighting.AddLight(Projectile.Center, GsOrichalcumSword.BloomMain.ToVector3() * (0.22f * VisualFade));

            if (!VaultUtils.isServer && Life % 3f == 0f) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.08f,
                    Main.rand.NextBool() ? GsOrichalcumSword.BloomMain : GsOrichalcumSword.BloomBright,
                    Main.rand.NextFloat(0.04f, 0.08f))?.Configure(9, 0.5f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.25f, Pitch = 0.6f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    Main.rand.NextBool() ? GsOrichalcumSword.BloomBright : GsOrichalcumSword.BloomMain,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(9, 14));
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>两片镜像月牙拼瓣身：粉晕垫底 + 粉瓣双片 + 亮缘 + 瓣尖亮点（无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (crescent == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = crescent.Size() * 0.5f;
            //瓣面张合呼吸
            float breath = 0.88f + 0.12f * MathF.Sin(Life * 0.32f + SegRand(4) * 6.28f);
            float rot = Projectile.rotation;

            //粉晕垫底
            Color haze = GsOrichalcumSword.BloomDeep * (0.5f * fade);
            haze.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, haze, 0f, glow.Size() * 0.5f, 0.34f * breath, SpriteEffects.None, 0);
            //瓣身：两片镜像月牙微错角拼合
            Color body = GsOrichalcumSword.BloomMain * (0.85f * fade);
            body.A = 0;
            Main.EntitySpriteDraw(crescent, pos, null, body, rot + 0.35f, origin,
                new Vector2(0.11f, 0.07f) * breath, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crescent, pos, null, body, rot - 0.35f, origin,
                new Vector2(0.11f, 0.07f) * breath, SpriteEffects.FlipVertically, 0);
            //亮缘
            Color rim = GsOrichalcumSword.BloomBright * (0.7f * fade);
            rim.A = 0;
            Main.EntitySpriteDraw(crescent, pos, null, rim, rot + 0.35f, origin,
                new Vector2(0.085f, 0.04f) * breath, SpriteEffects.None, 0);
            //瓣尖亮点
            Vector2 tip = pos + rot.ToRotationVector2() * (9f * breath);
            Color dot = GsOrichalcumSword.BloomHot * (0.55f * fade);
            dot.A = 0;
            Main.EntitySpriteDraw(glow, tip, null, dot, 0f, glow.Size() * 0.5f, 0.1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
