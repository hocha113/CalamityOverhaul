using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 附身飞斧重铸（★A 档）。材质：怨魂缠绕的陨铁斧。签名行为：①手持蓄力掷
    /// ②去程加速追猎，命中后缠上目标绕身连斩，每斩叠一层怨魂印 ③第五层怨魂爆裂，造成 150% 爆发
    /// </summary>
    internal class GsPossessedHatchet : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.PossessedHatchet;

        internal override int BoomerProjType => ModContent.ProjectileType<GsPossessedHatchetProj>();

        internal override int MaxAirborne => int.MaxValue;   //原版无同场上限，掷速由手持接管节流

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "A wind-up throw: the possessed axe accelerates after its prey, then latches on,\nwhirling around the victim and branding a soul mark with every slash";
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

    /// <summary>附身飞斧蓄力掷手持：快节奏起手</summary>
    internal class GsPossessedHatchetHeld : GsBoomerThrowHeldBase
    {
        protected override int SourceItemID => ItemID.PossessedHatchet;

        protected override int BoomerangType => ModContent.ProjectileType<GsPossessedHatchetProj>();

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
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.4f }, Owner.Center);
        }
    }

    /// <summary>怨魂斧体：追猎、缠斩、五印爆裂</summary>
    internal class GsPossessedHatchetProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.PossessedHatchet;

        protected override int OutTime => 60;
        protected override float OutDrag => 1f;          //去程走追猎加速，不用整体衰减
        protected override int HoverTime => 42;          //缠斩窗口
        protected override int HitCooldown => 12;
        protected override bool HoverOnFirstHit => false;
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

        /// <summary>第五印爆裂：150% 爆发判定</summary>
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
    }

    /// <summary>怨魂爆裂：存活 3 帧的无形判定箱，只留爆裂音，不画本体</summary>
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
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
