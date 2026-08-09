using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.Cinematics;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 插在地上的鬼雨伞，锚点=<see cref="Actor.Position"/>地表中心<br/>
    /// 靠近右键触发入雨演出（<see cref="OniRainWorldTransition"/> + <see cref="OniRainWorldCutscene"/>），交互纯本地。<br/>
    /// 雨世界里这把伞还在原地——再撑一次即深潜一层（<see cref="OniRainDescentTransition"/> +
    /// <see cref="OniRainDescentCutscene"/>），直到最深层。<br/>
    /// 贴图暂用原版雨伞占位，待专属美术替换。
    /// </summary>
    internal class OniRainWorldUmbrella : Actor
    {
        private const float InteractDistance = 150f;
        private const float DrawScale = 1.3f;

        //湿墨色板，与鬼雨体系一致
        private static readonly Color PaleTint = new(190, 205, 208);
        private static readonly Color MistDamp = new(58, 66, 70);

        private float swayTimer;
        private int dripTimer;
        private float promptAlpha;
        //触发确认帧：右键瞬间伞猛地一颤
        private int triggerKick;

        /// <summary>伞盖中心，交互距离与提示锚点</summary>
        public Vector2 CanopyAnchor => Position + new Vector2(0f, -34f);

        public override void OnSpawn(params object[] args) {
            Width = 48;
            Height = 72;
            //剔除扩张防半入屏弹出
            DrawExtendMode = 400;
            DrawLayer = ActorDrawLayer.AfterTiles;
            swayTimer = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            swayTimer += 0.016f;
            if (swayTimer > MathHelper.TwoPi) {
                swayTimer -= MathHelper.TwoPi;
            }

            if (Main.dedServ) {
                return;
            }

            if (triggerKick > 0) {
                triggerKick--;
            }

            Lighting.AddLight(CanopyAnchor, 0.10f, 0.14f, 0.15f);
            UpdateAmbience();
            UpdateInteraction();
        }

        /// <summary>演出躁动包络：入雨与深潜谁在演出谁说了算</summary>
        private static float CurrentAgitation => MathF.Max(
            OniRainWorldTransition.Active ? OniRainWorldTransition.UmbrellaAgitation : 0f,
            OniRainDescentTransition.Active ? OniRainDescentTransition.UmbrellaAgitation : 0f);

        //伞下漏雨与潮气，暗示伞底下藏着另一场雨；演出期间随躁动加密
        private void UpdateAmbience() {
            float agitation = CurrentAgitation;
            int interval = Math.Max(8, 42 - (int)((agitation + KickAmount) * 30f));

            dripTimer++;
            if (dripTimer >= interval) {
                dripTimer = 0;
                Vector2 pos = CanopyAnchor + new Vector2(Main.rand.NextFloat(-16f, 16f), 6f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                    new Vector2(0f, Main.rand.NextFloat(1.5f, 2.5f)),
                    PaleTint * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(20, 34), 0f);

                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        Position + new Vector2(Main.rand.NextFloat(-26f, 26f), -6f),
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.05f),
                        MistDamp * Main.rand.NextFloat(0.5f, 0.8f),
                        Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Main.rand.Next(80, 130));
                }
            }
        }

        private void UpdateInteraction() {
            Player player = Main.LocalPlayer;
            //雨世界外撑伞入雨；雨世界内且未达最深层，再撑一次深潜一层
            bool depthOpen = !OniRainWorldState.LocalIn
                || OniRainWorldState.LocalDepth < OniRainWorldState.MaxDepth;
            bool eligible = player != null && player.Alives()
                && depthOpen
                && !OniRainWorldTransition.Active
                && !OniRainDescentTransition.Active
                && !CutsceneDirector.IsPlaying;
            bool near = eligible && player.Center.Distance(CanopyAnchor) < InteractDistance;

            promptAlpha = MathHelper.Clamp(promptAlpha + (near && CanTrigger(player) ? 0.05f : -0.05f), 0f, 1f);

            if (near && promptAlpha > 0.5f && CanTrigger(player)
                && Main.mouseRight && Main.mouseRightRelease) {
                Trigger(player);
            }
        }

        private static bool CanTrigger(Player player)
            => !Main.mapFullscreen && !player.mouseInterface;

        private float KickAmount => triggerKick > 0 ? triggerKick / 26f : 0f;

        private void Trigger(Player player) {
            //触发确认帧：伞骨绷响 + 猛颤 + 伞沿甩出一圈水珠
            triggerKick = 26;
            SoundEngine.PlaySound(SoundID.Dig with {
                Pitch = 0.3f,
                Volume = 0.55f,
                MaxInstances = 3,
            }, CanopyAnchor);
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Pitch = -0.2f,
                Volume = 0.5f,
                MaxInstances = 3,
            }, CanopyAnchor);

            for (int i = 0; i < 9; i++) {
                float angle = -MathHelper.Pi * (0.1f + 0.8f * i / 8f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1.8f, 3.8f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    CanopyAnchor + new Vector2(Main.rand.NextFloat(-18f, 18f), 0f),
                    vel, PaleTint * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(18, 30), vel.X);
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Position + new Vector2(Main.rand.NextFloat(-20f, 20f), -8f),
                    new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -0.08f),
                    MistDamp * Main.rand.NextFloat(0.55f, 0.85f),
                    Main.rand.NextFloat(0.6f, 0.95f))
                    ?.Configure(Main.rand.Next(70, 110));
            }

            //运镜失败不致命，演出照走
            if (OniRainWorldState.LocalIn) {
                OniRainDescentTransition.Begin(player, Position);
                CutsceneDirector.Play<OniRainDescentCutscene>(player);
            }
            else {
                OniRainWorldTransition.Begin(player, Position);
                CutsceneDirector.Play<OniRainWorldCutscene>(player);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            Main.instance.LoadItem(ItemID.Umbrella);
            Texture2D umbrella = TextureAssets.Item[ItemID.Umbrella].Value;

            //演出期伞体颤抖：压镜段起颤、浮现段拉满，触发瞬间猛地一颤
            float agitation = CurrentAgitation;
            float shiver = (agitation * 0.05f + KickAmount * 0.09f)
                * MathF.Sin(Main.GlobalTimeWrappedHourly * 46f);
            float rotation = -0.13f + MathF.Sin(swayTimer) * 0.03f + shiver;

            //柄尾落在地表锚点
            Vector2 drawPos = Position - Main.screenPosition
                - new Vector2(0f, umbrella.Height * 0.5f * DrawScale - 4f);
            Vector2 origin = umbrella.Size() * 0.5f;

            //冷灰青软辉衬底
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(swayTimer * 2.4f) * 0.5f + 0.5f;
            Color backing = new Color(96, 122, 128) with { A = 0 } * (0.16f + pulse * 0.08f);
            spriteBatch.Draw(glow, drawPos, null, backing, 0f, glow.Size() / 2f,
                new Vector2(120f * DrawScale / glow.Width, 110f * DrawScale / glow.Height),
                SpriteEffects.None, 0f);

            //本体：环境光染向湿墨灰白，夜里保轮廓
            Color body = Lighting.GetColor((Position / 16f).ToPoint());
            body = Color.Lerp(body, PaleTint, 0.32f);
            spriteBatch.Draw(umbrella, drawPos, null, body, rotation, origin,
                DrawScale, SpriteEffects.None, 0f);

            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            DrawInteractPrompt(spriteBatch);
        }

        /// <summary>交互提示，柔光衬底+描边文字，湿墨冷灰青</summary>
        private void DrawInteractPrompt(SpriteBatch sb) {
            if (promptAlpha <= 0.01f) {
                return;
            }

            Vector2 textPos = CanopyAnchor - Main.screenPosition + new Vector2(0f, -64f);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            //雨世界内提示换成深潜口径
            string hint = OniRainWorldState.LocalIn
                ? OniRainWorldSystem.DescendHint.Value
                : OniRainWorldSystem.InteractHint.Value;
            Vector2 textSize = font.MeasureString(hint) * 0.9f;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;

            Vector2 backingScale = new((textSize.X + 50f) / glow.Width, (textSize.Y + 30f) / glow.Height);
            Color backingColor = new Color(70, 92, 98) with { A = 0 } * (promptAlpha * (0.32f + pulse * 0.1f));
            sb.Draw(glow, textPos, null, backingColor, 0f, glow.Size() / 2f, backingScale, SpriteEffects.None, 0f);

            Color textColor = new Color(214, 228, 230) * promptAlpha;
            Utils.DrawBorderString(sb, hint, textPos - textSize / 2f, textColor, 0.9f);

            float lineWidth = textSize.X * (0.7f + pulse * 0.25f);
            Vector2 linePos = textPos + new Vector2(0f, textSize.Y / 2f + 6f);
            Color lineColor = new Color(150, 176, 180) with { A = 0 } * (promptAlpha * 0.55f);
            sb.Draw(glow, linePos, null, lineColor, 0f, glow.Size() / 2f,
                new Vector2(lineWidth / glow.Width, 4f / glow.Height), SpriteEffects.None, 0f);
        }

        /// <summary>调试放伞：鼠标处向下吸附地面，清掉旧伞；仅单人/服务端权威</summary>
        internal static void DebugPlaceAt(Vector2 world) {
            if (VaultUtils.isClient) {
                Main.NewText("多人客户端不可直接放伞", Color.IndianRed);
                return;
            }

            //探地表
            int tileX = (int)(world.X / 16f);
            int tileY = (int)(world.Y / 16f);
            Vector2 ground = world;
            for (int i = 0; i < 120; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 40)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    ground = new Vector2(world.X, y * 16f);
                    break;
                }
            }

            foreach (OniRainWorldUmbrella actor in ActorLoader.GetActiveActors<OniRainWorldUmbrella>()) {
                ActorLoader.KillActor(actor.WhoAmI);
            }
            ActorLoader.NewActor<OniRainWorldUmbrella>(ground);
        }
    }
}
