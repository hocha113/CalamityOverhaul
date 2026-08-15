//KikasaReset.fx 鬼伞大范围重启的照片定格与雨痕冲刷合成
//TechReset: 定格帧照片化（去饱和银盐冷调+胶片颗粒+晕影），
//           雨痕冲刷遮罩自上而下把照片刷掉露出实时画面（前沿一条水线亮边），
//           倒带段实时画面加冷调+扫描线+微幅横向回卷抖动。
//直线算术+平 tex2D，无分支；s0=实时屏幕帧 s1=定格照片帧 s2=PerlinNoise

float uTime;    //秒
float uWash;    //0-1 冲刷进度：0=照片全覆盖，1=照片刷尽
float uRewind;  //0-1 倒带冷调/扫描线强度
float uSeed;    //本场种子，各端同值错开雨痕图案
float uAspect;  //宽/高

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);

static const float3 LUMA = float3(0.299, 0.587, 0.114);
static const float3 SILVER_TINT = float3(0.94, 0.99, 1.05);  //银盐冷灰
static const float3 REWIND_TINT = float3(0.86, 0.95, 1.10);  //倒带冷调乘色
static const float3 SHINE_COL   = float3(0.35, 0.42, 0.46);  //冲刷前沿水光

float4 PSReset(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;

    //====== 实时画面（倒带侧） ======
    //回卷横向抖动：按行噪声，幅度极小
    float jitter = (tex2D(uImage2, float2(uv.y * 3.1, uTime * 0.9)).r - 0.5)
        * 0.006 * uRewind;
    float3 live = tex2D(uImage0, float2(saturate(uv.x + jitter), uv.y)).rgb;
    //冷调 + 去一点饱和
    float liveG = dot(live, LUMA);
    live = lerp(live, lerp(live, liveG.xxx, 0.25) * REWIND_TINT, uRewind);
    //扫描线微调制
    live *= 1.0 - uRewind * 0.05 * (0.5 + 0.5 * sin(uv.y * 900.0 + uTime * 24.0));

    //====== 照片层（定格帧照片化） ======
    float3 photo = tex2D(uImage1, uv).rgb;
    float tone = pow(saturate(dot(photo, LUMA)), 0.92);
    float3 photoCol = tone * SILVER_TINT;
    //胶片颗粒：噪声图错帧采样
    float grain = tex2D(uImage2,
        uv * float2(6.0 * uAspect, 6.0) + float2(uSeed, uTime * 7.0)).r;
    photoCol += (grain - 0.5) * 0.10;
    //晕影
    float2 d = uv - 0.5;
    photoCol *= 1.0 - dot(d, d) * 0.85;

    //====== 雨痕冲刷遮罩：每列冲刷速度由噪声决定，前沿从屏顶扫到屏底 ======
    float col0 = tex2D(uImage2, float2(uv.x * 5.0 * uAspect + uSeed * 0.13, 0.15)).r;
    float col1 = tex2D(uImage2, float2(uv.x * 17.0 * uAspect - uSeed * 0.07, 0.62)).r;
    float streak = col0 * 0.7 + col1 * 0.3;
    //基础偏置压住 uWash=0 的屏顶，超扫 1.45 保证慢列也刷尽
    float front = uWash * 1.45 - 0.06;
    float edge = front - uv.y - streak * 0.30;
    //细水丝拉毛前沿
    float wisp = tex2D(uImage2,
        float2(uv.x * 40.0 * uAspect + uSeed, uv.y * 1.5 - uWash * 2.0)).r;
    edge += (wisp - 0.5) * 0.08;
    float mask = saturate(edge * 14.0);   //1=已刷掉露实时

    //前沿水线亮边：窄带反光，冲刷进行中才有
    float shine = saturate(1.0 - abs(edge) * 22.0)
        * saturate(uWash * 8.0) * (1.0 - uWash * 0.75);

    float3 colOut = lerp(photoCol, live, mask) + shine * SHINE_COL;
    return float4(colOut, 1.0);
}

technique TechReset
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSReset();
    }
}
