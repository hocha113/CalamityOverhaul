// ============================================================================
// QueenPrismBeam.fx 皇后棱镜折射光束
// UV.x 0光源→1棱晶节点 UV.y 横截面；Additive
// 材质=穿过水晶的圣光：白热芯 + 三色色散镶边 + 沿束晶闪
// 极角审计：全程笛卡尔UV，无 atan2
// ============================================================================

float4x4 transformMatrix;
float uTime;        //滚动时间
float fadeAlpha;    //整体透明度 0~1(含跑马灯亮度)
float uHueSeed;     //色相种子，区分并排光束
float seed;         //实例种子
float uBeamLen;     //光束世界长度，用于稳定噪声频率

// 噪声固定 s1：sampler_state 自动分配会落 s0，图元路径今日侥幸、批次路径必坏；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

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

//柔和棱彩调色板
float3 PrismHue(float t)
{
    return 0.72 + 0.28 * cos(6.28318 * (t + float3(0.0, 0.35, 0.68)));
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;                  //0 光源 → 1 节点
    float cross_ = (uv.y - 0.5) * 2.0;   //-1 ~ 1 横截面

    //噪声频率随实际长度稳定(约每300px一个周期)
    float lenScale = max(uBeamLen / 300.0, 0.5);

    //=========================================================
    //两端塑形：源端漏斗渐入，节点端聚成焦点(光被棱晶吸入)
    //=========================================================
    float srcTaper = lerp(0.30, 1.0, smoothstep(0.0, 0.12, along));
    float dstFocus = lerp(1.0, 0.16, smoothstep(0.80, 1.0, along));
    float taper = srcTaper * dstFocus;

    //主轴微扭(圣光稳定，幅度收敛)
    float wob = tex2D(noiseSamp, float2(along * lenScale * 0.8 - uTime * 1.1, seed)).r - 0.5;
    float axis = wob * 0.16 * (1.0 - along * 0.5);
    float d = abs(cross_ - axis) / max(taper, 0.05);

    //=========================================================
    //白热芯 + 三色色散镶边(棱镜身份)
    //=========================================================
    float hot = exp(-d * d * 300.0);
    float core = exp(-d * d * 60.0);

    //色散：RGB三层以不同横向偏移错开，越近节点越铺开(即将折射)
    float disperse = 0.10 + 0.22 * smoothstep(0.35, 1.0, along);
    float dR = abs(cross_ - axis - disperse) / max(taper, 0.05);
    float dG = abs(cross_ - axis) / max(taper, 0.05);
    float dB = abs(cross_ - axis + disperse) / max(taper, 0.05);
    float3 fringe;
    fringe.r = exp(-dR * dR * 90.0);
    fringe.g = exp(-dG * dG * 110.0);
    fringe.b = exp(-dB * dB * 90.0);

    //=========================================================
    //沿束晶闪：噪声阈值亮斑向节点行进
    //=========================================================
    float glintNoise = tex2D(noiseSamp, float2(along * lenScale * 2.6 - uTime * 2.4, cross_ * 0.5 + seed + 0.37)).r;
    float glint = smoothstep(0.78, 0.96, glintNoise) * core * 1.4;

    //推进脉冲：亮带自光源流向节点
    float pulse = frac(along * 2.2 - uTime * 2.8 + seed);
    float pulseGlow = exp(-pow((pulse - 0.5) * 4.0, 2.0)) * 0.45 * core;

    //外覆柔晕
    float halo = exp(-d * d * 3.4) * 0.4;

    //横向遮罩+端头交由taper处理
    float edgeMask = smoothstep(1.0, 0.72, abs(cross_));

    //=========================================================
    //调色
    //=========================================================
    float3 hue = PrismHue(uHueSeed);
    float3 hueB = PrismHue(uHueSeed + 0.4);
    float3 cCore = float3(1.0, 0.97, 0.92);

    float3 color = float3(0.0, 0.0, 0.0);
    color += cCore * hot * 1.25;
    color += hue * core * 0.85;
    color += fringe * 0.75;                    //色散原色镶边
    color += hueB * halo;
    color += cCore * glint;
    color += hue * pulseGlow;
    color *= edgeMask;

    float alpha = saturate(
          (hot * 0.95 + core * 0.65 + (fringe.r + fringe.g + fringe.b) * 0.22
        + halo * 0.4 + glint * 0.8 + pulseGlow * 0.45) * edgeMask
    );
    alpha *= fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass QueenPrismBeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
