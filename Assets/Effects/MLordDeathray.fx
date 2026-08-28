// ============================================================================
//MLordDeathray.fx 幻影死光（月总）
//UV.x 1光源→0末端 UV.y 横截面；Additive
//材质=幻影星质：星尘流沿束漂移、引力缘光在边界弯折、相位明灭
//无电弧无机械脉冲，与三机械光束划清界限
// ============================================================================

float4x4 transformMatrix;
float uTime;        //滚动时间
float fadeAlpha;    //整体透明度 0~1
float seed;         //实例种子，错开多束相位
float rootPinch;    //根部收窄量 0~0.95：束身增幅时根部保持原宽（喇叭外扩），0=全束等宽（默认）

//噪声显式钉在 s1：不吃 fxc 自动分配（s0 会被 SpriteBatch 画布贴图覆写）
texture uNoiseTex;
sampler noiseSamp : register(s1) = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;                  //1 光源 → 0 末端
    float cross_ = (uv.y - 0.5) * 2.0;   //-1 ~ 1 横截面

    //根部保宽：近源 14% 段横截面按 1-rootPinch 收窄（束身胀大时口部锚在本体上不跟涨）
    float widthScale = 1.0 - rootPinch * smoothstep(0.86, 1.0, along);
    cross_ /= max(widthScale, 0.05);

    //=========================================================
    //末端撕散：远端前沿被噪声撕成星雾，不留平切口
    //=========================================================
    float tipTurb = tex2D(noiseSamp, float2(along * 2.8 + uTime * 1.1, cross_ * 0.6 + seed + 0.5)).r - 0.5;
    float alongTip = along + tipTurb * 0.16;
    float tailFade = smoothstep(0.0, 0.34, alongTip);
    //末端纺锤收窄 + 光源端喇叭收口
    float taper = lerp(0.26, 1.0, smoothstep(0.0, 0.30, alongTip));
    float rootTaper = lerp(0.38, 1.0, smoothstep(1.0, 0.90, along));
    taper = min(taper, rootTaper);

    //=========================================================
    //主轴呼吸：低频噪声漂移，束身像被引力缓缓拉拽
    //=========================================================
    float sway = tex2D(noiseSamp, float2(along * 1.6 - uTime * 0.9, seed)).r - 0.5;
    float axis = sway * 0.34 * (1.0 - along * 0.6);
    float d = abs(cross_ - axis) / taper;

    //束体三层：宽晕/体/月白芯
    float halo = exp(-d * d * 2.6) * 0.5;
    float body = exp(-d * d * 34.0);
    float hot = exp(-d * d * 380.0);

    //=========================================================
    //星尘流：两倍频噪声阈成稀疏亮粒，沿束向光源漂（吸积方向）
    //=========================================================
    float grainA = tex2D(noiseSamp, float2(along * 6.5 + uTime * 1.8, cross_ * 1.7 + seed)).r;
    float grainB = tex2D(noiseSamp, float2(along * 11.0 + uTime * 3.1, cross_ * 2.9 + seed + 0.37)).r;
    float grains = smoothstep(0.62, 0.95, grainA * 0.6 + grainB * 0.55);
    grains *= exp(-d * d * 14.0);

    //=========================================================
    //引力缘光：束边界的细亮丝，随噪声波动（光在界面弯折）
    //=========================================================
    float rim = abs(abs(cross_ - axis) / taper - 0.62);
    float rimWave = tex2D(noiseSamp, float2(along * 3.2 - uTime * 1.4, seed + 0.71)).r - 0.5;
    float fringe = exp(-pow((rim + rimWave * 0.12) * 26.0, 2.0)) * 0.8;

    //=========================================================
    //相位明灭：整体亮度的呼吸（星质不恒亮）
    //=========================================================
    float phase = 0.9 + 0.1 * sin(uTime * 9.0 + seed * 21.0 + along * 6.0);

    //光源口辉：向光源汇聚成点
    float rootFlare = smoothstep(0.82, 1.0, along) * body * 1.3;

    //=========================================================
    //调色板：深空紫外缘 / 幽蓝青体 / 月白芯
    //=========================================================
    float3 cViolet = float3(0.32, 0.22, 0.62);
    float3 cTeal = float3(0.33, 0.90, 0.83);
    float3 cWhite = float3(0.86, 0.95, 1.00);

    float edgeMask = smoothstep(1.0, 0.80, abs(cross_));
    float bodyMask = tailFade * edgeMask * phase;

    float3 color = float3(0, 0, 0);
    color += cViolet * halo * 1.05;
    color += cTeal * body * 0.95;
    color += cWhite * hot * 0.9;
    color += cWhite * grains * 0.85;
    color += cTeal * fringe;
    color *= bodyMask;
    color += cWhite * rootFlare * 0.8;
    color += cTeal * rootFlare * 0.5;

    float alpha = saturate(
          (halo * 0.4 + body * 0.75 + hot * 0.9 + grains * 0.55 + fringe * 0.5) * bodyMask
        + rootFlare * 0.9
    );
    alpha *= fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass MLordDeathrayPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
