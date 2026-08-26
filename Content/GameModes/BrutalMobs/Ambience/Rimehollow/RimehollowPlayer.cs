using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rimehollow
{
    /// <summary>
    /// 冰雪洞穴逐玩家状态：「冽息」口部白气、「寒雾洼」寒意累积（仅本机玩家结算）、
    /// 「冰壁回声」挥击回声投递。寒意是逐玩家数据，禁止 static
    /// </summary>
    internal class RimehollowPlayer : ModPlayer
    {
        //==== 冽息（呼气节拍） ====
        private int breathIn = 60;

        //==== 冰壁回声（挥击节流） ====
        private int echoCooldown;
        private const int EchoCooldownFrames = 20;
        /// <summary>回声队列积压超过此值时不再投递（防噪）</summary>
        private const int EchoBacklogLimit = 10;

        //==== 寒雾洼（寒意，仅 whoAmI==myPlayer 的实例有意义） ====
        /// <summary>寒意 0~1，满则短暂寒颤</summary>
        internal float MistExposure;
        /// <summary>视野白雾边缘强度（平滑后的演出量，渲染层读取）</summary>
        internal float WhiteEdge;
        private bool mistTouched;
        private int mistFillTicks = 300;
        /// <summary>寒意灌满帧数，档位只调累积速度（1 残酷/2 修罗/3 毁灭）</summary>
        private static readonly int[] FillTicksByTier = [300, 235, 180];
        /// <summary>离雾衰减：约 1.5 秒退干净，快速通过无事</summary>
        private const float DecayPerTick = 1f / 90f;
        private const int ChillFrames = 120;

        /// <summary>寒雾洼实体逐帧上报：本机玩家正站在雾带里</summary>
        internal void MistTouch(int tier) {
            mistTouched = true;
            mistFillTicks = FillTicksByTier[Math.Clamp(tier, 1, 3) - 1];
        }

        public override void PostUpdate() {
            if (Main.dedServ) {
                return;
            }
            bool inZone = RimehollowAmbience.In(Player);

            UpdateBreath(inZone);
            UpdateSwingEcho(inZone);

            //寒意只在本机玩家实例上结算（AddBuff 本机施加原生同步）
            if (Player.whoAmI == Main.myPlayer) {
                UpdateMistExposure();
            }
        }

        /// <summary>「冽息」：口部周期呼出白气团，移动时呼吸更急促；水下无呼气</summary>
        private void UpdateBreath(bool inZone) {
            if (!inZone || Player.dead || Player.wet || Main.gamePaused) {
                return;
            }
            if (--breathIn > 0) {
                return;
            }
            breathIn = Player.velocity.Length() > 4f ? Main.rand.Next(95, 125) : Main.rand.Next(135, 175);

            //口部锚点：头部前侧
            Vector2 mouth = Player.MountedCenter + new Vector2(Player.direction * 7f, -9f + Player.gfxOffY);
            Vector2 drift = Player.velocity * 0.25f
                + new Vector2(Player.direction * 0.5f, -0.14f);
            for (int i = 0; i < 4; i++) {
                Dust puff = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(2f, 2f),
                    DustID.Cloud, drift + Main.rand.NextVector2Circular(0.22f, 0.14f),
                    185, default, Main.rand.NextFloat(0.55f, 0.85f));
                puff.noGravity = true;
            }
            //白气里偶发一粒冷晶微光，把"冷"写进呼吸
            if (Main.rand.NextBool(3)) {
                Dust sparkle = Dust.NewDustPerfect(mouth, DustID.Frost,
                    drift * 0.6f, 140, default, 0.7f);
                sparkle.noGravity = true;
            }
        }

        /// <summary>「冰壁回声」：镐斧锤与挥舞类武器起手后，冰壁回敬一声延迟的清脆反响</summary>
        private void UpdateSwingEcho(bool inZone) {
            if (echoCooldown > 0) {
                echoCooldown--;
            }
            if (!inZone || Player.dead || echoCooldown > 0) {
                return;
            }
            //起手帧检测（远端玩家的挥击在各端同样被模拟，附近的同伴也有回声）
            if (Player.itemAnimationMax <= 1 || Player.itemAnimation != Player.itemAnimationMax - 1) {
                return;
            }
            Item held = Player.HeldItem;
            bool swingTool = held.pick > 0 || held.axe > 0 || held.hammer > 0;
            bool swingWeapon = held.damage > 0 && held.useStyle == ItemUseStyleID.Swing;
            if (!swingTool && !swingWeapon) {
                return;
            }
            if (Player.Distance(Main.LocalPlayer.Center) > 950f) {
                return;
            }
            if (RimehollowAmbience.PendingEchoes() >= EchoBacklogLimit) {
                return;
            }
            echoCooldown = EchoCooldownFrames;

            //回声从侧向的冰壁方向传回：两声，一近一远一轻
            Vector2 wall = Player.Center + new Vector2(
                Main.rand.NextFloat(170f, 300f) * (Main.rand.NextBool() ? 1f : -1f),
                Main.rand.NextFloat(-90f, 60f));
            RimehollowAmbience.EnqueueEcho(RimehollowAmbience.EchoTink, 12, wall, 0.15f, -0.18f);
            RimehollowAmbience.EnqueueEcho(RimehollowAmbience.EchoTink, 26,
                wall + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f), 0.07f, -0.4f);
        }

        /// <summary>「寒雾洼」寒意结算：站雾累积、离雾快退，满则短暂原版寒颤后回落</summary>
        private void UpdateMistExposure() {
            //Boss 在场/城镇安宁：减益机制暂停，只余视觉雾
            bool accumulating = mistTouched && !Player.dead
                && !CWRWorld.HasBoss && !RimehollowAmbience.TownCalmNear(Player.Center);

            if (accumulating) {
                MistExposure += 1f / mistFillTicks;
            }
            else {
                MistExposure -= DecayPerTick;
            }
            MistExposure = MathHelper.Clamp(MistExposure, 0f, 1f);

            if (MistExposure >= 1f) {
                Player.AddBuff(BuffID.Chilled, ChillFrames);
                MistExposure = 0.35f;
                SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with {
                    Volume = 0.4f, Pitch = 0.45f, MaxInstances = 2
                }, Player.Center);
                for (int i = 0; i < 8; i++) {
                    Dust frost = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(14f, 20f),
                        DustID.Frost, Main.rand.NextVector2Circular(1.4f, 1f) - new Vector2(0f, 0.6f),
                        110, default, Main.rand.NextFloat(0.9f, 1.4f));
                    frost.noGravity = true;
                }
            }

            //白雾边缘：入雾立即有薄边，寒意越高越浓
            float target = mistTouched ? MathF.Max(0.30f, MistExposure) : MistExposure * 0.8f;
            WhiteEdge = MathHelper.Lerp(WhiteEdge, target, 0.08f);
            mistTouched = false;
        }

        public override void UpdateDead() {
            MistExposure = 0f;
            WhiteEdge *= 0.92f;
            mistTouched = false;
        }
    }
}
