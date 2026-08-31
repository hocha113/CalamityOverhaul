using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 银短剑重铸「月银」。<br/>
    /// 材质：吸饱月光的镜面银刃。签名行为：①夜间伤害 ×1.25 且刺速 +10%，命中迸月银辉尘
    /// ②血月之夜命中吸提生机，每秒至多回 1 点生命 ③夜间刃身月晕常亮，白天回落为素银
    /// </summary>
    internal class GsSilverShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.SilverShortsword;

        protected override string GsDescFallback =>
            "Reforged: mirror-polished silver that drinks moonlight, striking 25% harder and 10% faster at night;" +
            "\nunder a blood moon each hit steals back a sliver of life";

        protected override int HeldProjType => ModContent.ProjectileType<GsSilverShortswordHeld>();

        /// <summary>血月吸提的上次结算刻（myPlayer 路径消费，限频每秒 1 点）</summary>
        private uint lastHealTick;

        /// <summary>血月吸提（owner 端调用，自限频 60 刻）</summary>
        internal void TryBloodMoonHeal(Player player) {
            if (!Main.bloodMoon || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.statLife >= player.statLifeMax2 || Main.GameUpdateCount - lastHealTick < 60) {
                return;
            }
            lastHealTick = Main.GameUpdateCount;
            player.Heal(1);
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= !Main.dayTime ? 1.40f : 1.12f;//底伤 1.12，夜间签名乘区 ×1.25 = 1.40（约半程 uptime）
    }

    /// <summary>
    /// 银短剑手持突刺：轻捷镜面手感。夜间刺速 +10%、月银辉尘命中；
    /// 血月命中经方案限频回血
    /// </summary>
    internal class GsSilverShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.SilverShortsword;

        //月银色板
        internal static readonly Color SilverBright = new(238, 244, 254);
        internal static readonly Color SilverMain = new(192, 202, 218);
        internal static readonly Color MoonBlue = new(162, 192, 255);
        internal static readonly Color BloodMoonRed = new(255, 96, 96);

        protected override float WindupFrames => 2f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 2f;
        protected override float RecoverFrames => 6f;
        protected override float PullbackDist => 9f;
        protected override float StabReach => 33f;
        protected override float BladeLength => 43f;
        protected override float ThrustEasePower => 2.7f;
        protected override int HitstopFrames => 1;
        protected override float LeanAmp => 0.028f;
        protected override float ThrustPitch => 0.18f;

        protected override Color EdgeColor => SilverBright;
        protected override Color CoreColor => IsNight ? (Main.bloodMoon ? BloodMoonRed : MoonBlue) : SilverMain;

        private static bool IsNight => !Main.dayTime;

        protected override void OnInit() {
            //月照加速：夜间刺速 +10%（伤害乘区在方案 GsModifyWeaponDamage 动态结算）
            if (IsNight) {
                speedMul *= 1.10f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //血月吸提：限频结算在方案侧（owner 端命中 + myPlayer 双守门）
            if (Owner.whoAmI == Main.myPlayer
                && GodSmithScheme.TryGetScheme(ItemID.SilverShortsword, out GodSmithScheme s)
                && s is GsSilverShortsword silver) {
                silver.TryBloodMoonHeal(Owner);
            }
        }

        /// <summary>命中反馈：白天素银火花；夜间迸月银辉尘（上飘冷光），血月染红</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            Color accent = Main.bloodMoon ? BloodMoonRed : MoonBlue;
            int sparks = IsNight ? 6 : 4;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.55) * Main.rand.NextFloat(3f, 7.5f);
                Color c = Main.rand.NextBool(3) && IsNight ? accent : SilverBright;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.32f, 0.55f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            if (IsNight) {
                //月银辉尘：无重力冷光缓缓上飘
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.8f, 1.8f));
                    PRTLoader.NewParticle<PRT_Light>(pos + Main.rand.NextVector2Circular(8f, 8f), vel,
                        Main.rand.NextBool() ? accent : SilverBright, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(14, 22), 0.55f, 1.2f);
                }
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 3f), 100, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>夜间月晕常亮，血月更盛</summary>
        protected override float ExtraGlowStrength() => IsNight ? (Main.bloodMoon ? 0.26f : 0.18f) : 0f;

        /// <summary>夜间刺尖月晕（定值脉动，无随机；日夜状态各端一致）</summary>
        protected override void DrawOverBlade(SpriteBatch sb) {
            if (!IsNight || FanFade <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.whoAmI);
            Color accent = Main.bloodMoon ? BloodMoonRed : MoonBlue;
            Vector2 at = TipPos - Main.screenPosition;
            sb.Draw(glow, at, null, accent with { A = 0 } * (0.35f * FanFade * pulse), 0f,
                glow.Size() / 2f, 0.20f * pulse, SpriteEffects.None, 0f);
            sb.Draw(glow, at, null, SilverBright with { A = 0 } * (0.30f * FanFade * pulse), 0f,
                glow.Size() / 2f, 0.10f * pulse, SpriteEffects.None, 0f);
        }
    }
}
