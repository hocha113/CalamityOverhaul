using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·护卫套 T1】「誓约壁垒」：鎏金骑枪（有配重、会加速的铸金重器）。
    /// ①受击瞬间竖起誓约之盾，盾徽绕身巡回五秒；②壁垒期间近战命中会从肩后
    /// 掷出一柄鎏金誓约骑枪，骑枪越飞越快；③命中金属迸溅、清脆枪鸣，金屑坠地。<br/>
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

        //鎏金骑枪色板（比金套更偏古铜的骑士金）
        internal static readonly Color OathBright = new(255, 240, 190);
        internal static readonly Color OathMain = new(232, 176, 72);
        internal static readonly Color OathDeep = new(108, 68, 26);

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
                for (int i = 0; i < 8; i++) {
                    float ang = MathHelper.TwoPi * i / 8f;
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + ang.ToRotationVector2() * 24f,
                        ang.ToRotationVector2() * 1.2f, OathMain, 0.42f)?.Configure(false, 16);
                }
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
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //壁垒驻场：两点盾徽金辉绕身巡回（确定性角度，不掷 rand）
            Lighting.AddLight(player.Center, OathMain.ToVector3() * 0.2f);
            if (Main.GameUpdateCount % 3 == 0) {
                float baseAng = Main.GameUpdateCount * 0.045f;
                for (int i = 0; i < 2; i++) {
                    Vector2 at = player.Center + (baseAng + MathHelper.Pi * i).ToRotationVector2() * 30f;
                    PRTLoader.NewParticle<PRT_Light>(at, Vector2.Zero, OathBright, 0.08f)?.Configure(6, 0.75f);
                }
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
    /// 鎏金誓约骑枪：一根有配重的铸金长枪，出手后持续加速（骑士冲锋的劲头），
    /// 枪体沿速度方向拉长、枪尖亮芯前置；命中金属迸溅 + 枪鸣，亡处金屑受重力坠落
    /// </summary>
    internal class GsSquireOathLanceProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>穿透数由方案档位传入</summary>
        private ref float PierceSet => ref Projectile.ai[1];

        private float Seed => Projectile.identity * 0.8117f % 2.97f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

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

            //飞行相：枪尾拖金屑彗尾
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.6f,
                    Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    Main.rand.NextBool(3) ? GsSquireArmor.OathDeep : GsSquireArmor.OathMain,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(Projectile.Center, GsSquireArmor.OathMain.ToVector3() * (0.3f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //命中反馈：清脆枪鸣 + 金属迸溅
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool() ? GsSquireArmor.OathBright : GsSquireArmor.OathMain,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余痕：金屑受重力坠落，比枪体活得久
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsSquireArmor.OathBright, 0.12f)?.Configure(8, 0.7f);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f),
                    Main.rand.NextBool() ? GsSquireArmor.OathMain : GsSquireArmor.OathDeep,
                    Main.rand.NextFloat(0.26f, 0.45f))?.Configure(true, Main.rand.Next(18, 30));
            }
        }

        //==================== 绘制：三层鎏金枪体 + 速度拉伸 + 枪尖亮芯前置 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float speed = Projectile.velocity.Length();
            //越冲越长：枪体沿速度方向大幅拉伸
            float stretch = MathHelper.Clamp(speed * 0.045f, 0.2f, 1.15f);
            float wob = MathF.Sin(Life * 0.5f + Seed * 4f) * 0.05f;

            //古铜压边
            Main.EntitySpriteDraw(tex, pos, null, GsSquireArmor.OathDeep * (0.85f * fade), rotation, origin,
                new Vector2(0.20f + wob, 0.30f + stretch), SpriteEffects.None, 0);
            //鎏金主体
            Main.EntitySpriteDraw(tex, pos, null, GsSquireArmor.OathMain * fade, rotation, origin,
                new Vector2(0.15f + wob, 0.24f + stretch * 0.85f), SpriteEffects.None, 0);
            //白金亮芯：加色，前置到枪尖
            Color core = GsSquireArmor.OathBright with { A = 0 };
            Vector2 tipPos = pos + Projectile.velocity.SafeNormalize(Vector2.UnitX) * (10f + stretch * 26f);
            Main.EntitySpriteDraw(tex, tipPos, null, core * (0.7f * fade), rotation, origin,
                new Vector2(0.08f, 0.14f + stretch * 0.3f), SpriteEffects.None, 0);
            return false;
        }
    }
}
