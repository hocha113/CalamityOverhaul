using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Presentation;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    /// <summary>
    /// 沈幽全身立绘：面部差分补丁叠绘 + 黑雨汇聚入场（<see cref="ShenyoPortraitRainRenderer"/>）
    /// </summary>
    internal class ShenyoFullBodyPortrait : FullBodyPortraitBase
    {
        /// <summary>表情补丁对位矫正值，随立绘原图定死</summary>
        private static readonly Vector2 FaceOffset = new(88f, 58f);
        private readonly ShenyoPortraitRainRenderer rainRenderer = new();

        public override string PortraitKey => "ShenyoFullBody";

        protected override float FadeInDuration => 20f;

        internal Face currentFace;

        internal enum Face
        {
            None,
            Calm,      //常态平静
            CloseEye,  //闭目
            Murmur,    //轻语小口
            Smile,     //含笑开口
            Shock,     //惊愕张口
            Wry,       //抿嘴浅笑
            Scrutiny,  //眯眼打量
            Parted,    //微张软惊
            Pensive,   //抿嘴沉思
            Lidded,    //半阖冷眼
        }

        protected override void OnInitialize() {
            rainRenderer.Stop();
            scale = 1.2f;
            currentFace = Face.None;
        }

        internal void StartRainAssembly() {
            SkipFadeIn();
            rainRenderer.Start(ADVAsset.Shenyo);
            BlockDialogueAdvance = true;
            //起手一记压低的落水声，雨已经在下了
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Pitch = -0.55f,
                Volume = 0.45f,
                MaxInstances = 3,
            });
        }

        protected override void OnUpdate() {
            scale = 1.2f;
            drawColor = Color.White;
            if (rainRenderer.Update((OwnerDialogue?.ShowProgress ?? 0f) >= 0.92f)) {
                BlockDialogueAdvance = false;
                //定形收尾：轻一声水面收拢
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Pitch = 0.15f,
                    Volume = 0.35f,
                    MaxInstances = 3,
                });
            }
        }

        protected override void OnDraw(SpriteBatch spriteBatch, float alpha) {
            Texture2D portrait = ADVAsset.Shenyo;
            if (portrait == null || portrait.IsDisposed || OwnerDialogue == null) {
                return;
            }

            position = OwnerDialogue.GetPanelRect().Top() + new Vector2(-160f, -portrait.Height + 100f) * scale;
            Texture2D faceTexture = currentFace switch {
                Face.Calm => ADVAsset.Shenyo_Calm,
                Face.CloseEye => ADVAsset.Shenyo_CloseEye,
                Face.Murmur => ADVAsset.Shenyo_Murmur,
                Face.Smile => ADVAsset.Shenyo_Smile,
                Face.Shock => ADVAsset.Shenyo_Shock,
                Face.Wry => ADVAsset.Shenyo_Wry,
                Face.Scrutiny => ADVAsset.Shenyo_Scrutiny,
                Face.Parted => ADVAsset.Shenyo_Parted,
                Face.Pensive => ADVAsset.Shenyo_Pensive,
                Face.Lidded => ADVAsset.Shenyo_Lidded,
                _ => null,
            };

            if (rainRenderer.Draw(spriteBatch, portrait, faceTexture, FaceOffset,
                position, scale, rotation, drawColor, alpha)) {
                return;
            }

            Color color = drawColor * alpha;
            spriteBatch.Draw(portrait, position, null, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            if (faceTexture != null && !faceTexture.IsDisposed) {
                Vector2 facePosition = position + FaceOffset.RotatedBy(rotation) * scale;
                spriteBatch.Draw(faceTexture, facePosition, null, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        protected override void OnDeactivate() {
            rainRenderer.Stop();
            BlockDialogueAdvance = false;
        }
    }
}
