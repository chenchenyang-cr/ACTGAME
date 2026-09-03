using UnityEngine;
 namespace CombatEditor
{	
	[AbilityEvent]
	[CreateAssetMenu(menuName = "AbilityEvents / DynamicTrail")]
	public class AbilityEventObj_DynamicTrail : AbilityEventObj
	{
	    public CharacterNode.NodeType BaseNode = CharacterNode.NodeType.WeaponBase;
	    public CharacterNode.NodeType TipNode = CharacterNode.NodeType.WeaponTip;
	    public Material TrailMat;
	    [Header("Trail Shape")]
	    [Min(3)] public int MaxFrame = 14;
	    public int StopMultiplier = 4;
	    [Range(2,8)]
	    public int TrailSubs = 2;

	    public enum DistortionAxis { U, V }

	    [Header("Trail Appearance")]
	    public DistortionAxis AirDistortionAxis = DistortionAxis.U;
	    [ColorUsage(true, true)] public Color TrailColor = new Color(0.55f, 0.85f, 1f, 0.5f);
	    [Min(0f)] public float Brightness = 1.5f;
	    public Texture2D TrailTexture;
	    public Vector2 TextureTiling = Vector2.one;
	    public float TextureScrollSpeed = 0.8f;
	    [Range(0.01f, 1f)] public float TailFade = 0.25f;
	    [UnityEngine.Serialization.FormerlySerializedAs("Alpha")]
	    [Range(0f, 1f)] public float Opacity = 0.72f;
        [Range(0f, 1f)] public float AirTintStrength = 0.35f;
	    [HideInInspector]
	    public int NUM_VERTICES = 12;
	
	    [System.Serializable]
	    public enum TrailBehavior { FlowUV, StaticUV }
	    //[SerializeField]
	    //public TrailBehavior uvMethod;
	
	    //Write the data you need here.
	    public override EventTimeType GetEventTimeType()
	    {
	        return EventTimeType.EventRange;
	    }
	    public override AbilityEventEffect Initialize()
	    {
	        return new AbilityEventEffect_DynamicTrail(this);
	    }
#if UNITY_EDITOR
	    public override AbilityEventPreview InitializePreview()
	    {
	        return new AbilityEventPreview_DynamicTrail(this);
	    }
#endif
	}
	//Write you logic here
	public partial class AbilityEventEffect_DynamicTrail : AbilityEventEffect
	{
	    DynamicTrailGenerator trail;
	    Transform _base;
	    Transform _tip;
	    DynamicTrailExecutor executor;
	    public override void StartEffect()
	    {
	        base.StartEffect();
	
	
	        _base = _combatController.GetNodeTranform(EventObj.BaseNode);
	        _tip = _combatController.GetNodeTranform(EventObj.TipNode);
	        if (_base == null || _tip == null)
	        {
	            return;
	        }
	        trail = new DynamicTrailGenerator(_base, _tip, EventObj, AbilityEventObj_DynamicTrail.TrailBehavior.FlowUV);
	        trail.InitTrailMesh();
	        executor = trail._trailMeshObj.AddComponent<DynamicTrailExecutor>();
	        executor.trail = trail;
	        executor.StartTrail();
	
	    }
	    public override void EffectRunning()
	    {
	        base.EffectRunning();
	    }
	    public override void EndEffect()
	    {
	        if (executor != null)
	        {
	            executor.StopTrail();
	        }
	        base.EndEffect();
	    }
	}
	
	public partial class AbilityEventEffect_DynamicTrail : AbilityEventEffect
	{
	    AbilityEventObj_DynamicTrail EventObj => (AbilityEventObj_DynamicTrail)_EventObj;
	    public AbilityEventEffect_DynamicTrail(AbilityEventObj InitObj) : base(InitObj)
	    {
	        _EventObj = InitObj;
	    }
	}
}
