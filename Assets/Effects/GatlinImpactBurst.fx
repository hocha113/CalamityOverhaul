// ============================================================================
//GatlinImpactBurst.fx 加特林子弹命中爆点着色器
//通过SpriteBatch绘制单个UV 0..1的方形Quad，在Quad内用距离场做程序化爆点
//包含白炽核心闪光、冲击环、放射状高频火花丝、沿命中方向定向的热浪
//ps_3_0 / vs_3_0
// ============================================================================

float  uProgress;    //0=爆发开始 1=彻底消散
float  uIntensity;   //外部倍率，用于减弱/增强
float2 uDirection;   //命中弹道单位向量，用于定向加权
texture uNoiseTex;

sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU  = wrap;
    AddressV  = wrap;
};

#define PI 3.14159265

struct PSInput
{
    float4 Position  : POSITION0;
    float4 Color     : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    //以0.5为中心的极坐标，r 0..约1.4（角落）
    float2 uv = input.TexCoords - 0.5;
    float r   = length(uv) * 2.0;
    float ang = atan2(uv.y, uv.x);

    float life = saturate(uProgress);
    float inv  = 1.0 - life;

    //1. 中心白炽闪光：寿命前段最亮，指数衰减
    float flashLife = saturate(1.0 - life * 2.4);
    float flash     = pow(saturate(1.0 - r * 2.3), 4.0) * flashLife;

    //2. 冲击环：半径随寿命扩张，厚度随扩张变薄
    float ringR     = life * 0.95;
    float ringW     = lerp(0.22, 0.05, life);
    float ring      = exp(-pow((r - ringR) / ringW, 2.0)) * inv;

    //3. 放射状火花丝：沿角度采样噪声，在头几帧密集之后散开
    float angUV      = (ang + PI) / (2.0 * PI);
    float2 sUV1 = float2(angUV * 3.0, life * 0.25);
    float2 sUV2 = float2(angUV * 9.0 + 0.37, life * 0.55 + 0.11);
    float sNoise1   = tex2D(noiseSamp, sUV1).r;
    float sNoise2   = tex2D(noiseSamp, sUV2).r;
    float spikeMask = pow(sNoise1, 5.0) * pow(sNoise2, 2.0);
    //火花丝仅在环之外延展，越远越弱
    float spikeRadial = smoothstep(life * 1.6 + 0.2, 0.05, r) * smoothstep(0.02, 0.18, r);
    float spikes      = spikeMask * spikeRadial * inv;

    //4. 定向热浪：沿命中反方向偏置椭圆，制造不对称爆点
    float2 dir = uDirection;
    //沿dir方向拉伸，垂直方向收窄，得到与物块接触的扁平热浪
    float along      = dot(uv, dir);
    float side       = dot(uv, float2(-dir.y, dir.x));
    float heatR      = sqrt((along * 0.55) * (along * 0.55) + (side * 1.4) * (side * 1.4));
    float heat       = exp(-heatR * heatR * 18.0) * inv;
    //沿反弹方向偏置（-along越大越靠"后方"，越亮）
    heat *= smoothstep(-0.15, -0.55, along) * 0.6 + 0.4;

    //5. 总边界淡出：超出范围的像素完全剔除避免方块边
    float fade = smoothstep(1.0, 0.75, r);

    //颜色调色
    float3 hotWhite  = float3(1.50, 1.40, 1.18);
    float3 amber     = float3(1.10, 0.60, 0.20);
    float3 deepEmber = float3(0.95, 0.25, 0.06);

    float3 col = 0;
    col += hotWhite  * flash  * 3.8;
    col += amber     * ring   * 2.5;
    col += deepEmber * ring   * ring * 2.0;
    col += amber     * spikes * 3.0;
    col += deepEmber * spikes * spikes * 3.5;
    col += amber     * heat   * 1.2;
    col += hotWhite  * heat   * heat * 1.6;

    col *= fade * uIntensity;

    float a = saturate(flash * 1.6 + ring + spikes + heat) * fade * uIntensity;
    return float4(col, a);
}

technique Technique1
{
    pass P0
    {
        PixelShader  = compile ps_3_0 PixelShaderFunction();
    }
};
