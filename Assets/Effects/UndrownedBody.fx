// ============================================================================
// UndrownedBody.fx 不溺者躯体材质（泡胀尸青 + 锈橙镣痕 + 湿身水线）
// 精灵批 Immediate 消费：s0=原版日食鱼人帧表（SpriteBatch.Draw 自动上），s1=PerlinNoise
// 签名行为：尸肉明度重映射（暗部沉进尸青深处，高光泛白） / 锈斑贴解剖不贴屏幕 /
// 湿亮窄反射带缓慢巡身（圆形高光=塑料，窄带=湿皮） / 水线以下横向扰动+焦散光带+
// 线口一线水光（半淹的身体是被水"切开"的）
// uUvRect=当前帧 uv 矩形（帧表渗色双通道之一：一切采样钳进帧内，C# 侧另内缩 1px）
// uWaterV=水线在帧内的 v 坐标（>1.2 视为全干）
// 直线算术无分支；预乘输出进 AlphaBlend 批
// ============================================================================

float uTime;
float uSeed;
float4 uUvRect;     // x,y=帧左上 uv；z,w=帧 uv 跨度
float uWaterV;      // 水线 v（帧内 0~1；>1.2=全干）
float uWet;         // 湿亮强度 0~1
float uFlash;       // ≤2f 过曝拍（咆哮/受击）
float3 uColDeep;    // 尸青深部
float3 uColTeal;    // 尸青主体
float3 uColPale;    // 尸白高光
float3 uColRust;    // 锈橙

// PerlinNoise 实测值域 0.22~0.776，阈值一律先归一
sampler bodyTex : register(s0);
sampler noiseSamp : register(s1);

float nrm(float v) { return saturate((v - 0.22) / 0.556); }

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vColor : COLOR0) : COLOR0
{
    // 帧内局部坐标（锈斑/水线/扰动全在这套坐标里，跨帧稳定）
    float2 fuv = (uv - uUvRect.xy) / uUvRect.zw;

    // 水下横向扰动：越深摆得越宽，采样前钳回帧内防渗色
    float below = saturate((fuv.y - uWaterV) * 3.0);
    float wob = tex2D(noiseSamp, float2(fuv.y * 2.6 + uTime * 0.35, uSeed)).g - 0.5;
    float2 suv = uv;
    suv.x += wob * below * 0.012 * uUvRect.z;
    float2 lo = uUvRect.xy + uUvRect.zw * 0.004;
    float2 hi = uUvRect.xy + uUvRect.zw * 0.996;
    suv = clamp(suv, lo, hi);

    float4 src = tex2D(bodyTex, suv);

    // 明度重映射进尸青谱：暗部沉青，中部尸青，高光泛尸白
    float luma = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float3 col = lerp(uColDeep, uColTeal, saturate(luma * 1.35));
    col = lerp(col, uColPale, smoothstep(0.62, 0.95, luma) * 0.55);

    // 锈斑：低频噪声阈值贴帧局部坐标（镣痕锈渍长在身上不随屏幕漂）
    float rustN = tex2D(noiseSamp, fuv * float2(1.7, 2.3) + uSeed * 3.1).g;
    float rust = smoothstep(0.55, 0.8, nrm(rustN)) * smoothstep(0.25, 0.6, luma);
    col = lerp(col, uColRust * (0.5 + luma * 0.6), rust * 0.55);

    // 湿亮窄反射带：斜带缓慢巡身，只点亮高光区，水下不巡（水下有焦散）
    float band = fuv.x * 0.6 + fuv.y * 1.4 - uTime * 0.16 + uSeed;
    float sheen = smoothstep(0.42, 0.5, frac(band)) * smoothstep(0.58, 0.5, frac(band));
    col += uColPale * sheen * smoothstep(0.45, 0.85, luma) * uWet * (1.0 - below) * 0.30;

    // 水线以下：压暗 + 焦散横光带（噪声横向拉长）
    float caustN = tex2D(noiseSamp, float2(fuv.x * 1.1 + uTime * 0.1, fuv.y * 4.5 - uTime * 0.22 + uSeed)).r;
    float caustic = smoothstep(0.55, 0.85, nrm(caustN)) * below;
    col *= 1.0 - below * 0.34;
    col += uColTeal * caustic * 0.35;

    // 水线口一线水光：细缝 + 噪声碎化（整条实光带读作光剑，2026-08 沙盒毙过一版）
    float lineN = nrm(tex2D(noiseSamp, float2(fuv.x * 3.2 - uTime * 0.5, uSeed * 5.0)).r);
    float lineGlow = smoothstep(0.018, 0.0, abs(fuv.y - uWaterV)) * step(uWaterV, 1.2);
    col += uColPale * lineGlow * (0.12 + 0.26 * lineN);

    // 过曝拍：≤2f 的白闪，常态恒 0
    col = lerp(col, uColPale * 1.35, saturate(uFlash));

    // 预乘输出，光照与整体透明度由顶点色承载
    float a = src.a * vColor.a;
    return float4(col * vColor.rgb * a, a);
}

technique Technique1
{
    pass BodyPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
