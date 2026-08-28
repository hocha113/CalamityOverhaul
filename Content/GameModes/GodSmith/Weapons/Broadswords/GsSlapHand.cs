using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【天罚之掌】材质：神像掌骨蒙金皮的巨掌，掌心有拍痕。
    /// 签名：①原版身份保留：离谱击退与拍击粒子（含 Item175 拍声）照旧，
    /// 击退严格随出手向 ②每次命中拍出巨大掌印冲击：加色掌印闪光 + 径向气浪线
    /// ③终结拍「掌颂」：双掌合十，掌间震出小范围冲击波，滑稽而虔诚
    /// </summary>
    internal class GsSlapHand : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.SlapHand;

        protected override int HeldProjID => ModContent.ProjectileType<GsSlapHandHeld>();

        protected override string GsDescFallback =>
            "Reforged: a three-beat divine slapping; every hit stamps a giant palm print " +
            "with absurd knockback along the swing, and the finisher claps both palms together, " +
            "ringing out a small shockwave";

        //金皮掌骨色板
        internal static readonly Color PalmBright = new(255, 226, 178); //金皮亮缘
        internal static readonly Color PalmMain = new(236, 164, 118);   //掌肉体色
        internal static readonly Color PalmHot = new(255, 238, 120);    //天罚金光
        internal static readonly Color PalmDeep = new(58, 32, 24);      //掌影暗褐

        //拍表 1.0/1.0/1.25 均摊 ~1.08x，三拍循环 ~64 帧对原版 20 帧/斩 帧效率 ~0.94x，
        //掌颂冲击波 0.4x 只在终结拍出 → 综合 DPS 约为原版 102%~114%；
        //击退是身份：底伤 +4%，击退再 +15%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.04f;

        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => knockback *= 1.15f;
    }

    /// <summary>
    /// 天罚之掌手持：三拍掌击。0 正手掌 / 1 反手掌（宽扁小弧带前压推步）/
    /// 2 掌颂（长举合十，斩切瞬间掌间震出冲击波）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsSlapHandHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.SlapHand;
        protected override Color EdgeBright => GsSlapHand.PalmBright;
        protected override Color BodyMain => GsSlapHand.PalmMain;
        protected override Color HotAccent => GsSlapHand.PalmHot;
        protected override Color DeepShadow => GsSlapHand.PalmDeep;

        //推掌几何：触及短、判定极宽（一巴掌糊过去的面）
        protected override float BaseReach => 98f;
        protected override float CollisionWidth => 60f;
        protected override float PointBlankRadius => 52f;
        protected override float BladePark => 0.5f;

        private bool clapFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 正手掌：小后摆宽推，带步
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.3f, Follow = 0.7f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 1.3f, SwingPitch = 0.2f,
            },
            //拍1 反手掌
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.35f, Follow = 0.72f, ReachScale = 1f, LeanAmp = 0.055f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 1.3f, SwingPitch = 0.34f,
            },
            //拍2 掌颂：长举合十、滞一息、震出
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 14,
                RaiseBack = 1.6f, Follow = 0.8f, ReachScale = 1.08f, LeanAmp = 0.08f,
                DamageMult = 1.25f, Hitstop = 3, LungeSpeed = 2.4f, SwingPitch = -0.1f,
            },
        };

        /// <summary>掌颂爆发：掌间震出冲击波</summary>
        protected override void OnSlashBegin() {
            if (!IsFinisher || clapFired) {
                return;
            }
            clapFired = true;
            SetFlash(7);
            Vector2 dir = baseAngle.ToRotationVector2();
            int clapDamage = Math.Max(1, (int)(Projectile.damage * 0.4f));
            SpawnOwnedProj(ModContent.ProjectileType<GsSlapHandClapProj>(),
                Hand + dir * (FullReach * 0.55f), Vector2.Zero, clapDamage,
                Projectile.knockBack * 0.8f);
        }

        /// <summary>击退身份：命中再补一成五击退，终结拍更狠（方向已由基类钉死随出手向）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.Knockback *= IsFinisher ? 1.3f : 1.15f;

        /// <summary>命中记账（owner 端）：原版拍击粒子广播（内含 Item175 拍声）+ 掌印弹幕</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            ParticleOrchestraSettings settings = new() { PositionInWorld = target.Center };
            ParticleOrchestrator.RequestParticleSpawn(clientOnly: false,
                ParticleOrchestraType.SlapHand, settings, Owner.whoAmI);

            //巨大掌印：命中必出，方向随出手向
            SpawnOwnedProj(ModContent.ProjectileType<GsSlapHandPrintProj>(),
                target.Center, Vector2.Zero, 0, 0f, mainAngle, facingDir);
        }

        protected override void PlaySwingSound() {
            //掌风比刀风闷
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = Beat.SwingPitch - 0.3f }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item175 with { Volume = 0.5f, Pitch = -0.3f }, Owner.Center);
            }
        }

        /// <summary>掌颂合十：举相另一只虚掌自对侧合拢，滞相双掌贴定</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase > PhaseHold) {
                return;
            }
            float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            //镜掌角度：从主掌对侧合拢到出手线
            float mirrorAngle = MathHelper.Lerp(2f * baseAngle - mainAngle + swingDir * 0.9f * (1f - p),
                2f * baseAngle - mainAngle, p);
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            SpriteEffects mirrorEffect = effect == SpriteEffects.None
                ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            Vector2 at = Hand + (mirrorAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
            Color ghost = GsSlapHand.PalmHot * (0.25f + 0.3f * p);
            ghost.A = 0;
            sb.Draw(tex, at, null, ghost, mirrorAngle - rotOffset, tex.Size() / 2f, scale, mirrorEffect, 0);
        }
    }

    /// <summary>
    /// 巨大掌印：命中处拍出的演出弹幕（零伤）。掌形加色闪光 3 帧过冲撑满，
    /// 径向气浪线外推渐散；ai[0]=掌向 ai[1]=朝向符号。绘制全走确定性相位
    /// </summary>
    internal class GsSlapHandPrintProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 22;
        private float SlapAngle => Projectile.ai[0];
        private int Facing => Projectile.ai[1] >= 0f ? 1 : -1;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            Lighting.AddLight(Projectile.Center, GsSlapHand.PalmHot.ToVector3() * (0.5f * (1f - Life01)));
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.SlapHand);
            Texture2D palm = TextureAssets.Item[ItemID.SlapHand].Value;
            Texture2D air = CWRAsset.Airflow?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (air == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            //掌印 3 帧带 18% 过冲拍实
            float grow = Life <= 3f ? 1.18f * (Life / 3f)
                : MathHelper.Lerp(1.18f, 1f, MathHelper.Clamp((Life - 3f) / 4f, 0f, 1f));

            bool flip = MathF.Cos(SlapAngle) < 0f;
            SpriteEffects effect = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float rotOffset = flip ? -MathHelper.PiOver4 : MathHelper.PiOver4;

            //掌印本体：巨掌加色闪光，双层描出金边
            Color print = GsSlapHand.PalmHot * (0.72f * fade);
            print.A = 0;
            Main.EntitySpriteDraw(palm, center, null, print, SlapAngle + rotOffset,
                palm.Size() * 0.5f, 1.5f * grow, effect, 0);
            Color rim = GsSlapHand.PalmBright * (0.4f * fade);
            rim.A = 0;
            Main.EntitySpriteDraw(palm, center, null, rim, SlapAngle + rotOffset,
                palm.Size() * 0.5f, 1.62f * grow, effect, 0);

            //径向气浪线：八根流线自掌心外推、越推越散
            float push = MathHelper.Lerp(14f, 44f, Life01);
            for (int i = 0; i < 8; i++) {
                float ang = SlapAngle + MathHelper.Lerp(-0.9f, 0.9f, i / 7f) + (SegRand(i) - 0.5f) * 0.25f;
                Vector2 at = center + ang.ToRotationVector2() * (push * (0.7f + 0.5f * SegRand(i + 10)));
                Color line = GsSlapHand.PalmBright * (0.35f * fade);
                line.A = 0;
                Main.EntitySpriteDraw(air, at, null, line, ang, air.Size() * 0.5f,
                    new Vector2(0.34f, 0.045f), SpriteEffects.None, 0);
            }

            //掌心亮核
            Color core = GsSlapHand.PalmBright * (0.5f * fade * fade);
            core.A = 0;
            Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f, 0.5f * grow, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 掌颂冲击波：合十震出的环形冲击。8 帧过冲撑到满径，伤害只在扩张期结算一次，
    /// 击退向外且加重。首帧一记厚拍声。绘制全走确定性相位
    /// </summary>
    internal class GsSlapHandClapProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 22;
        private const float MaxRadius = 116f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：8 帧过冲 8% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
                float burst = p < 0.7f ? 1.08f * (p / 0.7f) : MathHelper.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                //合十：一记厚拍 + 空气嗡响
                SoundEngine.PlaySound(SoundID.Item175 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = 0.15f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6.5f),
                        Main.rand.NextBool() ? GsSlapHand.PalmHot : GsSlapHand.PalmBright,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
                }
            }
            Lighting.AddLight(Projectile.Center, GsSlapHand.PalmHot.ToVector3() * (0.6f * (1f - Life01)));
        }

        //伤害只在扩张期结算一次
        public override bool? CanDamage() => Life <= 9f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        /// <summary>击退向外并加重：被掌颂震飞是这把武器的教义</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);
            modifiers.Knockback *= 1.4f;
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D air = CWRAsset.Airflow?.Value;
            if (smear == null || glow == null || air == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //双弧合鸣：两道半圆涂抹对扣成环，随扩张转开
            for (int i = 0; i < 2; i++) {
                float rot = Life * 0.06f * (i == 0 ? 1f : -1f) + i * MathHelper.Pi + SegRand(3) * 6.28f;
                Color arc = GsSlapHand.PalmHot * (0.5f * fade);
                arc.A = 0;
                Main.EntitySpriteDraw(smear, center, null, arc, rot, smear.Size() * 0.5f,
                    new Vector2(radius / smear.Width * 2.3f, radius / smear.Height * 1.5f), SpriteEffects.None, 0);
            }

            //环缘光珠：一圈拍出去的气浪珠
            int beads = 10;
            for (int i = 0; i < beads; i++) {
                float ang = MathHelper.TwoPi * i / beads + SegRand(i) * 0.5f;
                Vector2 at = center + ang.ToRotationVector2() * radius;
                Color bead = GsSlapHand.PalmBright * (0.45f * fade);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.2f + 0.08f * SegRand(i + 20), SpriteEffects.None, 0);
            }

            //横向气浪线：冲击波掀起的水平流线
            for (int i = -1; i <= 1; i += 2) {
                Color line = GsSlapHand.PalmBright * (0.3f * fade);
                line.A = 0;
                Main.EntitySpriteDraw(air, center + new Vector2(i * radius * 0.7f, 0f), null, line,
                    i > 0 ? 0f : MathHelper.Pi, air.Size() * 0.5f,
                    new Vector2(0.5f * Life01 + 0.2f, 0.06f), SpriteEffects.None, 0);
            }

            //掌心金核
            Color core = GsSlapHand.PalmHot * (0.6f * fade * fade);
            core.A = 0;
            Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f, 0.6f, SpriteEffects.None, 0);
            return false;
        }
    }
}
