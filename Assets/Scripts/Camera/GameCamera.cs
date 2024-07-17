#if UNITY_EDITOR
using AI4Animation;
using UnityEngine;

[ExecuteInEditMode]
public class GameCamera : MonoBehaviour {

    public Transform Target = null;
	public Vector3 SelfOffset = Vector3.zero;
	public Vector3 TargetOffset = Vector3.zero;
    public string AutoLocateEditorName = string.Empty;
    
    [Range(0f, 1f)] public float Smoothing = 0f;
    [Range(0f, 10f)] public float FOV = 1.5f;
    [Range(0f, 360f)] public float Angle = 0f;
    [Range(-4f, 4f)] public float TranslateX = 0f;

    private Camera Camera;

    private Vector3 PreviousTarget;

    void Awake()
    {
        Camera = GetComponent<Camera>();
        PreviousTarget = Target == null ? Vector3.zero : Target.position;
    }

    Transform LocateTarget()
    {
        if (AutoLocateEditorName == string.Empty) return null;
        var editors = GameObject.FindObjectsOfType<MotionEditor>();
        foreach (var e in editors)
        {
            if (e.name == AutoLocateEditorName) return e.GetSession().GetActor().transform;
        }
        return null;
    }

    void LateUpdate() {
        if (Target == null)
        {
            Target = LocateTarget();
            return;
        }
        Quaternion rotation = Quaternion.Euler(0f, Angle, 0f);
        Vector3 target = Vector3.Lerp(PreviousTarget, Target.position, 1f - Smoothing);
        PreviousTarget = target;
        transform.position = target + rotation * (FOV*SelfOffset);
        transform.LookAt(target + TargetOffset);
        transform.localPosition += transform.right * TranslateX;
    }

}
#endif