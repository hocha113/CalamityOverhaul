using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·护卫套 T1】「誓约壁垒」：鎏金骑枪（有配重、会加速的铸金重器）。
    /// ①受击瞬间竖起誓约之盾五秒；②壁垒期间近战命中会从肩后
    /// 掷出一柄鎏金誓约骑枪，骑枪越飞越快；③命中清脆枪鸣。<br/>
    /// 与原版套装技联动：原版受击令弩车狂乱（Ballista Panic），神赋共享同一受击时刻，
    /// 弩车狂乱照常发动，你与弩车一起反击；窗口与冷却均为佩戴者端本地量
    /// </summary>
    internal class GsSquireArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsC";

        public override int[] HeadIDs => [ItemID.SquireGreatHelm];

        public override int BodyID => ItemID.SquirePlating;

        public override int LegsID => ItemID.SquireGreaves;

        protected override string EndowLineFallback =>
            "Oath Bulwark: taking a hit raises the oath shield for five seconds; melee strikes during it hurl a gilded oath lance from your shoulder";

        /// <summary>壁垒窗口帧数</summary>
        protected virtual int WindowFrames => 300;

        /// <summary>每次命中掷出的骑枪数</summary>
        protected virtual int LanceCount => 1;

        /// <summary>骑枪穿透数</summary>
        protected virtual int LancePierce => 2;

        /// <summary>掷枪间隔（帧）</summary>
        private const int LanceCooldown = 20;

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //与弩车狂乱同刻竖盾：受击即开窗（刷新式）
            state.EndowFlag = true;
            state.EndowTimer = Main.GameUpdateCount;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.8f, Pitch = -0.2f }, player.Center);
            }
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //掷枪冷却回落
            if (state.EndowCharge > 0) {
                state.EndowCharge--;
            }
            if (!state.EndowFlag) {
                return;
            }
            //窗口计时：到点收盾
            if (Main.GameUpdateCount - state.EndowTimer > (uint)WindowFrames) {
                state.EndowFlag = false;
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //骑枪自身命中不再触发，防自循环；假人不算数
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsSquireOathLanceProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy || !state.EndowFlag || state.EndowCharge > 0) {
                return;
            }
            if (!hit.DamageType.CountsAsClass(DamageClass.Melee)) {
                return;
            }

            state.EndowCharge = LanceCooldown;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_BallistaTowerShot with { Volume = 0.7f, Pitch = 0.25f }, player.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                //骑枪伤害按触发伤害折算并封顶；受击开窗 + 掷枪冷却双重闸，收益在神赋包络内
                int lanceDamage = Math.Clamp((int)(damageDone * 0.40f), 10, 320);
                for (int i = 0; i < LanceCount; i++) {
                    //自肩后上方出枪，双枪时上下错列
                    Vector2 spawn = player.Center + new Vector2(-player.direction * 26f, -34f - i * 18f);
                    Vector2 vel = (target.Center - spawn).SafeNormalize(Vector2.UnitX) * 15f;
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithSquireEndow"),
                        spawn, vel, ModContent.ProjectileType<GsSquireOathLanceProj>(),
                        lanceDamage, 3f, player.whoAmI, 0f, LancePierce);
                }
            }
        }
    }

    /// <summary>
    /// 【神赋·护卫套 T3 瓦尔哈拉骑士装】「誓约壁垒·瓦尔哈拉」：同一份誓约的重甲段。
    /// 壁垒六秒，每次近战命中掷出双枪，骑枪穿透更深
    /// </summary>
    internal class GsSquireValhallaArmor : GsSquireArmor
    {
        public override int[] HeadIDs => [ItemID.SquireAltHead];

        public override int BodyID => ItemID.SquireAltShirt;

        public override int LegsID => ItemID.SquireAltPants;

        protected override string EndowLineFallback =>
            "Oath Bulwark, Valhalla: the shield holds six seconds and every strike hurls twin oath lances";

        protected override int WindowFrames => 360;

        protected override int LanceCount => 2;

        protected override int LancePierce => 3;
    }

    /// <summary>
    /// 鎏金誓约骑枪：一根有配重的铸金长枪，出手后持续加速（骑士冲锋的劲头）；命中枪鸣
    /// </summary>
    internal class GsSquireOathLanceProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2BallistraProj;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>穿透数由方案档位传入</summary>
        private ref float PierceSet => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Life == 0f && PierceSet >= 1f) {
                Projectile.penetrate = (int)PierceSet;//档位穿透随 ai 过线，各端一致
            }
            Life++;
            //骑士冲锋：持续加速到冲刺极速
            if (Projectile.velocity.Length() < 26f) {
                Projectile.velocity *= 1.045f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //命中反馈：清脆枪鸣
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
        }
    }
}
