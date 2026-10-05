sampler2D uImage0 : register(s0);

// Polar mapping: source U wraps around the center, source V runs center to edge.
float2 Center = float2(0.5, 0.5);
float AspectRatio = 1.0;
float AngularTiling = 1.0;
float RadialTiling = 1.0;
float Rotation = 0.0;
// 旋转源贴图UV；PI / 2可以在放射线与同心圆之间切换
float TextureRotation = 0.0;
float FlowOffset = 0.0;
float EdgeFade = 0.02;
//只能选择0或者1，0表示不裁剪，1表示裁剪为圆形
bool ClipOutside = true;
float4 EffectColor = float4(1.0, 1.0, 1.0, 1.0);

static const float TwoPi = 6.28318530718;

float4 PolarFlowPS(float2 uv : TEXCOORD0, float4 inputColor : COLOR0) : COLOR0
{
    float2 fromCenter = uv - Center;
    fromCenter.x *= AspectRatio;

    float radius = length(fromCenter) * 2.0;
    float angle = atan2(fromCenter.y, fromCenter.x) - Rotation;

    float2 polarUV;
    polarUV.x = angle / TwoPi * AngularTiling;
    // Increasing FlowOffset moves texture features away from the center.
    polarUV.y = radius * RadialTiling - FlowOffset;

    float textureCos = cos(TextureRotation);
    float textureSin = sin(TextureRotation);
    float2 textureUV = polarUV - 0.5;
    polarUV = float2(
        textureUV.x * textureCos - textureUV.y * textureSin,
        textureUV.x * textureSin + textureUV.y * textureCos
    ) + 0.5;

    float4 color = tex2D(uImage0, frac(polarUV));

    float fadeWidth = max(EdgeFade, 0.0001);
    float circleAlpha = 1.0 - smoothstep(1.0 - fadeWidth, 1.0, radius);
    color *= lerp(1.0, circleAlpha, saturate(ClipOutside?1:0));

    return color * inputColor * EffectColor;
}

technique Technique1
{
    pass Apply
    {
        PixelShader = compile ps_3_0 PolarFlowPS();
    }
}
