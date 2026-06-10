// ============================================================================
// NeutronGravityWell.fx —— 洛希之弦引力井着色器
// 右键蓄力时在搭箭点凝聚的微型引力井：
// 旋转的吸积流螺旋 + 向心坠入丝线 + 光子环 + 暗芯
// 画布为以引力井为中心的正方形白图，uv(0.5,0.5)为井心
// intensity: 0~1 蓄力程度，控制整体规模与亮度
// ============================================================================

float uTime;          //时间
float intensity;      //0~1 蓄力进度
float fadeAlpha;      //整体透明度
float3 coreColor;     //光子环颜色（亮蓝白）
float3 diskColor;     //吸积流颜色（蓝紫）
float3 edgeColor;     //外缘颜色（深蓝）

sampler uNoiseTex : register(s1);

struct VSOutput {
    float4 Position : SV_Position;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

float4 PSGravityWell(VSOutput input) : COLOR0 {
    float2 centered = input.UV - 0.5;
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);

    //引力井整体随蓄力扩大
    float wellRadius = 0.10 + intensity * 0.30;
    //核心半径（事件视界）
    float coreRadius = wellRadius * 0.22;

    //========== (A) 吸积流螺旋 ==========
    //对数螺旋坐标：角度随半径扭曲，旋转随时间推进
    float swirl = angle + dist * 14.0 - uTime * (2.5 + intensity * 4.0);
    float spiralUVx = swirl / 6.2832;
    float spiralN = tex2D(uNoiseTex, float2(spiralUVx * 2.0, dist * 3.0 - uTime * 0.8)).r;

    //螺旋流带：噪声脊线形成断续的流光
    float streak = smoothstep(0.42, 0.78, spiralN);
    //只在核心外、井半径内出现，向两端衰减
    float diskMask = smoothstep(coreRadius, coreRadius * 2.2, dist)
                   * smoothstep(wellRadius, wellRadius * 0.55, dist);
    float disk = streak * diskMask;

    //========== (B) 向心坠入丝线 ==========
    float fallN = tex2D(uNoiseTex, float2(angle / 6.2832 + 0.5, dist * 6.0 + uTime * 2.2)).r;
    float threads = smoothstep(0.55, 0.9, fallN)
                  * smoothstep(wellRadius * 1.4, wellRadius * 0.5, dist)
                  * smoothstep(coreRadius, coreRadius * 2.0, dist);

    //========== (C) 光子环 ==========
    float ringDist = abs(dist - coreRadius * 1.45);
    float photonRing = smoothstep(coreRadius * 0.45, 0.0, ringDist);
    //蓄力满时光子环搏动
    photonRing *= 0.8 + 0.2 * sin(uTime * 10.0) * intensity;

    //========== (D) 暗芯 ==========
    //核心内部把所有光都吃掉（加色混合下表现为空洞）
    float darkCore = smoothstep(coreRadius * 0.9, coreRadius * 1.3, dist);

    //========== (E) 外缘引力晕 ==========
    float halo = smoothstep(wellRadius * 1.5, wellRadius * 0.8, dist)
               * smoothstep(wellRadius * 0.4, wellRadius * 0.9, dist) * 0.35;

    //合成
    float3 color = float3(0.0, 0.0, 0.0);
    color += diskColor * disk * (0.9 + intensity * 0.8);
    color += lerp(diskColor, coreColor, 0.55) * threads * 0.7;
    color += coreColor * photonRing * (0.8 + intensity * 1.2);
    color += edgeColor * halo;

    color *= darkCore;

    float alpha = saturate(disk + threads * 0.6 + photonRing + halo);
    alpha *= darkCore;
    alpha *= fadeAlpha * (0.35 + intensity * 0.65);

    //画布边缘柔化
    alpha *= smoothstep(0.5, 0.38, dist);

    return float4(color * alpha, alpha);
}

technique NeutronGravityWellPass {
    pass P0 {
        PixelShader = compile ps_3_0 PSGravityWell();
    }
}
