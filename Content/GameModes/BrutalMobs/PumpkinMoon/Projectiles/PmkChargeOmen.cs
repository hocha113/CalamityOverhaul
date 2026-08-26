using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 冥猎冲锋预告线：ai[0]=锚NPC索引 ai[1]=风味(0地狱犬/1无头骑士) ai[2]=锁定方向+10（0=未锁定）。
    /// 追踪期直读目标方向（浅角钳制与冲锋注入共用同一函数，线即弹道），锁定帧后冻结（预告即承诺），
    /// 权威端在锁定帧写 ai[2] 作各端纠偏；迟入端首帧见 ai[2] 非零即快进相位。
    /// 突进期保留为淡出余痕兼判定窗载体（冲锋命中点燃据此判窗），本体永不造成伤害
    /// </summary>
    internal class PmkChargeOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>地狱犬：预告总帧（≥30 契约）/末段锁定帧/突进窗帧</summary>
        internal const int HoundTelegraphFrames = 34;
        internal const int HoundLockFrames = 14;
        internal const int HoundStrikeFrames = 26;
        private const float HoundLaneLength = 430f;

        /// <summary>无头骑士：预告总帧/锁定帧/突进窗帧</summary>
        internal const int HorsemanTelegraphFrames = 42;
        internal const int HorsemanLockFrames = 16;
        internal const int HorsemanStrikeFrames = 40;
        private const float HorsemanLaneLength = 660f;

        /// <summary>冲锋浅角钳制（弧度）：预告线与速度注入共用（公平阀门，线即承诺）</summary>
        internal const float ChargeMaxTilt = 0.32f;

        /// <summary>线芯宽与柔光宽，画宽于怪体判定，覆盖原版 AI 突进期的残余漂移</summary>
        private const float LaneCoreWidth = 24f;
        private const float LaneGlowWidth = 58f;

        private static readonly Color HoundWarn = new Color(255, 84, 40, 0);
        private static readonly Color HorsemanWarn = new Color(255, 176, 64, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private bool IsHorseman => Projectile.ai[1] == 1f;
        /// <summary>风味对应的锚类型（index+type 双校验，槽位不是身份）</summary>
        private int ExpectedAnchorType => IsHorseman ? NPCID.HeadlessHorseman : NPCID.Hellhound;
        private int TelegraphFrames => IsHorseman ? HorsemanTelegraphFrames : HoundTelegraphFrames;
        private int LockFrames => IsHorseman ? HorsemanLockFrames : HoundLockFrames;
        private int StrikeFrames => IsHorseman ? HorsemanStrikeFrames : HoundStrikeFrames;
        private float LaneLength => IsHorseman ? HorsemanLaneLength : HoundLaneLength;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        internal bool InStrike => Elapsed >= TelegraphFrames;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;

        /// <summary>受害端判窗：该冲锋者当前是否处于突进窗（index+type 双校验）</summary>
        internal static bool IsStrikeWindowFor(int npcIndex, int npcType) {
            int type = ModContent.ProjectileType<PmkChargeOmen>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && (int)proj.ai[0] == npcIndex
                    && proj.ModProjectile is PmkChargeOmen omen
                    && omen.ExpectedAnchorType == npcType && omen.InStrike) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>浅角钳制的冲锋方向：NPC 侧注入与本实体追踪显示共用（线即弹道）</summary>
        internal static float ClampChargeDir(Vector2 from, Vector2 to) {
            Vector2 d = to - from;
            float tilt = MathHelper.Clamp(MathF.Atan2(d.Y, MathF.Abs(d.X)), -ChargeMaxTilt, ChargeMaxTilt);
            return d.X >= 0f ? tilt : MathHelper.Pi - tilt;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = HorsemanTelegraphFrames + HorsemanStrikeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TelegraphFrames + StrikeFrames;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入端：首帧 ai[2] 已非零 = 权威端早过锁定帧的同步证据，本地相位快进到锁定起点
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = StrikeFrames + LockFrames;
                }
            }

            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != ExpectedAnchorType) {
                //锚定怪没了/槽位被复用：冲锋不会发生，预告随之消散
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Projectile.ai[2] != 0f) {
                //权威端已写入锁定方向
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!Locked) {
                //追踪期：直读目标方向（各端从同步数据确定性推得）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers) {
                    Player player = Main.player[target];
                    if (player.Alives()) {
                        Projectile.rotation = ClampChargeDir(Projectile.Center, player.Center);
                    }
                }
            }
            //锁定后 rotation 冻结在最后追踪值，等待/无需 ai[2] 纠偏

            if (Elapsed == TelegraphFrames - LockFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.42f, Pitch = -0.35f }, Projectile.Center);
            }
            if (Elapsed == TelegraphFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.65f, Pitch = IsHorseman ? -0.45f : -0.1f }, Projectile.Center);
            }

            Color warn = IsHorseman ? HorsemanWarn : HoundWarn;
            Lighting.AddLight(Projectile.Center, warn.R / 255f * 0.15f, warn.G / 255f * 0.15f, warn.B / 255f * 0.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                //突进期余痕：可见窗与判定窗同一实体
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.22f;
            }
            else {
                strength = fadeIn * (Locked ? 1f : 0.55f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            //NPC 锚定绘制补 gfxOffY（上坡步进补偿）
            float gfxOff = 0f;
            if (AnchorIndex.TryGetNPC(out NPC anchor) && anchor.Alives()) {
                gfxOff = anchor.gfxOffY;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center + new Vector2(0f, gfxOff) - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLength / tex.Width;
            Color warn = IsHorseman ? HorsemanWarn : HoundWarn;
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            if (!Locked || InStrike) {
                //追踪期/余痕期：细芯 + 宽柔光
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.3f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪，宣告轨迹已承诺
                float lockT = MathHelper.Clamp((Elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Color core = new Color(255, 240, 214, 0) * (0.85f * flash * strength);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.65f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 18f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 8f) / tex.Height), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
