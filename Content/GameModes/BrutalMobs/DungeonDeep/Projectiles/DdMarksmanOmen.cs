using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles
{
    /// <summary>
    /// 射手预告体。ai[0]=来源打包（槽位+1|类型&lt;&lt;8） ai[1]=模式 ai[2]=锁定方向+10。
    /// 狙击模式：长标线追踪 26 帧→权威端写 ai[2] 一次性同步锁向（锁定即承诺）→18 帧后单发；
    /// 战术模式：生成帧锁角短扇面 34 帧→三连齐射，三次同角同缺口（走廊缺口槽被发射循环真正跳过）。
    /// 射手死亡/槽位复用即消散（击杀=有效反制），本体永不造成伤害
    /// </summary>
    internal class DdMarksmanOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        internal const int ModeSniper = 0;
        internal const int ModeTactical = 1;

        //==== 狙击（长标线单发） ====
        /// <summary>狙击预告总帧（契约 ≥40：追踪 26 + 锁定 18）</summary>
        internal const int SniperTelegraphFrames = 44;
        /// <summary>锁定段帧数（锁定即承诺，期间标线冻结白热）</summary>
        internal const int SniperLockFrames = 18;
        private const int SniperFadeFrames = 8;
        private const float SniperBoltSpeed = 16.5f;
        private const float SniperLaneLength = 980f;

        //==== 战术（短扇面三连） ====
        /// <summary>战术扇面预告帧（≥30 契约）</summary>
        internal const int TacticalTelegraphFrames = 34;
        /// <summary>三连齐射的发数与间隔</summary>
        internal const int TacticalVolleys = 3;
        internal const int TacticalVolleyGap = 8;
        /// <summary>扇面槽位数</summary>
        private const int TacticalFanSlots = 5;
        /// <summary>走廊缺口槽位：发射循环真正跳过此槽=可学习的安全巷（三连不换角不换缺口）</summary>
        internal const int TacticalCorridorSlot = 2;
        private const float TacticalHalfSpread = 0.30f;
        private const float TacticalPelletSpeed = 9.2f;
        private const float TacticalLaneLength = 300f;

        private static readonly Color SniperWarn = new Color(255, 70, 70, 0);
        private static readonly Color TacticalWarn = new Color(255, 200, 120, 0);

        private int SourcePacked => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private int Mode => (int)Projectile.ai[1];
        private bool Locked => Projectile.ai[2] != 0f;
        private float LockDir => Projectile.ai[2] - 10f;
        private int TelegraphTotal => Mode == ModeSniper ? SniperTelegraphFrames : TacticalTelegraphFrames;
        private int TotalLife => Mode == ModeSniper
            ? SniperTelegraphFrames + SniperFadeFrames
            : TacticalTelegraphFrames + TacticalVolleyGap * (TacticalVolleys - 1) + 6;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1100;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害（弹体在提交帧另行出膛）</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                Projectile.localAI[1] = TotalLife;
                //迟入玩家（狙击）：首帧已锁向=权威端早过追踪段，本地相位快进到锁定起点
                if (Mode == ModeSniper && Locked) {
                    Projectile.timeLeft = SniperLockFrames + SniperFadeFrames;
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //来源校验：射手倒了枪响不会来
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != SourcePacked >> 8) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Locked) {
                //已锁向：方向即承诺，冻结
                Projectile.rotation = LockDir;
            }
            else if (Mode == ModeSniper) {
                //追踪期：直读目标方向（各端从同步数据确定性推得）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Projectile.rotation = (Main.player[target].Center - Projectile.Center).ToRotation();
                }
            }

            int elapsed = Elapsed;
            if (Mode == ModeSniper) {
                if (elapsed == SniperTelegraphFrames - SniperLockFrames && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 5 }, Projectile.Center);
                }
                //单发提交：仅在已锁向时开火（未锁=不开火，失败方向=安全方向）
                if (elapsed == SniperTelegraphFrames && Locked) {
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                            LockDir.ToRotationVector2() * SniperBoltSpeed,
                            ModContent.ProjectileType<DdBoltProj>(), Projectile.damage, 0f, Main.myPlayer,
                            DdBoltProj.ModeSniper);
                    }
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item40 with { Volume = 0.65f, Pitch = -0.1f, MaxInstances = 4 }, Projectile.Center);
                    }
                }
            }
            else {
                //三连齐射：同角同缺口（缺口承诺三次不变）
                for (int k = 0; k < TacticalVolleys; k++) {
                    if (elapsed != TacticalTelegraphFrames + k * TacticalVolleyGap) {
                        continue;
                    }
                    if (!VaultUtils.isClient && Locked) {
                        FireFan();
                    }
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
                    }
                }
            }

            Color warn = Mode == ModeSniper ? SniperWarn : TacticalWarn;
            Lighting.AddLight(Projectile.Center, warn.R / 255f * 0.12f, warn.G / 255f * 0.12f, warn.B / 255f * 0.12f);
        }

        /// <summary>扇面第 i 槽的角度；走廊缺口槽返回 null（发射与预览共用同一判定）</summary>
        private float? FanAngle(int i) {
            if (i == TacticalCorridorSlot) {
                return null;
            }
            return LockDir + MathHelper.Lerp(-TacticalHalfSpread, TacticalHalfSpread, i / (float)(TacticalFanSlots - 1));
        }

        /// <summary>单次齐射：跳过走廊缺口槽内的所有弹位</summary>
        private void FireFan() {
            int boltType = ModContent.ProjectileType<DdBoltProj>();
            for (int i = 0; i < TacticalFanSlots; i++) {
                float? ang = FanAngle(i);
                if (ang == null) {
                    continue;//走廊缺口：预览里空着的方向就是安全方向
                }
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    ang.Value.ToRotationVector2() * TacticalPelletSpeed,
                    boltType, Projectile.damage, 0f, Main.myPlayer, DdBoltProj.ModePellet);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            int telegraph = TelegraphTotal;
            float fadeIn = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            float strength;
            if (elapsed >= telegraph) {
                strength = MathHelper.Clamp(1f - (elapsed - telegraph) / 14f, 0f, 1f) * 0.35f;
            }
            else {
                strength = fadeIn * (Locked ? 1f : 0.5f);
            }
            if (strength <= 0.02f) {
                return false;
            }

            Texture2D lane = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, lane.Height / 2f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            if (Mode == ModeSniper) {
                float scaleX = SniperLaneLength / lane.Width;
                bool lockFlash = Locked && elapsed < telegraph;
                if (lockFlash) {
                    //锁定期白热窄闪：轨迹已承诺
                    float lockT = MathHelper.Clamp((elapsed - (SniperTelegraphFrames - SniperLockFrames)) / (float)SniperLockFrames, 0f, 1f);
                    float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                    Main.EntitySpriteDraw(lane, drawPos, null, SniperWarn * (0.6f * flash * strength), Projectile.rotation,
                        origin, new Vector2(scaleX, 34f / lane.Height), SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(lane, drawPos, null, new Color(255, 240, 230, 0) * (0.8f * flash * strength),
                        Projectile.rotation, origin, new Vector2(scaleX, 8f / lane.Height), SpriteEffects.None, 0);
                }
                else {
                    //追踪期：细暗红线随目标摆动
                    Main.EntitySpriteDraw(lane, drawPos, null, SniperWarn * (0.4f * strength * pulse), Projectile.rotation,
                        origin, new Vector2(scaleX, 16f / lane.Height), SpriteEffects.None, 0);
                }
                return false;
            }

            //战术扇面：弹粒虚影（与发射同判缺口）+ 走廊亮巷
            int donor = ProjectileID.BulletDeadeye;
            Main.instance.LoadProjectile(donor);
            Texture2D ghost = TextureAssets.Projectile[donor].Value;
            int donorFrames = Math.Max(1, Main.projFrames[donor]);
            Rectangle frameRect = new(0, 0, ghost.Width, ghost.Height / donorFrames);
            float progress = MathHelper.Clamp(elapsed / (float)telegraph, 0f, 1f);
            float ghostDist = 22f + 30f * progress;
            for (int i = 0; i < TacticalFanSlots; i++) {
                float? ang = FanAngle(i);
                if (ang == null) {
                    continue;
                }
                Main.EntitySpriteDraw(ghost, drawPos + ang.Value.ToRotationVector2() * ghostDist, frameRect,
                    TacticalWarn * (0.55f * strength * pulse), ang.Value + MathHelper.PiOver2,
                    frameRect.Size() / 2f, 0.9f, SpriteEffects.None, 0);
            }
            //走廊亮巷：安全方向指示
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glowTex, drawPos + LockDir.ToRotationVector2() * (ghostDist + 26f), null,
                new Color(255, 246, 210, 0) * (0.45f * strength), LockDir, glowTex.Size() / 2f,
                new Vector2(TacticalLaneLength / glowTex.Width * 1.2f, 0.4f), SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
