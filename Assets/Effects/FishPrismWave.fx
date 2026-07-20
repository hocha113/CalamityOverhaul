// ============================================================================
//FishPrismWave.fx 棱彩冲击波波前：冷白发丝弧线 + 极窄前红后蓝色散边 + 波后暗干涉纹 + 弧端噪声撕裂
//几何：quad 局部坐标由 uv 展开，+x=行进方向，波前弧线 = 圆 SDF（length），d=0 落在 uFrontFrac 竖线上
//极角审计：全程笛卡尔坐标与 length()，无 atan2/theta/phi 消费，无缝隙风险
//混合：BlendState.AlphaBlend（预乘约定），rgb=加色光，a=覆盖；暗干涉纹只贡献 a 实现压暗
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float2 uSizePx;     //quad 像素尺寸
float uFrontFrac;   //波前线在 uv.x 上的位置 0..1
float uR;           //波前弧曲率半径 px
float uSpanY;       //弧的半展宽 px
float uDisp;        //色散边偏移 px
float3 uColLead;    //前缘色散色（红侧）
float3 uColCore;    //波前主体色（冷白 / 谱色）
float3 uColTrail;   //后缘色散色（蓝侧）
float uCoreGain;    //发丝线亮度增益（呼吸）
float uFade;        //整体包络 0..1
float uDark;        //暗干涉纹强度 0..1
float uTime;        //流动相位
float uSeed;        //每弹幕相位种子

texture uNoiseTex;
sampler noiseSamp = sampler_state
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

//高斯细线剖面
float gaussLine(float d, float sigma)
{
    float x = d / sigma;
    return exp(-x * x);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = float2((uv.x - uFrontFrac) * uSizePx.x, (uv.y - 0.5) * uSizePx.y);

    //波前弧：圆心在行进反方向 uR 处，d=0 恰好穿过原点，|y| 越大弧越向后弯
    float d = length(p - float2(-uR, 0.0)) - uR;

    //弧端撕裂包络：噪声调制端点消隐，禁平滑收口
    float yt = abs(p.y) / uSpanY;
    float tipN = tex2D(noiseSamp, float2(p.y * 0.008 + uSeed * 3.7, uSeed + uTime * 0.22)).r;
    float span = smoothstep(1.0, 0.42 + 0.3 * tipN, yt);

    //沿弧微闪烁：波前亮度的相位颗粒（笛卡尔输入，安全）
    float shim = 0.82 + 0.36 * tex2D(noiseSamp, float2(p.y * 0.012 + uTime * 0.6, uSeed * 5.3)).r;

    //三条发丝线：前缘色散 / 主体 / 后缘色散（透镜色差式 RGB 分离）
    float sigma = 1.6;
    float lLead = gaussLine(d - uDisp, sigma * 1.3);
    float lCore = gaussLine(d, sigma) * shim;
    float lTrail = gaussLine(d + uDisp, sigma * 1.3);

    //波后余迹：短指数尾被噪声撕碎，波前之前一无所有（锐利前缘）
    float wakeN = tex2D(noiseSamp, float2(p.x * 0.006 - uTime * 0.9 + uSeed, p.y * 0.006 + uSeed * 7.1)).r;
    float wake = exp(d * 0.055) * step(d, 0.0) * (0.30 + 0.70 * wakeN);

    //暗干涉纹：紧贴亮线后方的一道暗带，只压暗不发光
    float dk = gaussLine(d + uDisp * 3.4, sigma * 2.6) * uDark;

    float3 col = uColLead * lLead + uColCore * (lCore * uCoreGain) + uColTrail * lTrail;
    col += (uColTrail * 0.10 + uColCore * 0.05) * wake;

    float lum = lLead * 0.6 + lCore * min(uCoreGain, 1.2) + lTrail * 0.6 + wake * 0.13;
    float alpha = saturate(lum) * 0.9 + dk * 0.5;

    col *= span * uFade;
    alpha *= span * uFade;
    return float4(col, saturate(alpha)) * input.Color.a;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
