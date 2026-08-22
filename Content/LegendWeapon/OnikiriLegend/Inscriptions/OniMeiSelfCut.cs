using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 鬼丸「自斩」：刀真的脱手飞出去斩人再飞回来。<br/>
    /// 三拍：抽离（刀自手中拔起、原地一顿）→ 扑击（高速直取目标，命中帧落一记断斩）
    /// → 回鞘（沿去路倒飞回手）。全程 <see cref="IOniBladeOccupant.HardOccupiesBlade"/> 为真，
    /// 手上没刀就挥不了也走不了，这是它的代价，不是隐形数值。<br/>
    /// ai[0]=目标 whoAmI ai[1]=基础武器伤害
    /// </summary>
    internal class OniMeiSelfCut : ModProjectile, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>抽离：刀离手悬停蓄势</summary>
        private const int DrawFrames = 10;
        /// <summary>扑击：最长飞行帧数，超时也判定到点</summary>
        private const int LungeFrames = 22;
        /// <summary>回鞘帧数</summary>
        private const int ReturnFrames = 14;
        /// <summary>命中判定的贴近距离(px)</summary>
        private const float ReachPad = 26f;
        private const int GhostCapacity = 6;

        private static readonly Color PaperEdge = new(255, 240, 226);
        private static readonly Color InkDeep = new(52, 12, 18);

        private enum Phase : byte
        {
            Draw,
            Lunge,
            Return,
        }

        private Phase phase = Phase.Draw;
        private int phaseTimer;
        private bool initialized;
        private bool struck;
        private Vector2 launchPoint;
        private Vector2 cutPoint;
        private float bladeRotation;
        private readonly Vector2[] ghostPos = new Vector2[GhostCapacity];
        private readonly float[] ghostRot = new float[GhostCapacity];
        private int ghostCount;

        private int TargetId => (int)Projectile.ai[0];
        private int BaseWeaponDamage => Math.Max(1, (int)Projectile.ai[1]);
        private Player Owner => Main.player[Projectile.owner];

        bool IOniBladeOccupant.HardOccupiesBlade => true;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = DrawFrames + LungeFrames + ReturnFrames + 20;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>owner 端放刀；target 须在场</summary>
        internal static Projectile Fire(Player player, NPC target, int baseWeaponDamage,
            IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI || target?.active != true) {
                return null;
            }
            return Projectile.NewProjectileDirect(
                source ?? player.GetSource_Misc("CWR_OniMeiSelfCut"),
                player.Center, Vector2.Zero, ModContent.ProjectileType<OniMeiSelfCut>(),
                0, 0f, player.whoAmI, ai0: target.whoAmI, ai1: Math.Max(1, baseWeaponDamage));
        }

        /// <summary>本机玩家是否正有一把刀在外面</summary>
        internal static bool AnyOwned(Player player) {
            if (player == null) {
                return false;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<OniMeiSelfCut>()] > 0;
        }

        private NPC ResolveTarget() {
            int id = TargetId;
            if (id < 0 || id >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[id];
            return npc.active && npc.life > 0 ? npc : null;
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                launchPoint = Owner.Center;
                Projectile.Center = Owner.Center;
                NPC first = ResolveTarget();
                bladeRotation = first != null
                    ? (first.Center - Owner.Center).ToRotation()
                    : Vector2.UnitX.RotatedBy(Owner.direction > 0 ? 0f : MathHelper.Pi).ToRotation();
                PlayDrawCue();
            }
            phaseTimer++;

            switch (phase) {
                case Phase.Draw:
                    TickDraw();
                    break;
                case Phase.Lunge:
                    TickLunge();
                    break;
                default:
                    TickReturn();
                    break;
            }

            PushGhost();
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.12f, 0.10f));
        }

        /// <summary>抽离：刀自手边抬起、朝目标偏转，读作"它自己要去"</summary>
        private void TickDraw() {
            NPC target = ResolveTarget();
            Vector2 aim = target != null
                ? (target.Center - Owner.Center).SafeNormalize(Vector2.UnitX * Owner.direction)
                : Vector2.UnitX * Owner.direction;
            float t = phaseTimer / (float)DrawFrames;
            //蓄势期先向后一沉，再抬起来对准（有预备动作才不像贴图弹出）
            float back = MathF.Sin(t * MathHelper.Pi) * 18f;
            Projectile.Center = Owner.Center - aim * back + Vector2.UnitY * -10f * t;
            bladeRotation = bladeRotation.AngleLerp(aim.ToRotation(), 0.35f);
            launchPoint = Projectile.Center;
            if (phaseTimer >= DrawFrames) {
                phase = Phase.Lunge;
                phaseTimer = 0;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(CWRSound.KatanaSwing with { Pitch = 0.55f, Volume = 0.55f },
                        Projectile.Center);
                }
            }
        }

        /// <summary>扑击：直取目标，到位即落一记断斩</summary>
        private void TickLunge() {
            NPC target = ResolveTarget();
            if (target == null) {
                //目标中途没了：把刀停在原地虚斩一记再回来，不无声消失
                if (!struck) {
                    Strike(Projectile.Center, bladeRotation, hasTarget: false);
                }
                BeginReturn();
                return;
            }

            Vector2 toTarget = target.Center - Projectile.Center;
            float distance = toTarget.Length();
            //EaseIn：越接近越快，最后一段是扑，不是匀速平移
            float ease = MathHelper.Clamp(phaseTimer / (float)LungeFrames, 0f, 1f);
            float speed = MathHelper.Lerp(26f, 78f, ease * ease);
            Vector2 dir = toTarget.SafeNormalize(Vector2.UnitX);
            bladeRotation = bladeRotation.AngleLerp(dir.ToRotation(), 0.4f);

            float reach = distance - (target.width + target.height) * 0.22f - ReachPad;
            if (reach <= speed || phaseTimer >= LungeFrames) {
                Projectile.Center = target.Center;
                Strike(target.Center, bladeRotation, hasTarget: true);
                BeginReturn();
                return;
            }
            Projectile.Center += dir * speed;
        }

        /// <summary>落刀：断斩由 <see cref="CrimsonRendCleave"/> 结算，本体只管飞</summary>
        private void Strike(Vector2 at, float angle, bool hasTarget) {
            struck = true;
            cutPoint = at;
            if (Projectile.IsOwnedByLocalPlayer()) {
                int damage = Math.Max(1, (int)(BaseWeaponDamage * OniMeiCombat.SelfCutDamageMul));
                CrimsonRendCleave.Fire(Owner, at, angle, damage, 4f, scale: 1.05f,
                    flip: Main.rand.NextBool() ? 1 : -1, Projectile.GetSource_FromAI(),
                    CleaveStyle.Plain);
            }
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.KatanaHit with {
                Pitch = 0.20f,
                Volume = hasTarget ? 0.85f : 0.42f,
            }, at);
            Owner.CWR()?.GetScreenShake(hasTarget ? 2.4f : 1.0f);
            Vector2 dir = angle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(at + perp * Main.rand.NextFloat(-16f, 16f),
                    perp * Main.rand.NextFloat(-7f, 7f) + dir * Main.rand.NextFloat(-2f, 2f),
                    PaperEdge, Main.rand.NextFloat(0.20f, 0.36f))
                    ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
            }
        }

        private void BeginReturn() {
            phase = Phase.Return;
            phaseTimer = 0;
            cutPoint = Projectile.Center;
        }

        /// <summary>回鞘：沿去路倒飞回手，到手即消，刀权同帧归还</summary>
        private void TickReturn() {
            float t = MathHelper.Clamp(phaseTimer / (float)ReturnFrames, 0f, 1f);
            Vector2 home = Owner.Center;
            //回程走 EaseOut，越近越慢，最后轻轻并回手心
            float ease = 1f - (1f - t) * (1f - t);
            Projectile.Center = Vector2.Lerp(cutPoint, home, ease);
            Vector2 back = (home - Projectile.Center).SafeNormalize(Vector2.UnitX);
            bladeRotation = bladeRotation.AngleLerp(back.ToRotation() + MathHelper.Pi, 0.22f);
            if (t >= 1f || Vector2.DistanceSquared(Projectile.Center, home) < 64f) {
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.35f, Volume = 0.40f }, home);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_OniInkDrop>(home + Main.rand.NextVector2Circular(10f, 12f),
                            Main.rand.NextVector2Circular(1.2f, 0.8f), InkDeep,
                            Main.rand.NextFloat(0.14f, 0.24f))
                            ?.Configure(Main.rand.Next(14, 22));
                    }
                }
                Projectile.Kill();
            }
        }

        private void PushGhost() {
            for (int i = GhostCapacity - 1; i > 0; i--) {
                ghostPos[i] = ghostPos[i - 1];
                ghostRot[i] = ghostRot[i - 1];
            }
            ghostPos[0] = Projectile.Center;
            ghostRot[0] = bladeRotation;
            if (ghostCount < GhostCapacity) {
                ghostCount++;
            }
        }

        private void PlayDrawCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.25f, Volume = 0.55f }, Projectile.Center);
            //抽离：刀根一圈墨屑被带起，读作"从手里挣出去"
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f;
                PRTLoader.NewParticle<PRT_OniInkDrop>(Owner.Center + ang.ToRotationVector2() * 10f,
                    ang.ToRotationVector2() * Main.rand.NextFloat(1.4f, 3f), InkDeep,
                    Main.rand.NextFloat(0.14f, 0.26f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)phase);
            writer.Write((short)phaseTimer);
            writer.Write(bladeRotation);
            writer.WriteVector2(cutPoint);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            byte rawPhase = reader.ReadByte();
            phase = rawPhase <= (byte)Phase.Return ? (Phase)rawPhase : Phase.Draw;
            phaseTimer = reader.ReadInt16();
            bladeRotation = reader.ReadSingle();
            cutPoint = reader.ReadVector2();
            initialized = true;
        }

        public override bool PreDraw(ref Color lightColor) {
            //速度残影在前，本体压在最上，读出"这把刀在飞"
            for (int i = ghostCount - 1; i >= 1; i--) {
                float fade = 0.34f * (1f - i / (float)GhostCapacity);
                DrawBlade(ghostPos[i], ghostRot[i], Color.White * fade, 0.94f);
            }
            DrawBlade(Projectile.Center, bladeRotation, Color.White, 1f);
            return false;
        }

        private void DrawBlade(Vector2 worldPos, float rotation, Color color, float scale) {
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 size = blade.Size();
            Vector2 origin = size * OniBladePose.HiltUV;
            Vector2 tip = size * OniBladePose.TipUV;
            //刀尖严格指向 rotation：与手持时同一套支点数学，飞出去也不走形
            float spriteAngle = (tip - origin).ToRotation();
            Main.EntitySpriteDraw(blade, worldPos - Main.screenPosition, null, color,
                rotation - spriteAngle, origin, scale, SpriteEffects.None);
        }
    }
}
