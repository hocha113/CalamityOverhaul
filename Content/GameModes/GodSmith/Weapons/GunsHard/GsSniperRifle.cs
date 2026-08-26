using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 狙击步枪重铸：瞄准线轴招牌件。<br/>
    /// [狙击]：常亮贯通瞄准线；持械站稳 1.2 秒蓄满稳息表，该发必暴、+30% 伤、贯穿 +1，
    /// 出膛带音爆白线冲击帧；开火或移动即清表。长按右键保留原版狙击镜拉视野。<br/>
    /// [速射]：射速提到 22ut、伤害 -40%、无瞄准线的应急压制形态。<br/>
    /// 右键点按（15 tick 内松开）切换模式，长按走狙击镜，两者互不误触
    /// </summary>
    internal class GsSniperRifle : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.SniperRifle;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: tap right click to switch fire mode, hold to scope\n" +
            "Snipe mode paints a piercing aim line; stand still to steady your breath, a steadied shot always crits\n" +
            "Rapid mode trades power for speed";

        /// <summary>稳息蓄满所需 tick（1.2 秒）</summary>
        private const int SteadyFillTicks = 72;

        /// <summary>满息弹私有 flag</summary>
        private const int FlagSteady = 1;

        /// <summary>稳息未满色（暗红）</summary>
        private static readonly Color CalmRed = new(150, 52, 40);

        /// <summary>本次射击为满息弹的世界帧（打标窗口消费）；只在 owner 射击链读写</summary>
        private uint steadyShotTick = uint.MaxValue;

        /// <summary>右键已按住的 tick 数；只在 myPlayer 路径读写</summary>
        private int rightHoldTicks;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeSnipe", EnName = "Snipe",
                AimLine = GsAimLineKind.Line,
            },
            new GsFireMode {
                Key = "ModeRapid", EnName = "Rapid Fire",
                UseSpeed = 36f / 22f, DamageMul = 0.60f,
            },
        ];

        //==================== 右键：点按切换 / 长按狙击镜 ====================

        /// <summary>压掉基类的按下即切，改由松开时长判定</summary>
        protected override void OnRightPress(Item item, Player player) { }

        protected override void GsGunHoldLocal(Item item, Player player, GsGunsHardPlayer mp) {
            //右键点按/长按分离：短按松开才切模式，长按交给狙击镜
            if (player.controlUseTile) {
                rightHoldTicks++;
            }
            else {
                if (rightHoldTicks > 0 && rightHoldTicks <= 15) {
                    CycleMode(item, player);
                }
                rightHoldTicks = 0;
            }
            if (mp.ModeIndex == 0) {
                //狙击档：原版狙击镜姿态常置（scope 每帧由 ResetEffects 清，长按右键即原版拉视野）
                player.scope = true;
                TickSteady(player, mp, SteadyFillTicks);
                //满息就绪的边沿提示音（个人读数）
                if (mp.SteadyMeter >= 1f && !steadyReadyCued) {
                    steadyReadyCued = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = 0.65f }, player.Center);
                    }
                }
                if (mp.SteadyMeter < 1f) {
                    steadyReadyCued = false;
                }
            }
            else {
                mp.SteadyMeter = 0f;
            }
        }

        /// <summary>满息就绪提示已响过（防每帧重响）；本地玩家态</summary>
        private bool steadyReadyCued;

        internal override void GsGunHeldReset(Player player) {
            rightHoldTicks = 0;
            steadyReadyCued = false;
            steadyShotTick = uint.MaxValue;
        }

        //==================== 射击：满息弹判定与打标 ====================

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (mp.ModeIndex == 0 && mp.SteadyMeter >= 1f) {
                steadyShotTick = Main.GameUpdateCount;
                damage = (int)(damage * 1.30f);
            }
        }

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            bool steadied = steadyShotTick == Main.GameUpdateCount;
            //开火清表：满不满都清（稳息是逐发资源）
            mp.SteadyMeter = 0f;
            steadyReadyCued = false;
            if (steadied && !VaultUtils.isServer) {
                SonicBoomVisual(player, position, velocity);
            }
            return null;
        }

        /// <summary>音爆白线冲击帧：owner 本地演出（沿弹道铺白炽火星 + 枪口脉冲环）</summary>
        private static void SonicBoomVisual(Player player, Vector2 muzzle, Vector2 velocity) {
            SoundEngine.PlaySound(SoundID.Item40 with { Volume = 0.9f, Pitch = 0.5f }, muzzle);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f, Pitch = 0.3f }, muzzle);
            Vector2 unit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            float length = GsAimLineDraw.ScanLength(muzzle, unit, 1200f);
            int sparkCount = Math.Min(9, (int)(length / 90f) + 2);
            for (int i = 0; i < sparkCount; i++) {
                float along = length * (i + 1) / (sparkCount + 1);
                PRTLoader.NewParticle<PRT_Spark>(muzzle + unit * along,
                    unit * Main.rand.NextFloat(1.5f, 3f) + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Color.White, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(6, 11));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(muzzle + unit * 14f, Vector2.Zero,
                GameModeTheme.GodSmithEmber, 0f)?.Configure(0.05f, 0.5f, 12);
            PRTLoader.NewParticle<PRT_Light>(muzzle + unit * 10f, Vector2.Zero,
                Color.White, 0.14f)?.Configure(8, 0.8f);
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            bool steadied = steadyShotTick == Main.GameUpdateCount;
            router.MarkData = PackMark(mp.ModeIndex, steadied ? FlagSteady : 0);
            //贯穿 +1：命中在 owner 端裁决，出生窗口改足够；>0 守卫防 -1 无限穿写坏
            if (steadied && proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //满息弹必暴：绕开 crit 计算时序，直接锁定本次命中
            if (MarkFlagOf(router.MarkData) == FlagSteady) {
                modifiers.SetCrit();
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //满息弹飞行相：白金曳光（各端可见，频率克制）
            if (MarkFlagOf(router.MarkData) != FlagSteady || VaultUtils.isServer) {
                return;
            }
            if (proj.timeLeft % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.05f, GameModeTheme.GodSmithEmber,
                    Main.rand.NextFloat(0.26f, 0.4f))?.Configure(false, Main.rand.Next(8, 13));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //满息弹命中：白闪定音（owner 个人反馈）
            if (MarkFlagOf(router.MarkData) != FlagSteady || VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, Color.White, 0.18f)?.Configure(9, 0.85f);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    (-proj.velocity).SafeNormalize(Vector2.UnitX).RotatedByRandom(0.8) * Main.rand.NextFloat(3f, 7f),
                    GameModeTheme.GodSmithEmber, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 瞄准线：稳息渐变 ====================

        public override Color AimLineColor(Item item, Player player, GsGunsHardPlayer mp) {
            //暗红到金亮的稳息读数；满息后微过曝
            Color c = Color.Lerp(CalmRed, GameModeTheme.GodSmithEmber, mp.SteadyMeter);
            if (mp.SteadyMeter >= 1f) {
                float flare = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f);
                c = Color.Lerp(c, Color.White, 0.35f * flare);
            }
            return c;
        }
    }
}
