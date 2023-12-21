using Mobile_SRP.Runtime;
using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(MobileRenderPipelineAsset))]
public class MobileRPLightEditor : LightEditor {

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        if (
            !settings.lightType.hasMultipleDifferentValues &&
            (LightType)settings.lightType.enumValueIndex == LightType.Spot
        )
        {
            settings.DrawInnerAndOuterSpotAngle();
            settings.ApplyModifiedProperties();
        }
    }
}