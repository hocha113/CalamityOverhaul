using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 【阿卡莱斯】材质：秘银雨刃。
    /// 签名「甲缝识破」：①密集小角银雨乱舞，自带 20 点破甲（原版弹幕 595 身份保留）
    /// ②持续压同一目标攒识破，每层再破 2 甲
    /// ③攒满 8 层自动收束成处决突刺：刃影归拢、线判贯穿、单独一击 2.2 倍
    /// </summary>
    internal class GsArkhalis : GodSmithScheme
    {
        public override int TargetItemID => ItemID.Arkhalis;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: the flurry pierces 20 armor;\nsustained hits on one target build Insight, each stack adding 2 more armor penetration,\nand at 8 stacks the flurry snaps into a piercing execution thrust";
        public override bool? GsCanUseItem(Item item, Player player) {
            //手持乱舞在场即禁再触发（channel 驻场，held 自续，松手即收）
            if (HeldAlive<GsArkhalisHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsArkhalisHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            //全端返回 false 压掉原版乱舞；远端靠弹幕同步看到动作
            return false;
        }

        //底伤不加成（×1.0）：rare9 本体已强，原版 20 破甲全额保留 + 识破至多 +16 破甲
        //+ 处决突刺 2.2 倍一击的收益已计入包络，综合 DPS 约为原版 100%~115%
    }

    /// <summary>
    /// 阿卡莱斯手持乱舞：密集小角银雨（闪现间隔 3）。
    /// 识破记账 owner 权威，insight/insightTarget/相位经 NetHeldSend 随包过线；
    /// 攒满 8 层自动进处决突刺相（held 内状态机 flurry↔thrust）：
    /// 收束 4f（刃影向瞄准线归拢）→ 突刺 6f（线判 150×30、复击表清一次、单独一击 ×2.2、
    /// owner 前刺守坐骑）→ 收势 8f，突刺后识破清零回乱舞
    /// </summary>
    internal class GsArkhalisHeld : GsOdditiesFlurryHeldBase
    {
        protected override int SwordItemID => ItemID.Arkhalis;

        protected override int FlashInterval => 3;
        protected override float SpreadArc => 0.5f;
        protected override float SwingPitch => 0.12f;

        /// <summary>识破上限</summary>
        private const int InsightMax = 8;
        private const int ConvergeDur = 4;
        private const int StrikeDur = 6;
        private const int RecoverDur = 8;
        private const int ThrustTotal = ConvergeDur + StrikeDur + RecoverDur;
        /// <summary>突刺线判：玩家中心 → 瞄准向 × 150，宽 30</summary>
        private const float StrikeReach = 150f;
        private const float StrikeWidth = 30f;

        /// <summary>识破层数；owner 权威记账，NetHeldSend 过线</summary>
        private int insight;
        /// <summary>被识破目标的 whoAmI，换目标清零；-1 = 无</summary>
        private int insightTarget = -1;
        /// <summary>处决突刺相位开关（false=乱舞）</summary>
        private bool thrusting;
        private int thrustTimer;
        /// <summary>突刺命中顿帧：冻结相位计时（owner 端生效，远端演出差 2 帧级）</summary>
        private int thrustHitstop;
        private bool strikeStarted;
        private bool strikeEnded;
        private int preThrustDamage;

        private bool InStrike => strikeStarted && !strikeEnded;

        protected override bool FlurrySuspended => thrusting;

        /// <summary>收束演出进度：收束段 0→1，突刺段钉满，收势段回落</summary>
        protected override float PoseConvergence {
            get {
                if (!thrusting) {
                    return 0f;
                }
                if (thrustTimer < ConvergeDur) {
                    return thrustTimer / (float)ConvergeDur;
                }
                int recoverStart = ConvergeDur + StrikeDur;
                if (thrustTimer <= recoverStart) {
                    return 1f;
                }
                return 1f - MathHelper.Clamp((thrustTimer - recoverStart) / (float)RecoverDur, 0f, 1f);
            }
        }

        /// <summary>突刺/收势段本体刃沿瞄准线前送（34→120px 缓出），收束段与乱舞相用基类触及</summary>
        protected override float BladeReach {
            get {
                if (!thrusting || thrustTimer < ConvergeDur) {
                    return base.BladeReach;
                }
                float strikeT = MathHelper.Clamp((thrustTimer - ConvergeDur) / (float)StrikeDur, 0f, 1f);
                return MathHelper.Lerp(34f, 120f, 1f - MathF.Pow(1f - strikeT, 3f));
            }
        }

        //==================== 突刺状态机 ====================

        protected override void FlurryAI() {
            if (!thrusting) {
                return;
            }
            if (thrustHitstop > 0) {
                thrustHitstop--;
            }
            else {
                thrustTimer++;
            }

            if (!strikeStarted && thrustTimer >= ConvergeDur) {
                strikeStarted = true;
                BeginStrike();
            }
            if (strikeStarted && !strikeEnded && thrustTimer >= ConvergeDur + StrikeDur) {
                strikeEnded = true;
                EndStrike();
            }
            if (thrustTimer >= ThrustTotal) {
                EndThrust();
            }
        }

        /// <summary>owner 侧攒满触发；相位随 netUpdate 过线，远端从头播突刺演出</summary>
        private void StartThrust() {
            thrusting = true;
            thrustTimer = 0;
            thrustHitstop = 0;
            strikeStarted = strikeEnded = false;
            Projectile.netUpdate = true;
        }

        /// <summary>突刺窗开：伤害临时 ×2.2、复击冷却盖过窗口（单击）、复击表清一次、owner 前刺</summary>
        private void BeginStrike() {
            preThrustDamage = Projectile.damage;
            Projectile.damage = (int)(preThrustDamage * 2.2f);
            Projectile.localNPCHitCooldown = 30;
            for (int i = 0; i < Projectile.localNPCImmunity.Length; i++) {
                Projectile.localNPCImmunity[i] = 0;
            }
            if (Projectile.owner == Main.myPlayer && !Owner.mount.Active) {
                Owner.velocity += AimUnit * 4f;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = 0.1f }, Owner.Center);
            }
        }

        /// <summary>突刺窗关：伤害与复击节奏还原</summary>
        private void EndStrike() {
            Projectile.damage = preThrustDamage;
            Projectile.localNPCHitCooldown = 5;
        }

        /// <summary>收势结束：识破清零回乱舞相</summary>
        private void EndThrust() {
            if (strikeStarted && !strikeEnded) {
                strikeEnded = true;
                EndStrike();
            }
            thrusting = false;
            thrustTimer = 0;
            strikeStarted = strikeEnded = false;
            insight = 0;
            insightTarget = -1;
            if (Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }

        //==================== 联机：识破与相位过线 ====================

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write((byte)insight);
            writer.Write((short)insightTarget);
            writer.Write(thrusting);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            insight = reader.ReadByte();
            insightTarget = reader.ReadInt16();
            bool remoteThrust = reader.ReadBoolean();
            if (remoteThrust == thrusting) {
                return;
            }
            if (remoteThrust) {
                //远端从头播突刺演出
                thrusting = true;
                thrustTimer = 0;
                thrustHitstop = 0;
                strikeStarted = strikeEnded = false;
            }
            else {
                if (InStrike) {
                    strikeEnded = true;
                    EndStrike();
                }
                thrusting = false;
                thrustTimer = 0;
                strikeStarted = strikeEnded = false;
            }
        }

        //==================== 判定与命中 ====================

        public override bool? CanDamage() => InStrike ? null : base.CanDamage();

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!thrusting) {
                return null; //乱舞相：默认大盒判定
            }
            if (!InStrike) {
                return false;
            }
            //突刺窗：玩家中心沿瞄准向的线判
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Owner.Center, Owner.Center + (AimUnit * StrikeReach), StrikeWidth, ref point);
        }

        public override void CutTiles() {
            if (InStrike) {
                DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
                Utils.PlotTileLine(Owner.Center, Owner.Center + (AimUnit * StrikeReach), 24f, DelegateMethods.CutTiles);
                return;
            }
            base.CutTiles();
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            base.ModifyHitNPC(target, ref modifiers);
            //原版身份保留：阿卡莱斯乱舞自带 20 点破甲（原版弹幕 595 ArmorPenetration=20）
            modifiers.ArmorPenetration += 20f;
            //识破读层：只对被识破的那个目标追加（换目标时本钩子先于记账跑，不守会错发旧层数）
            if (insight > 0 && target.whoAmI == insightTarget) {
                modifiers.ArmorPenetration += 2f * insight;
            }
        }

        protected override void OnFlurryHit(NPC target, NPC.HitInfo hit, int damageDone) {
            if (thrusting) {
                //突刺命中：顿帧 2
                thrustHitstop = Math.Max(thrustHitstop, 2);
                return;
            }

            //识破记账：同目标 +1 层，换目标清零重计（owner 权威）
            if (Projectile.owner == Main.myPlayer) {
                if (target.whoAmI != insightTarget) {
                    insightTarget = target.whoAmI;
                    insight = 1;
                }
                else if (insight < InsightMax) {
                    insight++;
                }
                Projectile.netUpdate = true;
                if (insight >= InsightMax) {
                    StartThrust();
                }
            }
        }
    }
}
