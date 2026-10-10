Shader "Custom/AttackWarningEffect"
{
    Properties
    {
        // 主贴图：决定特效的形状（白色部分发光，黑色/透明部分不显示）
        _MainTex ("Shape Texture", 2D) = "white" {}
        
        // 颜色：使用 [HDR] 标签，允许你把亮度调得非常高来实现发光
        [HDR] _TintColor ("Tint Color", Color) = (3, 0, 0, 1)
        
        // 强度倍增器：方便在不改颜色的情况下直接调整亮度
        _Intensity ("Glow Intensity", Range(0, 50)) = 1.0
    }

    SubShader
    {
        // 1. 修改 Queue 为 Overlay，保证它最后绘制
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        
        Blend SrcAlpha One
        ZWrite Off
        ZTest Always
        Cull Off

        // 2. 新增这一行：关闭深度测试
        // Always = 总是通过测试（即总是显示在最前面，穿透墙壁和模型）
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;    // 接收来自粒子系统的颜色
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float _Intensity;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                
                // 关键点：将粒子系统的颜色(v.color) 与 Shader属性颜色(_TintColor) 相乘
                o.color = v.color * _TintColor * _Intensity;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 采样贴图颜色
                fixed4 texColor = tex2D(_MainTex, i.texcoord);
                
                // 2. 计算最终颜色：
                // 贴图颜色 * (粒子颜色 * HDR颜色 * 强度)
                // 我们只取贴图的 RGB 和 Alpha 参与计算
                fixed4 finalColor = texColor * i.color;
                
                // 3. 再次用贴图的 Alpha 乘以 RGB，确保透明区域完全不发光
                finalColor.rgb *= texColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}