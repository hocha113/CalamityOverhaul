using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles
{
    /// <summary>
    /// 处刑蓄力预兆：ai[0]=锚NPC索引 ai[1]=登记类型×10+模式 ai[2]=锁定方向+10（0=未锁定）。
    /// 模式：0 冲锋警示带 / 1 俯冲警示线 / 2 毒瓶落点标记（生成即锁死） / 3 蓄力瞄准短标。
    /// 追踪期直读目标方向，锁定帧后冻结（预告即承诺，服务端写 ai[2] 权威纠偏）；
    /// 执行期保留为余痕并向锚怪盖执行窗镜像戳（吸血鬼血狩印据此判窗），永不造成伤害
    /// </summary>
    internal class EclStrikeOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        internal const int ModeRushLane = 0;
        internal const int ModeDiveLine = 1;
        internal const int ModeDropMarker = 2;
        internal const int ModeAimStub = 3;

        /// <summary>落点标记模式余痕：覆盖毒瓶最长飞行+爆裂窗（56+18）</summary>
        private const int MarkerResidueFrames = 74;
        /// <summary>瞄准短标模式余痕（载荷出手后快速淡出）</summary>
        private const int StubResidueFrames = 24;

        /// <summary>冲锋警示带带宽（略宽于怪体，包住上坡步进与残余转向）</summary>
        private const float RushLaneWidth = 52f;
        /// <summary>俯冲警示线芯宽/柔光宽（画宽于怪体判定）</summary>
        private const float DiveCoreWidth = 26f;
        private const float DiveGlowWidth = 66f;

        private int AnchorIndex => (int)Projectile.ai[0];
        private int RecordedType => (int)Projectile.ai[1] / 10;
        private int Mode => (int)Projectile.ai[1] % 10;
        private bool Locked => Projectile.ai[2] != 0f;

        private EclProfile Profile => EclEclipseSets.Profiles[RecordedType];
        private int TelegraphFrames => Profile.Telegraph;

        private int ResidueFrames => Mode switch {
            ModeDropMarker => MarkerResidueFrames,
            ModeAimStub => StubResidueFrames,
            _ => Profile.Strike,
        };

        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        internal bool InStrike => Elapsed >= TelegraphFrames;
        private bool InLockPhase => !InStrike && Elapsed >= TelegraphFrames - Profile.LockFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //占位时长，首个 AI 帧按登记类型套定精确总长（各端由同步 ai[1] 确定性同解）
            Projectile.timeLeft = 130;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TelegraphFrames + ResidueFrames;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入端：ai[2] 已非零=服务端早过锁定帧，本地相位快进到锁定段起点，不重放追踪期
                if (Locked && Mode != ModeDropMarker) {
                    Projectile.timeLeft = ResidueFrames + Profile.LockFrames;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = -0.62f }, Projectile.Center);
                }
            }

            //锚校验用精确类型：吸血鬼中途变形（实例重建、攻击不会兑现）时预兆立即消散，
            //绝不留下无后续的假预告（破绽/血狩印才按形态对放行）
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            bool anchorValid = anchor.Alives() && anchor.type == RecordedType;

            if (Mode == ModeDropMarker) {
                //落点标记生成即锁死、永不移动；预告期掷瓶者倒下/变更则投掷不会发生，
                //出手后毒瓶已独立飞行，标记作为落点警示保留到爆裂窗结束
                if (!InStrike && !anchorValid) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                if (!anchorValid) {
                    //锚定怪没了（含吸血鬼中途变形打断本次攻击）：重击不会发生，预兆消散
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = Mode == ModeRushLane
                    ? anchor.Bottom + new Vector2(0f, anchor.gfxOffY - 4f)
                    : anchor.Center + new Vector2(0f, anchor.gfxOffY);
            }

            //方向解析：锁定后走权威 ai[2]，追踪期各端从同步数据确定性推得
            if (Locked) {
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (Mode != ModeDropMarker && !InLockPhase && anchorValid) {
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Vector2 toTarget = Main.player[target].Center - anchor.Center;
                    Projectile.rotation = Mode == ModeRushLane
                        ? (toTarget.X >= 0f ? 0f : MathHelper.Pi)
                        : toTarget.ToRotation();
                }
            }
            //锁定段未收到 ai[2] 时 rotation 冻结在最后追踪值，方向承诺不回摆

            if (Elapsed == TelegraphFrames - Profile.LockFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.46f, Pitch = -0.28f }, Projectile.Center);
            }
            if (Elapsed == TelegraphFrames && Mode is ModeRushLane or ModeDiveLine && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.62f, Pitch = -0.3f }, Projectile.Center);
            }

            //执行窗镜像戳：吸血鬼命中挂印只认这扇窗（实体已同步，各端一致）
            if (InStrike && anchorValid && Mode is ModeRushLane or ModeDiveLine
                && anchor.TryGetGlobalNPC(out EclipseNPC eclipse)) {
                eclipse.StampStrikeWindow();
            }

            //预告期低频警示尘（预算：至多 1 粒/帧）
            if (!InStrike && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                float progress = Elapsed / (float)TelegraphFrames;
                Vector2 dustPos = Mode == ModeDropMarker
                    ? Projectile.Center + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * EclManFlyFlaskProj.SplashRadius, 2f)
                    : Projectile.Center + Main.rand.NextVector2Circular(14f, 10f);
                Dust seep = Dust.NewDustPerfect(dustPos, DustID.Torch,
                    new Vector2(0f, -0.4f - progress), 150, Profile.Tint, 0.7f + progress * 0.5f);
                seep.noGravity = true;
            }

            Color warn = Profile.Tint;
            Lighting.AddLight(Projectile.Center, warn.R / 255f * 0.14f, warn.G / 255f * 0.14f, warn.B / 255f * 0.14f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.localAI[0] == 0f) {
                //首个 AI 帧之前 ai 槽可能尚未套定，不读参数表
                return false;
            }
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                //执行期余痕：随余痕窗线性退光
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)ResidueFrames, 0f, 1f)
                    * (Mode == ModeDropMarker ? 0.85f : 0.25f);
            }
            else {
                strength = fadeIn * (Locked || InLockPhase ? 1f : 0.55f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            return Mode switch {
                ModeRushLane => DrawRushLane(strength),
                ModeDiveLine => DrawDiveLine(strength),
                ModeDropMarker => DrawDropMarker(strength),
                _ => DrawAimStub(strength),
            };
        }

        private float LockFlash() {
            if (InStrike || !(Locked || InLockPhase)) {
                return 1f;
            }
            float lockT = MathHelper.Clamp((Elapsed - (TelegraphFrames - Profile.LockFrames)) / (float)Profile.LockFrames, 0f, 1f);
            return 0.72f + 0.28f * MathF.Sin(lockT * MathHelper.Pi * 5f);
        }

        /// <summary>冲锋警示带：贴地暗底色带 + 加色芯 + 向锁向行进的引导点</summary>
        private bool DrawRushLane(float strength) {
            Texture2D line = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, line.Height / 2f);
            float scaleX = Profile.LaneLength / line.Width;
            Color warn = Profile.Tint with { A = 0 };
            Color rim = new Color(38, 14, 12);
            float flash = LockFlash();

            //暗色实底（真 alpha 压亮背景）+ 加色芯
            Main.EntitySpriteDraw(line, drawPos, null, rim * (0.7f * strength), Projectile.rotation,
                origin, new Vector2(scaleX, RushLaneWidth * 1.2f / line.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, warn * (0.55f * strength * flash), Projectile.rotation,
                origin, new Vector2(scaleX, RushLaneWidth * 0.55f / line.Height), SpriteEffects.None, 0);

            //引导点沿锁向行进：方向可读
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float march = Main.GlobalTimeWrappedHourly * 2.2f % 1f;
            for (int i = 0; i < 4; i++) {
                float t = (march + i / 4f) % 1f;
                Vector2 dot = drawPos + dir * Profile.LaneLength * t;
                Main.EntitySpriteDraw(glow, dot, null, warn * (strength * 0.5f * (1f - t)), 0f,
                    glow.Size() / 2f, 0.16f, SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>俯冲警示线：细芯 + 宽柔光，锁定段白热收窄宣告承诺</summary>
        private bool DrawDiveLine(float strength) {
            Texture2D line = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, line.Height / 2f);
            float scaleX = Profile.LaneLength / line.Width;
            Color warn = Profile.Tint with { A = 0 };
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            if (!Locked && !InLockPhase || InStrike) {
                Main.EntitySpriteDraw(line, drawPos, null, warn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, DiveCoreWidth / line.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(line, drawPos, null, warn * (0.3f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, DiveGlowWidth / line.Height), SpriteEffects.None, 0);
            }
            else {
                float flash = LockFlash();
                Color core = new Color(255, 244, 224, 0) * (0.85f * flash * strength);
                Main.EntitySpriteDraw(line, drawPos, null, warn * (0.65f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (DiveGlowWidth + 18f) / line.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(line, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(scaleX, (DiveCoreWidth - 8f) / line.Height), SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>毒瓶落点标记：与爆裂半径同宽的地面椭圆（标记区=伤害区，同一常量）+ 坠落引导点</summary>
        private bool DrawDropMarker(float strength) {
            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 markerPos = Projectile.Center - Main.screenPosition;
            float width = EclManFlyFlaskProj.SplashRadius * 2f;
            float height = 40f;
            Color warn = Profile.Tint with { A = 0 };
            float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity * 0.8f);

            Main.EntitySpriteDraw(rim, markerPos, null, new Color(20, 34, 10) * (0.8f * strength), 0f,
                rim.Size() / 2f, new Vector2(width / rim.Width, height / rim.Height) * 1.12f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, markerPos, null, warn * (strength * (0.55f + 0.45f * pulse)), 0f,
                glow.Size() / 2f, new Vector2(width / glow.Width, height / glow.Height), SpriteEffects.None, 0);

            //坠落引导点：预告期自上而下行进，提示抛物来袭
            if (!InStrike) {
                float march = Main.GlobalTimeWrappedHourly * 1.6f % 1f;
                for (int i = 0; i < 3; i++) {
                    float t = (march + i / 3f) % 1f;
                    Vector2 dot = markerPos - Vector2.UnitY * (150f * (1f - t));
                    Main.EntitySpriteDraw(glow, dot, null, warn * (strength * 0.45f * t), 0f,
                        glow.Size() / 2f, 0.15f, SpriteEffects.None, 0);
                }
            }
            return false;
        }

        /// <summary>蓄力瞄准短标：源头汇聚辉光 + 锁向粗短标线</summary>
        private bool DrawAimStub(float strength) {
            Texture2D line = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, line.Height / 2f);
            Color warn = Profile.Tint with { A = 0 };
            float windup = MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);
            float flash = LockFlash();

            //蓄力汇聚：源头辉光随蓄力涨大，暗底衬托
            Texture2D rim = CWRAsset.Extra_98.Value;
            Main.EntitySpriteDraw(rim, drawPos, null, new Color(30, 12, 14) * (0.6f * strength), 0f,
                rim.Size() / 2f, 0.34f + 0.2f * windup, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, warn * (strength * (0.4f + 0.5f * windup) * flash), 0f,
                glow.Size() / 2f, 0.5f + 0.45f * windup, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(line, drawPos, null, warn * (0.6f * strength * flash), Projectile.rotation,
                origin, new Vector2(Profile.LaneLength / line.Width, 30f / line.Height), SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
