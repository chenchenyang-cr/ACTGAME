// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "LTY/ShaderNew/CanYing"
{
	Properties
	{
		[Header(OutLine________________________________________________________________________________)]_OutLine_Power("OutLine_Power", Float) = 0
		[Enum(Additive,1,AlphaBlend,10)]_Dst("材质模式", Float) = 1
		_OutLine_Tex("OutLine_Tex", 2D) = "white" {}
		[HDR]_OutLine_Color("OutLine_Color ", Color) = (0,0,0,0)
		_OutLineTex_ST("OutLineTex_ST", Vector) = (1,1,0,0)
		_OutLineTex_Panner("OutLineTex_Panner", Vector) = (0,0,0,1)
		_OutAlpha("OutAlpha", Float) = 1
		_OutAlphaMultiply("OutAlphaMultiply", Float) = 1
		[Enum(ON,0,OFF,1)]_OUT_offset_Switch("OUT_offset_Switch", Float) = 1
		_OutLineSmooth_Offset("OutLineSmooth_Offset", Float) = 0
		_OutLineSmooth_min("OutLineSmooth_min", Float) = 0
		_OutLineSmooth_max("OutLineSmooth_max", Float) = 1
		[Header(ScreenTex________________________________________________________________________________)]_ScreenTex("ScreenTex", 2D) = "white" {}
		[HDR]_ScreenTexColor("ScreenTexColor", Color) = (1,1,1,1)
		_Main_Alpha("Main_Alpha", Float) = 1
		_MainAlphaAdd("MainAlphaAdd", Float) = 0
		[HDR]_MainColorDiSE("MainColorDiSE", Color) = (0,0,0,0)
		_ScreenTex_Scale_Offset("ScreenTex_Scale_Offset", Vector) = (1,1,0,0)
		_ScreenTex_Panner("ScreenTex_Panner", Vector) = (0,0,0,1)
		_MainColorMaskSacle("MainColorMaskSacle", Float) = 1
		_MainColorMaskPower("MainColorMaskPower", Float) = 1
		[HDR][Header(FreColor________________________________________________________________________________)]_FreColor("FreColor", Color) = (1,1,1,0)
		_FrePower("FrePower", Float) = 1
		_FreScale("FreScale", Float) = 1
		[KeywordEnum(X,Y,Z)] _Offset_Direction("Offset_Direction", Float) = 0
		_FreVPmaskOffset("FreVPmaskOffset", Float) = 0
		_MaskSmoothMin("MaskSmoothMin", Float) = 0
		_MaskSmoothMax("MaskSmoothMax", Float) = 1
		[Header(DissovleTex)]_DissovleTex("DissovleTex", 2D) = "white" {}
		[Toggle(_USE_DISSLOVE_ON)] _use_disslove("use_disslove", Float) = 0
		[Enum(R,0,A,1)]_DissovleA_R("DissovleA_R", Float) = 0
		[Enum(OFF,0,ON,1)]_DissSC("Diss,S/C", Float) = 0
		_DissovleTex_Scale_Offset("DissovleTex_Scale_Offset", Vector) = (1,1,0,0)
		_DissovleTex_Panner("DissovleTex_Panner", Vector) = (0,0,0,1)
		_smooth("smooth", Range( 0.5 , 1)) = 0.5
		_Disspower("Disspower", Range( 0 , 1)) = 0
		[Header(LINE________________________________________________________________________________)]_SmoothMaxP("轮廓线强度", Float) = 1
		[HDR]_Color0("轮廓线颜色", Color) = (1,1,1,1)
		_SmoothMin("罗阔线SmoothMin", Float) = 0
		_SmoothMax("罗阔线SmoothMax", Float) = 1
		[Header(AlphaFre________________________________________________________________________________)]_AlphaFrepower("AlphaFrepower", Float) = 1
		_AlphaFreScale("AlphaFreScale", Float) = 0.5

	}
	
	SubShader
	{
		LOD 0

		Tags { "RenderType"="Opaque" }
		
		Pass
		{
			
			Name "First"
			CGINCLUDE
			#pragma target 3.0
			ENDCG
			Blend SrcAlpha [_Dst]
			AlphaToMask Off
			Cull Back
			ColorMask RGBA
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"
			#define ASE_NEEDS_FRAG_POSITION
			#pragma shader_feature_local _OFFSET_DIRECTION_X _OFFSET_DIRECTION_Y _OFFSET_DIRECTION_Z
			#pragma shader_feature _USE_DISSLOVE_ON


			struct appdata
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				float3 ase_normal : NORMAL;
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
			};

			uniform float _Dst;
			uniform sampler2D _ScreenTex;
			uniform float4 _ScreenTex_Panner;
			uniform float4 _ScreenTex_Scale_Offset;
			uniform float4 _ScreenTexColor;
			uniform float4 _MainColorDiSE;
			uniform float _MainColorMaskPower;
			uniform float _MainColorMaskSacle;
			uniform float _FrePower;
			uniform float _FreScale;
			uniform float4 _FreColor;
			uniform float _MaskSmoothMin;
			uniform float _MaskSmoothMax;
			uniform float _FreVPmaskOffset;
			uniform float _SmoothMin;
			uniform float _SmoothMax;
			uniform float _SmoothMaxP;
			uniform float4 _Color0;
			uniform float _AlphaFrepower;
			uniform float _AlphaFreScale;
			uniform float _MainAlphaAdd;
			uniform float _Main_Alpha;
			uniform float _smooth;
			uniform sampler2D _DissovleTex;
			uniform float4 _DissovleTex_Panner;
			uniform float4 _DissovleTex_Scale_Offset;
			uniform float _DissovleA_R;
			uniform float _Disspower;
			uniform float _DissSC;
			inline float4 ASE_ComputeGrabScreenPos( float4 pos )
			{
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				float4 o = pos;
				o.y = pos.w * 0.5f;
				o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
				return o;
			}
			

			
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				float4 ase_clipPos = UnityObjectToClipPos(v.vertex);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord = screenPos;
				float3 ase_worldNormal = UnityObjectToWorldNormal(v.ase_normal);
				o.ase_texcoord1.xyz = ase_worldNormal;
				float3 ase_worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				o.ase_texcoord2.xyz = ase_worldPos;
				
				o.ase_texcoord3 = v.vertex;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord1.w = 0;
				o.ase_texcoord2.w = 0;
				
				v.vertex.xyz +=  float3(0,0,0) ;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{
				fixed4 finalColor;
				float mulTime76 = _Time.y * _ScreenTex_Panner.z;
				float2 appendResult75 = (float2(_ScreenTex_Panner.x , _ScreenTex_Panner.y));
				float4 screenPos = i.ase_texcoord;
				float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( screenPos );
				float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
				float2 appendResult65 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
				float2 appendResult70 = (float2(_ScreenTex_Scale_Offset.x , _ScreenTex_Scale_Offset.y));
				float2 appendResult72 = (float2(_ScreenTex_Scale_Offset.z , _ScreenTex_Scale_Offset.w));
				float2 panner66 = ( mulTime76 * appendResult75 + (appendResult65*appendResult70 + appendResult72));
				float3 ase_worldNormal = i.ase_texcoord1.xyz;
				float3 ase_worldPos = i.ase_texcoord2.xyz;
				float3 ase_worldViewDir = UnityWorldSpaceViewDir(ase_worldPos);
				ase_worldViewDir = normalize(ase_worldViewDir);
				float dotResult35 = dot( ase_worldNormal , ase_worldViewDir );
				float temp_output_37_0 = saturate( dotResult35 );
				float temp_output_38_0 = ( 1.0 - temp_output_37_0 );
				float temp_output_85_0 = ( pow( temp_output_38_0 , _MainColorMaskPower ) * _MainColorMaskSacle );
				float4 lerpResult83 = lerp( ( tex2D( _ScreenTex, panner66 ) * _ScreenTex_Panner.w * _ScreenTexColor ) , _MainColorDiSE , temp_output_85_0);
				#if defined(_OFFSET_DIRECTION_X)
				float staticSwitch206 = i.ase_texcoord3.xyz.x;
				#elif defined(_OFFSET_DIRECTION_Y)
				float staticSwitch206 = i.ase_texcoord3.xyz.y;
				#elif defined(_OFFSET_DIRECTION_Z)
				float staticSwitch206 = i.ase_texcoord3.xyz.z;
				#else
				float staticSwitch206 = i.ase_texcoord3.xyz.x;
				#endif
				float smoothstepResult98 = smoothstep( _MaskSmoothMin , _MaskSmoothMax , ( staticSwitch206 + _FreVPmaskOffset ));
				float smoothstepResult138 = smoothstep( _SmoothMin , _SmoothMax , temp_output_38_0);
				float mulTime163 = _Time.y * _DissovleTex_Panner.z;
				float2 appendResult164 = (float2(_DissovleTex_Panner.x , _DissovleTex_Panner.y));
				float2 appendResult157 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
				float2 appendResult158 = (float2(_DissovleTex_Scale_Offset.x , _DissovleTex_Scale_Offset.y));
				float2 appendResult161 = (float2(_DissovleTex_Scale_Offset.z , _DissovleTex_Scale_Offset.w));
				float2 panner160 = ( mulTime163 * appendResult164 + (appendResult157*appendResult158 + appendResult161));
				float4 tex2DNode115 = tex2D( _DissovleTex, panner160 );
				float lerpResult120 = lerp( tex2DNode115.r , tex2DNode115.a , _DissovleA_R);
				float lerpResult122 = lerp( _Disspower , 0.0 , _DissSC);
				float smoothstepResult133 = smoothstep( ( 1.0 - _smooth ) , _smooth , saturate( ( ( ( lerpResult120 * _DissovleTex_Panner.w ) + 1.0 ) - ( lerpResult122 * 2.0 ) ) ));
				#ifdef _USE_DISSLOVE_ON
				float staticSwitch135 = smoothstepResult133;
				#else
				float staticSwitch135 = 1.0;
				#endif
				float4 appendResult80 = (float4(( lerpResult83 + ( pow( temp_output_38_0 , _FrePower ) * _FreScale * _FreColor * smoothstepResult98 ) + ( smoothstepResult138 * _SmoothMaxP * _Color0 ) ).rgb , ( ( ( temp_output_85_0 + ( pow( temp_output_37_0 , _AlphaFrepower ) * _AlphaFreScale ) + _MainAlphaAdd ) * _Main_Alpha ) * staticSwitch135 )));
				
				
				finalColor = appendResult80;
				return finalColor;
			}
			ENDCG
		}

		
		Pass
		{
			Name "Second"
			
			CGINCLUDE
			#pragma target 3.0
			ENDCG
			Blend SrcAlpha [_Dst]
			AlphaToMask Off
			Cull Front
			ColorMask RGBA
			ZWrite On
			ZTest LEqual
			
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"
			#pragma shader_feature _USE_DISSLOVE_ON


			struct appdata
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				float3 ase_normal : NORMAL;
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_OUTPUT_STEREO
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
			};

			uniform float _Dst;
			uniform float _OutLine_Power;
			uniform float4 _OutLine_Color;
			uniform sampler2D _OutLine_Tex;
			uniform float4 _OutLineTex_Panner;
			uniform float4 _OutLineTex_ST;
			uniform float _OutAlpha;
			uniform float _OutAlphaMultiply;
			uniform float _smooth;
			uniform sampler2D _DissovleTex;
			uniform float4 _DissovleTex_Panner;
			uniform float4 _DissovleTex_Scale_Offset;
			uniform float _DissovleA_R;
			uniform float _Disspower;
			uniform float _DissSC;
			uniform float _OutLineSmooth_min;
			uniform float _OutLineSmooth_max;
			uniform float _OutLineSmooth_Offset;
			uniform float _OUT_offset_Switch;
			inline float4 ASE_ComputeGrabScreenPos( float4 pos )
			{
				#if UNITY_UV_STARTS_AT_TOP
				float scale = -1.0;
				#else
				float scale = 1.0;
				#endif
				float4 o = pos;
				o.y = pos.w * 0.5f;
				o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
				return o;
			}
			

			
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				float4 ase_clipPos = UnityObjectToClipPos(v.vertex);
				float4 screenPos = ComputeScreenPos(ase_clipPos);
				o.ase_texcoord = screenPos;
				
				o.ase_texcoord1 = v.vertex;
				
				v.vertex.xyz += ( _OutLine_Power * v.ase_normal );
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{
				fixed4 finalColor;
				float mulTime187 = _Time.y * _OutLineTex_Panner.z;
				float2 appendResult188 = (float2(_OutLineTex_Panner.x , _OutLineTex_Panner.y));
				float4 screenPos = i.ase_texcoord;
				float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( screenPos );
				float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
				float2 appendResult190 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
				float2 appendResult191 = (float2(_OutLineTex_ST.x , _OutLineTex_ST.y));
				float2 appendResult185 = (float2(_OutLineTex_ST.z , _OutLineTex_ST.w));
				float2 panner193 = ( mulTime187 * appendResult188 + (appendResult190*appendResult191 + appendResult185));
				float4 appendResult200 = (float4(( _OutLine_Color * ( tex2D( _OutLine_Tex, panner193 ) * _OutLineTex_Panner.w ) ).rgb , _OutAlpha));
				float mulTime163 = _Time.y * _DissovleTex_Panner.z;
				float2 appendResult164 = (float2(_DissovleTex_Panner.x , _DissovleTex_Panner.y));
				float2 appendResult157 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
				float2 appendResult158 = (float2(_DissovleTex_Scale_Offset.x , _DissovleTex_Scale_Offset.y));
				float2 appendResult161 = (float2(_DissovleTex_Scale_Offset.z , _DissovleTex_Scale_Offset.w));
				float2 panner160 = ( mulTime163 * appendResult164 + (appendResult157*appendResult158 + appendResult161));
				float4 tex2DNode115 = tex2D( _DissovleTex, panner160 );
				float lerpResult120 = lerp( tex2DNode115.r , tex2DNode115.a , _DissovleA_R);
				float lerpResult122 = lerp( _Disspower , 0.0 , _DissSC);
				float smoothstepResult133 = smoothstep( ( 1.0 - _smooth ) , _smooth , saturate( ( ( ( lerpResult120 * _DissovleTex_Panner.w ) + 1.0 ) - ( lerpResult122 * 2.0 ) ) ));
				#ifdef _USE_DISSLOVE_ON
				float staticSwitch135 = smoothstepResult133;
				#else
				float staticSwitch135 = 1.0;
				#endif
				float smoothstepResult177 = smoothstep( _OutLineSmooth_min , _OutLineSmooth_max , ( i.ase_texcoord1.xyz.x + _OutLineSmooth_Offset ));
				float lerpResult201 = lerp( smoothstepResult177 , 1.0 , _OUT_offset_Switch);
				
				
				finalColor = ( ( appendResult200 * _OutAlphaMultiply ) * staticSwitch135 * lerpResult201 );
				return finalColor;
			}
			ENDCG
		}
		
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18935
403;527;1328;600;5025.738;-1940.894;1;True;False
Node;AmplifyShaderEditor.GrabScreenPosition;156;-4417.837,3770.469;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;155;-4385.967,3946.554;Inherit;False;Property;_DissovleTex_Scale_Offset;DissovleTex_Scale_Offset;33;0;Create;True;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;162;-3942.378,4065.928;Inherit;False;Property;_DissovleTex_Panner;DissovleTex_Panner;34;0;Create;True;0;0;0;False;0;False;0,0,0,1;0,0,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;158;-4096.619,3924.272;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;161;-4097.15,4023.358;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;157;-4098.442,3823.117;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;163;-3678.255,4154.278;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;164;-3669.533,4049.815;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;159;-3842.225,3840.666;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GrabScreenPosition;189;1712.345,645.7269;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;102;-4059.166,3168.249;Inherit;False;2440.182;539.9002;溶解;29;135;133;132;130;129;128;126;124;123;122;121;120;119;117;116;115;114;112;111;110;109;108;107;106;105;104;103;118;165;;1,1,1,1;0;0
Node;AmplifyShaderEditor.PannerNode;160;-3414.359,3832.317;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;194;1744.345,821.7269;Inherit;False;Property;_OutLineTex_ST;OutLineTex_ST;4;0;Create;True;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;191;2032.345,805.7269;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;185;2012.646,942.3916;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;115;-3394.669,3199.353;Inherit;True;Property;_DissovleTex;DissovleTex;29;1;[Header];Create;True;1;DissovleTex;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector4Node;186;1924.286,1106.621;Inherit;False;Property;_OutLineTex_Panner;OutLineTex_Panner;5;0;Create;True;0;0;0;False;0;False;0,0,0,1;0,0,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;190;2032.345,709.7269;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;116;-3262.721,3384.671;Inherit;False;Property;_DissovleA_R;DissovleA_R;31;1;[Enum];Create;True;0;2;R;0;A;1;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;188;2183.167,1037.249;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;192;2288.346,725.7269;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;120;-3079.644,3228.243;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;117;-3437.447,3558.168;Inherit;False;Property;_DissSC;Diss,S/C;32;1;[Enum];Create;True;0;2;OFF;0;ON;1;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;114;-3732.577,3458.683;Inherit;False;Property;_Disspower;Disspower;37;0;Create;True;0;0;0;False;0;False;0;0.35;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;187;2173.485,1151.411;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;121;-3072.292,3400.453;Inherit;False;Constant;_Float3;Float 3;10;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;119;-3252.977,3589.238;Inherit;False;Constant;_Float7;Float 7;11;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;193;2602.814,781.5566;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;122;-3267.047,3458.068;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;165;-2931.557,3223.922;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;170;2911.65,820.5782;Inherit;True;Property;_OutLine_Tex;OutLine_Tex;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;123;-2806.426,3223.82;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;124;-3082.117,3475.644;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;126;-2698.981,3223.499;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PosVertexDataNode;168;2550.388,1243.398;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;128;-2843.121,3483.574;Inherit;False;Property;_smooth;smooth;36;0;Create;True;0;0;0;False;0;False;0.5;0.75;0.5;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;171;3373.327,731.848;Inherit;False;Property;_OutLine_Color;OutLine_Color ;3;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;169;2535.343,1403.244;Inherit;False;Property;_OutLineSmooth_Offset;OutLineSmooth_Offset;9;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;195;3253.369,916.7395;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;173;2762.545,1543.248;Inherit;False;Property;_OutLineSmooth_min;OutLineSmooth_min;10;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;130;-2569.073,3222.342;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;176;3775.975,891.3141;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode;129;-2572.384,3309.105;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;172;2775.474,1620.476;Inherit;False;Property;_OutLineSmooth_max;OutLineSmooth_max;11;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;174;2775.553,1262.107;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;181;3645.919,998.9054;Inherit;False;Property;_OutAlpha;OutAlpha;6;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;132;-2416.806,3208.007;Inherit;False;Constant;_Float8;Float 8;14;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;200;4055.504,922.8975;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SmoothstepOpNode;133;-2435.251,3282.85;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;177;3105.519,1300.053;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;203;3364.848,1602.031;Inherit;False;Property;_OUT_offset_Switch;OUT_offset_Switch;8;1;[Enum];Create;True;0;2;ON;0;OFF;1;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;175;3982.755,1039.013;Inherit;False;Property;_OutAlphaMultiply;OutAlphaMultiply;7;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;202;3344.159,1358.887;Inherit;False;Constant;_Float0;Float 0;43;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;101;-4684.169,1878.133;Inherit;False;1610.242;748;世界坐标下半身菲尼尔;12;100;99;98;94;95;92;91;93;97;96;90;206;;1,1,1,1;0;0
Node;AmplifyShaderEditor.NormalVertexDataNode;180;4111.583,1481.098;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;201;3653.118,1230.767;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;88;-4261.906,963.5866;Inherit;False;900.0201;488.4696;菲尼尔;5;33;36;35;37;38;;1,1,1,1;0;0
Node;AmplifyShaderEditor.StaticSwitch;135;-2195.924,3273.514;Inherit;False;Property;_use_disslove;use_disslove;30;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;178;4333.534,974.072;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;179;4191.525,1392.117;Inherit;False;Property;_OutLine_Power;OutLine_Power;0;1;[Header];Create;True;1;OutLine________________________________________________________________________________;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;118;-2516.411,3548.044;Inherit;False;864.7548;118.4831;软粒子;4;134;131;127;125;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;89;-3197.414,-95.04452;Inherit;False;2378.835;797.6553;屏幕贴图;19;87;86;85;84;76;75;74;72;70;73;83;82;77;67;78;66;68;65;64;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;97;-4382.25,2322.501;Inherit;False;Property;_FreVPmaskOffset;FreVPmaskOffset;26;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;146;-2203.219,1031.875;Inherit;False;Property;_SmoothMin;罗阔线SmoothMin;42;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;74;-2910.209,464.698;Inherit;False;Property;_ScreenTex_Panner;ScreenTex_Panner;18;0;Create;True;0;0;0;False;0;False;0,0,0,1;0,0,0,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DotProductOpNode;35;-3996.78,1097.863;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;75;-2702.563,398.9854;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SaturateNode;37;-3746.174,1097.863;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;84;-1843.997,487.9562;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;204;3817.703,1042.709;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;110;-3818.985,3359.089;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ColorNode;95;-3584.2,1997.657;Inherit;False;Property;_FreColor;FreColor;21;2;[HDR];[Header];Create;True;1;FreColor________________________________________________________________________________;0;0;False;0;False;1,1,1,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;134;-1834.11,3587.314;Inherit;False;soft;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;93;-3760.762,2510.191;Inherit;False;Property;_FrePower;FrePower;22;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;125;-2391.309,3586.453;Inherit;False;Property;_soft;soft;24;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;68;-2546.15,83.8035;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;1,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;83;-1083.58,104.0651;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;96;-4085.156,2138.762;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;151;-2695.442,1570.017;Inherit;False;Property;_AlphaFreScale;AlphaFreScale;45;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;86;-2114.333,598.8315;Inherit;False;Property;_MainColorMaskPower;MainColorMaskPower;20;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;87;-1835.365,594.6717;Inherit;False;Property;_MainColorMaskSacle;MainColorMaskSacle;19;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;33;-4209.687,1013.587;Inherit;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleTimeNode;76;-2690.287,494.8495;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;79;-256.3995,840.8596;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;131;-1976.6,3594.776;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;112;-3582.059,3234.044;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.WireNode;147;-1670.141,2043.85;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;103;-4038.542,3547.586;Inherit;False;Property;_DissovleRotator;DissovleRotator;35;0;Create;True;0;0;0;False;0;False;0;90;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;150;-3141.571,1517.28;Inherit;False;Property;_AlphaFrepower;AlphaFrepower;44;1;[Header];Create;True;1;AlphaFre________________________________________________________________________________;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;91;-3559.789,2185.551;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;108;-4012.515,3438.374;Inherit;False;Property;_Dissovle_V_speed;Dissovle_V_speed;39;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;109;-4016.377,3226.317;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;85;-1554.605,483.2884;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;36;-4211.905,1222.057;Inherit;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;92;-3276.318,2202.44;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;3;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.GrabScreenPosition;64;-3122.15,3.803511;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;144;-1932.829,1256.928;Inherit;False;Property;_Color0;轮廓线颜色;41;1;[HDR];Create;False;0;0;0;False;0;False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;106;-4009.4,3352.194;Inherit;False;Property;_Dissovle_U_speed;Dissovle_U_speed;38;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;77;-1571.724,104.621;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;107;-3687.429,3556.501;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;152;-1031.255,1121.652;Inherit;True;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;94;-3487.979,2387.424;Inherit;False;Property;_FreScale;FreScale;23;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;111;-3794.051,3229.797;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode;38;-3559.882,1097.863;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;78;-1866.691,305.6427;Inherit;False;Property;_ScreenTexColor;ScreenTexColor;13;1;[HDR];Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;104;-4039.796,3621.275;Inherit;False;Constant;_Float13;Float 13;13;0;Create;True;0;0;0;False;0;False;180;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PosVertexDataNode;90;-4583.583,2030.049;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SmoothstepOpNode;138;-1907.784,1039.549;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;127;-2221.558,3573.236;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;148;-2929.518,1496.887;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;145;-2200.893,1148.182;Inherit;False;Property;_SmoothMax;罗阔线SmoothMax;43;0;Create;False;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;66;-2162.15,51.80352;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;67;-1919.435,58.96208;Inherit;True;Property;_ScreenTex;ScreenTex;12;1;[Header];Create;True;1;ScreenTex________________________________________________________________________________;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;143;-1910.829,1176.928;Inherit;False;Property;_SmoothMaxP;轮廓线强度;40;1;[Header];Create;False;1;LINE________________________________________________________________________________;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;98;-3760.001,2172.921;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;153;-766.2725,1120.208;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;73;-3090.15,179.8035;Inherit;False;Property;_ScreenTex_Scale_Offset;ScreenTex_Scale_Offset;17;0;Create;True;0;0;0;False;0;False;1,1,0,0;1,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;80;680.5767,918.1786;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode;65;-2802.15,67.8035;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;99;-4048.001,2380.921;Inherit;False;Property;_MaskSmoothMin;MaskSmoothMin;27;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;105;-3864.276,3555.43;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;72;-2821.849,300.4682;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;149;-2457.25,1491.254;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;154;-1269.159,1250.279;Inherit;False;Property;_MainAlphaAdd;MainAlphaAdd;15;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;182;4573.408,1462.03;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;136;-216.38,1155.411;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;205;5033.759,1144.114;Inherit;False;Property;_Dst;材质模式;1;1;[Enum];Create;False;0;2;Additive;1;AlphaBlend;10;0;True;0;False;1;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;70;-2802.15,163.8035;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ColorNode;82;-1430.853,178.9555;Inherit;False;Property;_MainColorDiSE;MainColorDiSE;16;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;100;-4035.17,2451.418;Inherit;False;Property;_MaskSmoothMax;MaskSmoothMax;28;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;81;-972.01,1354.725;Inherit;False;Property;_Main_Alpha;Main_Alpha;14;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;199;4566.689,1012.88;Inherit;False;3;3;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.StaticSwitch;206;-4327.243,2061.79;Inherit;False;Property;_Offset_Direction;Offset_Direction;25;0;Create;True;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;3;X;Y;Z;Create;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;141;-1596.749,1012.714;Inherit;True;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;166;863.9122,924.4959;Float;False;True;-1;2;ASEMaterialInspector;0;11;LTY/ShaderNew/CanYing;003dfa9c16768d048b74f75c088119d8;True;First;0;0;First;2;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;RenderType=Opaque=RenderType;False;False;0;True;True;2;5;False;-1;10;True;205;0;1;False;-1;0;False;-1;True;0;False;-1;0;False;-1;False;False;False;False;False;False;False;False;False;True;0;False;-1;True;True;0;False;-1;True;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;0;True;2;False;0;;0;0;Standard;0;0;2;True;True;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;167;4714.835,1039.197;Float;False;False;-1;2;ASEMaterialInspector;0;11;New Amplify Shader;003dfa9c16768d048b74f75c088119d8;True;Second;0;1;Second;2;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;RenderType=Opaque=RenderType;False;False;0;True;True;2;5;False;-1;10;True;205;0;1;False;-1;0;False;-1;True;0;False;-1;0;False;-1;False;False;False;False;False;False;False;False;False;True;0;False;-1;True;True;1;False;-1;False;True;True;True;True;True;0;False;-1;False;False;False;False;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;True;1;False;-1;True;3;False;-1;True;False;0;False;-1;0;False;-1;True;0;True;2;False;0;;0;0;Standard;0;False;0
WireConnection;158;0;155;1
WireConnection;158;1;155;2
WireConnection;161;0;155;3
WireConnection;161;1;155;4
WireConnection;157;0;156;1
WireConnection;157;1;156;2
WireConnection;163;0;162;3
WireConnection;164;0;162;1
WireConnection;164;1;162;2
WireConnection;159;0;157;0
WireConnection;159;1;158;0
WireConnection;159;2;161;0
WireConnection;160;0;159;0
WireConnection;160;2;164;0
WireConnection;160;1;163;0
WireConnection;191;0;194;1
WireConnection;191;1;194;2
WireConnection;185;0;194;3
WireConnection;185;1;194;4
WireConnection;115;1;160;0
WireConnection;190;0;189;1
WireConnection;190;1;189;2
WireConnection;188;0;186;1
WireConnection;188;1;186;2
WireConnection;192;0;190;0
WireConnection;192;1;191;0
WireConnection;192;2;185;0
WireConnection;120;0;115;1
WireConnection;120;1;115;4
WireConnection;120;2;116;0
WireConnection;187;0;186;3
WireConnection;193;0;192;0
WireConnection;193;2;188;0
WireConnection;193;1;187;0
WireConnection;122;0;114;0
WireConnection;122;2;117;0
WireConnection;165;0;120;0
WireConnection;165;1;162;4
WireConnection;170;1;193;0
WireConnection;123;0;165;0
WireConnection;123;1;121;0
WireConnection;124;0;122;0
WireConnection;124;1;119;0
WireConnection;126;0;123;0
WireConnection;126;1;124;0
WireConnection;195;0;170;0
WireConnection;195;1;186;4
WireConnection;130;0;126;0
WireConnection;176;0;171;0
WireConnection;176;1;195;0
WireConnection;129;0;128;0
WireConnection;174;0;168;1
WireConnection;174;1;169;0
WireConnection;200;0;176;0
WireConnection;200;3;181;0
WireConnection;133;0;130;0
WireConnection;133;1;129;0
WireConnection;133;2;128;0
WireConnection;177;0;174;0
WireConnection;177;1;173;0
WireConnection;177;2;172;0
WireConnection;201;0;177;0
WireConnection;201;1;202;0
WireConnection;201;2;203;0
WireConnection;135;1;132;0
WireConnection;135;0;133;0
WireConnection;178;0;200;0
WireConnection;178;1;175;0
WireConnection;35;0;33;0
WireConnection;35;1;36;0
WireConnection;75;0;74;1
WireConnection;75;1;74;2
WireConnection;37;0;35;0
WireConnection;84;0;38;0
WireConnection;84;1;86;0
WireConnection;204;0;181;0
WireConnection;204;1;81;0
WireConnection;110;0;106;0
WireConnection;110;1;108;0
WireConnection;134;0;131;0
WireConnection;68;0;65;0
WireConnection;68;1;70;0
WireConnection;68;2;72;0
WireConnection;83;0;77;0
WireConnection;83;1;82;0
WireConnection;83;2;85;0
WireConnection;96;0;206;0
WireConnection;96;1;97;0
WireConnection;76;0;74;3
WireConnection;79;0;83;0
WireConnection;79;1;147;0
WireConnection;79;2;141;0
WireConnection;131;0;127;0
WireConnection;112;0;111;0
WireConnection;112;2;110;0
WireConnection;147;0;92;0
WireConnection;91;0;38;0
WireConnection;91;1;93;0
WireConnection;85;0;84;0
WireConnection;85;1;87;0
WireConnection;92;0;91;0
WireConnection;92;1;94;0
WireConnection;92;2;95;0
WireConnection;92;3;98;0
WireConnection;77;0;67;0
WireConnection;77;1;74;4
WireConnection;77;2;78;0
WireConnection;107;0;105;0
WireConnection;107;1;104;0
WireConnection;152;0;85;0
WireConnection;152;1;149;0
WireConnection;152;2;154;0
WireConnection;111;0;109;0
WireConnection;111;2;107;0
WireConnection;38;0;37;0
WireConnection;138;0;38;0
WireConnection;138;1;146;0
WireConnection;138;2;145;0
WireConnection;127;0;125;0
WireConnection;148;0;37;0
WireConnection;148;1;150;0
WireConnection;66;0;68;0
WireConnection;66;2;75;0
WireConnection;66;1;76;0
WireConnection;67;1;66;0
WireConnection;98;0;96;0
WireConnection;98;1;99;0
WireConnection;98;2;100;0
WireConnection;153;0;152;0
WireConnection;153;1;81;0
WireConnection;80;0;79;0
WireConnection;80;3;136;0
WireConnection;65;0;64;1
WireConnection;65;1;64;2
WireConnection;105;0;103;0
WireConnection;72;0;73;3
WireConnection;72;1;73;4
WireConnection;149;0;148;0
WireConnection;149;1;151;0
WireConnection;182;0;179;0
WireConnection;182;1;180;0
WireConnection;136;0;153;0
WireConnection;136;1;135;0
WireConnection;70;0;73;1
WireConnection;70;1;73;2
WireConnection;199;0;178;0
WireConnection;199;1;135;0
WireConnection;199;2;201;0
WireConnection;206;1;90;1
WireConnection;206;0;90;2
WireConnection;206;2;90;3
WireConnection;141;0;138;0
WireConnection;141;1;143;0
WireConnection;141;2;144;0
WireConnection;166;0;80;0
WireConnection;167;0;199;0
WireConnection;167;1;182;0
ASEEND*/
//CHKSM=27204340CFD3BBF864F000D81EA509E5D47928F8