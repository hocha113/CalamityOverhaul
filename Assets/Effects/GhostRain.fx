//鬼雨「召雨」湿墨阴幕，预乘 Alpha
//TechSky: 全屏冷灰青天幕，上重下轻+双层湿斑噪声缓漂+尸青渗色+
//         下缘一线非地平线的缝；直线算术+平 tex2D，无分支

float uTime;        //秒
float uSeed;        //风暴种子相位
float uIntensity;   //0-1 阴幕在场强度

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

float4 PSSky(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;

    //双层湿斑：大团慢漂+细纹反向，浸透宣纸上未干的水痕
    float n0 = tex2D(noiseSamp, uv * float2(2.1, 1.3)
        + float2(uSeed * 0.31, uTime * 0.007)).r;
    float n1 = tex2D(noiseSamp, uv * float2(5.4, 3.6)
        + float2(-uTime * 0.005, uSeed * 0.77)).r;
    float wet = n0 * 0.62 + n1 * 0.38;

    //冷灰青底：顶最沉，向下稍透
    float3 deep = float3(0.055, 0.070, 0.092);
    float3 pale = float3(0.125, 0.157, 0.188);
    float3 col = lerp(deep, pale, uv.y * 0.85);
    col *= 0.70 + 0.52 * wet;

    //尸青渗色，量极小
    col += float3(-0.006, 0.014, 0.012) * (n1 - 0.5);

    //缝：下方一条不是地平线的微光，被湿斑轻微顶起
    float seamY = 0.78 + (n0 - 0.5) * 0.05;
    float seam = exp2(-abs(uv.y - seamY) * 130.0);
    col += float3(0.10, 0.125, 0.13) * seam * (0.30 + 0.36 * n1);

    //上重下轻的覆盖，底部留出地形剪影
    float cover = saturate(0.60 - 0.26 * uv.y + 0.10 * wet);
    float a = saturate(uIntensity) * cover * vertexColor.a;
    return float4(col * a, a);
}

technique TechSky
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSSky();
    }
}
