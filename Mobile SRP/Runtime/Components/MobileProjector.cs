using UnityEngine;

[ExecuteInEditMode]
public class MobileProjector : MonoBehaviour
{
    [SerializeField] private Vector3 projectorOffset;
    [SerializeField] private float size = 10f;
    [SerializeField] private Vector3 boxRotation;
    [SerializeField] private float boxSize;
    
    private Decal m_Decal;

    private void OnEnable()
    {
        DecalRenderer.AddDecal(this, m_Decal);
    }

    private void OnDisable()
    {
        DecalRenderer.RemoveDecal(this);
    }

    private void Update()
    {
        if (!transform.hasChanged) return;
        UpdateProjector();
        transform.hasChanged = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Update the projector matrix in edit mode
        UpdateProjector();
        //draw the projector angle
        Gizmos.color = Color.green;
        Vector3 position = transform.position;
        Gizmos.matrix = Matrix4x4.TRS(position + projectorOffset, transform.rotation, Vector3.one);
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * size);
        Gizmos.DrawWireSphere(Vector3.forward * size, 0.1f);
        
        //draw the box
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(position, Quaternion.Euler(boxRotation), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boxSize, boxSize,boxSize));
    }

    private void UpdateProjector()
    {
        var boxScale = new Vector3(boxSize, boxSize, boxSize);
        Matrix4x4 boxMatrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(boxRotation), boxScale);
        
        m_Decal.BoxMatrix = boxMatrix.inverse;
        
        // Get the projector's rotation and position
        Quaternion projectorRotation = transform.rotation;
        Vector3 projectorPosition = transform.position;

        // Create the projection matrix for an orthographic projection
        Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-size, size, -size, size, 0f, size);

        // Create the view matrix using the projector's rotation and position
        Matrix4x4 viewMatrix = Matrix4x4.TRS(projectorPosition + projectorOffset, projectorRotation, Vector3.one).inverse;

        // Combine the projection and view matrices
        Matrix4x4 projectorMatrix = projectionMatrix * viewMatrix;
        m_Decal.ProjectorMatrix = projectorMatrix;
        DecalRenderer.UpdateDecals();
    }
}