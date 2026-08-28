using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 附身飞斧重铸（★A 档）。材质：怨魂缠绕的陨铁斧。签名行为：①手持蓄力掷，举斧时怨焰渐亮
    /// ②去程加速追猎，命中后缠上目标绕身连斩，每斩叠一层怨魂印 ③第五层怨魂爆裂，
    /// 造成 150% 爆发并震出魂环 ④全程怨绿鬼火拖尾，余焰长过斧体
    /// </summary>
    internal class GsPossessedHatchet : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.PossessedHatchet;

        internal override int BoomerProjType => ModContent.ProjectileType<GsPossessedHatchetProj>();

        internal override int MaxAirborne => int.MaxValue;   //原版无同场上限，掷速由手持接管节流

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "A wind-up throw: the possessed axe accelerates after its prey, then latches on,\n" +
            "whirling around the victim and branding a soul mark with every slash\n" +
            "The fifth mark ruptures for 150% damage; right click to redirect the axe mid-flight";

        public override bool? GsCanUseItem(Item item, Player player) {
            //手持在场即冷却；镜像 GsIronBroadsword 的接管范式
            if (HeldAlive<GsPossessedHatchetHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsPossessedHatchetHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            //全端返回 false 压掉原版挥舞，远端靠手持弹幕同步看动作
            return false;
        }
    }

    /// <summary>附身飞斧蓄力掷手持：快节奏起手，怨焰蓄势</summary>
    internal class GsPossessedHatchetHeld : GsBoomerThrowHeldBase
    {
        protected override int SourceItemID => ItemID.PossessedHatchet;

        protected override int BoomerangType => ModContent.ProjectileType<GsPossessedHatchetProj>();

        protected override Color GlowColor => GsPossessedHatchetProj.SoulGreen;

        protected override int RaiseDur => 7;

        protected override int ReleaseDur => 6;

        protected override float ThrowSpeedMul => 1.2f;

        protected override float ForwardStep => 1.2f;

        protected override SoundStyle ThrowSound => SoundID.Item1 with { Volume = 0.8f, Pitch = -0.25f };

        protected override void OnReleaseFX() {
            base.OnReleaseFX();
            if (VaultUtils.isServer) {
                return;
            }
            //出手怨焰扑散
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.4f }, Owner.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_SoulFire>(Owner.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f) - new Vector2(0f, 1f),
                    GsPossessedHatchetProj.SoulGreen, Main.rand.NextFloat(0.5f, 0.8f));
            }
        }
    }

    /// <summary>怨魂斧体：追猎、缠斩、五印爆裂</summary>
    internal class GsPossessedHatchetProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.PossessedHatchet;

        /// <summary>怨魂绿</summary>
        internal static readonly Color SoulGreen = new(110, 240, 140);

        protected override Color GlowColor => SoulGreen;

        protected override Color TrailColor => new(70, 190, 110);

        protected override int OutTime => 60;
        protected override float OutDrag => 1f;          //去程走追猎加速，不用整体衰减
        protected override int HoverTime => 42;          //缠斩窗口
        protected override int HitCooldown => 12;
        protected override int RedirectCharges => 2;
        protected override bool HoverOnFirstHit => false;
        protected override bool AllowCommandInOut => true;
        protected override float GhostBaseAlpha => 0.3f;
        protected override SoundStyle HitSound => SoundID.Tink with { Volume = 0.4f, Pitch = -0.15f };

        /// <summary>缠附目标（ai[2] 过线：0=无，否则 whoAmI+1）</summary>
        private NPC LatchTarget {
            get {
                int id = (int)Projectile.ai[2] - 1;
                if (id < 0 || id >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[id];
                return npc.active && npc.CanBeChasedBy(Projectile) ? npc : null;
            }
            set => Projectile.ai[2] = value == null ? 0f : value.whoAmI + 1;
        }

        /// <summary>目标 whoAmI → 怨魂印层数（owner 判定端本地量）</summary>
        private readonly Dictionary<int, int> soulMarks = [];

        /// <summary>绘制读数用：当前缠附目标的印数</summary>
        private int MarksOnLatch
            => LatchTarget != null && soulMarks.TryGetValue(LatchTarget.whoAmI, out int n) ? n : 0;

        protected override void OnOutTick(Player owner) {
            //追猎：无目标先索敌，有目标弧线加速咬合（速度 10→19 递增曲线）
            NPC target = LatchTarget;
            if (target == null) {
                target = Projectile.FindTargetWithinRange(700f);
                if (target != null && Projectile.IsOwnedByLocalPlayer()) {
                    LatchTarget = target;
                    Projectile.netUpdate = true;
                }
            }
            float speed = MathF.Min(19f, 10f + (PhaseTimer * 0.35f));
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX * spinDir) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
            }
            else {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX * spinDir)
                    * (speed * (0.9f + (0.12f * MathF.Sin(PhaseTimer * 0.3f))));
            }
        }

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            LatchTarget ??= target;
            soulMarks.TryGetValue(target.whoAmI, out int marks);
            marks++;
            if (marks >= 5) {
                soulMarks[target.whoAmI] = 0;
                SoulRupture(target);
                EnterPhase(PhaseReturn, Owner);
                return;
            }
            soulMarks[target.whoAmI] = marks;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.3f, Pitch = 0.2f + (marks * 0.1f) }, target.Center);
            }
            //命中即入缠斩：绕着咬住的目标连转
            if (Phase == PhaseOut || Phase == PhaseDash) {
                LatchTarget = target;
                EnterPhase(PhaseHover, Owner);
            }
        }

        /// <summary>第五印爆裂：150% 爆发判定 + 魂环演出</summary>
        private void SoulRupture(NPC target) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 1.5f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsPossessedHatchetSoulBurstProj>(), dmg, 6f, Owner.whoAmI);
            }
        }

        protected override void OnHoverTick(Player owner) {
            NPC target = LatchTarget;
            if (target == null) {
                //目标没了就收手
                if (PhaseTimer > 2) {
                    EnterPhase(PhaseReturn, owner);
                }
                return;
            }
            //缠斩轨道：绕目标高速转圈，轨道半径贴着目标身板
            float orbitRot = (PhaseTimer * 0.24f * spinDir) + (Projectile.whoAmI * 1.3f);
            float radius = (MathF.Max(target.width, target.height) * 0.5f) + 34f;
            Vector2 desired = target.Center + (orbitRot.ToRotationVector2() * radius);
            Projectile.velocity = (desired - Projectile.Center) * 0.35f;
        }

        /// <summary>缠斩期转速拉满，读作狂性发作</summary>
        protected override float SpinTarget(int phase)
            => phase == PhaseHover ? 1.2f : base.SpinTarget(phase);

        protected override void FlightFX(Player owner) {
            //怨绿鬼火拖尾：寿命长过斧体的余焰
            int interval = Phase == PhaseHover ? 2 : 4;
            if (PhaseTimer % interval == 0) {
                PRTLoader.NewParticle<PRT_SoulFire>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    (-Projectile.velocity * 0.05f) - new Vector2(0f, Main.rand.NextFloat(0.4f, 1f)),
                    TrailColor, Main.rand.NextFloat(0.45f, 0.75f));
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, SoulGreen, 0.26f)?.Configure(10, 0.85f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_SoulFire>(target.Center,
                    Main.rand.NextVector2Circular(2.5f, 2.5f) - new Vector2(0f, 1.2f),
                    SoulGreen, Main.rand.NextFloat(0.4f, 0.65f));
            }
        }

        protected override void PostDrawLayers(SpriteBatch sb, Vector2 drawPos, Color lightColor) {
            //怨魂印读数：斧顶弧排至多五枚魂点，第五枚将满时整排提亮
            int marks = MarksOnLatch;
            if (marks <= 0) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float bright = marks >= 4 ? 0.85f : 0.55f;
            for (int i = 0; i < marks; i++) {
                float rad = MathHelper.Pi + (MathHelper.Pi / 6f * (i - 2f));
                Vector2 off = rad.ToRotationVector2() * -30f;
                float tw = 0.8f + (0.2f * MathF.Sin((Main.GlobalTimeWrappedHourly * 7f) + i + Projectile.whoAmI));
                Color c = SoulGreen * (bright * tw);
                c.A = 0;
                sb.Draw(glow, drawPos + off, null, c, 0f, glow.Size() / 2f, 0.16f, SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>怨魂爆裂：短判定 + 魂环鬼火演出</summary>
    internal class GsPossessedHatchetSoulBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 76;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] != 0f || VaultUtils.isServer) {
                return;
            }
            Projectile.localAI[0] = 1f;
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.85f, Pitch = -0.55f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                GsPossessedHatchetProj.SoulGreen, 1f)?.Configure(0.25f, 1.5f, 18);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsPossessedHatchetProj.SoulGreen, 0.6f)?.Configure(14, 1f);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = (MathHelper.TwoPi / 10f * i).ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f);
                PRTLoader.NewParticle<PRT_SoulFire>(Projectile.Center, vel,
                    GsPossessedHatchetProj.SoulGreen, Main.rand.NextFloat(0.5f, 0.85f));
            }
        }
    }
}
