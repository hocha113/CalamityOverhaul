using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.Items.Summon.Deepclaws;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Armor.Deepcrystals
{
    /// <summary>
    /// 渊晶套装每玩家状态:聚泡引爆量表与空化反震。
    /// 攒泡/引爆/碎泡全在 owner 侧结算,层数变化经 <see cref="DeepcrystalNet"/> 转播,
    /// 旁观端由 <see cref="DeepcrystalBubbleLayer"/> 画环绕气泡;引爆弹幕走 NewProjectile 原生同步。不入库
    /// </summary>
    internal class DeepcrystalPlayer : ModPlayer
    {
        public const int MaxCharge = 5;
        /// <summary>攒泡节流(帧),湿身更快</summary>
        private const int GainInterval = 30;
        private const int GainIntervalWet = 20;
        /// <summary>脱战多久开始消散/每颗消散步进</summary>
        private const int DecayDelay = 300;
        private const int DecayStep = 60;
        private const int HurtBurstCooldown = 240;
        /// <summary>湿身引爆增幅</summary>
        private const float WetPayloadMul = 1.25f;

        //各职业引爆基伤与受击反震基伤(按对应职业总增伤缩放)
        private const int MeleeBurstDamage = 300;
        private const int JetTickDamage = 80;
        private const int WaterShotDamage = 130;
        private const int SeekBubbleDamage = 90;
        private const int HurtBurstDamage = 160;

        public bool SetActive;
        /// <summary>引爆演出归属职业,由头盔 UpdateArmorSet 每帧挂上</summary>
        public DamageClass SetClass = DamageClass.Generic;

        /// <summary>当前气泡层数。owner 权威,旁观端由网络包写入</summary>
        public int Charge;
        /// <summary>各泡凝聚时刻,驱动冒出动画</summary>
        public readonly uint[] BubbleBirth = new uint[MaxCharge];
        /// <summary>最近一次引爆/碎泡时刻与碎泡数量,驱动渲染层的破膜演出</summary>
        public uint LastDetonateTick;
        public uint LastShatterTick;
        public int ShatterCount;

        private uint nextGainAt;
        private uint lastGainTick;
        private uint nextDecayAt;
        private uint hurtBurstReadyAt;

        public override void ResetEffects() {
            //职业与激活随头盔每帧重挂;层数保留,卸甲在 PostUpdateMiscEffects 清
            SetActive = false;
        }

        public override void PostUpdateMiscEffects() {
            //湿身水膜尾迹:纯表现,各端画各自可见的玩家
            if (!Main.dedServ && SetActive && Player.wet && !Player.dead
                && Player.velocity.Length() > 2f && Main.GameUpdateCount % 3 == 0) {
                EverdeepVFX.ShedDroplet(Player.Center + Main.rand.NextVector2Circular(10f, 16f),
                    -Player.velocity * 0.15f, 0.7f);
            }

            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!SetActive) {
                if (Charge > 0) {
                    SetCharge(0);
                }
                return;
            }
            //脱战消散:超时后每步掉一颗
            if (Charge > 0 && Main.GameUpdateCount - lastGainTick > DecayDelay
                && Main.GameUpdateCount >= nextDecayAt) {
                nextDecayAt = Main.GameUpdateCount + DecayStep;
                SetCharge(Charge - 1);
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => TryGain(target);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            //引爆产物与反震自身不回充,防自喂
            if (proj.type == ModContent.ProjectileType<DeepclawSnapBurst>()
                || proj.type == ModContent.ProjectileType<DeepcrystalJetBeam>()
                || proj.type == ModContent.ProjectileType<DeepcrystalWaterShot>()
                || proj.type == ModContent.ProjectileType<DeepcrystalSeekBubble>()) {
                return;
            }
            TryGain(target);
        }

        private void TryGain(NPC target) {
            if (!SetActive || Player.whoAmI != Main.myPlayer || Player.dead
                || target == null || !target.active || target.friendly || target.dontTakeDamage) {
                return;
            }
            if (Main.GameUpdateCount < nextGainAt) {
                return;
            }
            nextGainAt = Main.GameUpdateCount + (uint)(Player.wet ? GainIntervalWet : GainInterval);
            lastGainTick = Main.GameUpdateCount;
            if (Charge + 1 >= MaxCharge) {
                Detonate(target);
                return;
            }
            SetCharge(Charge + 1);
        }

        /// <summary>满彼:全泡崩缩,按头盔职业引爆演出</summary>
        private void Detonate(NPC target) {
            LastDetonateTick = Main.GameUpdateCount;
            SetCharge(0, DeepcrystalNet.FlagDetonate);
            DetonateVisual();
            SoundEngine.PlaySound(SoundID.Item96 with { Volume = 0.8f, Pitch = 0.2f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.1f }, Player.Center);

            bool wet = Player.wet;
            float mul = wet ? WetPayloadMul : 1f;
            Vector2 aim = target.active ? target.Center
                : Player.Center.FindClosestNPC(800f)?.Center ?? Main.MouseWorld;
            var source = Player.GetSource_Misc("DeepcrystalDetonate");

            if (SetClass == DamageClass.Melee) {
                Projectile.NewProjectile(source, aim, Vector2.Zero
                    , ModContent.ProjectileType<DeepclawSnapBurst>()
                    , Scale(MeleeBurstDamage, DamageClass.Melee, mul), 9f, Player.whoAmI, 1.5f);
            }
            else if (SetClass == DamageClass.Ranged) {
                float ang = (aim - Player.Center).SafeNormalize(Vector2.UnitX).ToRotation();
                Projectile.NewProjectile(source, Player.Center, Vector2.Zero
                    , ModContent.ProjectileType<DeepcrystalJetBeam>()
                    , Scale(JetTickDamage, DamageClass.Ranged, mul), 5f, Player.whoAmI, ang, wet ? 1f : 0f);
            }
            else if (SetClass == DamageClass.Magic) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spawn = Player.Center + new Vector2((i - 1) * 26f, -34f);
                    Vector2 vel = (aim - spawn).SafeNormalize(Vector2.UnitX).RotatedBy((i - 1) * 0.16f) * 11.5f
                        + new Vector2(0f, -3.2f);
                    Projectile.NewProjectile(source, spawn, vel
                        , ModContent.ProjectileType<DeepcrystalWaterShot>()
                        , Scale(WaterShotDamage, DamageClass.Magic, mul), 4f, Player.whoAmI, aim.X, aim.Y);
                }
            }
            else {
                //召唤(及未识别职业兜底):环出四颗追踪泡
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = (MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.3f, 0.3f))
                        .ToRotationVector2() * 4.5f;
                    Projectile.NewProjectile(source, Player.Center + vel * 6f, vel
                        , ModContent.ProjectileType<DeepcrystalSeekBubble>()
                        , Scale(SeekBubbleDamage, DamageClass.Summon, mul), 3f, Player.whoAmI, wet ? 1f : 0f);
                }
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (!SetActive || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //狂战士惩罚:受击震碎全部气泡
            if (Charge > 0) {
                LastShatterTick = Main.GameUpdateCount;
                ShatterCount = Charge;
                SetCharge(0, DeepcrystalNet.FlagShatter);
                ShatterVisual();
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.5f, Pitch = -0.1f }, Player.Center);
            }
            //空化反震(带冷却)
            if (Main.GameUpdateCount < hurtBurstReadyAt) {
                return;
            }
            hurtBurstReadyAt = Main.GameUpdateCount + HurtBurstCooldown;
            Projectile.NewProjectile(Player.GetSource_Misc("DeepcrystalSetBonus"), Player.Center, Vector2.Zero
                , ModContent.ProjectileType<DeepclawSnapBurst>()
                , Scale(HurtBurstDamage, SetClass, 1f), 8f, Player.whoAmI, 1.2f);
        }

        private int Scale(int baseDamage, DamageClass dc, float mul)
            => (int)(Player.GetTotalDamage(dc).ApplyTo(baseDamage) * mul);

        /// <summary>owner 侧改层数并转播;value 相同且无旗标时静默</summary>
        private void SetCharge(int value, byte flags = 0) {
            value = (int)MathHelper.Clamp(value, 0, MaxCharge);
            if (value > Charge) {
                for (int i = Charge; i < value; i++) {
                    BubbleBirth[i] = Main.GameUpdateCount;
                }
            }
            if (value == Charge && flags == 0) {
                return;
            }
            Charge = value;
            DeepcrystalNet.Send(Player, (byte)value, flags);
        }

        /// <summary>旁观端应用网络层数与演出旗标</summary>
        public void ApplyNetCharge(int charge, byte flags) {
            int old = Charge;
            Charge = (int)MathHelper.Clamp(charge, 0, MaxCharge);
            for (int i = old; i < Charge; i++) {
                BubbleBirth[i] = Main.GameUpdateCount;
            }
            if ((flags & DeepcrystalNet.FlagDetonate) != 0) {
                LastDetonateTick = Main.GameUpdateCount;
                DetonateVisual();
            }
            if ((flags & DeepcrystalNet.FlagShatter) != 0) {
                LastShatterTick = Main.GameUpdateCount;
                ShatterCount = Math.Max(old, 1);
                ShatterVisual();
            }
        }

        /// <summary>第 i 颗环绕泡的世界坐标(渲染层与破膜演出共用)</summary>
        public Vector2 BubbleOrbitPos(int i) {
            float t = Main.GlobalTimeWrappedHourly;
            float ang = MathHelper.TwoPi * i / MaxCharge + t * 1.5f;
            float bob = MathF.Sin(t * 2.3f + i * 1.7f) * 4f;
            return Player.MountedCenter + ang.ToRotationVector2() * (30f + bob) - new Vector2(0f, 6f);
        }

        public void DetonateVisual() {
            if (Main.dedServ) {
                return;
            }
            EverdeepVFX.SplashBurst(Player.Center, new Vector2(0f, -8f), 1.2f);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(Player.Center
                    , Main.rand.NextVector2Circular(4f, 4f)
                    , SeaShrimpVFX.Glow, Main.rand.NextFloat(0.6f, 1f))?.Configure(12);
            }
        }

        public void ShatterVisual() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < ShatterCount; i++) {
                Vector2 pos = BubbleOrbitPos(i);
                for (int k = 0; k < 3; k++) {
                    EverdeepVFX.ShedDroplet(pos, Main.rand.NextVector2Circular(1.6f, 1.6f)
                        - Vector2.UnitY * 0.8f, 0.75f);
                }
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(Player.Center
                    , Main.rand.NextVector2Circular(3.5f, 3.5f)
                    , SeaShrimpVFX.Film, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(10);
            }
        }
    }

    /// <summary>
    /// 环绕气泡渲染:一次 Immediate 批画完全场佩戴者的量表泡(FishronBubble 水膜,
    /// 批次合同镜像 <see cref="SeaShrimpBubbleRender"/>:DrawAfterTiles 上膛 + 首次消费闩锁)。
    /// 引爆/碎泡后的短暂破膜残影也在此层
    /// </summary>
    internal class DeepcrystalBubbleLayer : RenderHandle
    {
        private const int PopFrames = 10;
        private static bool armed;

        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => armed = true;

        public override void DrawBeforePlayers(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (!armed || Main.gameMenu || Main.dedServ || !SeaShrimpVFX.BubblePathReady) {
                return;
            }
            armed = false;

            //先探一遍有没有要画的佩戴者,空场不开批
            bool any = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (NeedsDraw(Main.player[i], out _)) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect fx = EffectLoader.FishronBubble.Value;
            fx.Parameters["uTint"]?.SetValue(SeaShrimpVFX.Film.ToVector3());
            fx.Parameters["uDeepColor"]?.SetValue(SeaShrimpVFX.Deep.ToVector3());
            graphicsDevice.Textures[1] = CWRAsset.PerlinNoise.Value;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!NeedsDraw(player, out DeepcrystalPlayer dcp)) {
                    continue;
                }
                DrawFor(spriteBatch, fx, pixel, player, dcp);
            }

            spriteBatch.End();
        }

        private static bool NeedsDraw(Player player, out DeepcrystalPlayer dcp) {
            dcp = null;
            if (player is not { active: true, dead: false } || !player.TryGetModPlayer(out dcp)) {
                return false;
            }
            uint now = Main.GameUpdateCount;
            bool popping = now - dcp.LastDetonateTick < PopFrames || now - dcp.LastShatterTick < PopFrames;
            return dcp.SetActive && (dcp.Charge > 0 || popping);
        }

        private static void DrawFor(SpriteBatch sb, Effect fx, Texture2D pixel, Player player, DeepcrystalPlayer dcp) {
            uint now = Main.GameUpdateCount;
            float radius = player.wet ? 11.5f : 9.5f;

            //在场量表泡
            for (int i = 0; i < dcp.Charge; i++) {
                float grow = MathHelper.Clamp((now - dcp.BubbleBirth[i]) / 8f, 0.3f, 1f);
                var body = new SeaShrimpBubbleBodyParams {
                    Center = dcp.BubbleOrbitPos(i),
                    Radius = radius * grow,
                    Wobble = 0.5f,
                    Arm = 0f,
                    Burst = 0f,
                    Fade = grow,
                    Seed = player.whoAmI * 7.3f + i * 1.9f,
                };
                SeaShrimpVFX.DrawBubbleInBatch(sb, fx, pixel, in body);
            }

            //引爆:一颗大泡崩缩;碎泡:原位泡破膜残影
            float detAge = now - dcp.LastDetonateTick;
            if (detAge < PopFrames) {
                var body = new SeaShrimpBubbleBodyParams {
                    Center = player.MountedCenter,
                    Radius = MathHelper.Lerp(20f, 44f, detAge / PopFrames),
                    Wobble = 0.8f,
                    Arm = 0f,
                    Burst = detAge / PopFrames,
                    Fade = 1f,
                    Seed = player.whoAmI * 7.3f,
                };
                SeaShrimpVFX.DrawBubbleInBatch(sb, fx, pixel, in body);
            }
            float shaAge = now - dcp.LastShatterTick;
            if (shaAge < PopFrames) {
                for (int i = 0; i < dcp.ShatterCount; i++) {
                    var body = new SeaShrimpBubbleBodyParams {
                        Center = dcp.BubbleOrbitPos(i),
                        Radius = radius,
                        Wobble = 0.9f,
                        Arm = 0f,
                        Burst = shaAge / PopFrames,
                        Fade = 1f,
                        Seed = player.whoAmI * 7.3f + i * 1.9f,
                    };
                    SeaShrimpVFX.DrawBubbleInBatch(sb, fx, pixel, in body);
                }
            }
        }
    }
}
