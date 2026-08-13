// ============================================================================
//DungeonworldFog.fx 深牢迷雾——世界锚定密度场雾（单 technique 双 pass）
//FogFilter: Filters.Scene 前景瘴气（拷屏合成）/ FogOverlay: PostDrawTiles 背景雾（预乘 AlphaBlend）
//密度住在世界坐标: s1 密度窗口纹理(rgb=受光雾色, a=密度, DungeonworldFogSim 每 2 tick 上传)
//直线算术、无分支、无 atan2、噪声全走绑定贴图（FNA3D 翻译纪律）
//uniform 是设备全局状态：两个调用点各自全参数重设，防跨调用残值串场
// ============================================================================

sampler uScreen : register(s0);   //滤镜通道=拷屏；覆盖通道=白像素画布（不采样）
sampler uDensity : register(s1);  //密度窗口纹理，LinearClamp
sampler uNoise : register(s2);    //Masking/PerlinNoise 512²，LinearWrap；G 通道实测域 0.22~0.776

float2 uScreenSize;   //目标像素尺寸
float2 uWorldScale;   //目标px→世界px 仿射（每通道各自矩阵求逆后上载）
float2 uWorldOffset;
float2 uFogOrigin;    //密度窗口原点（世界px，整雾元对齐）
float2 uFogUvMul;     //1/(容量雾元数×64px)
float4 uFogUvClamp;   //xy=min uv, zw=max uv（半 texel 内缩到实际窗口子矩形）
float2 uPhase;        //层间噪声去相相位（前后层错开防同相贴纸感）
float uTime;
float uLayerMul;      //本层不透明度系数（背景 0.78 / 前景 0.42）
float uPresence;      //全局淡入淡出

//共用求值：密度采样 + 3 倍频翻涌 + 受光雾色
float4 FogEval(float2 tpx) {
    float2 world = uWorldOffset + tpx * uWorldScale;
    float2 fuv = clamp((world - uFogOrigin) * uFogUvMul, uFogUvClamp.xy, uFogUvClamp.zw);
    float4 cell = tex2D(uDensity, fuv);

    //世界锚定滚动噪声：y 正向偏移=纹样上飘，x 交替横向翻涌
    float2 wuv = world * 0.00074 + uPhase;
    float n1 = tex2D(uNoise, wuv + float2(uTime * 0.006, uTime * 0.014)).g;
    float n2 = tex2D(uNoise, wuv * 2.17 + float2(-uTime * 0.011, uTime * 0.026)).g;
    float n3 = tex2D(uNoise, wuv * 4.63 + float2(uTime * 0.019, uTime * 0.043)).g;
    float turb = n1 * 0.50 + n2 * 0.32 + n3 * 0.18;
    //域校准：turb 理论域≈0.22~0.78（PerlinNoise G 实测），映到 0..1，禁高分位死阈值
    float tn = saturate((turb - 0.30) * 2.6);

    float a = saturate(cell.a * (0.45 + 1.15 * tn)) * uLayerMul * uPresence;
    float3 col = cell.rgb * (0.88 + 0.24 * tn);
    return float4(col, a);
}

//前景瘴气：拷屏合成。忽略 COLOR0（签名同 ScrapSiegeFilter——
//FilterManager 中间级把 ColorOfTheSkies 当顶点色传入，消费它会引入夜色二次压暗）
float4 PSFogFilter(float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uScreen, uv);
    float4 fog = FogEval(uv * uScreenSize);
    return float4(lerp(src.rgb, fog.rgb, fog.a), src.a);
}

//背景雾：预乘输出进 AlphaBlend 批（暗雾必须能压暗——加色批物理上画不出暗，全线预乘）
float4 PSFogOverlay(float2 uv : TEXCOORD0) : COLOR0 {
    float4 fog = FogEval(uv * uScreenSize);
    return float4(fog.rgb * fog.a, fog.a);
}

//注意：Filters.Scene 的 ScreenShaderData 按 pass 名（"FogFilter"）查表，不是 technique 名；
//传错名会在 ShaderData.Apply 空引用并连锁 FilterManager 半开批崩溃（ScrapSiege 2026-08 事故）
technique TechFog {
    pass FogFilter {
        PixelShader = compile ps_3_0 PSFogFilter();
    }
    pass FogOverlay {
        PixelShader = compile ps_3_0 PSFogOverlay();
    }
}
