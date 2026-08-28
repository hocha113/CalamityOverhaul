using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stonewake.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stonewake
{
    /// <summary>
    /// 石醒双厅主控。残酷模式下花岗岩洞与大理石洞各得一层环境特色：<br/>
    /// 「电萤」花岗岩厅常态氛围：蓝白电弧在晶簇间低频跳跃+静电嘶鸣底噪+能量微粒沿地面流动；<br/>
    /// 「双厅回响」两厅环境音互异：电噪嘶鸣 vs 圣殿空旷低鸣+远处石像转头声，音量随在场包络淡入淡出；<br/>
    /// 「共振脉冲」与「凝视之柱」的权威调度也在这里跑（决策只在权威端，视觉与声音只在客户端）。<br/>
    /// 档位只调脉冲/光柱频率，机制形状不变
    /// </summary>
    internal class StonewakeAmbience : ModSystem
    {
        /// <summary>花岗岩厅在场包络 0~1（本地屏幕演出量）</summary>
        internal static float GraniteEnv { get; private set; }
        /// <summary>大理石厅在场包络 0~1（本地屏幕演出量）</summary>
        internal static float MarbleEnv { get; private set; }

        //==== 共振脉冲（花岗岩厅）调度参数 ====
        /// <summary>脉冲冷却，档位只调频率</summary>
        private static readonly int[] PulseCooldownByTier = [520, 430, 340];
        /// <summary>脉冲全局并发上限</summary>
        private const int PulseCap = 3;

        //==== 凝视之柱（大理石厅）调度参数 ====
        private static readonly int[] PillarCooldownByTier = [560, 470, 380];
        private const int PillarCap = 4;
        /// <summary>光柱伤害 = 缩放后大理石接触锚 × 此值（微量档）</summary>
        private const float PillarDamageFrac = 0.45f;

        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int TriggerRetryFrames = 30;

        //==== 双厅回响：环境声循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例） ====
        private static SlotId graniteHissSlot;
        private static SlotId marbleDroneSlot;
        /// <summary>花岗岩厅底噪：静电嘶鸣</summary>
        private static readonly SoundStyle GraniteHissStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>大理石厅底噪：圣殿空旷低鸣</summary>
        private static readonly SoundStyle MarbleDroneStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };

        //==== 氛围计时（屏幕级演出，纯本地） ====
        private static int arcTimer;
        private static int marbleMoteTimer;
        private static int grindTimer;
        private static int grindEchoTimer;
        private static Vector2 grindPos;

        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                UpdateClient();
            }
            if (VaultUtils.isServer || VaultUtils.isSinglePlayer) {
                UpdateAuthority();
            }
        }

        public override void ClearWorld() {
            GraniteEnv = 0f;
            MarbleEnv = 0f;
            arcTimer = 0;
            marbleMoteTimer = 0;
            grindTimer = 0;
            grindEchoTimer = 0;
        }

        //==================== 客户端：包络 / 回响 / 电萤 ====================

        private static void UpdateClient() {
            if (Main.gameMenu) {
                GraniteEnv = 0f;
                MarbleEnv = 0f;
                return;
            }
            bool on = GameModeSystem.BrutalActive;
            Player local = Main.LocalPlayer;
            float graniteTarget = on && local.active && local.ZoneGranite ? 1f : 0f;
            float marbleTarget = on && local.active && local.ZoneMarble ? 1f : 0f;
            //~1s 缓升缓降，离厅淡出不硬切
            GraniteEnv = Approach(GraniteEnv, graniteTarget);
            MarbleEnv = Approach(MarbleEnv, marbleTarget);

            UpdateAmbientLoops();
            if (Main.gamePaused) {
                return;
            }
            //Boss 在场：纯视觉氛围保留但减弱（掷硬币减半密度）
            bool dimmed = CWRWorld.HasBoss && Main.rand.NextBool();
            if (GraniteEnv > 0.35f && !dimmed) {
                UpdateGraniteFireflies();
            }
            if (MarbleEnv > 0.35f && !dimmed) {
                UpdateMarbleMotes();
            }
            UpdateMarbleGrind();
        }

        private static float Approach(float value, float target) {
            value = MathHelper.Lerp(value, target, 0.045f);
            if (target <= 0f && value < 0.003f) {
                value = 0f;
            }
            return value;
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateAmbientLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (GraniteEnv > 0.02f && !SoundEngine.TryGetActiveSound(graniteHissSlot, out _)) {
                graniteHissSlot = SoundEngine.PlaySound(GraniteHissStyle, null, UpdateGraniteHiss);
            }
            if (MarbleEnv > 0.02f && !SoundEngine.TryGetActiveSound(marbleDroneSlot, out _)) {
                marbleDroneSlot = SoundEngine.PlaySound(MarbleDroneStyle, null, UpdateMarbleDrone);
            }
        }

        //花岗岩厅：静电嘶鸣，音高微抬读作电噪
        private static bool UpdateGraniteHiss(ActiveSound sound) {
            if (Main.gameMenu || GraniteEnv <= 0.003f) {
                return false;
            }
            float dim = CWRWorld.HasBoss ? 0.55f : 1f;
            sound.Volume = 0.32f * GraniteEnv * dim;
            sound.Pitch = 0.14f;
            sound.Position = null;
            return true;
        }

        //大理石厅：低鸣压到谷底，读作圣殿空旷混响
        private static bool UpdateMarbleDrone(ActiveSound sound) {
            if (Main.gameMenu || MarbleEnv <= 0.003f) {
                return false;
            }
            float dim = CWRWorld.HasBoss ? 0.55f : 1f;
            sound.Volume = 0.30f * MarbleEnv * dim;
            sound.Pitch = -0.72f;
            sound.Position = null;
            return true;
        }

        /// <summary>电萤：低频晶簇间电弧跳跃 + 地面能量微粒（常态预算 ≤30/s 量级）</summary>
        private static void UpdateGraniteFireflies() {
            //地面微粒：约每 3 帧一粒，沿地表横向流动
            if (Main.rand.NextBool(3) && StonewakeFX.TryFindExposedTile(TileID.Granite, out Vector2 face)) {
                Dust mote = Dust.NewDustPerfect(face, DustID.Electric,
                    new Vector2(Main.rand.NextFloat(-1.3f, 1.3f), 0f), 130, default,
                    Main.rand.NextFloat(0.5f, 0.8f) * GraniteEnv);
                mote.noGravity = true;
            }

            //电弧跳跃：低频事件，一次 3~5 段弧粒
            if (--arcTimer > 0) {
                return;
            }
            arcTimer = 26 + Main.rand.Next(30);
            if (!StonewakeFX.TryFindExposedTile(TileID.Granite, out Vector2 from)) {
                return;
            }
            //第二锚点：再取一处裸露晶面，太远则收成短弧入空气
            Vector2 to;
            if (StonewakeFX.TryFindExposedTile(TileID.Granite, out Vector2 second)
                && second.Distance(from) is > 40f and < 230f) {
                to = second;
            }
            else {
                to = from + Main.rand.NextVector2Unit() * Main.rand.NextFloat(44f, 92f);
            }

            Vector2 dir = (to - from).SafeNormalize(Vector2.UnitX);
            float len = from.Distance(to);
            int segments = (int)MathHelper.Clamp(len / 34f, 3f, 5f);
            for (int i = 0; i < segments; i++) {
                float t = (i + 0.5f) / segments;
                Vector2 pos = Vector2.Lerp(from, to, t) + Main.rand.NextVector2Circular(5f, 5f);
                Color tint = Main.rand.NextBool() ? StonewakeFX.GraniteSpark : StonewakeFX.GraniteCore;
                PRTLoader.NewParticle<PRT_GraniteVolt>(pos, dir * Main.rand.NextFloat(1.2f, 2.2f),
                    tint, Main.rand.NextFloat(0.28f, 0.44f)).Configure(Main.rand.Next(3, 6));
            }
            //两端晶簇亮点
            PRTLoader.NewParticle<PRT_Light>(from, Vector2.Zero, StonewakeFX.GraniteSpark, 0.10f).Configure(10, 0.7f);
            PRTLoader.NewParticle<PRT_Light>(to, Vector2.Zero, StonewakeFX.GraniteCore, 0.08f).Configure(8, 0.55f);
            Lighting.AddLight(Vector2.Lerp(from, to, 0.5f), StonewakeFX.GraniteCore.ToVector3() * 0.5f);
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                Volume = 0.22f * GraniteEnv,
                Pitch = Main.rand.NextFloat(0.2f, 0.55f),
                MaxInstances = 3,
            }, Vector2.Lerp(from, to, 0.5f));
        }

        /// <summary>大理石厅常态：石殿浮尘与鎏金微光（刻意稀疏，厅的身份靠回响与光柱）</summary>
        private static void UpdateMarbleMotes() {
            if (--marbleMoteTimer > 0) {
                return;
            }
            marbleMoteTimer = 8 + Main.rand.Next(6);
            if (!StonewakeFX.TryFindExposedTile(TileID.Marble, out Vector2 face)) {
                return;
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(face, Vector2.Zero, StonewakeFX.MarbleGold, 0.07f)
                    .Configure(12, 0.5f);
            }
            else {
                PRTLoader.NewParticle<PRT_Smoke>(face, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), 0.25f),
                    StonewakeFX.MarbleDust, Main.rand.NextFloat(0.16f, 0.28f)).Configure(30, 0.26f, 0.02f);
            }
        }

        /// <summary>远处石像转头声：低频两拍（磨石+落定咔哒），从玩家远侧传来</summary>
        private static void UpdateMarbleGrind() {
            //第二拍：磨石后 9 帧的落定咔哒
            if (grindEchoTimer > 0 && --grindEchoTimer == 0 && MarbleEnv > 0.1f) {
                SoundEngine.PlaySound(SoundID.Tink with {
                    Volume = 0.24f * MarbleEnv,
                    Pitch = -0.5f,
                    MaxInstances = 2,
                }, grindPos);
            }
            if (--grindTimer > 0) {
                return;
            }
            grindTimer = 480 + Main.rand.Next(520);
            if (MarbleEnv <= 0.55f) {
                return;
            }
            grindPos = Main.LocalPlayer.Center
                + Main.rand.NextVector2Unit() * Main.rand.NextFloat(480f, 880f);
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.34f * MarbleEnv,
                Pitch = -0.75f,
                MaxInstances = 2,
            }, grindPos);
            grindEchoTimer = 9;
        }

        //==================== 权威端：脉冲 / 光柱调度 ====================

        private static void UpdateAuthority() {
            if (!GameModeSystem.BrutalActive) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0 || CWRWorld.HasBoss) {
                return;//Boss 在场：伤害/减益机制暂停，不新增触发
            }
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                StonewakePlayer sp = player.GetModPlayer<StonewakePlayer>();
                if (player.ZoneGranite) {
                    TickPulse(player, sp, tier);
                }
                if (player.ZoneMarble) {
                    TickPillar(player, sp, tier);
                }
            }
        }

        /// <summary>共振脉冲：附近花岗岩地表长出晶簇充能，随后能量环外扩</summary>
        private static void TickPulse(Player player, StonewakePlayer sp, int tier) {
            if (sp.PulseCooldown <= 0) {
                //初次入厅错拍起表，避免多人进厅齐响
                sp.PulseCooldown = 150 + Main.rand.Next(180);
                return;
            }
            if (--sp.PulseCooldown > 0) {
                return;
            }
            sp.PulseCooldown = TriggerRetryFrames;
            if (StonewakeFX.TownNpcNear(player.Center)) {
                return;//城镇安宁：减益机制不触发
            }
            if (StonewakeFX.CountActive(ModContent.ProjectileType<StonewakeResonanceRingProj>()) >= PulseCap) {
                return;
            }
            if (!StonewakeFX.TryFindGraniteAnchor(player, out Vector2 anchor)) {
                return;
            }
            Projectile.NewProjectile(new EntitySource_Misc("CWR_StonewakePulse"), anchor, Vector2.Zero,
                ModContent.ProjectileType<StonewakeResonanceRingProj>(), 0, 0f, Main.myPlayer);
            sp.PulseCooldown = PulseCooldownByTier[tier - 1];
        }

        /// <summary>凝视之柱：目标脚下的大理石地面刻纹亮起，随后立起石化光柱</summary>
        private static void TickPillar(Player player, StonewakePlayer sp, int tier) {
            if (sp.PillarCooldown <= 0) {
                sp.PillarCooldown = 180 + Main.rand.Next(200);
                return;
            }
            if (--sp.PillarCooldown > 0) {
                return;
            }
            sp.PillarCooldown = TriggerRetryFrames;
            if (StonewakeFX.TownNpcNear(player.Center)) {
                return;
            }
            if (StonewakeFX.CountActive(ModContent.ProjectileType<StonewakeGazePillarProj>()) >= PillarCap) {
                return;
            }
            if (!StonewakeFX.TryFindMarbleFloor(player, out Vector2 basePos)) {
                return;
            }
            int damage = (int)(StonewakeFX.ScaledContact(StonewakeFX.MarbleContactAnchor) * PillarDamageFrac);
            Projectile.NewProjectile(new EntitySource_Misc("CWR_StonewakePillar"), basePos, Vector2.Zero,
                ModContent.ProjectileType<StonewakeGazePillarProj>(), damage, 2f, Main.myPlayer);
            sp.PillarCooldown = PillarCooldownByTier[tier - 1];
        }
    }
}
